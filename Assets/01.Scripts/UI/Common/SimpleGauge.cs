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
        UpdateProgress((float)current / max);
        _textGaugeFill.text = GetProgressTextByGaugeType(current, max, _gaugeType);
    }

    public void UpdateProgress(float progressRatio)
    {
        _gaugeFill.fillAmount = progressRatio;
    }

    private string GetProgressTextByGaugeType(int current, int max, GaugeType type)
    {
        float ratio = Mathf.Clamp01((float)current / max);
        switch (type)
        {
            case GaugeType.Percent:
                return $"{(int)ratio}%";
            case GaugeType.Fraction:
                return $"{current}/{max}";
            case GaugeType.OnlyValue:
                return current.ToString();
            default:
                return $"{current}/{max}";
        }
    }
}