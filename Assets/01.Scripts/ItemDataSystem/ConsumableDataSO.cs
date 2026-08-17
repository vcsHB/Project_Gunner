using UnityEngine;

/// <summary>
/// 먹거나 써서 없어지는 아이템. 붕대, 통조림, 물병 같은 것.
///
/// 쿨타임은 <b>아이템 종류별</b>이다. 붕대를 쓰면 인벤토리의 모든 붕대가 같이 잠긴다.
/// 개체별로 두면 칸을 나눠 담아 쿨타임을 우회할 수 있다.
/// </summary>
[CreateAssetMenu(menuName = "SO/Item/ConsumableData")]
public class ConsumableDataSO : ItemDataSO
{
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
