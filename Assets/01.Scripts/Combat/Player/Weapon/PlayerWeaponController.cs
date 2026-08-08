using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 장착된 무기 데이터를 실제 오브젝트로 만들고 손에 들려준다.
///
/// 무기 슬롯마다 인스턴스를 하나씩 만들어 두고 전환할 때 활성화만 바꾼다.
/// 매번 만들고 부수면 그 무기에 끼워둔 파츠와 개량이 날아간다.
/// </summary>
public class PlayerWeaponController : MonoBehaviour, IAgentComponent
{
    [SerializeField] private Transform _weaponHandleRoot;

    [Tooltip("무기를 들 수 있는 슬롯. 순서대로 전환된다.")]
    [SerializeField]
    private List<EquipSlotType> _weaponSlots = new()
    {
        EquipSlotType.PrimaryWeapon,
        EquipSlotType.SecondaryWeapon,
    };

    public event Action<PlayerWeaponBase> OnWeaponChangedEvent;

    private readonly Dictionary<EquipSlotType, PlayerWeaponBase> _instances = new();

    private Player _player;
    private EquipmentController _equipment;

    public PlayerWeaponBase Current { get; private set; }
    public EquipSlotType CurrentSlot { get; private set; } = EquipSlotType.None;

    public void Initialize(Agent owner)
    {
        _player = owner as Player;

        if (_weaponHandleRoot == null)
            _weaponHandleRoot = transform;
    }

    public void AfterInitialize()
    {
        _equipment = _player.GetCompo<EquipmentController>();

        if (_equipment != null)
            _equipment.OnEquipChangedEvent += HandleEquipChanged;

        if (_player.Input != null)
        {
            _player.Input.OnSlotCycleEvent += CycleWeapon;
            _player.Input.OnAttackEvent += HandleAttack;
        }

        SwitchTo(FindFirstEquippedSlot());
    }

    public void Dispose()
    {
        if (_equipment != null)
            _equipment.OnEquipChangedEvent -= HandleEquipChanged;

        if (_player != null && _player.Input != null)
        {
            _player.Input.OnSlotCycleEvent -= CycleWeapon;
            _player.Input.OnAttackEvent -= HandleAttack;
        }
    }

    #region Switching

    /// <summary>해당 슬롯의 무기를 손에 든다. None이면 전부 내린다.</summary>
    public void SwitchTo(EquipSlotType slot)
    {
        if (CurrentSlot == slot) return;

        if (Current != null)
        {
            Current.OnUnequipped();
            Current.gameObject.SetActive(false);
        }

        CurrentSlot = slot;
        Current = slot == EquipSlotType.None ? null : GetOrCreate(slot);

        if (Current != null)
        {
            Current.gameObject.SetActive(true);
            Current.OnEquipped();
        }

        OnWeaponChangedEvent?.Invoke(Current);
    }

    /// <summary>다음/이전 무기 슬롯으로. 비어있는 슬롯은 건너뛴다.</summary>
    public void CycleWeapon(int direction)
    {
        if (_weaponSlots.Count == 0 || direction == 0) return;

        int start = _weaponSlots.IndexOf(CurrentSlot);
        if (start < 0) start = 0;

        for (int step = 1; step <= _weaponSlots.Count; step++)
        {
            int index = start + direction * step;

            // 음수도 감싸도록 두 번 나눈다.
            index = ((index % _weaponSlots.Count) + _weaponSlots.Count) % _weaponSlots.Count;

            EquipSlotType slot = _weaponSlots[index];
            if (_equipment == null || _equipment.Get(slot) == null) continue;

            SwitchTo(slot);
            return;
        }
    }

    #endregion

    private void HandleAttack(bool pressed)
    {
        if (Current == null) return;

        if (pressed)
            Current.OnAttackPressed();
        else
            Current.OnAttackReleased();
    }

    private void HandleEquipChanged(EquipSlotType slot, ItemStack stack)
    {
        if (!_weaponSlots.Contains(slot)) return;

        // 그 슬롯의 기존 오브젝트는 더 이상 유효하지 않다.
        DestroyInstance(slot);

        if (slot == CurrentSlot)
        {
            CurrentSlot = EquipSlotType.None;
            Current = null;

            SwitchTo(!stack.IsEmpty ? slot : FindFirstEquippedSlot());
        }
        else if (Current == null)
        {
            SwitchTo(slot);
        }
    }

    private PlayerWeaponBase GetOrCreate(EquipSlotType slot)
    {
        if (_instances.TryGetValue(slot, out PlayerWeaponBase exist) && exist != null)
            return exist;

        if (_equipment == null) return null;

        ItemStack stack = _equipment.GetStack(slot);
        PlayerWeaponDataSO data = stack.Resolve<PlayerWeaponDataSO>();
        if (data == null) return null;

        if (data.playerWeaponPrefab == null)
        {
            Debug.LogError($"[PlayerWeapon] {data.DisplayName}에 프리팹이 없습니다.", data);
            return null;
        }

        PlayerWeaponBase weapon = Instantiate(data.playerWeaponPrefab, _weaponHandleRoot);
        weapon.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        weapon.gameObject.SetActive(false);

        // 개체 상태가 있으면 파츠와 개량이 여기서 복원된다.
        weapon.Initialize(data, _player, stack.ResolveInstance<WeaponItemInstance>());
        _instances[slot] = weapon;

        return weapon;
    }

    private void DestroyInstance(EquipSlotType slot)
    {
        if (!_instances.TryGetValue(slot, out PlayerWeaponBase weapon)) return;

        _instances.Remove(slot);

        if (weapon == null) return;

        if (weapon.IsEquipped)
            weapon.OnUnequipped();

        // 파츠를 떼지 않는다. 개체 상태는 오브젝트가 사라져도 남아야 한다.
        Destroy(weapon.gameObject);
    }

    private EquipSlotType FindFirstEquippedSlot()
    {
        if (_equipment == null) return EquipSlotType.None;

        for (int i = 0; i < _weaponSlots.Count; i++)
        {
            if (_equipment.Get(_weaponSlots[i]) != null)
                return _weaponSlots[i];
        }

        return EquipSlotType.None;
    }
}
