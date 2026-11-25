using UnityEngine;

/// <summary>
/// 떠 있는 적(드론·부표)의 몸을 그림자 위로 띄운다. 높이는 <see cref="EnemyMoveController.Altitude"/>(로직)가 정하고 여기는 그리기만 한다 —
/// 플레이어의 <see cref="GlideFeedback"/>과 같은 나눔.
///
/// 띄우는 대상(<see cref="_liftTargets"/>)에는 히트박스·총구가 같이 있다 — 보이는 곳에 맞고 보이는 곳에서 쏜다. <b>그림자는 넣지 않는다.</b>
/// 떠 있는 동안 위아래로 천천히 흔들린다. 추락하면 고도가 0으로 내려가며 흔들림도 잦아든다.
/// </summary>
public class UnitHoverFeedback : MonoBehaviour
{
    [Tooltip("화면 위로 띄울 것. 몸체 스프라이트·머리. 그림자는 넣지 않는다.")]
    [SerializeField] private Transform[] _liftTargets;

    [SerializeField, Min(0f)] private float _bobAmplitude = 0.05f;
    [SerializeField, Min(0f)] private float _bobFrequency = 1.3f;

    [Tooltip("그림자. 높이 있을수록 작아진다. 비우면 건드리지 않는다.")]
    [SerializeField] private Transform _shadow;
    [SerializeField, Range(0.2f, 1f)] private float _shadowScale = 0.7f;

    private EnemyMoveController _move;
    private Vector3[] _base;
    private Vector3 _shadowBase;
    private float _phase;
    private float _maxAltitude;

    private void Start()
    {
        Agent owner = GetComponentInParent<Agent>();
        _move = owner != null ? owner.GetCompo<EnemyMoveController>() : null;

        if (_liftTargets == null) _liftTargets = new Transform[0];

        _base = new Vector3[_liftTargets.Length];
        for (int i = 0; i < _liftTargets.Length; i++)
            if (_liftTargets[i] != null) _base[i] = _liftTargets[i].localPosition;

        if (_shadow != null) _shadowBase = _shadow.localScale;
        _maxAltitude = _move != null ? Mathf.Max(0.0001f, _move.Altitude) : 0.0001f;

        // 떼로 있을 때 같은 박자로 흔들리면 하나처럼 보인다. 연출이라 시드를 쓰지 않는다.
        _phase = Random.value * Mathf.PI * 2f;
    }

    private void LateUpdate()
    {
        if (_move == null) return;

        float altitude = _move.Altitude;
        float altitude01 = Mathf.Clamp01(altitude / _maxAltitude);
        float bob = Mathf.Sin(Time.time * _bobFrequency * Mathf.PI * 2f + _phase) * _bobAmplitude * altitude01;

        for (int i = 0; i < _liftTargets.Length; i++)
        {
            if (_liftTargets[i] == null) continue;

            _liftTargets[i].localPosition = _base[i] + Vector3.up * (altitude + bob);
        }

        if (_shadow != null)
            _shadow.localScale = _shadowBase * Mathf.Lerp(1f, _shadowScale, altitude01);
    }
}
