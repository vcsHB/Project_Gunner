using System;
using UnityEngine;

public class Health : MonoBehaviour, IDamageable, IAgentComponent
{
    [SerializeField] private float _currentHealth;
    [SerializeField] private float _maxHealth = 100f;

    public event Action<DamageData> OnDamagedEvent;
    public event Action<float, float> OnHealthChangedEvent; // (current, max)
    public event Action OnDeadEvent;

    public bool IsDead { get; private set; }

    /// <summary>대시/스폰 무적 등에서 외부가 켜고 끈다.</summary>
    public bool IsInvincible { get; set; }

    public float CurrentHealth => _currentHealth;
    public float MaxHealth => _maxHealth;
    public float Normalized => _maxHealth <= 0f ? 0f : _currentHealth / _maxHealth;

    private Agent _owner;
    private AgentStatus _status;

    public void Initialize(Agent owner)
    {
        _owner = owner;
    }

    public void AfterInitialize()
    {
        _status = _owner.GetCompo<AgentStatus>();

        if (_status != null)
        {
            _status.Health.OnChangedEvent += HandleMaxHealthChanged;
            _maxHealth = _status.Health.TotalValue;
        }

        ResetHealth();
    }

    public void Dispose()
    {
        if (_status != null)
            _status.Health.OnChangedEvent -= HandleMaxHealthChanged;
    }

    public DamageResponse ApplyDamage(DamageData damageData)
    {
        if (IsDead || IsInvincible || damageData.damage <= 0f)
            return DamageResponse.Miss;

        float defense = _status != null ? _status.Defense.TotalValue : 0f;
        float finalDamage = DamageHandler.ApplyDefense(damageData.damage, defense);

        SetHealth(_currentHealth - finalDamage);
        OnDamagedEvent?.Invoke(damageData);

        if (_currentHealth <= 0f)
            Die();

        return DamageResponse.Hit();
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f) return;

        SetHealth(_currentHealth + amount);
    }

    /// <summary>풀에서 재사용하거나 리스폰할 때 호출.</summary>
    public void ResetHealth()
    {
        IsDead = false;
        IsInvincible = false;
        SetHealth(_maxHealth);
    }

    private void SetHealth(float value)
    {
        _currentHealth = Mathf.Clamp(value, 0f, _maxHealth);
        OnHealthChangedEvent?.Invoke(_currentHealth, _maxHealth);
    }

    private void Die()
    {
        if (IsDead) return;

        IsDead = true;
        OnDeadEvent?.Invoke();
    }

    // 버프 등으로 최대 체력이 변하면 현재 체력의 비율을 유지한다.
    private void HandleMaxHealthChanged(float newMaxHealth)
    {
        float ratio = Normalized;
        _maxHealth = newMaxHealth;
        SetHealth(_maxHealth * ratio);
    }
}
