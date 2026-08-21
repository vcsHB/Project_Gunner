using UnityEngine;

/// <summary>
/// 먹거나 써서 없어지는 아이템. 붕대, 통조림, 물병 같은 것.
///
/// 쿨타임은 <b>아이템 종류별</b>이다. 붕대를 쓰면 인벤토리의 모든 붕대가 같이 잠긴다.
/// 개체별로 두면 칸을 나눠 담아 쿨타임을 우회할 수 있다.
/// </summary>
[CreateAssetMenu(menuName = "SO/Item/ConsumableData")]
public class ConsumableDataSO : ItemDataSO, IHandItemData
{
    [Header("Hand")]
    [Tooltip("손에 들었을 때 만들어질 프리팹. 비우면 공용 소모품 손을 쓴다. " +
             "투척무기처럼 손에 보이거나 2단계 동작이 필요한 것만 채운다.")]
    [SerializeField] private HandActionBase _handPrefab;

    [Header("Use")]
    [Tooltip("다 쓰는 데 걸리는 시간(초). 0이면 즉시 발동한다. 도중에 움직이거나 맞으면 취소된다.")]
    [SerializeField, Min(0f)] private float _useDuration = 1f;

    [Tooltip("쓰고 나서 같은 종류를 다시 쓸 수 있게 되기까지의 시간(초).")]
    [SerializeField, Min(0f)] private float _useCooldown = 3f;

    [Tooltip("한 번 쓸 때 없어지는 개수.")]
    [SerializeField, Min(1)] private int _consumeCount = 1;

    [Header("회복")]
    [SerializeField] private float _healthRestore;
    [SerializeField] private float _staminaRestore;
    [SerializeField] private float _hungerRestore;
    [SerializeField] private float _thirstRestore;

    [Header("Throw")]
    [Tooltip("던지는 아이템인지. 켜면 우클릭이 안전핀 제거 + 조준, 좌클릭이 던지기가 된다.")]
    [SerializeField] private bool _isThrowable;

    [Tooltip("던져질 투사체. 신관 시간은 투사체의 수명으로 들어간다.")]
    [SerializeField] private PoolType _throwProjectile = PoolType.None;

    [SerializeField, Min(0.1f)] private float _throwSpeed = 12f;

    [Tooltip("안전핀을 뽑은 뒤 터지기까지의 시간. 0이면 부딪힐 때 터진다.")]
    [SerializeField, Min(0f)] private float _fuseTime = 3f;

    [Tooltip("켜면 핀을 뽑는 순간부터 신관이 탄다. 손에서 터질 수 있다.")]
    [SerializeField] private bool _startFuseOnPull = true;

    public HandActionBase HandPrefab => _handPrefab;

    public bool IsThrowable => _isThrowable;
    public PoolType ThrowProjectile => _throwProjectile;
    public float ThrowSpeed => _throwSpeed;
    public float FuseTime => _fuseTime;
    public bool StartFuseOnPull => _startFuseOnPull;

    public float UseDuration => _useDuration;
    public float UseCooldown => _useCooldown;
    public int ConsumeCount => _consumeCount;

    public float HealthRestore => _healthRestore;
    public float StaminaRestore => _staminaRestore;
    public float HungerRestore => _hungerRestore;
    public float ThirstRestore => _thirstRestore;

    /// <summary>다 쓴 뒤 대상에게 효과를 넣는다.</summary>
    public void Apply(Agent target)
    {
        if (target == null) return;

        if (_healthRestore > 0f)
        {
            Health health = target.GetCompo<Health>();
            if (health != null)
                health.Heal(_healthRestore);
        }

        VitalStatus vitals = target.GetCompo<VitalStatus>();
        if (vitals == null) return;

        vitals.Restore(VitalType.Stamina, _staminaRestore);
        vitals.Restore(VitalType.Hunger, _hungerRestore);
        vitals.Restore(VitalType.Thirst, _thirstRestore);
    }
}
