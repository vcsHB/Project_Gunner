using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 소모품의 사용 진행과 쿨타임을 들고 간다.
///
/// <b>입력을 직접 받지 않는다.</b> 우클릭이 사용을 뜻하는지는 손에 든 것이 정하고,
/// <see cref="ConsumableHand"/>가 여기로 넘긴다.
///
/// 손에 든 오브젝트는 핫바를 넘길 때마다 파괴되므로 쿨타임을 거기 둘 수 없다.
/// 쿨타임 표는 <b>플레이어별 상태</b>라 정적으로도 둘 수 없다 — 멀티에서 섞인다.
/// 키는 아이템 Id다. 같은 종류를 칸만 나눠 담아 쿨타임을 우회하는 것을 막는다.
/// </summary>
public class PlayerItemUseController : MonoBehaviour, IAgentComponent
{
    /// <summary>사용이 시작/취소/완료되었을 때. 인자는 진행 중인지.</summary>
    public event Action<bool> OnUsingChangedEvent;

    /// <summary>쿨타임이 새로 걸렸을 때. (아이템 Id)</summary>
    public event Action<uint> OnCooldownStartedEvent;

    // 아이템 Id -> 쿨타임이 끝나는 시각
    private readonly Dictionary<uint, float> _cooldownEnd = new();

    private Player _player;
    private PlayerHotbar _hotbar;
    private InventoryController _inventory;

    private ConsumableDataSO _using;
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
    }

    public void Dispose() { }

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

    /// <summary>손에 든 소모품이 부른다. 쓸 수 없는 상태면 아무 일도 하지 않는다.</summary>
    public bool TryBeginUse(ItemStack stack)
    {
        if (IsUsing) return false;

        ConsumableDataSO data = stack.Resolve<ConsumableDataSO>();
        if (data == null) return false;

        if (ItemUsability.IsBlocked(stack, out string reason))
        {
            Debug.Log($"[Use] {data.DisplayName} - {reason}");
            return false;
        }

        if (IsOnCooldown(stack.itemId)) return false;

        _using = data;
        _useStartTime = Time.time;
        _useEndTime = _useStartTime + data.UseDuration;

        OnUsingChangedEvent?.Invoke(true);

        // 즉발 아이템은 다음 Update를 기다릴 이유가 없다.
        if (data.UseDuration <= 0f)
            CompleteUse();

        return true;
    }

    public void CancelUse()
    {
        if (!IsUsing) return;

        _using = null;
        OnUsingChangedEvent?.Invoke(false);
    }

    private void Update()
    {
        if (!IsUsing) return;

        // 사용 중에 죽으면 붙들고 있을 이유가 없다.
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

        _using = null;
        OnUsingChangedEvent?.Invoke(false);

        if (data == null) return;

        // 다 쓰기 직전에 칸이 비었을 수 있다. 효과를 먼저 주고 못 빼면 공짜가 된다.
        if (!TryConsume(data)) return;

        data.Apply(_player);
        StartCooldown(data);
    }

    /// <summary>
    /// 지금 든 칸에서 뺀다. 손에 든 것이 곧 쓰는 것이라 선택 칸을 그대로 본다.
    /// 쓰는 도중 칸이 바뀌었으면 손도 바뀌었을 것이고, 그때는 이미 취소되어 여기 오지 않는다.
    /// </summary>
    /// <summary>
    /// 들고 있는 것을 하나 소모하고 쿨타임을 건다. 투척처럼 사용 시간을 거치지 않는 것이 쓴다.
    /// </summary>
    public bool ConsumeHeld(ConsumableDataSO data)
    {
        if (data == null || !TryConsume(data)) return false;

        StartCooldown(data);
        return true;
    }

    private bool TryConsume(ConsumableDataSO data)
    {
        Inventory inventory = _inventory != null ? _inventory.Inventory : null;
        if (inventory == null || _hotbar == null) return false;

        int slot = _hotbar.SelectedIndex;
        ItemStack stack = inventory[slot];

        if (stack.itemId != data.Id || stack.count < data.ConsumeCount) return false;

        return inventory.RemoveAt(slot, data.ConsumeCount);
    }

    private void StartCooldown(ConsumableDataSO data)
    {
        if (data.UseCooldown <= 0f) return;

        _cooldownEnd[data.Id] = Time.time + data.UseCooldown;
        OnCooldownStartedEvent?.Invoke(data.Id);
    }
}
