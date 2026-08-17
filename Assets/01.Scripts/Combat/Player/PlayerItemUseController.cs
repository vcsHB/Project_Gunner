using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 핫바에서 든 소모품을 쓴다. 사용 시간과 쿨타임을 여기서 관리한다.
///
/// 쿨타임 표는 <b>플레이어별 상태</b>라 여기 둔다. 정적으로 두면 멀티에서 섞인다.
/// 키는 아이템 Id다 — 같은 종류를 칸만 나눠 담아 쿨타임을 우회하는 것을 막는다.
/// </summary>
public class PlayerItemUseController : MonoBehaviour, IAgentComponent
{
    /// <summary>사용 진행이 시작/취소/완료되었을 때. 인자는 진행 중인지.</summary>
    public event Action<bool> OnUsingChangedEvent;

    /// <summary>쿨타임이 새로 걸렸을 때. (아이템 Id)</summary>
    public event Action<uint> OnCooldownStartedEvent;

    // 아이템 Id -> 쿨타임이 끝나는 시각
    private readonly Dictionary<uint, float> _cooldownEnd = new();

    private Player _player;
    private PlayerHotbar _hotbar;
    private InventoryController _inventory;

    private ConsumableDataSO _using;
    private int _usingSlot = -1;
    private float _useEndTime;
    private float _useStartTime;

    public bool IsUsing => _using != null;

    /// <summary>0~1. 사용 중이 아니면 0.</summary>
    public float UseProgress01
    {
        get
        {
            if (!IsUsing) return 0f;

            float duration = _useEndTime - _useStartTime;
            return duration <= 0f ? 1f : Mathf.Clamp01((Time.time - _useStartTime) / duration);
        }
    }

    public void Initialize(Agent owner)
    {
        _player = owner as Player;
    }

    public void AfterInitialize()
    {
        _hotbar = _player.GetCompo<PlayerHotbar>();
        _inventory = _player.GetCompo<InventoryController>();

        if (_player.Input != null)
            _player.Input.OnUseEvent += HandleUse;

        // 쓰던 도중 다른 칸으로 넘기면 취소된다. 손에 없는 것을 계속 쓸 수는 없다.
        if (_hotbar != null)
            _hotbar.OnSelectedChangedEvent += HandleSelectedChanged;
    }

    public void Dispose()
    {
        if (_player != null && _player.Input != null)
            _player.Input.OnUseEvent -= HandleUse;

        if (_hotbar != null)
            _hotbar.OnSelectedChangedEvent -= HandleSelectedChanged;
    }

    #region 쿨타임

    /// <summary>남은 쿨타임(초). 없으면 0.</summary>
    public float GetCooldownRemain(uint itemId)
    {
        if (itemId == 0 || !_cooldownEnd.TryGetValue(itemId, out float end)) return 0f;

        return Mathf.Max(0f, end - Time.time);
    }

    /// <summary>1이면 막 걸린 것, 0이면 풀린 것. 게이지가 그대로 쓴다.</summary>
    public float GetCooldownRatio(uint itemId)
    {
        float remain = GetCooldownRemain(itemId);
        if (remain <= 0f) return 0f;

        ConsumableDataSO data = new ItemStack(itemId, 1).Resolve<ConsumableDataSO>();
        float total = data != null ? data.UseCooldown : 0f;

        return total <= 0f ? 0f : Mathf.Clamp01(remain / total);
    }

    public bool IsOnCooldown(uint itemId) => GetCooldownRemain(itemId) > 0f;

    #endregion

    private void HandleSelectedChanged(int index)
    {
        if (IsUsing && index != _usingSlot)
            CancelUse();
    }

    private void HandleUse()
    {
        // 인벤토리를 열어둔 채로 소모품이 쓰이면 안 된다.
        if (UIInputBlocker.IsBlocked || IsUsing || _hotbar == null) return;

        ItemStack stack = _hotbar.SelectedStack;
        ConsumableDataSO data = stack.Resolve<ConsumableDataSO>();
        if (data == null) return;

        if (ItemUsability.IsBlocked(stack, out string reason))
        {
            Debug.Log($"[Use] {data.DisplayName} - {reason}");
            return;
        }

        if (IsOnCooldown(stack.itemId)) return;

        BeginUse(data, _hotbar.SelectedIndex);
    }

    private void BeginUse(ConsumableDataSO data, int slotIndex)
    {
        _using = data;
        _usingSlot = slotIndex;
        _useStartTime = Time.time;
        _useEndTime = _useStartTime + data.UseDuration;

        OnUsingChangedEvent?.Invoke(true);

        // 즉발 아이템은 다음 Update를 기다릴 이유가 없다.
        if (data.UseDuration <= 0f)
            CompleteUse();
    }

    public void CancelUse()
    {
        if (!IsUsing) return;

        _using = null;
        _usingSlot = -1;

        OnUsingChangedEvent?.Invoke(false);
    }

    private void Update()
    {
        if (!IsUsing) return;

        // 사용 중에 죽거나 아이템이 사라지면 붙들고 있을 이유가 없다.
        if (_player.IsDead)
        {
            CancelUse();
            return;
        }

        if (Time.time >= _useEndTime)
            CompleteUse();
    }

    private void CompleteUse()
    {
        ConsumableDataSO data = _using;
        int slot = _usingSlot;

        _using = null;
        _usingSlot = -1;

        OnUsingChangedEvent?.Invoke(false);

        Inventory inventory = _inventory != null ? _inventory.Inventory : null;
        if (inventory == null || data == null) return;

        // 다 쓰기 직전에 칸이 비었을 수 있다. 효과를 먼저 주고 못 빼면 공짜가 된다.
        ItemStack stack = inventory[slot];
        if (stack.itemId != data.Id || stack.count < data.ConsumeCount) return;

        if (!inventory.RemoveAt(slot, data.ConsumeCount)) return;

        data.Apply(_player);
        StartCooldown(data);
    }

    private void StartCooldown(ConsumableDataSO data)
    {
        if (data.UseCooldown <= 0f) return;

        _cooldownEnd[data.Id] = Time.time + data.UseCooldown;
        OnCooldownStartedEvent?.Invoke(data.Id);
    }
}
