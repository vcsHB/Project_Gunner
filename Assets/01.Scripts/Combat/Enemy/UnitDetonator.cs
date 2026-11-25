using System;
using UnityEngine;

/// <summary>
/// 터지는 몸. 자폭 드론과 부표(기뢰)가 쓴다. 유닛 루트에 붙이고, 자식에 폭발 캐스터(<see cref="CircleCaster"/> + 데미지 이펙터)를 둔다.
///
/// | | 자폭 드론 | 부표 |
/// |---|---|---|
/// | 불붙는 조건 | 대상(플레이어 편)이 <see cref="_triggerRadius"/> 안 | 누구든(플레이어·적) 닿음 |
/// | 예고 | <see cref="_fuse"/>초 멈춰서 깜빡임 | 없음(0) — 닿으면 바로 |
/// | 맞히는 편 | 캐스터의 편 설정 — 플레이어·중립 | 전부 |
/// | 쏴서 부수면 | 추락해 땅에 닿을 때 터진다 | 그 자리에서 터진다 — 옆 부표도 맞아 연쇄 |
///
/// <b>죽으면 터진다</b>(<see cref="_detonateOnWreck"/>). 부서지는 순간(<see cref="Health.OnWreckedEvent"/>)이라 떠 있던 것은 추락이 끝난 자리다.
/// 스스로 터질 때(퓨즈 끝)는 추락하지 않고 그 자리에서 부서진다(<see cref="Health.Kill(bool)"/>).
///
/// 위력은 여기 <see cref="_damage"/>로 고정해 실어 보낸다 — 터질 때는 몸이 이미 없어 스탯을 읽을 수 없다.
/// 불붙는 조건의 대상 찾기는 캐스터와 같이 <see cref="TargetLayerTableSO"/>로 마스크를 만든다(LayerMask 직접 금지).
/// </summary>
[RequireAgentComponent(typeof(Health))]
public class UnitDetonator : MonoBehaviour, IAgentComponent
{
    /// <summary>불이 붙었다. 인자는 터질 때까지(초). 깜빡임이 듣는다.</summary>
    public event Action<float> OnFuseStartedEvent;

    [SerializeField] private CircleCaster _explosion;
    [SerializeField, Min(0f)] private float _damage = 40f;

    [Header("불붙는 조건")]
    [Tooltip("이 안에 대상이 들어오면 불이 붙는다(유닛). 0이면 닿아서는 안 터진다(맞아서만).")]
    [SerializeField, Min(0f)] private float _triggerRadius = 0.8f;

    [Tooltip("불을 붙이는 편. 자폭 드론은 플레이어, 부표는 플레이어 + 적.")]
    [SerializeField] private FactionType _triggerFactions = FactionType.Player;

    [Tooltip("불붙고 터질 때까지(초). 0이면 닿는 즉시.")]
    [SerializeField, Min(0f)] private float _fuse = 0.6f;

    [Tooltip("불붙은 동안 멈춘다. 예고를 보고 빠질 틈.")]
    [SerializeField] private bool _stopWhileFused = true;

    [Header("죽을 때")]
    [SerializeField] private bool _detonateOnWreck = true;

    [Tooltip("맞아서 부서질 때 터지기까지(초). 부표 연쇄가 한 프레임에 다 터지지 않게 한다.")]
    [SerializeField, Min(0f)] private float _chainDelay = 0.12f;

    private Agent _owner;
    private Health _health;
    private AgentStatus _status;

    private readonly Collider2D[] _buffer = new Collider2D[16];
    private ContactFilter2D _filter;

    private bool _fused;
    private bool _detonated;
    private float _fuseLeft;

    public bool IsFused => _fused;

    /// <summary>폭발 캐스터. AI가 이걸 근접 공격 캐스터로 잘못 집지 않게 거른다.</summary>
    public CasterBase Explosion => _explosion;

    public void Initialize(Agent owner) => _owner = owner;

    public void AfterInitialize()
    {
        _health = _owner.GetCompo<Health>();
        _status = _owner.GetCompo<AgentStatus>();

        if (_health != null) _health.OnWreckedEvent += HandleWrecked;

        _filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = TargetLayerTableSO.MaskOf(TargetType.Ground | TargetType.Air),
            useTriggers = true,
        };

        if (_explosion == null) _explosion = GetComponentInChildren<CircleCaster>(true);
    }

    public void Dispose()
    {
        if (_health != null) _health.OnWreckedEvent -= HandleWrecked;

        // 멈춤 modifier를 걸어 둔 채로 사라지면 안 된다(RULES — 건 modifier는 Dispose에서 걷는다).
        if (_status != null) _status.MoveSpeed.RemoveModifier(this);
    }

    private void Update()
    {
        if (_detonated || _owner == null || _owner.IsDead) return;

        if (_fused)
        {
            _fuseLeft -= Time.deltaTime;
            if (_fuseLeft <= 0f) SelfDetonate();
            return;
        }

        if (_triggerRadius > 0f && IsTriggered()) Ignite();
    }

    private bool IsTriggered()
    {
        int count = Physics2D.OverlapCircle(transform.position, _triggerRadius, _filter, _buffer);

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = _buffer[i];
            if (hit == null) continue;

            HitBox hitBox = hit.GetComponent<HitBox>();
            TargetBase target = hitBox != null ? hitBox.Target : hit.GetComponentInParent<TargetBase>();
            if (target == null || !target.IsTargetable || target.Owner == _owner) continue;
            if ((target.Faction & _triggerFactions) == 0) continue;

            return true;
        }

        return false;
    }

    private void Ignite()
    {
        _fused = true;
        _fuseLeft = _fuse;

        if (_stopWhileFused && _status != null)
            _status.MoveSpeed.AddModifier(this, -1f, ModifierMode.Percent);

        OnFuseStartedEvent?.Invoke(_fuse);

        if (_fuse <= 0f) SelfDetonate();
    }

    /// <summary>스스로 터진다. 떠 있어도 추락하지 않는다 — 그 자리에서.</summary>
    private void SelfDetonate()
    {
        Detonate(0f);
        _health?.Kill(allowCrash: false);
    }

    private void HandleWrecked()
    {
        if (_detonateOnWreck) Detonate(_chainDelay);
    }

    private void Detonate(float delay)
    {
        if (_detonated || _explosion == null) return;
        _detonated = true;

        // 몸이 곧 사라진다. 위력을 값으로 실어 보내고, 주인을 떼어 자기 몸이 빠지는 규칙이 엉뚱하게 걸리지 않게 한다.
        _explosion.Power = new CastPower(_damage, 0f);
        _explosion.Owner = null;
        _explosion.transform.position = transform.position;

        DelayedCast.Run(_explosion, delay);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, _triggerRadius);
    }
#endif
}
