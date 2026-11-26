using UnityEngine;

/// <summary>
/// 빠르게 착륙해 바닥을 긁으면 화면이 흔들린다.
///
/// 이동이 카메라를 직접 부르지 않는다 — <see cref="PlayerMoveController.OnSkidStartedEvent"/>를 듣는다.
/// 발사·피격 흔들림(<see cref="WeaponFireCameraShake"/>, <see cref="DamageCameraShake"/>)과 같은 자리에 산다.
/// 빨리 내려앉을수록 세고 길게 흔들리되 상한이 있다.
/// </summary>
public class SkidCameraShake : MonoBehaviour
{
    [Tooltip("착륙 속도 비율(착륙 최고 속도 대비)이 이 값일 때 최대 세기가 된다.")]
    [SerializeField, Min(1.01f)] private float _fullShakeRatio = 2f;

    [SerializeField, Min(0f)] private float _minPower = 0.12f;
    [SerializeField, Min(0f)] private float _maxPower = 0.35f;
    [SerializeField, Min(0.01f)] private float _minDuration = 0.3f;
    [SerializeField, Min(0.01f)] private float _maxDuration = 0.7f;

    private PlayerMoveController _move;
    private CameraShakeEffector _shake;

    // HUD와 같은 방식. Agent의 컴포넌트 초기화가 Awake에서 끝나므로 Start에서 붙는다.
    private void Start()
    {
        if (_move == null)
            Bind(PlayerPilot.LocalMecha);

        // 갈아타면 다시 붙는다.
        PlayerPilot.OnLocalMechaChangedEvent += Bind;
    }

    public void Bind(Player player)
    {
        Unbind();

        if (player == null) return;

        _shake = CameraManager.Instance != null
            ? CameraManager.Instance.GetEffector<CameraShakeEffector>()
            : null;

        if (_shake == null) return;

        _move = player.GetCompo<PlayerMoveController>();

        if (_move != null)
            _move.OnSkidStartedEvent += HandleSkidStarted;
    }

    public void Unbind()
    {
        if (_move != null)
        {
            _move.OnSkidStartedEvent -= HandleSkidStarted;
            _move = null;
        }

        _shake = null;
    }

    private void OnDestroy()
    {
        PlayerPilot.OnLocalMechaChangedEvent -= Bind;
        Unbind();
    }

    private void HandleSkidStarted(float speedRatio)
    {
        if (_shake == null) return;

        float t = Mathf.InverseLerp(1f, _fullShakeRatio, speedRatio);

        _shake.Shake(Mathf.Lerp(_minPower, _maxPower, t), Mathf.Lerp(_minDuration, _maxDuration, t));
    }
}
