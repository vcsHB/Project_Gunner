using UnityEngine;

public class PartPlayerStatus : MonoBehaviour
{
    [Header("Default Status Displays")]
    [SerializeField] private SimpleGauge _healthGauge;

    [Tooltip("체력 게이지와 같은 자리에 겹친다. 실드가 남아 있으면 이것만, 없으면 체력만 보인다. 비워두면 자식 중 이름이 GaugeShield인 게이지를 찾는다.")]
    [SerializeField] private SimpleGauge _shieldGauge;

    [UnityEngine.Serialization.FormerlySerializedAs("_staminaGauge")]
    [SerializeField] private SimpleGauge _powerGauge;
    [UnityEngine.Serialization.FormerlySerializedAs("_hungerGauge")]
    [SerializeField] private SimpleGauge _fuelGauge;

    [Header("Additional")]
    [SerializeField] private ConditionDisplayGroup _conditionDisplayGroup;

    private Health _health;
    private VitalStatus _vitals;
    private ShieldStatus _shield;

    private const string ShieldGaugeName = "GaugeShield";

    private void Awake()
    {
        if (_shieldGauge != null) return;

        foreach (SimpleGauge gauge in GetComponentsInChildren<SimpleGauge>(true))
        {
            if (gauge.name != ShieldGaugeName) continue;

            _shieldGauge = gauge;
            break;
        }
    }

    public void Bind(Player player)
    {
        Unbind();

        if (player == null) return;

        _health = player.GetCompo<Health>();
        if (_health != null)
        {
            _health.OnHealthChangedEvent += HandleHealthChanged;
            HandleHealthChanged(_health.CurrentHealth, _health.MaxHealth);
        }

        _shield = player.GetCompo<ShieldStatus>();
        if (_shield != null)
        {
            _shield.OnShieldChangedEvent += HandleShieldChanged;
            HandleShieldChanged(_shield.Current, _shield.Max);
        }
        else
        {
            HandleShieldChanged(0f, 0f);
        }

        _vitals = player.GetCompo<VitalStatus>();
        if (_vitals != null)
        {
            _vitals.OnVitalChangedEvent += HandleVitalChanged;

            // 이벤트는 값이 바뀔 때만 온다. 붙는 순간의 값은 직접 한 번 읽어야 한다.
            foreach (VitalType type in System.Enum.GetValues(typeof(VitalType)))
                HandleVitalChanged(type, _vitals.Get(type), _vitals.GetMax(type));
        }

        if (_conditionDisplayGroup != null)
            _conditionDisplayGroup.Bind(player.GetCompo<AgentConditionEffector>());
    }

    public void Unbind()
    {
        if (_health != null)
        {
            _health.OnHealthChangedEvent -= HandleHealthChanged;
            _health = null;
        }

        if (_vitals != null)
        {
            _vitals.OnVitalChangedEvent -= HandleVitalChanged;
            _vitals = null;
        }

        if (_shield != null)
        {
            _shield.OnShieldChangedEvent -= HandleShieldChanged;
            _shield = null;
        }

        if (_conditionDisplayGroup != null)
            _conditionDisplayGroup.Unbind();
    }

    private void HandleVitalChanged(VitalType type, float current, float max)
    {
        SimpleGauge gauge = GetGauge(type);
        if (gauge == null) return;

        gauge.UpdateProgress(Mathf.CeilToInt(current), Mathf.CeilToInt(max));
    }

    private SimpleGauge GetGauge(VitalType type)
    {
        switch (type)
        {
            case VitalType.Power: return _powerGauge;
            case VitalType.Fuel: return _fuelGauge;
        }

        return null;
    }

    private void OnDestroy() => Unbind();

    /// <summary>
    /// 실드와 체력 게이지는 같은 자리에 겹쳐 있다. <b>실드가 남아 있으면 실드만, 없으면 체력만</b> 보인다.
    /// 실드가 깨진 순간(0) 체력 게이지로 바뀌고, 다시 차기 시작하면 실드 게이지로 돌아온다.
    /// 숨겨진 쪽도 값은 계속 갱신된다 — 켜지는 순간 옛 값이 보이면 안 된다.
    /// </summary>
    private void HandleShieldChanged(float current, float max)
    {
        bool showShield = _shieldGauge != null && max > 0f && current > 0f;

        if (_shieldGauge != null)
        {
            SetGaugeVisible(_shieldGauge, showShield);

            if (max > 0f)
                _shieldGauge.UpdateProgress(Mathf.CeilToInt(current), Mathf.CeilToInt(max));
        }

        if (_healthGauge != null)
            SetGaugeVisible(_healthGauge, !showShield);
    }

    private static void SetGaugeVisible(SimpleGauge gauge, bool visible)
    {
        if (gauge.gameObject.activeSelf != visible)
            gauge.gameObject.SetActive(visible);
    }

    private void HandleHealthChanged(float current, float max)
    {
        if (_healthGauge == null) return;

        // 게이지 텍스트가 정수라 올림으로 맞춘다. 1 남았는데 0으로 보이면 안 된다.
        _healthGauge.UpdateProgress(Mathf.CeilToInt(current), Mathf.CeilToInt(max));
    }
}
