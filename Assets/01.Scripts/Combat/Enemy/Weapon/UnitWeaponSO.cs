using UnityEngine;

/// <summary>
/// 단일 기체(터렛·드론·보스 포대)의 무기 하나. <b>아이템이 아니다</b> — Id 대역도, 루팅도, 인벤토리도 없다.
/// 플레이어 무기(<see cref="PlayerWeaponDataSO"/>)와 섞지 않는 이유는 둘의 관심사가 달라서다.
/// 플레이어 무기는 개량·과열·탄종·내구도가 핵심이고, 적 무기는 "언제 쏘는지 읽히는가"가 핵심이다.
///
/// 여기에는 <b>한 발이 무엇인가</b>(투사체·위력·사거리·맞히는 대상)만 있다.
/// <b>언제 어떻게 쏘는가</b>는 파생 타입이 정한다 — 새 공격 방식은 파생 타입 하나로 늘어난다.
///
/// | 파생 | 공격 방식 |
/// |---|---|
/// | <see cref="UnitBurstWeaponSO"/> | 연발 → 쉼. 단발·연사·산탄이 전부 이것의 수치 차이 |
/// | <see cref="UnitPrecisionWeaponSO"/> | 조준선으로 노리다 고정 → 강한 한 발 |
///
/// 사거리(<see cref="Range"/>)는 AI의 사격 거리이자 화면에 그리는 공격 범위다. 한 곳에서만 정한다.
/// </summary>
public abstract class UnitWeaponSO : ScriptableObject
{
    [Header("한 발")]
    [SerializeField] private PoolType _projectile = PoolType.None;

    [SerializeField, Min(0f)] private float _damage = 10f;

    [Tooltip("1을 넘으면 치명타 확정 + 초과분이 극대화 확률. 플레이어 무기와 같은 규칙.")]
    [SerializeField, Min(0f)] private float _criticalRate;

    [SerializeField, Min(0.01f)] private float _projectileSpeed = 14f;

    [Tooltip("탄이 날아가는 거리이자 AI가 쏘기 시작하는 거리, 화면에 그리는 공격 범위.")]
    [SerializeField, Min(0.5f)] private float _range = 10f;

    [Tooltip("탄이 실제로 날아가는 거리. 0이면 사거리와 같다. 사거리보다 길면 빗나간 탄이 범위 밖까지 이어진다 — 사거리 끝에서 뚝 끊기면 어색하다.")]
    [SerializeField, Min(0f)] private float _travelRange;

    [Tooltip("추가로 뚫는 대상 수. 0이면 하나 맞히고 사라진다.")]
    [SerializeField, Min(0)] private int _penetration;

    [Tooltip("0보다 크면 사라질 때 이 반경으로 터진다. 투사체 프리팹에 폭발 캐스터가 있어야 한다.")]
    [SerializeField, Min(0f)] private float _explosionRadius;

    [Tooltip("맞히는 대상. 지상 전용 / 대공 / 둘 다. 탄의 판정과 화면의 범위 표시가 같이 따른다.")]
    [SerializeField] private TargetType _hitTargets = TargetType.Ground;

    [Tooltip("맞히는 편. 기본은 플레이어·중립 — 적끼리(떼로 다니는 드론) 서로 맞히지 않는다.")]
    [SerializeField] private FactionType _hitFactions = FactionType.Player | FactionType.Neutral;

    [Header("반응")]
    [Tooltip("사정권에 들어오고 첫 공격을 시작하기까지(초). 들어오는 순간 맞으면 접근할 방법이 없다.")]
    [SerializeField, Min(0f)] private float _engageDelay = 0.4f;

    public PoolType Projectile => _projectile;
    public float Damage => _damage;
    public float CriticalRate => _criticalRate;
    public float ProjectileSpeed => _projectileSpeed;
    public float Range => _range;

    /// <summary>탄이 날아가는 거리. 사거리(쏘기 시작하는 거리)보다 짧지 않다.</summary>
    public float TravelRange => Mathf.Max(_range, _travelRange);
    public int Penetration => _penetration;
    public float ExplosionRadius => _explosionRadius;
    public TargetType HitTargets => _hitTargets;
    public FactionType HitFactions => _hitFactions;
    public float EngageDelay => _engageDelay;

    /// <summary>이 무기의 공격 방식. 기체마다 하나씩 만든다 — 진행 상태를 SO에 두면 같은 무기를 든 터렛끼리 섞인다.</summary>
    public abstract UnitFirePattern CreatePattern();
}
