using UnityEngine;

/// <summary>
/// 화면을 흔든다. <b>따라가기와 다른 Transform을 건드려야 한다.</b>
///
/// 따라가기(<see cref="CameraFollowEffector"/>)는 리그의 월드 좌표를 쓰고,
/// 흔들림은 그 아래 카메라의 <b>로컬</b> 좌표를 쓴다. 둘이 같은 오브젝트에 걸리면
/// 매 프레임 서로를 덮어써서 흔들림이 아예 보이지 않는다 — 에러도 나지 않는다.
///
/// 이 컴포넌트가 CameraManager에 붙어 있는 이유는 <see cref="CameraManager.GetEffector{T}"/>가
/// 자식에서 모으기 때문이다. 흔들 대상은 따로 지정한다.
/// </summary>
public class CameraShakeEffector : MonoBehaviour, ICameraEffector
{
    [Tooltip("실제로 흔들 대상. 리그 아래의 카메라를 넣는다. 비우면 자기 자신 — 보통 틀린 설정이다.")]
    [SerializeField] private Transform _target;

    [SerializeField] private float _defaultPower = 0.2f;
    [SerializeField] private float _defaultDuration = 0.15f;

    private Vector3 _originPosition;

    private float _power;
    private float _duration;
    private float _remainTime;

    public bool IsShaking => _remainTime > 0f;

    public void Intialize()
    {
        if (_target == null)
        {
            _target = transform;

            Debug.LogWarning(
                "[Shake] 흔들 대상이 비어 있어 자기 자신을 흔듭니다. " +
                "카메라가 자식이 아니면 화면은 그대로입니다.", this);
        }

        _originPosition = _target.localPosition;
    }

    public void Shake() => Shake(_defaultPower, _defaultDuration);

    public void Shake(float power, float duration)
    {
        // 이미 흔들리는 중이면 더 강한 쪽을 우선한다. 더하면 연사에서 화면을 못 본다.
        if (IsShaking && power < _power) return;

        _power = power;
        _duration = duration;
        _remainTime = duration;
    }

    public void StopShake()
    {
        _remainTime = 0f;

        if (_target != null)
            _target.localPosition = _originPosition;
    }

    private void LateUpdate()
    {
        if (!IsShaking || _target == null) return;

        _remainTime -= Time.deltaTime;

        if (_remainTime <= 0f)
        {
            StopShake();
            return;
        }

        // 시간이 지날수록 진폭이 줄어든다.
        float currentPower = _power * (_remainTime / _duration);

        _target.localPosition = _originPosition + (Vector3)(Random.insideUnitCircle * currentPower);
    }
}
