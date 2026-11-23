using UnityEngine;

/// <summary>
/// <b>연발 → 쉼.</b> 몇 발 쏘고 쉰다. 쉬는 동안이 플레이어가 파고들 틈이다.
///
/// 수치만으로 여러 공격이 된다.
///
/// | 하고 싶은 것 | 설정 |
/// |---|---|
/// | 단발 포 | 연발 1, 쉼 길게 |
/// | 기관포 | 연발 많이, 간격 짧게, 흩어짐 조금 |
/// | 산탄 | 한 번에 여러 발 + 부채꼴 |
/// </summary>
[CreateAssetMenu(menuName = "SO/Unit/Weapon/Burst")]
public class UnitBurstWeaponSO : UnitWeaponSO
{
    [Header("연발")]
    [Tooltip("한 번 공격에 쏘는 횟수.")]
    [SerializeField, Min(1)] private int _shotsPerBurst = 3;

    [Tooltip("연발 안에서 발 사이 간격(초).")]
    [SerializeField, Min(0.01f)] private float _shotInterval = 0.12f;

    [Tooltip("연발이 끝나고 다음 연발까지 쉬는 시간(초).")]
    [SerializeField, Min(0f)] private float _burstRest = 1.5f;

    [Header("탄 퍼짐")]
    [Tooltip("한 번 쏠 때 나가는 탄 수. 2 이상이면 산탄.")]
    [SerializeField, Min(1)] private int _pelletsPerShot = 1;

    [Tooltip("산탄이 펼쳐지는 부채꼴 전체 각도(도). 탄이 이 안에 고르게 놓인다.")]
    [SerializeField, Range(0f, 180f)] private float _pelletFan = 30f;

    [Tooltip("발마다 무작위로 흩어지는 전체 각도(도). 시드 난수.")]
    [SerializeField, Range(0f, 90f)] private float _spread = 4f;

    public int ShotsPerBurst => _shotsPerBurst;
    public float ShotInterval => _shotInterval;
    public float BurstRest => _burstRest;
    public int PelletsPerShot => _pelletsPerShot;
    public float PelletFan => _pelletFan;
    public float Spread => _spread;

    public override UnitFirePattern CreatePattern() => new BurstPattern(this);

    private class BurstPattern : UnitFirePattern
    {
        private readonly UnitBurstWeaponSO _data;

        private float _engageLeft;
        private float _restUntil;
        private bool _resting;
        private float _shotLeft;
        private int _shotsLeft;
        private bool _engaged;

        public BurstPattern(UnitBurstWeaponSO data) => _data = data;

        public override void Tick(bool aimed, float deltaTime)
        {
            if (!_engaged)
            {
                _engaged = true;
                _engageLeft = _data.EngageDelay;
            }

            if (_engageLeft > 0f)
            {
                _engageLeft -= deltaTime;
                return;
            }

            if (_shotsLeft > 0)
            {
                TickBurst(aimed, deltaTime);
                return;
            }

            if (_resting)
            {
                if (Now < _restUntil) return;

                _resting = false;
                Weapon.SetPhase(UnitWeaponPhase.Idle, 0f);
            }

            // 대상 쪽을 보고 있을 때만 새 연발을 시작한다.
            if (!aimed) return;

            _shotsLeft = _data.ShotsPerBurst;
            _shotLeft = 0f;
            Weapon.SetPhase(UnitWeaponPhase.Firing, _data.ShotsPerBurst * _data.ShotInterval);

            TickBurst(true, 0f);
        }

        private void TickBurst(bool aimed, float deltaTime)
        {
            _shotLeft -= deltaTime;
            if (_shotLeft > 0f) return;

            // 연발 중에 대상이 빠져나가 포탑이 덜 돌았으면 그 발은 기다린다. 엉뚱한 쪽으로 쏟아내지 않는다.
            if (!aimed) return;

            Weapon.Fire(_data.PelletsPerShot, _data.PelletFan, _data.Spread);

            _shotsLeft--;
            _shotLeft = _data.ShotInterval;

            if (_shotsLeft <= 0) BeginRest();
        }

        private void BeginRest()
        {
            _shotsLeft = 0;
            _resting = true;
            _restUntil = Now + _data.BurstRest;
            Weapon.SetPhase(UnitWeaponPhase.Resting, _data.BurstRest);
        }

        public override void Stop(bool cancel)
        {
            _engaged = false;
            _engageLeft = 0f;

            if (cancel)
            {
                _shotsLeft = 0;
                _resting = false;
                Weapon.SetPhase(UnitWeaponPhase.Idle, 0f);
                return;
            }

            // 연발을 끊어도 쉼은 준다. 사정권 경계를 들락거려 쉼 없이 쏘게 하지 않는다.
            if (_shotsLeft > 0) BeginRest();
        }
    }
}
