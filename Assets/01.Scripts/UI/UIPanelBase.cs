using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class UIPanelBase : MonoBehaviour, IWindowPanel
{
    [SerializeField] private bool _shownOnStart;

    [Tooltip("떠 있는 동안 조준·발사 같은 게임플레이 입력을 막는다. 인벤토리처럼 화면을 잡는 패널만 켠다.")]
    [SerializeField] private bool _blocksGameplayInput;

    public CanvasGroup canvasGroup;

    public bool IsShown { get; private set; }

    // 두 번 밀거나 두 번 빼면 카운터가 어긋난다. 실제로 밀었는지 기억한다.
    private bool _pushedBlock;

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

    // LocalizedText는 자기가 알아서 갈아끼우지만, 코드가 조립하는 문구는 아무도 다시 그려주지 않는다.
    // 언어를 바꿨을 때 잔탄 라벨만 이전 언어로 남는 걸 막는다.
    protected virtual void OnEnable()
    {
        Localization.OnChangedEvent += RefreshLocalization;

        // 떠 있는 채로 껐다 켜면 OnDisable에서 뺐던 것을 다시 밀어야 한다.
        ApplyBlock(IsShown);
    }

    protected virtual void OnDisable()
    {
        Localization.OnChangedEvent -= RefreshLocalization;

        // 열린 채로 꺼지거나 파괴되면 아무도 빼주지 않아 조작이 영영 막힌다.
        ReleaseBlock();
    }

    /// <summary>
    /// 언어가 바뀌었을 때 다시 그린다.
    /// 코드가 문자열을 조립하는 패널만 오버라이드하면 된다.
    /// </summary>
    protected virtual void RefreshLocalization() { }

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
        ApplyBlock(value);

        if (canvasGroup == null) return;

        canvasGroup.alpha = value ? 1f : 0f;
        canvasGroup.interactable = value;
        canvasGroup.blocksRaycasts = value;
    }

    private void ApplyBlock(bool shown)
    {
        if (!_blocksGameplayInput) return;

        if (shown && !_pushedBlock)
        {
            UIInputBlocker.Push();
            _pushedBlock = true;
            return;
        }

        if (!shown)
            ReleaseBlock();
    }

    private void ReleaseBlock()
    {
        if (!_pushedBlock) return;

        _pushedBlock = false;
        UIInputBlocker.Pop();
    }
}
