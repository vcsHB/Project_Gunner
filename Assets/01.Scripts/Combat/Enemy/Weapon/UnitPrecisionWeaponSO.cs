using UnityEngine;

/// <summary>
/// <b>정밀 사격 — 조준선 → 고정 → 강한 한 발.</b>
///
/// 1. 노림(<see cref="ChargeTime"/>) — 조준선이 뜨고 기체는 계속 대상을 따라 돈다. "날 노린다"를 알린다.
/// 2. 고정(<see cref="LockTime"/>) — 기체가 더는 돌지 않고 조준선이 번쩍인다. 이 사이에 선 밖으로 빠지면 안 맞는다.
/// 3. 발사 — 한 발. 빠르고 세다.
/// 4. 쉼(<see cref="Cooldown"/>). 사정권 밖에서도 흐른다.
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

    public float ChargeTime => _chargeTime;
    public float LockTime => _lockTime;
    public float Cooldown => _cooldown;

    public override UnitFirePattern CreatePattern() => new PrecisionPattern(this);

    private class PrecisionPattern : UnitFirePattern
    {
        private readonly UnitPrecisionWeaponSO _data;

        private UnitWeaponPhase _phase = UnitWeaponPhase.Idle;
        private float _left;
        private float _restUntil;
        private bool _engaged;

        public PrecisionPattern(UnitPrecisionWeaponSO data) => _data = data;

        public override bool IsAimLocked => _phase == UnitWeaponPhase.Locked;

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

                    FireAndRest();
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

            // 고정까지 갔으면 끝까지 쏜다 — 예고를 보고 피한 사람에게 거짓말을 하지 않는다.
            // 그 한 발은 Tick이 다시 오지 않으므로 여기서 바로 낸다.
            if (_phase == UnitWeaponPhase.Locked)
            {
                FireAndRest();
                return;
            }

            if (_phase == UnitWeaponPhase.Charging)
                Enter(UnitWeaponPhase.Idle, 0f);
        }

        private void FireAndRest()
        {
            Weapon.Fire(1, 0f, 0f);

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
