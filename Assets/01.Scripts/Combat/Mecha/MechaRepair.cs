using System;
using UnityEngine;

/// <summary>
/// 필드 수리. 적재함의 <see cref="RepairKitDataSO"/>를 하나 써서 <b>시간에 걸쳐</b> 코어(<see cref="Health"/>)와
/// 모든 부위(<see cref="MechaPartHealth"/>)를 채운다. 부서진 부위도 충분히 차면 되살아난다.
///
/// <b>맞으면 끊긴다.</b> 실드에 막혀도 끊긴다 — 공격받는 중이라는 사실은 같다. 키트는 이미 썼으므로 돌아오지 않는다.
///
/// 플레이어는 R(<see cref="PlayerInput.OnRepairEvent"/>)로, 다른 주체(AI)는 <see cref="TryStartRepair"/>를 직접 부른다.
/// 게이지는 "목표값"만 받는다 — 차오르는 건 여기(로직)고, UI는 끊긴 줄 모른다 (MILESTONE M2 게이지 표현).
/// </summary>
[RequireAgentComponent(typeof(Health))]
[RequireAgentComponent(typeof(InventoryController))]
[RequireAgentComponent(typeof(MechaPartHealth), optional: true)]
public class MechaRepair : MonoBehaviour, IAgentComponent
{
    /// <summary>수리가 시작됐다. 인자는 걸리는 시간.</summary>
    public event Action<float> OnRepairStartedEvent;

    /// <summary>수리가 끝났다. true면 다 돌았고, false면 맞아서 끊겼다.</summary>
    public event Action<bool> OnRepairEndedEvent;

    /// <summary>수리를 시작하지 못했다(키트 없음·고칠 것 없음). 경고음·UI 안내용.</summary>
    public event Action<RepairFailReason> OnRepairRejectedEvent;

    public enum RepairFailReason
    {
        NoKit,
        NothingToRepair,
        Busy,
        Dead,
    }

    private Agent _owner;
    private Health _health;
    private MechaPartHealth _parts;
    private InventoryController _inventory;
    private Player _player;

    private float _ratePerSecond;
    private float _remaining;

    public bool IsRepairing => _remaining > 0f;

    /// <summary>남은 시간(초). 수리 중이 아니면 0.</summary>
    public float Remaining => _remaining;

    public void Initialize(Agent owner)
    {
        _owner = owner;
        _player = owner as Player;
    }

    public void AfterInitialize()
    {
        _health = _owner.GetCompo<Health>();
        _parts = _owner.GetCompo<MechaPartHealth>();
        _inventory = _owner.GetCompo<InventoryController>();

        if (_health != null)
        {
            _health.OnDamagedEvent += HandleDamaged;
            _health.OnDeadEvent += HandleDead;
        }

        if (_player != null)
        {
            _player.OnInputChangedEvent += HandleInputChanged;
            HandleInputChanged(null, _player.Input);
        }
    }

    public void Dispose()
    {
        if (_health != null)
        {
            _health.OnDamagedEvent -= HandleDamaged;
            _health.OnDeadEvent -= HandleDead;
        }

        if (_player != null)
        {
            _player.OnInputChangedEvent -= HandleInputChanged;
            HandleInputChanged(_player.Input, null);
        }

        _remaining = 0f;
    }

    private void HandleInputChanged(PlayerInput previous, PlayerInput current)
    {
        if (previous != null) previous.OnRepairEvent -= HandleRepairInput;
        if (current != null) current.OnRepairEvent += HandleRepairInput;
    }

    private void HandleRepairInput() => TryStartRepair();

    /// <param name="preferredSlot">적재함의 이 칸에 있는 키트를 쓴다(인벤토리 우클릭 "사용"). -1이면 앞칸부터.</param>
    public bool TryStartRepair(int preferredSlot = -1)
    {
        if (_health == null || _health.IsDead) return Reject(RepairFailReason.Dead);
        if (IsRepairing) return Reject(RepairFailReason.Busy);

        bool damaged = _health.CurrentHealth < _health.MaxHealth || (_parts != null && _parts.IsDamaged);
        if (!damaged) return Reject(RepairFailReason.NothingToRepair);

        if (!TryConsumeKit(preferredSlot, out RepairKitDataSO kit)) return Reject(RepairFailReason.NoKit);

        _remaining = kit.Duration;
        _ratePerSecond = kit.Amount / kit.Duration;

        OnRepairStartedEvent?.Invoke(kit.Duration);
        return true;
    }

    private bool Reject(RepairFailReason reason)
    {
        OnRepairRejectedEvent?.Invoke(reason);
        return false;
    }

    /// <summary>적재함 앞칸부터 찾는다. 종류가 여럿이면 먼저 만난 것을 쓴다.</summary>
    private bool TryConsumeKit(int preferredSlot, out RepairKitDataSO kit)
    {
        kit = null;

        Inventory inventory = _inventory != null ? _inventory.Inventory : null;
        if (inventory == null) return false;

        if (preferredSlot >= 0)
        {
            RepairKitDataSO chosen = inventory[preferredSlot].Resolve<RepairKitDataSO>();
            if (chosen != null && inventory.RemoveAt(preferredSlot, 1))
            {
                kit = chosen;
                return true;
            }
        }

        for (int i = 0; i < inventory.Capacity; i++)
        {
            ItemStack stack = inventory[i];
            if (stack.IsEmpty) continue;

            RepairKitDataSO found = stack.Resolve<RepairKitDataSO>();
            if (found == null) continue;

            if (!inventory.RemoveAt(i, 1)) continue;

            kit = found;
            return true;
        }

        return false;
    }

    private void Update()
    {
        if (!IsRepairing) return;

        float deltaTime = Mathf.Min(Time.deltaTime, _remaining);
        _remaining -= deltaTime;

        float amount = _ratePerSecond * deltaTime;
        _health.Heal(amount);
        _parts?.Repair(amount);

        if (_remaining <= 0f)
            End(true);
    }

    private void HandleDamaged(DamageData data)
    {
        if (IsRepairing) End(false);
    }

    private void HandleDead()
    {
        if (IsRepairing) End(false);
    }

    private void End(bool completed)
    {
        _remaining = 0f;
        OnRepairEndedEvent?.Invoke(completed);
    }
}
