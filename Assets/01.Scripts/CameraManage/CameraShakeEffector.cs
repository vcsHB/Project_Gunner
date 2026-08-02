using UnityEngine;

public class CameraShakeEffector : MonoBehaviour, ICameraEffector
{
    [SerializeField] private float _defaultPower = 0.2f;
    [SerializeField] private float _defaultDuration = 0.15f;

    private Transform _transform;
    private Vector3 _originPosition;

    private float _power;
    private float _duration;
    private float _remainTime;

    public bool IsShaking => _remainTime > 0f;

    public void Intialize()
    {
        _transform = transform;
        _originPosition = _transform.localPosition;
    }

    public void Shake() => Shake(_defaultPower, _defaultDuration);

    public void Shake(float power, float duration)
    {
        // 이미 흔들리는 중이면 더 강한 쪽을 우선한다.
        if (IsShaking && power < _power) return;

        _power = power;
        _duration = duration;
        _remainTime = duration;
    }

    public void StopShake()
    {
        _remainTime = 0f;
        _transform.localPosition = _originPosition;
    }

    private void LateUpdate()
    {
        if (!IsShaking) return;

        _remainTime -= Time.deltaTime;

        if (_remainTime <= 0f)
        {
            StopShake();
            return;
        }

        // 시간이 지날수록 진폭이 줄어든다.
        float currentPower = _power * (_remainTime / _duration);
        _transform.localPosition = _originPosition + (Vector3)(Random.insideUnitCircle * currentPower);
    }
}
