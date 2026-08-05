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
    public event Action OnReloadEvent;
    public event Action OnInventoryToggleEvent;

    /// <summary>장비 슬롯 넘기기. -1 = 이전, +1 = 다음</summary>
    public event Action<int> OnSlotCycleEvent;

    private Controls _controls;

    #region Current Values

    public Vector2 MoveInput { get; private set; }

    public bool IsAttacking { get; private set; }
    public bool IsSprinting { get; private set; }
    public bool IsCrawling { get; private set; }

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
        _controls.Dispose();
        _controls = null;

        // 플레이 모드를 나가도 SO는 살아있으므로 눌린 상태가 남지 않게 정리한다.
        MoveInput = Vector2.zero;
        IsAttacking = false;
        IsSprinting = false;
        IsCrawling = false;
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
            OnReloadEvent?.Invoke();
    }

    public void OnInventory(InputAction.CallbackContext context)
    {
        if (context.performed)
            OnInventoryToggleEvent?.Invoke();
    }

    public void OnInventoryScroll(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        // Analog 값이라 부호만 쓴다. 휠을 굴린 양과 무관하게 한 칸씩 넘긴다.
        float value = context.ReadValue<float>();
        if (Mathf.Approximately(value, 0f)) return;

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
}
