using System;
using UnityEngine;

/// <summary>
/// 손에 든 투척무기. <b>두 단계</b>로 동작한다.
///
/// 우클릭 = 안전핀 제거 + 조준, 좌클릭 = 던지기.
/// 핀을 뽑기 전에 좌클릭해도 나가지 않는다 — 실수로 던지는 것을 막는다.
///
/// 핀 상태를 이 오브젝트가 들고 가는 이유는, 그게 <b>손에 들고 있는 동안만</b> 의미 있는
/// 상태이기 때문이다. 칸을 넘기면 사라져야 한다(핀은 다시 꽂힌다).
/// </summary>
public class ThrowableHand : ConsumableHand
{
    [Tooltip("탄이 생기는 위치. 비우면 손 위치에서 나간다.")]
    [SerializeField] private Transform _muzzle;

    /// <summary>안전핀 상태가 바뀌었을 때. HUD가 신관 남은 시간을 띄우는 데 쓴다.</summary>
    public event Action<bool> OnPinPulledEvent;

    private PlayerAimController _aim;
    private float _fuseEndTime;

    public bool IsPinPulled { get; private set; }

    /// <summary>핀을 뽑은 뒤 터지기까지 남은 시간. 안 뽑았으면 0.</summary>
    public float FuseRemain
        => IsPinPulled && Data != null && Data.StartFuseOnPull
            ? Mathf.Max(0f, _fuseEndTime - Time.time)
            : 0f;

    public override void Initialize(Agent owner, ItemStack stack)
    {
        base.Initialize(owner, stack);

        if (owner != null)
            _aim = owner.GetCompo<PlayerAimController>();
    }

    #region 핀

    // 소모품의 "사용"을 쓰지 않는다. 투척은 사용 시간이 아니라 핀 상태로 굴러간다.
    public override void OnSecondaryPressed() => PullPin();

    public override void OnSecondaryReleased()
    {
        // 핀은 다시 꽂히지 않는다. 뽑는 순간 되돌릴 수 없어야 긴장이 산다.
        // 조준만 푼다.
        if (_aim != null)
            _aim.SetAiming(false);
    }

    private void PullPin()
    {
        if (IsPinPulled || Data == null) return;

        IsPinPulled = true;
        _fuseEndTime = Time.time + Data.FuseTime;

        if (_aim != null)
            _aim.SetAiming(true);

        OnPinPulledEvent?.Invoke(true);
    }

    #endregion

    public override void OnPrimaryPressed()
    {
        if (!IsPinPulled) return;

        Throw();
    }

    public override void OnUnequipped()
    {
        base.OnUnequipped();

        if (_aim != null)
            _aim.SetAiming(false);
    }

    private void Update()
    {
        if (!IsPinPulled || Data == null || !Data.StartFuseOnPull) return;

        // 들고 있다 시간이 다 되면 손에서 터진다. 핀을 뽑아두고 미루면 그 대가를 치른다.
        if (Time.time >= _fuseEndTime)
            Throw();
    }

    /// <summary>
    /// 투사체를 만들고 인벤토리에서 하나 뺀다.
    /// 남은 신관 시간이 곧 투사체의 수명이다 — 늦게 던질수록 빨리 터진다.
    /// </summary>
    private void Throw()
    {
        if (Data == null) return;

        float fuseRemain = FuseRemain;
        IsPinPulled = false;
        OnPinPulledEvent?.Invoke(false);

        if (_aim != null)
            _aim.SetAiming(false);

        SpawnProjectile(fuseRemain);

        if (Use != null)
            Use.ConsumeHeld(Data);
    }

    private void SpawnProjectile(float fuseRemain)
    {
        if (Data.ThrowProjectile == PoolType.None)
        {
            Debug.LogError($"[Throwable] {Data.DisplayName}에 투사체가 지정되지 않았습니다.", this);
            return;
        }

        if (ObjectPool.Get(Data.ThrowProjectile) is not Projectile projectile) return;

        Vector2 origin = _muzzle != null ? _muzzle.position : transform.position;
        Vector2 direction = _aim != null ? _aim.AimDirection : (Vector2)transform.right;

        // 조준점까지의 거리만큼만 날아간다. 던지는 무기는 조준한 곳에 떨어져야 한다.
        float distance = _aim != null
            ? Vector2.Distance(origin, _aim.AimPosition)
            : Data.ThrowSpeed;

        ProjectileLaunchData launch = new()
        {
            owner = Owner,
            direction = direction,
            speed = Data.ThrowSpeed,
            range = Mathf.Max(0.1f, distance),
            // 위력과 폭발 반경은 프리팹에 붙은 이펙터가 갖는다. 무기 스탯에서 오지 않는다.
            power = null,
            penetration = 0,
            explosionRadius = 0f,
        };

        projectile.Launch(origin, launch);
        projectile.OverrideLifeTime(Data.StartFuseOnPull ? fuseRemain : Data.FuseTime);
    }
}
