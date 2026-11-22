using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum GaugeType
{
    Percent,
    Fraction,
    OnlyValue
}

/// <summary>
/// 채움 게이지. 주 게이지 + 서브게이지 둘(늘어날 때 / 줄어들 때).
///
/// <b>게이지는 목표값만 받는다.</b> 체력이 "시간에 걸쳐 차오르는" 건 회복 로직이고, 여기는 받은 목표까지 보여주는 방식만 정한다.
/// 회복이 끊기면 목표값이 멈출 뿐 게이지는 끊긴 줄 모른다.
///
/// <list type="bullet">
/// <item><b>늘어날 때</b> — <see cref="IncreaseMode.Instant"/>: 주 게이지가 바로 목표로.
/// <see cref="IncreaseMode.SubFirst"/>: 서브게이지(초록)가 먼저 목표에 가 있고 주 게이지가 따라 찬다.</item>
/// <item><b>줄어들 때</b> — 주 게이지는 항상 바로 내려간다. <see cref="DecreaseMode.Instant"/>: 그걸로 끝.
/// <see cref="DecreaseMode.BlinkThenDrain"/>: 깎인 구간이 서브게이지(흰색)로 남아 한 번 점멸하고 금방 빠진다.</item>
/// </list>
///
/// 처음 값과 꺼져 있다 켜질 때는 연출 없이 바로 맞춘다 — 붙는 순간이나 실드↔체력 게이지가 바뀌는 순간 0에서 차오르면 안 된다.
/// 기본값은 둘 다 Instant(옛 동작 그대로)다. 매 프레임 조금씩 변하는 게이지(과열·전력 소모)는 Instant로 둘 것.
/// </summary>
public class SimpleGauge : MonoBehaviour, IProgressUpdateable
{
    public enum IncreaseMode
    {
        Instant,
        SubFirst,
    }

    public enum DecreaseMode
    {
        Instant,
        BlinkThenDrain,
    }

    [SerializeField] private Image _gaugeFill;
    [SerializeField] private Image _gaugeIncreaseSubFill;
    [SerializeField] private Image _gaugeDecreaseSubFill;
    [SerializeField] private TextMeshProUGUI _textGaugeFill;
    [SerializeField] private GaugeType _gaugeType;

    [Header("Sub Gauge")]
    [SerializeField] private IncreaseMode _increaseMode = IncreaseMode.Instant;
    [SerializeField] private DecreaseMode _decreaseMode = DecreaseMode.Instant;

    [Tooltip("SubFirst: 주 게이지가 따라 차는 속도(초당 전체 비율).")]
    [SerializeField, Min(0.01f)] private float _followSpeed = 0.6f;

    [Tooltip("BlinkThenDrain: 깎인 구간이 점멸하는 시간(초). 켜짐-꺼짐-켜짐 한 번.")]
    [SerializeField, Min(0f)] private float _blinkDuration = 0.15f;

    [Tooltip("BlinkThenDrain: 점멸 뒤 깎인 구간이 빠지는 속도(초당 전체 비율).")]
    [SerializeField, Min(0.01f)] private float _drainSpeed = 2.5f;

    private float _target;
    private float _main;
    private float _decrease;
    private float _blinkTimer;
    private bool _initialized;

    public void UpdateProgress(int current, int max)
    {
        UpdateProgress(GetRatio(current, max));

        // 텍스트가 없는 게이지도 있다.
        if (_textGaugeFill != null)
            _textGaugeFill.text = GetProgressTextByGaugeType(current, max, _gaugeType);
    }

    public void UpdateProgress(float progressRatio)
    {
        float next = Mathf.Clamp01(progressRatio);

        // 꺼져 있으면 Update가 안 돈다. 연출을 걸어두면 켜지는 순간 옛 값에서 움직이기 시작한다.
        if (!_initialized || !isActiveAndEnabled)
        {
            Snap(next);
            return;
        }

        if (Mathf.Approximately(next, _target)) return;

        if (next > _target)
            HandleIncrease(next);
        else
            HandleDecrease(next);

        _target = next;
        Apply();
    }

    private void HandleIncrease(float next)
    {
        if (_increaseMode == IncreaseMode.Instant)
            _main = next;

        // 깎인 구간이 남아 있던 중에 다시 찼으면 그 아래는 지운다.
        _decrease = Mathf.Max(_decrease, _main);
    }

    private void HandleDecrease(float next)
    {
        float before = Mathf.Max(_main, _decrease);

        _main = next;

        if (_decreaseMode == DecreaseMode.BlinkThenDrain)
        {
            _decrease = before;
            _blinkTimer = _blinkDuration;
        }
        else
        {
            _decrease = next;
        }
    }

    private void Snap(float value)
    {
        _initialized = true;
        _target = value;
        _main = value;
        _decrease = value;
        _blinkTimer = 0f;
        Apply();
    }

    private void OnEnable()
    {
        if (_initialized)
            Snap(_target);
    }

    private void Update()
    {
        if (!_initialized) return;

        float dt = Time.unscaledDeltaTime;
        bool changed = false;

        if (_main < _target)
        {
            _main = Mathf.MoveTowards(_main, _target, _followSpeed * dt);
            changed = true;
        }

        if (_blinkTimer > 0f)
        {
            _blinkTimer -= dt;
            changed = true;
        }
        else if (_decrease > _main)
        {
            _decrease = Mathf.MoveTowards(_decrease, _main, _drainSpeed * dt);
            changed = true;
        }

        if (changed) Apply();
    }

    private void Apply()
    {
        if (_gaugeFill != null)
            _gaugeFill.fillAmount = _main;

        // 주 게이지가 맨 위에 그려진다. 서브게이지는 주 게이지보다 긴 만큼만 보인다.
        if (_gaugeIncreaseSubFill != null)
            _gaugeIncreaseSubFill.fillAmount = Mathf.Max(_target, _main);

        if (_gaugeDecreaseSubFill != null)
        {
            _gaugeDecreaseSubFill.fillAmount = Mathf.Max(_decrease, _main);
            _gaugeDecreaseSubFill.canvasRenderer.SetAlpha(BlinkAlpha());
        }
    }

    // 켜짐 → 꺼짐 → 켜짐. 점멸 구간을 셋으로 나눠 가운데만 끈다.
    private float BlinkAlpha()
    {
        if (_blinkTimer <= 0f || _blinkDuration <= 0f) return 1f;

        float t = 1f - _blinkTimer / _blinkDuration;
        return t > 0.33f && t < 0.66f ? 0f : 1f;
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
