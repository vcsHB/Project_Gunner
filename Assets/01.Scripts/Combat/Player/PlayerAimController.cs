using UnityEngine;

/// <summary>
/// Look 입력을 월드 조준 방향으로 바꾼다. 무기 회전은 이 값을 따라간다.
/// </summary>
public class PlayerAimController : MonoBehaviour, IAgentComponent
{
    [Tooltip("조준 방향으로 회전할 오브젝트. 보통 무기를 들고 있는 루트.")]
    [SerializeField] private Transform _aimPivot;

    private Player _player;
    private Camera _camera;

    /// <summary>조준하고 있는 월드 좌표.</summary>
    public Vector2 AimPosition { get; private set; }

    /// <summary>캐릭터에서 조준점으로 향하는 방향. 항상 정규화되어 있다.</summary>
    public Vector2 AimDirection { get; private set; } = Vector2.right;

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

        if (_aimPivot != null)
            _aimPivot.right = AimDirection;
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
