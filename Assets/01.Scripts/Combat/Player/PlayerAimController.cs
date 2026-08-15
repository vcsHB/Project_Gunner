using UnityEngine;

/// <summary>
/// Look 입력을 월드 조준 방향으로 바꾼다. 무기 회전은 이 값을 따라간다.
/// </summary>
public class PlayerAimController : MonoBehaviour, IAgentComponent
{
    [Tooltip("조준 방향으로 회전할 오브젝트. 보통 무기를 들고 있는 루트.")]
    [SerializeField] private Transform _aimPivot;

    [Tooltip("왼쪽을 조준할 때 무기가 뒤집히지 않도록 Y를 미러링한다. " +
             "위에서 완전히 내려다보는 리소스라면 꺼도 된다.")]
    [SerializeField] private bool _keepUpright = true;

    [Tooltip("|조준 x|가 이 값보다 작으면 좌우를 바꾸지 않는다. 위/아래를 겨눌 때 떨리는 것을 막는다.")]
    [SerializeField, Range(0f, 0.5f)] private float _facingDeadzone = 0.05f;

    private Player _player;
    private Camera _camera;

    /// <summary>조준하고 있는 월드 좌표.</summary>
    public Vector2 AimPosition { get; private set; }

    /// <summary>캐릭터에서 조준점으로 향하는 방향. 항상 정규화되어 있다.</summary>
    public Vector2 AimDirection { get; private set; } = Vector2.right;

    /// <summary>
    /// 지금 왼쪽을 보고 있는지. 캐릭터 몸통 스프라이트처럼 같이 뒤집혀야 하는 것이 읽어간다.
    /// 데드존 안에서는 마지막 값을 유지하므로 조준 x의 부호와 매 프레임 일치하지는 않는다.
    /// </summary>
    public bool IsFacingLeft { get; private set; }

    public void Initialize(Agent owner)
    {
        _player = owner as Player;
    }

    public void AfterInitialize()
    {
        _camera = CameraManager.HasInstance ? CameraManager.Instance.MainCamera : Camera.main;
    }

    public void Dispose()
    {
    }

    private void Update()
    {
        if (_player == null || _player.Input == null) return;

        UpdateAim(_player.Input);

        if (_aimPivot == null) return;

        _aimPivot.right = AimDirection;
        ApplyUpright();
    }

    /// <summary>
    /// 피벗의 +X를 커서로 돌리면 왼쪽을 볼 때 +Y가 아래를 향한다. 무기가 뒤집혀 보이는 이유다.
    /// Y를 미러링해서 세워둔다.
    ///
    /// 무기가 아니라 <b>피벗</b>을 뒤집는 이유는 총구 오프셋 때문이다.
    /// 총열 위에 찍힌 총구(+Y)는 무기가 뒤집히면 아래로 내려와야 맞는데,
    /// 피벗을 뒤집으면 스프라이트와 총구·파츠 장착점이 한꺼번에 따라온다.
    /// </summary>
    private void ApplyUpright()
    {
        bool flip = false;

        if (_keepUpright)
        {
            // 위/아래를 정확히 겨누면 x가 0 근처에서 떨린다. 그 구간에서는 마지막 방향을 유지한다.
            if (Mathf.Abs(AimDirection.x) > _facingDeadzone)
                IsFacingLeft = AimDirection.x < 0f;

            flip = IsFacingLeft;
        }
        else
        {
            IsFacingLeft = false;
        }

        Vector3 scale = _aimPivot.localScale;
        float target = Mathf.Abs(scale.y) * (flip ? -1f : 1f);

        // 매 프레임 스케일을 쓰면 transform이 계속 dirty가 된다. 부호가 바뀔 때만 건드린다.
        if (!Mathf.Approximately(scale.y, target))
            _aimPivot.localScale = new Vector3(scale.x, target, scale.z);
    }

    private void UpdateAim(PlayerInput input)
    {
        if (_camera == null || !input.HasPointer) return;

        Vector2 pointer = input.PointerPosition;

        // 2D 직교 카메라라 z는 카메라와의 거리만 맞으면 된다.
        Vector3 screen = new(pointer.x, pointer.y, -_camera.transform.position.z);
        AimPosition = _camera.ScreenToWorldPoint(screen);

        Vector2 delta = AimPosition - (Vector2)transform.position;

        // 캐릭터 위에 커서가 겹치면 방향이 튀므로 마지막 방향을 유지한다.
        if (delta.sqrMagnitude > 0.0001f)
            AimDirection = delta.normalized;
    }
}
