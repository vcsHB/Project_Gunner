using UnityEngine;

/// <summary>
/// <b>정밀 사격 — 조준선 → 고정 → 발사.</b>
///
/// 1. 노림(<see cref="ChargeTime"/>) — 조준선이 뜨고 기체는 계속 대상을 따라 돈다. "날 노린다"를 알린다.
/// 2. 고정(<see cref="LockTime"/>) — 기체가 더는 돌지 않고 조준선이 번쩍인다. 이 사이에 선 밖으로 빠지면 안 맞는다.
/// 3. 발사 — <see cref="Shots"/>발. 1이면 강한 한 발(저격·대포), 여럿이면 <b>조준선을 따라 긋는 연발</b>(드론 기관총).
///    연발 동안에도 고정은 풀리지 않는다 — 선 위로 쏟아진다. 조준선은 다 쏠 때까지 남는다.
/// 4. 쉼(<see cref="Cooldown"/>). 조준선이 꺼진다. 사정권 밖에서도 흐른다.
///
/// 노리는 도중 대상이 사정권을 벗어나면(<see cref="UnitFirePattern.Stop"/>) 처음부터 다시 한다.
/// 고정된 뒤에는 끊지 않는다 — 고정을 보고 피했는데 쏘지도 않으면 예고가 거짓말이 된다.
/// </summary>
[CreateAssetMenu(menuName = "SO/Unit/Weapon/Precision")]
public class UnitPrecisionWeaponSO : UnitWeaponSO
{
    [Header("정밀 사격")]
    [Tooltip("조준선을 띄우고 따라 도는 시간(초).")]
    [SerializeField, Min(0f)] private float _chargeTime = 1.2f;

    [Tooltip("조준을 고정하고 기다리는 시간(초). 피할 구간이다. 0이면 피할 수 없는 저격이 된다.")]
    [SerializeField, Min(0f)] private float _lockTime = 0.45f;

    [Tooltip("쏘고 나서 다시 노리기 시작하기까지(초).")]
    [SerializeField, Min(0f)] private float _cooldown = 2.5f;

    [Header("발사")]
    [Tooltip("고정 뒤 쏘는 발 수. 1이면 한 발, 여럿이면 조준선을 따라 긋는 연발.")]
    [SerializeField, Min(1)] private int _shots = 1;

    [Tooltip("연발 간격(초).")]
    [SerializeField, Min(0.01f)] private float _shotInterval = 0.08f;

    [Tooltip("발마다 흩어지는 전체 각도(도). 시드 난수.")]
    [SerializeField, Range(0f, 45f)] private float _spread;

    public float ChargeTime => _chargeTime;
    public float LockTime => _lockTime;
    public float Cooldown => _cooldown;
    public int Shots => _shots;
    public float ShotInterval => _shotInterval;
    public float Spread => _spread;

    public override UnitFirePattern CreatePattern() => new PrecisionPattern(this);

    private class PrecisionPattern : UnitFirePattern
    {
        private readonly UnitPrecisionWeaponSO _data;

        private UnitWeaponPhase _phase = UnitWeaponPhase.Idle;
        private float _left;
        private float _restUntil;
        private bool _engaged;
        private int _shotsLeft;

        public PrecisionPattern(UnitPrecisionWeaponSO data) => _data = data;

        // 연발 중에도 고정 — 선을 보고 빠진 사람에게 따라 꺾이는 탄을 보내지 않는다.
        public override bool IsAimLocked => _phase == UnitWeaponPhase.Locked || _phase == UnitWeaponPhase.Firing;

        public override void Tick(bool aimed, float deltaTime)
        {
            if (!_engaged)
            {
                _engaged = true;

                // 쉬는 중이 아니었으면 반응 시간부터.
                if (_phase == UnitWeaponPhase.Idle) _left = _data.EngageDelay;
            }

            switch (_phase)
            {
                case UnitWeaponPhase.Idle:
                    _left -= deltaTime;
                    if (_left > 0f || !aimed) return;

                    Enter(UnitWeaponPhase.Charging, _data.ChargeTime);
                    return;

                case UnitWeaponPhase.Charging:
                    _left -= deltaTime;
                    if (_left > 0f) return;

                    Enter(UnitWeaponPhase.Locked, _data.LockTime);
                    return;

                case UnitWeaponPhase.Locked:
                    _left -= deltaTime;
                    if (_left > 0f) return;

                    BeginVolley();
                    return;

                case UnitWeaponPhase.Firing:
                    _left -= deltaTime;
                    if (_left > 0f) return;

                    FireOne();
                    return;

                case UnitWeaponPhase.Resting:
                    if (Now < _restUntil) return;

                    Enter(UnitWeaponPhase.Idle, 0f);
                    return;
            }
        }

        public override void Stop(bool cancel)
        {
            _engaged = false;

            if (cancel)
            {
                if (_phase != UnitWeaponPhase.Idle) Enter(UnitWeaponPhase.Idle, 0f);
                return;
            }

            // 고정까지 갔으면 쏜다 — 예고를 보고 피한 사람에게 거짓말을 하지 않는다.
            // Tick이 다시 오지 않으므로 첫 발은 여기서 바로 내고 쉰다. 연발 도중이면 남은 발은 버린다.
            if (_phase == UnitWeaponPhase.Locked)
            {
                Weapon.Fire(1, 0f, _data.Spread);
                Rest();
                return;
            }

            if (_phase == UnitWeaponPhase.Firing)
            {
                Rest();
                return;
            }

            if (_phase == UnitWeaponPhase.Charging)
                Enter(UnitWeaponPhase.Idle, 0f);
        }

        private void BeginVolley()
        {
            _shotsLeft = _data.Shots;
            Enter(UnitWeaponPhase.Firing, _data.Shots * _data.ShotInterval);
            _left = 0f;

            FireOne();
        }

        private void FireOne()
        {
            Weapon.Fire(1, 0f, _data.Spread);

            _shotsLeft--;
            _left = _data.ShotInterval;

            if (_shotsLeft <= 0) Rest();
        }

        private void Rest()
        {
            _shotsLeft = 0;
            _restUntil = Now + _data.Cooldown;
            Enter(UnitWeaponPhase.Resting, _data.Cooldown);
        }

        private void Enter(UnitWeaponPhase phase, float duration)
        {
            _phase = phase;
            _left = duration;
            Weapon.SetPhase(phase, duration);
        }
    }
}
