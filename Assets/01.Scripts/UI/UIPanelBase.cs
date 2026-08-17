using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class UIPanelBase : MonoBehaviour, IWindowPanel
{
    [SerializeField] private bool _shownOnStart;

    public CanvasGroup canvasGroup;

    public bool IsShown { get; private set; }

    protected virtual void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        // 없으면 Show/Hide가 조용히 아무 일도 안 한다.
        // "패널이 안 숨겨지는데 에러도 없다"가 가장 찾기 어려운 부류라 여기서 보장한다.
        // alpha 1 / interactable true로 붙으므로 있던 화면이 달라지지는 않는다.
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        SetShown(_shownOnStart);
    }

    public void Show()
    {
        if (IsShown) return;

        SetShown(true);
        OnShow();
    }

    public void Hide()
    {
        if (!IsShown) return;

        SetShown(false);
        OnHide();
    }

    public void Toggle()
    {
        if (IsShown) Hide();
        else Show();
    }

    /// <summary>보여진 직후. 데이터 갱신 같은 걸 여기서 한다.</summary>
    protected virtual void OnShow() { }

    protected virtual void OnHide() { }

    // 오브젝트를 끄지 않고 CanvasGroup으로 처리한다.
    // SetActive를 쓰면 꺼진 동안 이벤트 구독이 유지되지 않는 컴포넌트가 생긴다.
    private void SetShown(bool value)
    {
        IsShown = value;

        if (canvasGroup == null) return;

        canvasGroup.alpha = value ? 1f : 0f;
        canvasGroup.interactable = value;
        canvasGroup.blocksRaycasts = value;
    }
}
