using UnityEngine;

public class PartPlayerStatus : MonoBehaviour
{
    [Header("Default Status Displays")]
    [SerializeField] private SimpleGauge _healthGauge;

    // 스태미나/허기/갈증은 아직 데이터가 없다. 시스템이 생기면 여기에 연결한다.
    [SerializeField] private SimpleGauge _staminaGauge;
    [SerializeField] private SimpleGauge _hungerGauge;
    [SerializeField] private SimpleGauge _thirstyGauge;

    [Header("Additional")]
    [SerializeField] private ConditionDisplayGroup _conditionDisplayGroup;

    private Health _health;

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

        if (_conditionDisplayGroup != null)
            _conditionDisplayGroup.Unbind();
    }

    private void OnDestroy() => Unbind();

    private void HandleHealthChanged(float current, float max)
    {
        if (_healthGauge == null) return;

        // 게이지 텍스트가 정수라 올림으로 맞춘다. 1 남았는데 0으로 보이면 안 된다.
        _healthGauge.UpdateProgress(Mathf.CeilToInt(current), Mathf.CeilToInt(max));
    }
}
