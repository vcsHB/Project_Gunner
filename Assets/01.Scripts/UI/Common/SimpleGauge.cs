using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum GaugeType
{
    Percent,
    Fraction,
    OnlyValue
}
public class SimpleGauge : MonoBehaviour, IProgressUpdateable
{
    [SerializeField] private Image _gaugeFill;
    [SerializeField] private Image _gaugeIncreaseSubFill;
    [SerializeField] private Image _gaugeDecreaseSubFill;
    [SerializeField] private TextMeshProUGUI _textGaugeFill;
    [SerializeField] private GaugeType _gaugeType;
    public void UpdateProgress(int current, int max)
    {
        UpdateProgress(GetRatio(current, max));

        // 텍스트가 없는 게이지도 있다.
        if (_textGaugeFill != null)
            _textGaugeFill.text = GetProgressTextByGaugeType(current, max, _gaugeType);
    }

    public void UpdateProgress(float progressRatio)
    {
        _gaugeFill.fillAmount = Mathf.Clamp01(progressRatio);
    }

    private static float GetRatio(int current, int max) => max <= 0 ? 0f : Mathf.Clamp01((float)current / max);

    private string GetProgressTextByGaugeType(int current, int max, GaugeType type)
    {
        float ratio = GetRatio(current, max);
        switch (type)
        {
            case GaugeType.Percent:
                return $"{Mathf.RoundToInt(ratio * 100f)}%";
            case GaugeType.Fraction:
                return $"{current}/{max}";
            case GaugeType.OnlyValue:
                return current.ToString();
            default:
                return $"{current}/{max}";
        }
    }
}