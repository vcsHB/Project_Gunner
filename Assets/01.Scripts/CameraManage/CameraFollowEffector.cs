using UnityEngine;

/// <summary>
/// 카메라가 플레이어와 조준점 <b>사이</b>를 본다. 플레이어를 정확히 따라가면
/// 조준하는 쪽이 화면 가장자리라 멀리 있는 적이 안 보인다.
///
/// 치우치는 정도에 상한을 둔다. 조준점을 화면 밖까지 끌고 가면
/// 플레이어가 화면에서 밀려나 자기 위치를 놓친다.
///
/// 흔들림(CameraShakeEffector)과 겹치지 않도록 이 컴포넌트는 <b>리그의 위치만</b> 옮긴다.
/// 흔들림은 그 아래 카메라의 로컬 좌표를 건드린다.
/// </summary>
[DefaultExecutionOrder(AimExecutionOrder.CameraFollow)]
public class CameraFollowEffector : MonoBehaviour, ICameraEffector
{
    [Tooltip("따라갈 대상. 비우면 씬에서 Player를 찾는다.")]
    [SerializeField] private Transform _target;

    [Tooltip("실제로 움직일 오브젝트. 비우면 자기 자신.")]
    [SerializeField] private Transform _rig;
    [Header("따라가기")]
    [Tooltip("0이면 플레이어만, 1이면 조준점만 본다. 0.5면 정확히 중간.")]
    [SerializeField, Range(0f, 1f)] private float _aimWeight = 0.5f;

    [Tooltip("카메라가 치우칠 수 있는 최대치. x는 반너비, y는 반높이에 대한 비율이다. " +
             "절대 거리가 아니라 비율이라 해상도나 줌이 바뀌어도 화면 밖으로 나가지 않는다.")]
    [SerializeField] private Vector2 _maxAimOffsetRatio = new(0.35f, 0.35f);

    [Tooltip("따라붙는 부드러움. 작을수록 즉각적이다.")]
    [SerializeField] private float _smoothTime = 0.12f;

    [Header("정조준 줌")]
    [Tooltip("정조준 중일 때 orthographicSize에 곱하는 값. 1보다 작으면 확대된다.")]
    [SerializeField, Range(0.3f, 1f)] private float _aimZoomScale = 0.85f;

    [Tooltip("줌이 바뀌는 속도.")]
    [SerializeField] private float _zoomSpeed = 6f;

    private Camera _camera;
    private PlayerAimController _aim;

    private Vector3 _velocity;
    private float _baseSize;
    private bool _isAiming;

    public void Intialize()
    {
        if (_rig == null)
            _rig = transform;

        _camera = CameraManager.HasInstance ? CameraManager.Instance.MainCamera : Camera.main;

        if (_camera != null)
            _baseSize = _camera.orthographicSize;

        // 흔들림은 localPosition을, 따라가기는 position을 쓴다.
        // 같은 오브젝트에 있으면 매 프레임 서로를 덮어써서 흔들림이 아예 안 보인다.
        if (_rig.GetComponent<CameraShakeEffector>() != null)
        {
            Debug.LogWarning(
                "[Camera] 따라가기와 흔들림이 같은 오브젝트에 있습니다. " +
                "따라가기는 리그(부모)에, 흔들림은 카메라(자식)에 두세요.", this);
        }
    }

    // 플레이어의 컴포넌트 초기화가 Awake에서 끝나므로 Start에서 붙는다. HUD와 같은 방식이다.
    private void Start()
    {
        if (_aim == null)
            Bind(FindAnyObjectByType<Player>());
    }

    public void Bind(Player player)
    {
        Unbind();

        if (player == null) return;

        if (_target == null)
            _target = player.transform;

        _aim = player.GetCompo<PlayerAimController>();

        if (_aim != null)
        {
            _aim.OnAimingChangedEvent += HandleAimingChanged;
            HandleAimingChanged(_aim.IsAiming);
        }

        // 붙는 순간 목표 지점으로 순간이동시킨다. 안 그러면 원점에서 날아온다.
        SnapToTarget();
    }

    public void Unbind()
    {
        if (_aim == null) return;

        _aim.OnAimingChangedEvent -= HandleAimingChanged;
        _aim = null;
    }

    private void OnDestroy() => Unbind();

    private void HandleAimingChanged(bool aiming) => _isAiming = aiming;

    // 이동이 FixedUpdate에서 끝난 뒤에 따라가야 캐릭터가 떨리지 않는다.
    private void LateUpdate()
    {
        if (_target == null) return;

        Vector3 desired = GetDesiredPosition();

        _rig.position = Vector3.SmoothDamp(_rig.position, desired, ref _velocity, _smoothTime);

        UpdateZoom(Time.deltaTime);
    }

    private Vector3 GetDesiredPosition()
    {
        Vector2 player = _target.position;

        // 조준 컨트롤러가 없으면 그냥 플레이어를 따라간다. 무기 없이 돌아다니는 씬도 있다.
        Vector2 focus = _aim != null ? _aim.AimPosition : player;

        // 치우침도 축별로 자른다. 거리 하나로 자르면 세로가 짧은 화면에서
        // 플레이어가 위아래 화면 밖으로 밀려난다.
        Vector2 offset = (focus - player) * _aimWeight;
        Vector2 max = Vector2.Scale(GetHalfExtents(), ClampRatio(_maxAimOffsetRatio));

        offset.x = Mathf.Clamp(offset.x, -max.x, max.x);
        offset.y = Mathf.Clamp(offset.y, -max.y, max.y);

        Vector2 target = player + offset;

        // z는 카메라가 원래 있던 깊이를 유지한다. 2D에서 z를 건드리면 컬링이 어긋난다.
        return new Vector3(target.x, target.y, _rig.position.z);
    }

    /// <summary>지금 카메라가 보여주는 반너비·반높이. 줌이 바뀌면 같이 바뀐다.</summary>
    private Vector2 GetHalfExtents()
    {
        float halfHeight = _camera != null ? _camera.orthographicSize : 5f;
        float aspect = _camera != null ? _camera.aspect : 16f / 9f;

        return new Vector2(halfHeight * aspect, halfHeight);
    }

    // Vector2에는 Range를 걸 수 없다. 음수나 1을 넘는 값이 들어오면 화면 밖으로 나가므로 여기서 막는다.
    private static Vector2 ClampRatio(Vector2 ratio)
        => new(Mathf.Clamp01(ratio.x), Mathf.Clamp01(ratio.y));

    private void UpdateZoom(float deltaTime)
    {
        if (_camera == null || !_camera.orthographic) return;

        float target = _isAiming ? _baseSize * _aimZoomScale : _baseSize;

        _camera.orthographicSize = Mathf.Lerp(
            _camera.orthographicSize, target, 1f - Mathf.Exp(-_zoomSpeed * deltaTime));
    }

    private void SnapToTarget()
    {
        if (_target == null) return;

        _velocity = Vector3.zero;
        _rig.position = GetDesiredPosition();
    }
}
