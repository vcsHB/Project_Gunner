using UnityEngine;

public class PartPlayerStatus : MonoBehaviour
{
    [Header("Default Status Displays")]
    [SerializeField] private SimpleGauge _healthGauge;

    [SerializeField] private SimpleGauge _staminaGauge;
    [SerializeField] private SimpleGauge _hungerGauge;
    [SerializeField] private SimpleGauge _thirstyGauge;

    [Header("Additional")]
    [SerializeField] private ConditionDisplayGroup _conditionDisplayGroup;

    private Health _health;
    private VitalStatus _vitals;

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
            case VitalType.Stamina: return _staminaGauge;
            case VitalType.Hunger: return _hungerGauge;
            case VitalType.Thirst: return _thirstyGauge;
        }

        return null;
    }

    private void OnDestroy() => Unbind();

    private void HandleHealthChanged(float current, float max)
    {
        if (_healthGauge == null) return;

        // 게이지 텍스트가 정수라 올림으로 맞춘다. 1 남았는데 0으로 보이면 안 된다.
        _healthGauge.UpdateProgress(Mathf.CeilToInt(current), Mathf.CeilToInt(max));
    }
}
