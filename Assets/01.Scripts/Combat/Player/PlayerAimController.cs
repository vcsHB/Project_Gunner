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
    [Header("조준 거리")]
    [Tooltip("코어가 없을 때 쓸 조준 가능 거리. 코어를 달면 그쪽 값을 쓴다.")]
    [SerializeField] private float _defaultAimRange = 15f;

    [Tooltip("조준점이 화면 가장자리에서 안쪽으로 둘 여백(월드 단위). 0이면 화면 끝까지 간다. " +
             "비율이 아니라 거리다 - 비율로 두면 화면이 클수록 여백이 같이 커져서 " +
             "커서가 가장자리 근처에 있을 때 조준점이 멈춰 떨어져 나간다.")]
    [SerializeField] private Vector2 _viewPadding = new(0.3f, 0.3f);

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
    private MechaPartController _parts;

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
    /// 지금 조준 가능 거리. <b>코어가 정한다.</b>
    ///
    /// 무기에서 코어로 옮긴 이유는 좌우 무장이 동시에 활성이기 때문이다.
    /// 무기별로 두면 서로 다른 값을 주장하고, 어느 쪽을 쓸지 매 프레임 골라야 한다.
    ///
    /// <b>조준점을 자르는 데는 쓰지 않는다.</b> 사거리 밖이라고 조준점을 붙들면
    /// 커서와 갈라져 조작이 어긋난 것처럼 보인다. 조준점은 커서를 그대로 따라간다.
    /// </summary>
    public float AimRange
    {
        get
        {
            CoreDataSO core = _parts != null ? _parts.Core : null;

            return core != null ? Mathf.Max(0.5f, core.AimRange) : _defaultAimRange;
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
        _parts = _player.GetCompo<MechaPartController>();

        if (_weapons != null)
        {
            _weapons.OnHandChangedEvent += HandleHandChanged;

            SubscribeWeapon(EquipSlotType.ArmLeft, _weapons.Left);
            SubscribeWeapon(EquipSlotType.ArmRight, _weapons.Right);
        }

        UIInputBlocker.OnBlockedChangedEvent += HandleInputBlocked;
    }

    public void Dispose()
    {
        if (_weapons != null)
        {
            _weapons.OnHandChangedEvent -= HandleHandChanged;

            SubscribeWeapon(EquipSlotType.ArmLeft, null);
            SubscribeWeapon(EquipSlotType.ArmRight, null);
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
    /// 커서는 원래 화면 안에 있으므로 평소에는 걸리지 않는다.
    /// <b>반동으로 밀린 만큼</b>과, 마우스가 게임 창 밖으로 나간 경우만 여기서 잡힌다.
    ///
    /// 여백은 비율이 아니라 <b>거리</b>다. 비율로 두면 화면이 클수록 여백이 같이 커져서,
    /// 커서가 가장자리 근처에 있을 때 조준점이 멈춰 커서에서 떨어져 나간다.
    ///
    /// 카메라가 이미 옮겨간 뒤(AimExecutionOrder)에 불리므로 현재 프레임 위치를 그대로 쓴다.
    /// </summary>
    private Vector2 ClampIntoView(Vector2 world)
    {
        if (_camera == null || !_camera.orthographic) return world;

        Vector2 center = _camera.transform.position;

        float halfHeight = _camera.orthographicSize;
        float halfWidth = halfHeight * _camera.aspect;

        // 여백이 화면 절반보다 크면 조준점이 중앙에 붙어버린다. 절반까지만 인정한다.
        float limitX = halfWidth - Mathf.Clamp(_viewPadding.x, 0f, halfWidth);
        float limitY = halfHeight - Mathf.Clamp(_viewPadding.y, 0f, halfHeight);

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

    // 좌우가 동시에 쏘므로 반동도 양쪽에서 들어온다. 슬롯별로 따로 들고 있어야
    // 한쪽을 떼었을 때 그쪽 구독만 정확히 걷힌다.
    private PlayerRangedWeapon _subscribedLeft;
    private PlayerRangedWeapon _subscribedRight;

    private void HandleHandChanged(EquipSlotType slot, HandActionBase hand)
        => SubscribeWeapon(slot, hand);

    private void SubscribeWeapon(EquipSlotType slot, HandActionBase hand)
    {
        ref PlayerRangedWeapon field = ref _subscribedLeft;
        if (slot == EquipSlotType.ArmRight) field = ref _subscribedRight;
        else if (slot != EquipSlotType.ArmLeft) return;

        if (field != null)
        {
            field.OnFiredEvent -= HandleFired;
            field = null;
        }

        field = hand as PlayerRangedWeapon;

        if (field != null)
            field.OnFiredEvent += HandleFired;
    }

    #endregion
}
