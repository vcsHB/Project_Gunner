using System;
using UnityEngine;

/// <summary>
/// 투사체를 쏘는 무기. 발사 모드, 쿨타임, 산탄, 탄약 소모가 여기 모인다.
///
/// 이펙트·소리·카메라 흔들림은 <b>하지 않는다.</b> 이벤트만 쏘고 표현은 구독자가 한다.
/// 전투 로직이 직접 재생하기 시작하면 서버 권한 구조로 옮길 수 없다.
///
/// 타이머를 코루틴 대신 Update로 도는 이유는 무기가 핫바 전환 때마다 파괴되기 때문이다.
/// 파괴 도중 코루틴이 살아있으면 이미 없는 무기가 한 발 더 쏜다.
/// </summary>
public class PlayerRangedWeapon : PlayerWeaponBase
{
    [Tooltip("탄이 생기는 위치. 비워두면 무기 본체에서 나간다.")]
    [SerializeField] private Transform _muzzle;

    /// <summary>실제로 한 발 나갔을 때. 인자는 반동 세기다. (머즐 플래시, 소리, 화면 흔들림용)</summary>
    public event Action<float> OnFiredEvent;

    /// <summary>탄이 없어 격발이 실패했을 때.</summary>
    public event Action OnDryFireEvent;

    public WeaponAmmoController Ammo { get; private set; }

    private PlayerAimController _aim;

    private float _cooldown;
    private bool _triggerHeld;

    // 점사
    private int _burstLeft;
    private float _burstTimer;

    // 차지
    private bool _isCharging;
    private float _chargeElapsed;

    /// <summary>차지가 하나도 안 됐을 때의 최소 위력. 0으로 두면 실수 격발이 완전 낭비가 된다.</summary>
    private const float MinChargeRatio = 0.25f;

    public bool IsCharging => _isCharging;

    /// <summary>0~1. 차지 무기 UI가 그대로 쓴다.</summary>
    public float ChargeRatio01 => _isCharging ? ChargeRatio() : 0f;

    public override void Initialize(PlayerWeaponDataSO data, Agent owner, WeaponItemInstance instance = null)
    {
        base.Initialize(data, owner, instance);

        InventoryController inventoryController = null;
        if (owner != null)
        {
            _aim = owner.GetCompo<PlayerAimController>();
            inventoryController = owner.GetCompo<InventoryController>();
        }

        Ammo = new WeaponAmmoController(this, inventoryController);
    }

    public override void OnUnequipped()
    {
        // 차지를 먼저 끈다. base가 OnAttackReleased를 부르는데,
        // 차지 중이면 그게 격발로 이어져서 무기를 넘기는 것만으로 한 발이 나간다.
        _isCharging = false;
        _chargeElapsed = 0f;
        _burstLeft = 0;

        base.OnUnequipped();

        Ammo?.CancelReload();
    }

    #region Input

    public override void OnAttackPressed()
    {
        _triggerHeld = true;

        switch (CurrentFireMode)
        {
            case WeaponFireMode.Charge:
                _isCharging = true;
                _chargeElapsed = 0f;
                break;

            case WeaponFireMode.Burst:
                if (_cooldown <= 0f && _burstLeft <= 0)
                {
                    _burstLeft = Mathf.Max(1, Mathf.RoundToInt(Status.Value(WeaponStatType.BurstCount)));
                    _burstTimer = 0f;
                }
                break;

            default:
                TryFire(1f);
                break;
        }
    }

    public override void OnAttackReleased()
    {
        _triggerHeld = false;

        if (!_isCharging) return;

        // 차지는 뗄 때 나간다. 덜 찼으면 덜 찬 만큼만.
        float ratio = ChargeRatio();
        _isCharging = false;
        _chargeElapsed = 0f;

        TryFire(ratio);
    }

    public override void OnReloadRequested() => Ammo.BeginReload();

    public override void OnAmmoCycleRequested(int direction) => Ammo.CycleAmmoType(direction);

    #endregion

    private void Update()
    {
        // Initialize 전에는 스탯도 탄창도 없다. 프리팹을 씬에 직접 놓고 켜두면 여기로 들어온다.
        if (Ammo == null) return;

        float deltaTime = Time.deltaTime;

        if (_cooldown > 0f)
            _cooldown -= deltaTime;

        Ammo.Tick(deltaTime);

        if (_isCharging)
            _chargeElapsed += deltaTime;

        TickBurst(deltaTime);

        if (_triggerHeld && CurrentFireMode == WeaponFireMode.Auto)
            TryFire(1f);
    }

    private void TickBurst(float deltaTime)
    {
        if (_burstLeft <= 0) return;

        if (_burstTimer > 0f)
        {
            _burstTimer -= deltaTime;
            return;
        }

        // 점사 중에는 쿨타임을 보지 않는다. 묶음이 끝난 뒤에 한 번만 건다.
        if (!Shoot(1f))
        {
            _burstLeft = 0;
            return;
        }

        _burstLeft--;

        if (_burstLeft > 0)
            _burstTimer = Mathf.Max(0f, Status.Value(WeaponStatType.BurstInterval));
        else
            _cooldown = Mathf.Max(0f, Status.Value(WeaponStatType.AttackCooltime));
    }

    private float ChargeRatio()
    {
        float chargeTime = Status.Value(WeaponStatType.ChargeTime);
        if (chargeTime <= 0f) return 1f;

        return Mathf.Clamp(_chargeElapsed / chargeTime, MinChargeRatio, 1f);
    }

    /// <summary>쿨타임을 확인하고 쏜다.</summary>
    private void TryFire(float powerRatio)
    {
        if (_cooldown > 0f) return;
        if (!Shoot(powerRatio)) return;

        _cooldown = Mathf.Max(0f, Status.Value(WeaponStatType.AttackCooltime));
    }

    /// <summary>실제 격발. 쿨타임은 보지 않는다(점사가 자기 간격으로 돌기 때문).</summary>
    private bool Shoot(float powerRatio)
    {
        // 내구도가 다 닳은 무기는 탄이 있어도 나가지 않는다.
        // 슬롯의 _itemNotUseable과 같은 판정을 쓴다 — 표시와 동작이 갈리면 안 된다.
        if (Instance != null && Instance.IsBroken)
        {
            OnDryFireEvent?.Invoke();
            _triggerHeld = false;
            return false;
        }

        if (!Ammo.CanFire)
        {
            OnDryFireEvent?.Invoke();

            // 빈 방아쇠를 당기면 알아서 재장전한다. 매번 R을 누르게 하면 손이 바쁘다.
            if (!Ammo.IsReloading)
                Ammo.BeginReload();

            _triggerHeld = false;
            return false;
        }

        if (!Ammo.TryConsumeForShot()) return false;

        // 한 발이 실제로 나갔을 때만 닳는다. 빈 격발로 총이 망가지면 억울하다.
        Instance?.ConsumeDurability();

        FireProjectiles(powerRatio);

        OnFiredEvent?.Invoke(Status.Value(WeaponStatType.Recoil));
        return true;
    }

    private void FireProjectiles(float powerRatio)
    {
        PoolType pool = ResolveProjectilePool();
        if (pool == PoolType.None)
        {
            Debug.LogError($"[Weapon] {Data.DisplayName}에 투사체가 지정되지 않았습니다.", this);
            return;
        }

        Vector2 origin = _muzzle != null ? _muzzle.position : transform.position;
        Vector2 aim = _aim != null ? _aim.AimDirection : (Vector2)transform.right;

        int count = Mathf.Max(1, Mathf.RoundToInt(Status.Value(WeaponStatType.ProjectileCount)));
        float spread = Mathf.Max(0f, Status.Value(WeaponStatType.Spread));

        ProjectileLaunchData data = new()
        {
            owner = Owner,
            speed = Mathf.Max(0.01f, Status.Value(WeaponStatType.ProjectileSpeed)) * powerRatio,
            range = Mathf.Max(0.01f, Status.Value(WeaponStatType.Range)),
            power = new CastPower(
                Status.Value(WeaponStatType.Damage) * powerRatio,
                Status.Value(WeaponStatType.CriticalRate)),
            penetration = Mathf.Max(0, Mathf.RoundToInt(Status.Value(WeaponStatType.Penetration))),
            explosionRadius = Mathf.Max(0f, Status.Value(WeaponStatType.ExplosionRadius)),
        };

        for (int i = 0; i < count; i++)
        {
            data.direction = ApplySpread(aim, spread);

            Projectile projectile = ObjectPool.Get(pool) as Projectile;
            if (projectile == null) return;

            projectile.Launch(origin, data);
        }
    }

    /// <summary>탄약이 자기 투사체를 지정했으면 그것, 아니면 무기의 기본 투사체.</summary>
    private PoolType ResolveProjectilePool()
    {
        AmmoDataSO ammo = Ammo.LoadedAmmo;
        if (ammo != null && ammo.ProjectilePool != PoolType.None)
            return ammo.ProjectilePool;

        return Data.DefaultProjectile;
    }

    /// <summary>
    /// 산탄 각도. UnityEngine.Random이 아니라 시드 난수를 쓴다 —
    /// 멀티에서 클라마다 탄이 다른 곳으로 날아가면 안 된다.
    /// </summary>
    private static Vector2 ApplySpread(Vector2 direction, float spreadDegrees)
    {
        if (spreadDegrees <= 0f) return direction;

        float half = spreadDegrees * 0.5f;
        float angle = GameRandom.Combat.Range(-half, half);

        return (Vector2)(Quaternion.Euler(0f, 0f, angle) * direction);
    }
}
