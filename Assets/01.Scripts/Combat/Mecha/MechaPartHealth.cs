using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 팔·추진체의 부위 체력. <b>코어 체력은 여기 없다</b> — 코어는 <see cref="Health"/>(AgentStatus.Health) 그대로이고,
/// 코어가 0이 되면 죽는다.
///
/// 피해 경로: 실드 → (실드가 0이면) 맞은 부위 → 그 부위가 없거나 이미 부서졌으면 본체.
/// <see cref="Health"/>가 방어력 계산 뒤에 <see cref="TryAbsorb"/>를 부른다. 넘친 피해는 버린다 — 실드와 같은 규칙.
///
/// 부서지면 —
/// <list type="bullet">
/// <item>그 부위의 히트박스를 끈다. 뒤에 있던 코어가 대신 맞는다.</item>
/// <item>팔: 그 무기를 막는다(<see cref="HandActionBase.SetDisabled"/>).</item>
/// <item>추진체: <see cref="OnPartDestroyedEvent"/>를 이동이 듣는다(활공·부스터 불가 + 감속).</item>
/// </list>
///
/// 부위 체력은 판마다 가득 찬 채로 시작한다. 아이템 내구도와는 별개다.
/// 수리(<see cref="Repair"/>)로 차오르고, <see cref="_restoreRatio"/>까지 차면 부서진 부위가 되살아난다.
/// </summary>
[RequireAgentComponent(typeof(EquipmentController))]
[RequireAgentComponent(typeof(MechaAssembler))]
[RequireAgentComponent(typeof(HandController), optional: true)]
public class MechaPartHealth : MonoBehaviour, IAgentComponent
{
    /// <summary>(부위, 현재, 최대)</summary>
    public event Action<EquipSlotType, float, float> OnPartHealthChangedEvent;
    public event Action<EquipSlotType> OnPartDestroyedEvent;
    public event Action<EquipSlotType> OnPartRestoredEvent;

    [Tooltip("부서진 부위가 수리로 되살아나는 체력 비율. 1이면 가득 차야 돌아온다.")]
    [SerializeField, Range(0.05f, 1f)] private float _restoreRatio = 0.3f;

    private static readonly EquipSlotType[] Parts =
    {
        EquipSlotType.ArmLeft,
        EquipSlotType.ArmRight,
        EquipSlotType.Booster,
    };

    private struct PartState
    {
        public float current;
        public float max;
        public bool destroyed;
    }

    private readonly PartState[] _states = new PartState[Parts.Length];

    private Agent _owner;
    private EquipmentController _equipment;
    private MechaAssembler _assembler;
    private HandController _hands;

    public IReadOnlyList<EquipSlotType> Slots => Parts;

    public void Initialize(Agent owner)
    {
        _owner = owner;
    }

    public void AfterInitialize()
    {
        _equipment = _owner.GetCompo<EquipmentController>();
        _assembler = _owner.GetCompo<MechaAssembler>();
        _hands = _owner.GetCompo<HandController>(true);

        if (_equipment != null)
            _equipment.OnEquipChangedEvent += HandleEquipChanged;

        // 몸이 다시 세워지거나 팔이 다시 만들어지면 새 히트박스가 켜진 채로 온다. 부서진 상태를 다시 입힌다.
        if (_assembler != null)
            _assembler.OnAssembledEvent += ApplyAllStates;

        if (_hands != null)
            _hands.OnHandChangedEvent += HandleHandChanged;

        for (int i = 0; i < Parts.Length; i++)
            ResetPart(i, _equipment != null ? _equipment.GetStack(Parts[i]) : default);
    }

    public void Dispose()
    {
        if (_equipment != null)
            _equipment.OnEquipChangedEvent -= HandleEquipChanged;

        if (_assembler != null)
            _assembler.OnAssembledEvent -= ApplyAllStates;

        if (_hands != null)
            _hands.OnHandChangedEvent -= HandleHandChanged;
    }

    private static int IndexOf(EquipSlotType slot)
    {
        for (int i = 0; i < Parts.Length; i++)
        {
            if (Parts[i] == slot) return i;
        }

        return -1;
    }

    #region 조회

    public bool IsDestroyed(EquipSlotType slot)
    {
        int i = IndexOf(slot);
        return i >= 0 && _states[i].destroyed;
    }

    public float GetCurrent(EquipSlotType slot)
    {
        int i = IndexOf(slot);
        return i >= 0 ? _states[i].current : 0f;
    }

    public float GetMax(EquipSlotType slot)
    {
        int i = IndexOf(slot);
        return i >= 0 ? _states[i].max : 0f;
    }

    /// <summary>수리할 것이 남았는가 (깎인 부위가 하나라도).</summary>
    public bool IsDamaged
    {
        get
        {
            for (int i = 0; i < _states.Length; i++)
            {
                if (_states[i].max > 0f && _states[i].current < _states[i].max) return true;
            }

            return false;
        }
    }

    #endregion

    #region 피해 / 수리

    /// <summary>
    /// 그 부위가 피해를 받았으면 true. 부위 체력이 없거나(0) 이미 부서졌으면 false — 본체가 받는다.
    /// </summary>
    public bool TryAbsorb(EquipSlotType slot, float damage)
    {
        int i = IndexOf(slot);
        if (i < 0) return false;

        ref PartState state = ref _states[i];
        if (state.max <= 0f || state.destroyed) return false;

        state.current = Mathf.Max(0f, state.current - damage);
        OnPartHealthChangedEvent?.Invoke(slot, state.current, state.max);

        if (state.current <= 0f)
        {
            state.destroyed = true;
            ApplyState(i);
            OnPartDestroyedEvent?.Invoke(slot);
        }

        return true;
    }

    /// <summary>모든 부위를 같은 양만큼 채운다. 부서진 부위는 <see cref="_restoreRatio"/>까지 차면 돌아온다.</summary>
    public void Repair(float amount)
    {
        if (amount <= 0f) return;

        for (int i = 0; i < Parts.Length; i++)
        {
            ref PartState state = ref _states[i];
            if (state.max <= 0f || state.current >= state.max) continue;

            state.current = Mathf.Min(state.max, state.current + amount);
            OnPartHealthChangedEvent?.Invoke(Parts[i], state.current, state.max);

            if (state.destroyed && state.current >= state.max * _restoreRatio)
            {
                state.destroyed = false;
                ApplyState(i);
                OnPartRestoredEvent?.Invoke(Parts[i]);
            }
        }
    }

    /// <summary>새 판·리스폰. 전부 가득 채우고 되살린다.</summary>
    public void ResetAll()
    {
        for (int i = 0; i < Parts.Length; i++)
            ResetPart(i, _equipment != null ? _equipment.GetStack(Parts[i]) : default);
    }

    #endregion

    #region 장착 / 상태 입히기

    /// <summary>부위가 갈렸다. 새 부품은 새것이다 — 가득 찬 채로, 부서지지 않은 채로.</summary>
    private void HandleEquipChanged(EquipSlotType slot, ItemStack stack)
    {
        int i = IndexOf(slot);
        if (i < 0) return;

        ResetPart(i, stack);
    }

    private void ResetPart(int index, ItemStack stack)
    {
        EquipableItemDataSO data = stack.IsEmpty ? null : stack.Resolve<EquipableItemDataSO>();
        float max = data != null ? data.PartHealth : 0f;

        bool wasDestroyed = _states[index].destroyed;

        _states[index] = new PartState { current = max, max = max, destroyed = false };
        ApplyState(index);

        OnPartHealthChangedEvent?.Invoke(Parts[index], max, max);

        if (wasDestroyed)
            OnPartRestoredEvent?.Invoke(Parts[index]);
    }

    private void HandleHandChanged(EquipSlotType slot, HandActionBase hand)
    {
        int i = IndexOf(slot);
        if (i >= 0) ApplyState(i);
    }

    private void ApplyAllStates()
    {
        for (int i = 0; i < Parts.Length; i++)
            ApplyState(i);
    }

    /// <summary>부서졌으면 그 부위의 히트박스를 끄고, 팔이면 무기를 막는다. 되살아나면 되돌린다.</summary>
    private void ApplyState(int index)
    {
        EquipSlotType slot = Parts[index];
        bool destroyed = _states[index].destroyed;

        Transform mount = _assembler != null ? _assembler.GetMountPoint(slot) : null;
        if (mount != null)
        {
            foreach (HitBox hitBox in mount.GetComponentsInChildren<HitBox>(true))
            {
                if (hitBox.TryGetComponent(out Collider2D collider))
                    collider.enabled = !destroyed;
            }
        }

        HandActionBase hand = _hands != null ? _hands.Get(slot) : null;
        if (hand != null)
            hand.SetDisabled(destroyed);
    }

    #endregion
}
