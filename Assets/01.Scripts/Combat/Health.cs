using System;
using UnityEngine;

public class Health : MonoBehaviour, IDamageable, IAgentComponent
{
    [SerializeField] private float _currentHealth;
    [SerializeField] private float _maxHealth = 100f;

    public event Action<DamageData> OnDamagedEvent;
    public event Action<float, float> OnHealthChangedEvent; // (current, max)
    /// <summary>
    /// 죽었다 — <b>조종을 잃은 순간.</b> 입력·AI·조준·판정이 여기서 꺼진다.
    /// 몸이 부서지는 연출(폭발·잔해·흔들림·새 판)은 <see cref="OnWreckedEvent"/>를 들을 것 — 떠 있으면 추락하는 동안 둘이 벌어진다.
    /// </summary>
    public event Action OnDeadEvent;

    /// <summary>몸이 부서졌다(땅에 닿아 폭발). 땅에서 죽으면 <see cref="OnDeadEvent"/> 바로 뒤, 떠 있었으면 추락이 끝날 때.</summary>
    public event Action OnWreckedEvent;

    /// <summary>죽어 있다가 되살아났다(새 판·리스폰). 사망 연출을 되돌릴 쪽이 듣는다.</summary>
    public event Action OnRevivedEvent;

    public bool IsDead { get; private set; }

    /// <summary>몸이 부서졌다. 죽었지만 아직 추락 중이면 false.</summary>
    public bool IsWrecked { get; private set; }

    /// <summary>대시/스폰 무적 등에서 외부가 켜고 끈다.</summary>
    public bool IsInvincible { get; set; }

    public float CurrentHealth => _currentHealth;
    public float MaxHealth => _maxHealth;
    public float Normalized => _maxHealth <= 0f ? 0f : _currentHealth / _maxHealth;

    private Agent _owner;
    private AgentStatus _status;
    private ShieldStatus _shield;
    private MechaPartHealth _parts;
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
        _shield = _owner != null ? _owner.GetCompo<ShieldStatus>() : null;
        _parts = _owner != null ? _owner.GetCompo<MechaPartHealth>() : null;

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

        // 실드가 먼저 받는다. 남아 있었으면 그 타격은 거기서 끝난다 — 방어력도 타지 않는다.
        // 피격 이벤트는 그대로 쏜다. 맞았다는 사실(흔들림·피격 표시)은 실드에 막혀도 같다.
        if (_shield != null && _shield.TryAbsorb(damageData.damage))
        {
            damageData.blockedByShield = true;
            OnDamagedEvent?.Invoke(damageData);
            return DamageResponse.Hit();
        }

        float defense = _status != null ? _status.Defense.TotalValue : 0f;
        float resistance = _status != null ? _status.DamageResistance.TotalValue : 0f;
        float finalDamage = DamageHandler.CalculateFinalDamage(damageData.damage, defense, resistance);

        // 팔·추진체에 맞았으면 그 부위가 받는다. 부위가 없거나 이미 부서졌으면 본체(코어)로 온다.
        if (_parts != null && _parts.TryAbsorb(damageData.hitPart, finalDamage))
        {
            OnDamagedEvent?.Invoke(damageData);
            return DamageResponse.Hit();
        }

        SetHealth(_currentHealth - finalDamage);
        OnDamagedEvent?.Invoke(damageData);

        if (_currentHealth <= 0f)
            Die();

        return DamageResponse.Hit();
    }

    /// <summary>
    /// 방어력·저항을 무시하는 피해. 굶주림·탈수처럼 전투가 아닌 원인에 쓴다.
    /// 갑옷을 입었다고 덜 굶지는 않는다. 실드도 거치지 않는다 — 연료 고갈은 안에서 생기는 손상이다.
    /// </summary>
    public void ApplyDirectDamage(float amount)
    {
        if (IsDead || amount <= 0f) return;

        SetHealth(_currentHealth - amount);

        if (_currentHealth <= 0f)
            Die();
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f) return;

        SetHealth(_currentHealth + amount);
    }

    /// <summary>풀에서 재사용하거나 리스폰할 때 호출. 실드도 같이 채운다.</summary>
    public void ResetHealth()
    {
        bool wasDead = IsDead;

        IsDead = false;
        IsWrecked = false;
        IsInvincible = false;
        SetHealth(_maxHealth);

        if (_shield != null)
            _shield.ResetShield();

        if (_parts != null)
            _parts.ResetAll();

        if (wasDead)
            OnRevivedEvent?.Invoke();
    }

    /// <summary>
    /// 즉사. 실드·방어력·무적을 전부 무시한다. 착지 불가 지역으로의 추락처럼 "맞아서"가 아닌 죽음에 쓴다.
    /// </summary>
    public void Kill()
    {
        if (IsDead) return;

        SetHealth(0f);
        Die();
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

        // 떠 있으면 추락이 끝날 때 부서진다. 리스너가 되살렸으면(그럴 일은 없지만) 거기서 멈춘다.
        if (!IsDead) return;

        IDeathCrash crash = _owner != null ? _owner.GetCompo<IDeathCrash>() : null;
        if (crash != null && crash.TryBeginCrash(Wreck)) return;

        Wreck();
    }

    /// <summary>몸이 부서진다. 추락이 땅에 닿을 때 <see cref="IDeathCrash"/>가 부른다. 되살아난 뒤에 늦게 와도 무시한다.</summary>
    private void Wreck()
    {
        if (!IsDead || IsWrecked) return;

        IsWrecked = true;
        OnWreckedEvent?.Invoke();
    }

    // 버프 등으로 최대 체력이 변하면 현재 체력의 비율을 유지한다.
    private void HandleMaxHealthChanged(float newMaxHealth)
    {
        float ratio = Normalized;
        _maxHealth = newMaxHealth;
        SetHealth(_maxHealth * ratio);
    }
}
