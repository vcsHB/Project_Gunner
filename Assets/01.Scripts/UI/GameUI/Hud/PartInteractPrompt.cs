using TMPro;
using UnityEngine;

/// <summary>
/// 상호작용할 수 있는 것 위에 뜨는 "[E] 탐색". <see cref="PlayerInteractor.Current"/>를 읽기만 한다. HUD의 상시 구성 요소.
///
/// 월드 위치를 화면으로 옮겨 따라간다. 인벤토리 화면이 떠 있으면 숨긴다 — 이미 열었다.
/// 키 이름은 지금 고정("E")이다. 키 재설정이 생기면 입력 바인딩에서 읽어 올 것.
/// </summary>
public class PartInteractPrompt : MonoBehaviour
{
    [Tooltip("켜고 끌 대상. 비우면 첫 자식.")]
    [SerializeField] private RectTransform _root;
    [SerializeField] private TextMeshProUGUI _text;

    [Tooltip("대상 위치에서 화면 위로 얼마나 띄울지(월드 유닛).")]
    [SerializeField] private Vector2 _worldOffset = new(0f, 0.9f);

    [SerializeField] private string _keyLabel = "E";

    private PlayerInteractor _interactor;
    private PanelInventory _panel;
    private Canvas _canvas;
    private Camera _worldCamera;
    private IInteractable _shown;

    private void Start()
    {
        Player player = FindAnyObjectByType<Player>();
        _interactor = player != null ? player.GetCompo<PlayerInteractor>() : null;
        _panel = FindAnyObjectByType<PanelInventory>();
        _canvas = GetComponentInParent<Canvas>();
        _worldCamera = Camera.main;

        if (_root == null && transform.childCount > 0) _root = transform.GetChild(0) as RectTransform;
        SetVisible(false);
    }

    private void LateUpdate()
    {
        IInteractable target = _interactor != null ? _interactor.Current : null;
        bool show = target != null && (_panel == null || !_panel.IsShown);

        SetVisible(show);
        if (!show) return;

        if (!ReferenceEquals(target, _shown) && _text != null)
            _text.text = Localization.Format("ui.interact.prompt", "[{0}] {1}", _keyLabel, target.InteractLabel);

        _shown = target;
        Follow(target.InteractPosition + _worldOffset);
    }

    private void Follow(Vector2 world)
    {
        if (_root == null || _canvas == null || _worldCamera == null) return;

        RectTransform parent = _root.parent as RectTransform;
        if (parent == null) return;

        Canvas root = _canvas.rootCanvas;
        Camera uiCamera = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;

        Vector3 screen = _worldCamera.WorldToScreenPoint(world);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, uiCamera, out Vector2 local))
            _root.anchoredPosition = local;
    }

    private void SetVisible(bool visible)
    {
        if (_root != null && _root.gameObject.activeSelf != visible)
            _root.gameObject.SetActive(visible);

        if (!visible) _shown = null;
    }
}
