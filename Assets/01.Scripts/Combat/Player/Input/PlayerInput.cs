using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 입력을 읽어 이벤트로 뿌리기만 한다. 게임 로직은 여기에 두지 않는다.
///
/// 에셋 하나를 공유하므로 지금은 로컬 1인 기준이다.
/// 멀티에서는 플레이어마다 다른 입력원이 필요하다.
/// </summary>
[CreateAssetMenu(menuName = "SO/Input/PlayerInput")]
public class PlayerInput : ScriptableObject, Controls.IPlayerActions
{
    public event Action<Vector2> OnMoveEvent;
    public event Action<bool> OnAttackEvent;   // true=누름, false=뗌
    public event Action<bool> OnSprintEvent;
    public event Action<bool> OnCrawlEvent;
    public event Action OnUseEvent;
    public event Action OnInteractEvent;

    /// <summary>
    /// true=누름, false=뗌. 짧게 눌렀는지 길게 눌렀는지는 여기서 판단하지 않는다 —
    /// 입력 리더에 시간 로직이 들어가면 게임 규칙이 SO로 새어 들어간다.
    /// </summary>
    public event Action<bool> OnReloadEvent;

    public event Action OnInventoryToggleEvent;

    /// <summary>장비 슬롯 넘기기. -1 = 이전, +1 = 다음</summary>
    public event Action<int> OnSlotCycleEvent;

    private Controls _controls;

    #region Current Values

    public Vector2 MoveInput { get; private set; }

    public bool IsAttacking { get; private set; }
    public bool IsSprinting { get; private set; }
    public bool IsCrawling { get; private set; }
    public bool IsReloadHeld { get; private set; }

    /// <summary>
    /// 조준용 화면 좌표. 지금은 마우스 전용이다.
    /// 패드로 조준하려면 Look 액션을 추가하고 여기를 갈아끼울 것.
    /// </summary>
    public Vector2 PointerPosition
        => Pointer.current != null ? Pointer.current.position.ReadValue() : Vector2.zero;

    public bool HasPointer => Pointer.current != null;

    #endregion

    private void OnEnable()
    {
        if (_controls != null) return;

        _controls = new Controls();
        _controls.Player.SetCallbacks(this);
        _controls.Player.Enable();
    }

    private void OnDisable()
    {
        if (_controls == null) return;

        _controls.Player.Disable();

        // Dispose는 내부적으로 Destroy를 부른다.
        // 이 OnDisable은 플레이 모드를 빠져나올 때(도메인 리로드)도 불리는데,
        // 그 시점엔 이미 에디트 모드라 Destroy가 에러를 낸다. 그때는 그냥 두면 리로드가 정리한다.
        if (Application.isPlaying)
            _controls.Dispose();

        _controls = null;

        // 플레이 모드를 나가도 SO는 살아있으므로 눌린 상태가 남지 않게 정리한다.
        MoveInput = Vector2.zero;
        IsAttacking = false;
        IsSprinting = false;
        IsCrawling = false;
        IsReloadHeld = false;
    }

    #region Controls.IPlayerActions

    public void OnMove(InputAction.CallbackContext context)
    {
        MoveInput = context.ReadValue<Vector2>();
        OnMoveEvent?.Invoke(MoveInput);
    }

    public void OnAttack(InputAction.CallbackContext context)
    {
        if (context.performed)
            SetAttacking(true);
        else if (context.canceled)
            SetAttacking(false);
    }

    public void OnUse(InputAction.CallbackContext context)
    {
        if (context.performed)
            OnUseEvent?.Invoke();
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        if (context.performed)
            OnInteractEvent?.Invoke();
    }

    public void OnSprint(InputAction.CallbackContext context)
    {
        if (context.performed)
            SetSprinting(true);
        else if (context.canceled)
            SetSprinting(false);
    }

    public void OnCrawl(InputAction.CallbackContext context)
    {
        if (context.performed)
            SetCrawling(true);
        else if (context.canceled)
            SetCrawling(false);
    }

    public void OnReload(InputAction.CallbackContext context)
    {
        if (context.performed)
            SetReloadHeld(true);
        else if (context.canceled)
            SetReloadHeld(false);
    }

    public void OnInventory(InputAction.CallbackContext context)
    {
        if (context.performed)
            OnInventoryToggleEvent?.Invoke();
    }

    public void OnInventoryScroll(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        // 마우스 휠은 Vector2(DeltaControl), 패드 축은 float으로 들어온다.
        // 어느 쪽에 바인딩하든 터지지 않도록 실제 값 타입을 보고 읽는다.
        float value = context.valueType == typeof(Vector2)
            ? context.ReadValue<Vector2>().y
            : context.ReadValue<float>();

        if (Mathf.Approximately(value, 0f)) return;

        // 굴린 양과 무관하게 한 칸씩 넘긴다.
        OnSlotCycleEvent?.Invoke(value > 0f ? 1 : -1);
    }

    #endregion

    private void SetAttacking(bool value)
    {
        if (IsAttacking == value) return;

        IsAttacking = value;
        OnAttackEvent?.Invoke(value);
    }

    private void SetSprinting(bool value)
    {
        if (IsSprinting == value) return;

        IsSprinting = value;
        OnSprintEvent?.Invoke(value);
    }

    private void SetCrawling(bool value)
    {
        if (IsCrawling == value) return;

        IsCrawling = value;
        OnCrawlEvent?.Invoke(value);
    }

    private void SetReloadHeld(bool value)
    {
        if (IsReloadHeld == value) return;

        IsReloadHeld = value;
        OnReloadEvent?.Invoke(value);
    }
}
