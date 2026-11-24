using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 겹친 아이템을 몇 개 떼어 낼지 정하는 창. 우클릭 → 분할(<see cref="ItemContextActions"/>)로 뜬다.
/// 슬라이더와 입력칸이 같은 값을 가리키고, 확인을 눌러야 옮긴다. 떼어 낸 것은 같은 적재함의 첫 빈 칸으로 간다.
///
/// 반만 떼는 건 Alt 드래그가 더 빠르다(<see cref="ItemTransfer.GetHalfCount"/>). 이 창의 처음 값도 같은 규칙이다.
///
/// 떠 있는 동안 원래 칸이 바뀌면(쓰거나 버려서) 확인할 때 다시 읽어 범위를 맞춘다 — 열 때 본 개수를 믿지 않는다.
/// </summary>
public class PopupItemSplit : UIPanelBase
{
    public static PopupItemSplit Instance { get; private set; }

    [SerializeField] private TextMeshProUGUI _textTitle;
    [SerializeField] private Image _imageIcon;
    [SerializeField] private Slider _slider;
    [SerializeField] private TMP_InputField _input;
    [SerializeField] private TextMeshProUGUI _textError;

    private Inventory _inventory;
    private int _slotKey = -1;
    private bool _syncing;

    protected override void Awake()
    {
        base.Awake();
        Instance = this;

        if (_slider != null)
        {
            _slider.wholeNumbers = true;
            _slider.onValueChanged.AddListener(HandleSlider);
        }

        if (_input != null)
        {
            _input.contentType = TMP_InputField.ContentType.IntegerNumber;
            _input.onEndEdit.AddListener(HandleInput);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Open(Inventory inventory, int slotKey)
    {
        ItemStack stack = inventory != null ? inventory[slotKey] : ItemStack.Empty;
        if (stack.IsEmpty || stack.count < 2) return;

        _inventory = inventory;
        _slotKey = slotKey;

        ItemDataSO data = stack.Resolve();
        if (_textTitle != null)
            _textTitle.text = Localization.Format("ui.split.title", "{0} 나누기", data != null ? data.DisplayName : string.Empty);

        if (_imageIcon != null)
        {
            _imageIcon.sprite = data != null ? data.IconSprite : null;
            _imageIcon.enabled = _imageIcon.sprite != null;
        }

        SetError(null);

        if (_slider != null)
        {
            _slider.minValue = 1;
            _slider.maxValue = stack.count - 1;
        }

        SetAmount(ItemTransfer.GetHalfCount(stack));
        Show();
    }

    /// <summary>버튼: 확인.</summary>
    public void OnClickConfirm()
    {
        ItemStack stack = _inventory != null ? _inventory[_slotKey] : ItemStack.Empty;
        if (stack.IsEmpty || stack.count < 2)
        {
            Hide();
            return;
        }

        int amount = Mathf.Clamp(ReadAmount(), 1, stack.count - 1);
        int target = _inventory.FindEmptySlot();

        if (target < 0)
        {
            SetError(ItemText.Label("action.split.noRoom", "빈 칸 없음"));
            return;
        }

        if (!ItemTransfer.Move(_inventory, _slotKey, _inventory, target, amount))
        {
            ItemTransfer.CanMove(_inventory, _slotKey, _inventory, target, amount, out string reason);
            SetError(reason);
            return;
        }

        Hide();
    }

    /// <summary>버튼: 취소.</summary>
    public void OnClickCancel() => Hide();

    protected override void OnHide()
    {
        _inventory = null;
        _slotKey = -1;
    }

    private void HandleSlider(float value)
    {
        if (_syncing) return;

        SetAmount(Mathf.RoundToInt(value));
    }

    private void HandleInput(string text)
    {
        if (_syncing) return;

        SetAmount(int.TryParse(text, out int value) ? value : ReadAmount());
    }

    /// <summary>슬라이더와 입력칸을 같은 값으로. 범위 밖이면 끝으로 붙인다.</summary>
    private void SetAmount(int amount)
    {
        int max = _slider != null ? Mathf.RoundToInt(_slider.maxValue) : amount;
        amount = Mathf.Clamp(amount, 1, Mathf.Max(1, max));

        _syncing = true;
        if (_slider != null) _slider.SetValueWithoutNotify(amount);
        if (_input != null) _input.SetTextWithoutNotify(amount.ToString());
        _syncing = false;
    }

    private int ReadAmount() => _slider != null ? Mathf.RoundToInt(_slider.value) : 1;

    private void SetError(string message)
    {
        if (_textError == null) return;

        _textError.gameObject.SetActive(!string.IsNullOrEmpty(message));
        _textError.text = message ?? string.Empty;
    }

    /// <summary>인벤토리가 닫힐 때.</summary>
    public static void HideIfOpen()
    {
        if (Instance != null && Instance.IsShown) Instance.Hide();
    }
}
