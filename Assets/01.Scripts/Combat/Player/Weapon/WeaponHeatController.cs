using System;
using UnityEngine;

/// <summary>
/// 연사를 제한하는 <b>열</b>. 재장전이 없어진 자리를 메운다.
///
/// 발사마다 열이 오르고, 가만히 두면 식는다. 열이 높을수록 공격속도가 아주 미미하게
/// 떨어지고, 용량을 채우면 일정 시간 그 무기를 쓸 수 없다(오버히트 락).
///
/// <b>열은 무기 오브젝트가 들고 간다.</b> <see cref="ItemInstance"/>가 아니다 —
/// 핫바가 없어져 무기가 전투 중에 파괴되지 않고, 교체는 안전지대에서만 일어나므로
/// 그때 식는 것이 자연스럽다. 개체 상태로 저장하면 "창고에 넣어둔 총이 아직 뜨거운" 상태가 생긴다.
///
/// 소프트 페널티는 <see cref="WeaponStatus"/>의 origin 기반 modifier로 건다.
/// 모듈·개량이 쓰는 방식과 같아서, 열이 식으면 자기 것만 깔끔하게 걷힌다.
/// </summary>
public class WeaponHeatController
{
    /// <summary>열이 가득 찼을 때 공격속도가 얼마나 떨어지는가. 0.15면 쿨타임 +15%.</summary>
    private const float MaxSoftPenalty = 0.15f;

    /// <summary>
    /// 페널티를 이 폭으로 끊어서 건다. 매 프레임 modifier를 갈면 스탯이 쉬지 않고
    /// 변경 이벤트를 쏘고, 그걸 구독한 UI가 같이 돈다. 눈에 안 보이는 차이를 위해 그럴 이유가 없다.
    /// </summary>
    private const float PenaltyStep = 0.01f;

    /// <summary>(현재 열, 용량). 게이지가 그대로 쓴다.</summary>
    public event Action<float, float> OnHeatChangedEvent;

    /// <summary>락이 걸리거나 풀렸을 때. true면 잠긴 것이다.</summary>
    public event Action<bool> OnOverheatChangedEvent;

    private readonly PlayerWeaponBase _weapon;

    private float _heat;
    private float _lockLeft;
    private float _lockDuration;
    private float _appliedPenalty;

    public WeaponHeatController(PlayerWeaponBase weapon)
    {
        _weapon = weapon;
    }

    private WeaponStatus Status => _weapon.Status;

    private float Capacity => Mathf.Max(0f, Status.Value(WeaponStatType.HeatCapacity));
    private float PerShot => Mathf.Max(0f, Status.Value(WeaponStatType.HeatPerShot));

    /// <summary>
    /// 열을 쓰지 않는 무기인가. 발당 상승이나 용량이 0이면 꺼둔 것으로 본다 —
    /// 아직 열 수치를 매기지 않은 무기가 첫 발에 잠기면 안 된다.
    /// </summary>
    public bool IsEnabled => PerShot > 0f && Capacity > 0f;

    /// <summary>0~1. 게이지와 색 변화에 쓴다.</summary>
    public float Ratio01 => Capacity > 0f ? Mathf.Clamp01(_heat / Capacity) : 0f;

    public float Heat => _heat;

    /// <summary>과열로 잠겨 있는가. 기다리면 풀린다 — 손을 써야 풀리는 상태가 아니다.</summary>
    public bool IsLocked => _lockLeft > 0f;

    /// <summary>락이 얼마나 남았는가. 1에서 0으로 간다. 소모품 쿨타임 UI가 그대로 읽는다.</summary>
    public float LockRatio01 => _lockDuration > 0f ? Mathf.Clamp01(_lockLeft / _lockDuration) : 0f;

    /// <summary>한 발 나갔다. 실제로 탄이 나간 뒤에만 부를 것 — 빈 격발로 달아오르면 억울하다.</summary>
    public void AddShot()
    {
        if (!IsEnabled) return;

        SetHeat(_heat + PerShot);

        if (_heat >= Capacity)
            Lock();
    }

    public void Tick(float deltaTime)
    {
        if (!IsEnabled) return;

        if (_lockLeft > 0f)
        {
            _lockLeft -= deltaTime;

            if (_lockLeft <= 0f)
            {
                _lockLeft = 0f;

                // 락이 풀리는 순간 열을 비운다. 안 그러면 한 발 쏘자마자 다시 잠긴다.
                SetHeat(0f);
                OnOverheatChangedEvent?.Invoke(false);
            }

            return;
        }

        float cooling = Mathf.Max(0f, Status.Value(WeaponStatType.HeatCooling));
        if (cooling <= 0f || _heat <= 0f) return;

        SetHeat(_heat - cooling * deltaTime);
    }

    /// <summary>안전지대에서 무기를 바꿀 때처럼, 즉시 식혀야 할 때.</summary>
    public void Reset()
    {
        bool wasLocked = IsLocked;

        _lockLeft = 0f;
        SetHeat(0f);

        if (wasLocked)
            OnOverheatChangedEvent?.Invoke(false);
    }

    /// <summary>무기 오브젝트가 사라질 때. 걸어둔 modifier를 남기고 가면 안 된다.</summary>
    public void Dispose()
    {
        Status?.Get(WeaponStatType.AttackCooltime)?.RemoveModifier(this);
        _appliedPenalty = 0f;
    }

    private void Lock()
    {
        _lockDuration = Mathf.Max(0f, Status.Value(WeaponStatType.OverheatLockTime));

        // 락 시간이 0이면 잠그지 않는다. 0으로 잠그면 그 프레임에 바로 풀리면서
        // 열만 비워지는, 페널티가 사실상 없는 무기가 된다.
        if (_lockDuration <= 0f)
        {
            SetHeat(0f);
            return;
        }

        _lockLeft = _lockDuration;

        OnOverheatChangedEvent?.Invoke(true);
    }

    private void SetHeat(float value)
    {
        float capacity = Capacity;
        float next = Mathf.Clamp(value, 0f, capacity);

        if (Mathf.Approximately(next, _heat)) return;

        _heat = next;

        ApplySoftPenalty();

        OnHeatChangedEvent?.Invoke(_heat, capacity);
    }

    /// <summary>
    /// 열이 높을수록 쿨타임이 길어진다. 끊어서 걸기 때문에 매 프레임 갈아끼우지 않는다.
    /// </summary>
    private void ApplySoftPenalty()
    {
        float penalty = Mathf.Round(Ratio01 * MaxSoftPenalty / PenaltyStep) * PenaltyStep;

        if (Mathf.Approximately(penalty, _appliedPenalty)) return;

        _appliedPenalty = penalty;

        Status<float> cooltime = Status.Get(WeaponStatType.AttackCooltime);
        if (cooltime == null) return;

        cooltime.RemoveModifier(this);

        if (penalty > 0f)
            cooltime.AddModifier(this, penalty, ModifierMode.Percent);
    }
}
