using UnityEngine;

public class UIPanelBase : MonoBehaviour, IWindowPanel
{
    [SerializeField] private bool _shownOnStart;

    public CanvasGroup canvasGroup;

    public bool IsShown { get; private set; }

    protected virtual void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

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
