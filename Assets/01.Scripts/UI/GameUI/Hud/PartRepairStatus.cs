using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 필드 수리(R) 표시. 남은 키트 수는 항상, 진행 막대와 안내 문구는 수리 중·직후에만 보인다.
///
/// <see cref="MechaRepair"/>의 이벤트만 듣는다 — 수리가 UI를 부르지 않는다.
/// 진행도는 <see cref="MechaRepair.Remaining"/>에서 읽는다. 맞아서 끊기면 "중단"을 잠깐 보여주고 사라진다.
/// </summary>
public class PartRepairStatus : MonoBehaviour
{
    [Tooltip("진행 막대 + 문구 묶음. 수리 중·직후에만 보인다.")]
    [SerializeField] private CanvasGroup _progressGroup;
    [SerializeField] private Image _progressFill;
    [SerializeField] private TextMeshProUGUI _label;
    [Tooltip("남은 키트 수. 항상 보인다.")]
    [SerializeField] private TextMeshProUGUI _kitCount;

    [Header("문구")]
    [SerializeField] private string _repairingText = "수리 중";
    [SerializeField] private string _completedText = "수리 완료";
    [SerializeField] private string _interruptedText = "수리 중단";
    [SerializeField] private string _noKitText = "수리 키트 없음";
    [SerializeField] private string _nothingText = "수리할 곳 없음";
    [SerializeField] private string _kitCountFormat = "R 수리  ×{0}";

    [Header("색")]
    [SerializeField] private Color _normalColor = Color.white;
    [SerializeField] private Color _warningColor = new(1f, 0.45f, 0.35f);

    [Tooltip("끝났거나 거절된 뒤 문구가 남아 있는 시간(초). 그다음 흐려진다.")]
    [SerializeField, Min(0f)] private float _messageHold = 1f;
    [SerializeField, Min(0.01f)] private float _fadeDuration = 0.3f;

    private MechaRepair _repair;
    private InventoryController _inventory;
    private Inventory _boundInventory;

    private float _duration;
    private float _holdTimer;

    // HUD와 같은 방식. Agent의 컴포넌트 초기화가 Awake에서 끝나므로 Start에서 붙는다.
    private void Start()
    {
        if (_repair == null)
            Bind(PlayerPilot.LocalMecha);

        // 갈아타면 다시 붙는다.
        PlayerPilot.OnLocalMechaChangedEvent += Bind;
    }

    public void Bind(Player player)
    {
        Unbind();

        SetGroupAlpha(0f);

        if (player == null) return;

        _repair = player.GetCompo<MechaRepair>();
        _inventory = player.GetCompo<InventoryController>();

        if (_repair != null)
        {
            _repair.OnRepairStartedEvent += HandleStarted;
            _repair.OnRepairEndedEvent += HandleEnded;
            _repair.OnRepairRejectedEvent += HandleRejected;
        }

        RefreshKitCount();
    }

    public void Unbind()
    {
        if (_repair != null)
        {
            _repair.OnRepairStartedEvent -= HandleStarted;
            _repair.OnRepairEndedEvent -= HandleEnded;
            _repair.OnRepairRejectedEvent -= HandleRejected;
            _repair = null;
        }

        BindInventory(null);
        _inventory = null;
    }

    private void OnDestroy()
    {
        PlayerPilot.OnLocalMechaChangedEvent -= Bind;
        Unbind();
    }

    #region 키트 수

    // 인벤토리는 출격 때 갈아끼워질 수 있다(적재 칸 수 변경). 매번 지금 것에 붙는다.
    private void BindInventory(Inventory inventory)
    {
        if (_boundInventory == inventory) return;

        if (_boundInventory != null)
            _boundInventory.OnSlotChangedEvent -= HandleSlotChanged;

        _boundInventory = inventory;

        if (_boundInventory != null)
            _boundInventory.OnSlotChangedEvent += HandleSlotChanged;
    }

    private void HandleSlotChanged(int slot) => RefreshKitCount();

    private void RefreshKitCount()
    {
        Inventory inventory = _inventory != null ? _inventory.Inventory : null;
        BindInventory(inventory);

        if (_kitCount == null) return;

        int count = 0;
        if (inventory != null)
        {
            for (int i = 0; i < inventory.Capacity; i++)
            {
                ItemStack stack = inventory[i];
                if (stack.IsEmpty || stack.Resolve<RepairKitDataSO>() == null) continue;

                count += stack.count;
            }
        }

        _kitCount.text = string.Format(_kitCountFormat, count);
        _kitCount.color = count > 0 ? _normalColor : _warningColor;
    }

    #endregion

    #region 수리 이벤트

    private void HandleStarted(float duration)
    {
        _duration = Mathf.Max(0.01f, duration);
        _holdTimer = 0f;

        ShowMessage(_repairingText, _normalColor, 0f);
        RefreshKitCount();
    }

    private void HandleEnded(bool completed)
    {
        ShowMessage(completed ? _completedText : _interruptedText,
            completed ? _normalColor : _warningColor,
            completed ? 1f : FillAmount());

        _holdTimer = _messageHold;
    }

    private void HandleRejected(MechaRepair.RepairFailReason reason)
    {
        // 수리 중에 또 누른 것은 알릴 필요가 없다. 죽은 뒤는 사망 연출이 따로 있다.
        if (reason == MechaRepair.RepairFailReason.Busy || reason == MechaRepair.RepairFailReason.Dead) return;

        string text = reason == MechaRepair.RepairFailReason.NoKit ? _noKitText : _nothingText;
        Color color = reason == MechaRepair.RepairFailReason.NoKit ? _warningColor : _normalColor;

        ShowMessage(text, color, 0f);
        _holdTimer = _messageHold;
    }

    private void ShowMessage(string text, Color color, float fill)
    {
        if (_label != null)
        {
            _label.text = text;
            _label.color = color;
        }

        if (_progressFill != null)
            _progressFill.fillAmount = fill;

        SetGroupAlpha(1f);
    }

    #endregion

    private float FillAmount() => _progressFill != null ? _progressFill.fillAmount : 0f;

    private void Update()
    {
        if (_repair != null && _repair.IsRepairing)
        {
            if (_progressFill != null)
                _progressFill.fillAmount = 1f - _repair.Remaining / _duration;

            return;
        }

        if (_progressGroup == null || _progressGroup.alpha <= 0f) return;

        if (_holdTimer > 0f)
        {
            _holdTimer -= Time.unscaledDeltaTime;
            return;
        }

        SetGroupAlpha(_progressGroup.alpha - Time.unscaledDeltaTime / _fadeDuration);
    }

    private void SetGroupAlpha(float alpha)
    {
        if (_progressGroup != null)
            _progressGroup.alpha = Mathf.Clamp01(alpha);
    }
}
