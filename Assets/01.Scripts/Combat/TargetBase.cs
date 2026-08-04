using System;
using UnityEngine;

/// <summary>
/// "전투에서 하나의 대상으로 취급되는 단위". 피해 계산이 아니라 선택되는 방식을 담당한다.
/// - HitBox가 여러 개여도 대상은 하나라는 동일성 기준
/// - TargetType 분류와 물리 레이어 동기화
/// - 대상이 바뀌거나 사라졌음을 알리는 단일 구독 창구
/// - 조준점
/// </summary>
public class TargetBase : MonoBehaviour, IAgentComponent
{
    [SerializeField] private TargetType _defaultTargetType = TargetType.Ground;

    [Tooltip("비워두면 자기 transform. 큰 유닛은 피벗이 발밑이라 따로 잡아주는 게 좋다.")]
    [SerializeField] private Transform _aimPoint;

    [Tooltip("비워두면 자식에서 찾는다. 파괴 불가 대상이면 비워둔 채로 둔다.")]
    [SerializeField] private Health _health;

    [Tooltip("비워두면 자식 Collider2D를 전부 수집한다. TargetType이 바뀌면 이 콜라이더들의 레이어가 함께 바뀐다.")]
    [SerializeField] private Collider2D[] _colliders;

    /// <summary>(대상, 이전 타입, 새 타입)</summary>
    public event Action<TargetBase, TargetType, TargetType> OnTargetTypeChangedEvent;

    /// <summary>사망 또는 풀 반납. 참조를 캐시해 둔 쪽(유도탄, AI 어그로, 락온 UI)은 이것만 구독하면 된다.</summary>
    public event Action<TargetBase> OnTargetLostEvent;

    public Agent Owner { get; private set; }

    /// <summary>null이면 피해를 받지 않는 대상(디코이, 파괴 불가 엄폐물).</summary>
    public IDamageable Damageable => _health;

    public TargetType CurrentTargetType { get; private set; }

    public bool IsAlive => _health == null || !_health.IsDead;

    /// <summary>None이면 일시적으로 노려질 수 없다. (회피 중 무적 등)</summary>
    public bool IsTargetable => CurrentTargetType != TargetType.None && IsAlive;

    public Vector2 AimPosition => _aimPoint != null ? _aimPoint.position : transform.position;

    private bool _initialized;

    private void Awake()
    {
        Owner = GetComponentInParent<Agent>();

        // Agent가 없는 단독 대상(폭발통 등)은 아무도 Initialize를 불러주지 않는다.
        if (Owner == null)
            EnsureInitialized();
    }

    public void Initialize(Agent owner)
    {
        Owner = owner;
    }

    public void AfterInitialize()
    {
        EnsureInitialized();
    }

    public void Dispose()
    {
        if (_health != null)
            _health.OnDeadEvent -= HandleDead;
    }

    private void OnDestroy() => Dispose();

    private void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        if (_health == null)
            _health = GetComponentInChildren<Health>(true);

        if (_colliders == null || _colliders.Length == 0)
            _colliders = GetComponentsInChildren<Collider2D>(true);

        if (_health != null)
            _health.OnDeadEvent += HandleDead;

        CurrentTargetType = _defaultTargetType;
        ApplyLayer(CurrentTargetType);
    }

    /// <summary>
    /// 분류를 바꾸고 콜라이더 레이어까지 맞춘다.
    /// 물리로 매번 새로 찾는 캐스터는 이것만으로 자동으로 맞고,
    /// 참조를 들고 있는 쪽은 이벤트를 받아 다시 판단해야 한다.
    /// </summary>
    public void SetTargetType(TargetType next)
    {
        if (CurrentTargetType == next) return;

        TargetType prev = CurrentTargetType;
        CurrentTargetType = next;
        ApplyLayer(next);

        OnTargetTypeChangedEvent?.Invoke(this, prev, next);
    }

    public void ResetTargetType() => SetTargetType(_defaultTargetType);

    /// <summary>풀 반납처럼 사망이 아닌 이유로 사라질 때 호출한다.</summary>
    public void NotifyLost() => OnTargetLostEvent?.Invoke(this);

    private void ApplyLayer(TargetType type)
    {
        // None은 대응 레이어가 없다. 레이어는 그대로 두고 IsTargetable로 걸러진다.
        if (type == TargetType.None) return;

        int layer = TargetLayerTableSO.LayerOf(type);
        if (layer < 0) return;

        for (int i = 0; i < _colliders.Length; i++)
        {
            if (_colliders[i] == null) continue;

            _colliders[i].gameObject.layer = layer;
        }
    }

    private void HandleDead() => NotifyLost();

#if UNITY_EDITOR
    private void OnValidate()
    {
        // TargetType은 캐스터가 조합해서 쓰라고 Flags지만, 유닛 본인은 하나만 가져야 한다.
        // 조합된 값은 레이어 대응표에서 찾을 수 없다.
        if (_defaultTargetType != TargetType.None && !Mathf.IsPowerOfTwo((int)_defaultTargetType))
            Debug.LogWarning($"[TargetBase] {name}: TargetType은 하나만 선택해야 합니다.", this);
    }
#endif
}
