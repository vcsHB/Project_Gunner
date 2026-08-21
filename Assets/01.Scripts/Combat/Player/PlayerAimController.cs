using System;
using UnityEngine;

/// <summary>
/// 마우스 위치를 월드 조준점으로 바꾼다. 무기 회전과 카메라가 이 값을 따라간다.
///
/// <b>마우스는 조준점의 절대 위치다.</b> FPS처럼 마우스 이동량으로 조준점을 밀지 않는다.
/// 인벤토리가 커서를 그대로 쓰기 때문에, 커서를 잠그는 방식으로 가면 UI를 열 때마다
/// 입력 모드를 갈아타야 하고 그 경계에서 버그가 생긴다.
///
/// 그래서 반동은 조준점에 <b>덧붙는 오프셋</b>으로 준다. AimPosition에 직접 써넣으면
/// 다음 프레임에 마우스 값으로 덮여 사라진다.
/// 대신 반동은 스스로 회복하는 연출·명중률 효과이지, 플레이어가 내려서 잡는 조작이 아니다.
/// </summary>
[DefaultExecutionOrder(AimExecutionOrder.Aim)]
public class PlayerAimController : MonoBehaviour, IAgentComponent
{
    [Tooltip("조준 방향으로 회전할 오브젝트. 보통 무기를 들고 있는 루트.")]
    [SerializeField] private Transform _aimPivot;

    [Tooltip("왼쪽을 조준할 때 무기가 뒤집히지 않도록 Y를 미러링한다. " +
             "위에서 완전히 내려다보는 리소스라면 꺼도 된다.")]
    [SerializeField] private bool _keepUpright = true;

    [Tooltip("|조준 x|가 이 값보다 작으면 좌우를 바꾸지 않는다. 위/아래를 겨눌 때 떨리는 것을 막는다.")]
    [SerializeField, Range(0f, 0.5f)] private float _facingDeadzone = 0.05f;

    [Header("조준 거리")]
    [Tooltip("무기가 없을 때 쓸 조준 가능 거리. 무기를 들면 AimRange 스탯을 쓴다.")]
    [SerializeField] private float _defaultAimRange = 5f;

    [Tooltip("조준점이 머물 수 있는 범위. x는 반너비, y는 반높이에 대한 비율이다. 1이면 화면 끝까지 간다. " +
             "무기 사거리가 아무리 길어도 안 보이는 곳은 조준할 수 없다.")]
    [SerializeField] private Vector2 _viewMargin = new(0.85f, 0.85f);

    [Header("반동")]
    [Tooltip("반동 1당 조준점이 발사 방향으로 밀리는 거리.")]
    [SerializeField] private float _recoilKickPerLevel = 0.25f;

    [Tooltip("반동 오프셋이 0으로 돌아오는 속도. 클수록 빨리 잡힌다.")]
    [SerializeField] private float _recoilRecoverSpeed = 6f;

    [Tooltip("반동 오프셋이 커질 수 있는 최대 거리. 조준점이 화면 밖으로 튀는 것을 막는다.")]
    [SerializeField] private float _maxRecoilOffset = 1.5f;

    /// <summary>정조준 상태가 바뀌었을 때. 카메라 줌이 구독한다.</summary>
    public event Action<bool> OnAimingChangedEvent;

    private Player _player;
    private Camera _camera;
    private PlayerHandController _weapons;

    private Vector2 _recoilOffset;

    /// <summary>커서가 가리키는 월드 좌표. 거리 제한과 반동을 적용하기 전 값이다.</summary>
    public Vector2 PointerPosition { get; private set; }

    /// <summary>실제 조준점. 거리 제한과 반동이 반영된 최종 위치다.</summary>
    public Vector2 AimPosition { get; private set; }

    /// <summary>캐릭터에서 조준점으로 향하는 방향. 항상 정규화되어 있다.</summary>
    public Vector2 AimDirection { get; private set; } = Vector2.right;

    /// <summary>우클릭 정조준 중인지.</summary>
    public bool IsAiming { get; private set; }

    /// <summary>
    /// 지금 왼쪽을 보고 있는지. 캐릭터 몸통 스프라이트처럼 같이 뒤집혀야 하는 것이 읽어간다.
    /// 데드존 안에서는 마지막 값을 유지하므로 조준 x의 부호와 매 프레임 일치하지는 않는다.
    /// </summary>
    public bool IsFacingLeft { get; private set; }

    /// <summary>
    /// 지금 무기 기준 조준 가능 거리.
    ///
    /// <b>조준점을 자르는 데는 쓰지 않는다.</b> 사거리 밖이라고 조준점을 붙들면
    /// 커서와 갈라져 조작이 어긋난 것처럼 보인다. 조준점은 커서를 그대로 따라간다.
    /// 이 값은 카메라 거리나 명중률 감소 기준처럼 다른 곳에서 쓰라고 남겨둔 것이고,
    /// 지금은 읽는 곳이 없다.
    /// </summary>
    public float AimRange
    {
        get
        {
            PlayerRangedWeapon weapon = _weapons != null ? _weapons.Current as PlayerRangedWeapon : null;
            if (weapon == null || weapon.Status == null) return _defaultAimRange;

            return Mathf.Max(0.5f, weapon.Status.Value(WeaponStatType.AimRange));
        }
    }

    public void Initialize(Agent owner)
    {
        _player = owner as Player;
    }

    public void AfterInitialize()
    {
        _camera = CameraManager.HasInstance ? CameraManager.Instance.MainCamera : Camera.main;
        _weapons = _player.GetCompo<PlayerHandController>();

        if (_weapons != null)
            _weapons.OnHandChangedEvent += HandleHandChanged;

        SubscribeWeapon(_weapons != null ? _weapons.Current : null);

        UIInputBlocker.OnBlockedChangedEvent += HandleInputBlocked;
    }

    public void Dispose()
    {
        if (_weapons != null)
        {
            _weapons.OnHandChangedEvent -= HandleHandChanged;
            SubscribeWeapon(null);
        }

        UIInputBlocker.OnBlockedChangedEvent -= HandleInputBlocked;
    }

    // 카메라가 옮겨간 뒤에 조준점을 계산해야 커서와 어긋나지 않는다.
    // Update에서 계산하면 그 뒤 LateUpdate에서 카메라가 움직인 만큼 조준점이 커서에서 밀린다.
    private void LateUpdate()
    {
        if (_player == null || _player.Input == null) return;

        UpdateAim(_player.Input);
        TickRecoil(Time.deltaTime);

        if (_aimPivot == null) return;

        _aimPivot.right = AimDirection;
        ApplyUpright();
    }

    private void UpdateAim(PlayerInput input)
    {
        if (_camera != null && input.HasPointer)
        {
            Vector2 pointer = input.PointerPosition;

            // 2D 직교 카메라라 z는 카메라와의 거리만 맞으면 된다.
            Vector3 screen = new(pointer.x, pointer.y, -_camera.transform.position.z);
            PointerPosition = _camera.ScreenToWorldPoint(screen);
        }

        Vector2 origin = transform.position;

        // 무기 사거리로는 자르지 않는다. 사거리 밖이라고 조준점을 안쪽에 붙들면
        // 커서와 조준점이 갈라져서 조작이 어긋난 것처럼 보인다.
        // 사거리는 탄이 얼마나 날아가는지(Range)로만 판정한다.
        //
        // 화면 밖은 자른다. 반동으로 밀린 것까지 포함해서.
        AimPosition = ClampIntoView(PointerPosition + _recoilOffset);

        Vector2 delta = AimPosition - origin;

        // 캐릭터 위에 조준점이 겹치면 방향이 튀므로 마지막 방향을 유지한다.
        if (delta.sqrMagnitude > 0.0001f)
            AimDirection = delta.normalized;
    }

    /// <summary>
    /// 조준점을 화면 안으로 잡아둔다.
    ///
    /// <b>축별로 잘라야 한다.</b> 16:9면 세로 반높이가 가로 반너비의 절반 남짓이라,
    /// 거리 하나로 원을 그려 자르면 가로는 멀쩡한데 세로만 화면을 벗어난다.
    /// (무기 aimRange 17에 반높이 10이면 위아래로 7이 밖으로 나간다)
    ///
    /// 카메라가 이미 옮겨간 뒤(AimExecutionOrder)에 불리므로 현재 프레임 위치를 그대로 쓴다.
    /// </summary>
    private Vector2 ClampIntoView(Vector2 world)
    {
        if (_camera == null || !_camera.orthographic) return world;

        Vector2 center = _camera.transform.position;

        float halfHeight = _camera.orthographicSize;
        float halfWidth = halfHeight * _camera.aspect;

        // Vector2에는 Range를 걸 수 없다. 1을 넘으면 화면 밖이 되므로 여기서 막는다.
        float limitX = halfWidth * Mathf.Clamp01(_viewMargin.x);
        float limitY = halfHeight * Mathf.Clamp01(_viewMargin.y);

        return new Vector2(
            Mathf.Clamp(world.x, center.x - limitX, center.x + limitX),
            Mathf.Clamp(world.y, center.y - limitY, center.y + limitY));
    }

    #region 반동

    /// <summary>
    /// 한 발 나갈 때마다 조준점을 발사 방향으로 민다.
    /// 조준점이 밀리면 AimDirection도 같이 밀리므로, 연사할수록 탄이 벌어진다.
    /// </summary>
    private void HandleFired(float recoil)
    {
        if (recoil <= 0f) return;

        // 정조준 중에는 반동이 덜 튄다. 조준의 이득이 명중률만이면 체감이 약하다.
        float scale = IsAiming ? 0.5f : 1f;

        _recoilOffset += AimDirection * (recoil * _recoilKickPerLevel * scale);
        _recoilOffset = Vector2.ClampMagnitude(_recoilOffset, _maxRecoilOffset);
    }

    private void TickRecoil(float deltaTime)
    {
        if (_recoilOffset.sqrMagnitude <= 0.000001f)
        {
            _recoilOffset = Vector2.zero;
            return;
        }

        _recoilOffset = Vector2.MoveTowards(
            _recoilOffset, Vector2.zero, _recoilRecoverSpeed * deltaTime);
    }

    #endregion

    #region 정조준

    private void HandleInputBlocked(bool blocked)
    {
        if (blocked)
            SetAiming(false);
    }

    /// <summary>
    /// 손에 든 것이 켜고 끈다. 총은 정조준에 쓰고, 투척무기는 안전핀을 뽑을 때 같이 켠다.
    /// 입력을 여기서 직접 받지 않는 이유는 우클릭의 의미가 든 것마다 다르기 때문이다.
    /// </summary>
    public void SetAiming(bool value)
    {
        if (IsAiming == value) return;

        IsAiming = value;
        OnAimingChangedEvent?.Invoke(value);
    }

    #endregion

    #region 무기 구독

    private PlayerRangedWeapon _subscribed;

    private void HandleHandChanged(HandActionBase hand)
    {
        // 무기를 놓으면 정조준도 풀린다. 안 그러면 카메라가 확대된 채로 굳는다.
        SetAiming(false);
        SubscribeWeapon(hand);
    }

    private void SubscribeWeapon(HandActionBase hand)
    {
        if (_subscribed != null)
        {
            _subscribed.OnFiredEvent -= HandleFired;
            _subscribed = null;
        }

        _subscribed = hand as PlayerRangedWeapon;

        if (_subscribed != null)
            _subscribed.OnFiredEvent += HandleFired;
    }

    #endregion

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
}
