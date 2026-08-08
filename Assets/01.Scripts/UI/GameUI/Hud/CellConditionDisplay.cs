using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CellConditionDisplay : MonoBehaviour
{
    [SerializeField] private SlicedFilledImage _gaugeFill;

    [SerializeField] private Image _imageConditionIcon;
    [SerializeField] private TextMeshProUGUI _textLeftTime;

    public ConditionBase Condition { get; private set; }

    public void SetData(ConditionBase condition, ConditionVisualTableSO visualTable)
    {
        Condition = condition;

        if (_imageConditionIcon != null)
        {
            Sprite icon = visualTable != null ? visualTable.GetIcon(condition.Type) : null;

            _imageConditionIcon.sprite = icon;
            _imageConditionIcon.color = visualTable != null ? visualTable.GetTint(condition.Type) : Color.white;
            _imageConditionIcon.enabled = icon != null;
        }

        Refresh();
    }

    public void Clear()
    {
        Condition = null;
    }

    /// <summary>남은 시간이 계속 줄어들므로 매 프레임 갱신한다.</summary>
    public void Refresh()
    {
        if (Condition == null) return;

        // 영구 상태는 남은 시간이라는 개념이 없다. 게이지를 꽉 채우고 시간은 숨긴다.
        if (Condition.IsPermanent)
        {
            if (_gaugeFill != null) _gaugeFill.fillAmount = 1f;
            if (_textLeftTime != null) _textLeftTime.text = string.Empty;
            return;
        }

        float ratio = Condition.Duration <= 0f ? 0f : Mathf.Clamp01(Condition.RemainTime / Condition.Duration);

        if (_gaugeFill != null)
            _gaugeFill.fillAmount = ratio;

        if (_textLeftTime != null)
            _textLeftTime.text = Mathf.CeilToInt(Mathf.Max(0f, Condition.RemainTime)).ToString();
    }
}
