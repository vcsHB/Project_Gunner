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
    private bool _initialized;

    private void Awake()
    {
        // Agent가 없는 단독 대상(폭발통, 파괴 가능한 엄폐물)은 아무도 Initialize를 불러주지 않는다.
        if (GetComponentInParent<Agent>() == null)
            EnsureInitialized();
    }

    public void Initialize(Agent owner)
    {
        _owner = owner;
    }

    public void AfterInitialize()
    {
        EnsureInitialized();
    }

    public void Dispose()
    {
        if (_status != null)
            _status.Health.OnChangedEvent -= HandleMaxHealthChanged;
    }

    private void OnDestroy() => Dispose();

    private void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        // Agent가 없으면 스탯도 없다. 이 경우 인스펙터의 _maxHealth를 그대로 쓴다.
        _status = _owner != null ? _owner.GetCompo<AgentStatus>() : null;

        if (_status != null)
        {
            _status.Health.OnChangedEvent += HandleMaxHealthChanged;
            _maxHealth = _status.Health.TotalValue;
        }

        ResetHealth();
    }

    public DamageResponse ApplyDamage(DamageData damageData)
    {
        if (IsDead || IsInvincible || damageData.damage <= 0f)
            return DamageResponse.Miss;

        float defense = _status != null ? _status.Defense.TotalValue : 0f;
        float resistance = _status != null ? _status.DamageResistance.TotalValue : 0f;
        float finalDamage = DamageHandler.CalculateFinalDamage(damageData.damage, defense, resistance);

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
