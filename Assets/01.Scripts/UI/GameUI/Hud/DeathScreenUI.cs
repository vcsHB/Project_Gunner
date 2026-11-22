using TMPro;
using UnityEngine;

/// <summary>
/// 기체 파괴(사망) 화면. 제목·잃은 것 안내·돌아가기까지 남은 시간 세 줄.
///
/// <see cref="RunController"/>의 이벤트만 듣는다. 죽으면 서서히 나타나고, 새 판이 시작되면 사라진다.
/// 무엇을 잃었는지를 여기서 알려준다 — 처음 죽어 보는 사람이 "왜 다 없어졌지"가 되지 않게.
///
/// 남은 시간 줄만 매 프레임 갱신한다. 제목·안내는 죽을 때 한 번 넣는다.
/// </summary>
public class DeathScreenUI : MonoBehaviour
{
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private TextMeshProUGUI _textTitle;
    [SerializeField] private TextMeshProUGUI _textDetail;
    [SerializeField] private TextMeshProUGUI _textAutoQuitLeftTime;

    [SerializeField] private string _titleText = "기체 파괴 — 비상 탈출";
    [SerializeField] private string _detailText = "기체와 적재물을 잃었습니다.";
    [Tooltip("{0} = 남은 초(올림).")]
    [SerializeField] private string _autoQuitLeftTime = "{0}초 뒤 주둔지로 돌아갑니다";

    [SerializeField, Min(0.01f)] private float _fadeIn = 0.6f;

    private RunController _run;
    private bool _shown;
    private int _lastSeconds = -1;

    private void Start()
    {
        if (_group == null) _group = GetComponent<CanvasGroup>();
        SetAlpha(0f);

        _run = FindAnyObjectByType<RunController>();
        if (_run == null) return;

        _run.OnPlayerDiedEvent += HandleDied;
        _run.OnRunStartedEvent += HandleRunStarted;
    }

    private void OnDestroy()
    {
        if (_run == null) return;

        _run.OnPlayerDiedEvent -= HandleDied;
        _run.OnRunStartedEvent -= HandleRunStarted;
    }

    private void HandleDied(float respawnDelay)
    {
        _shown = true;
        _lastSeconds = -1;

        if (_textTitle != null) _textTitle.text = _titleText;
        if (_textDetail != null) _textDetail.text = _detailText;

        UpdateLeftTime();
    }

    private void HandleRunStarted()
    {
        _shown = false;
        SetAlpha(0f);
    }

    private void Update()
    {
        if (!_shown) return;

        if (_group != null && _group.alpha < 1f)
            SetAlpha(_group.alpha + Time.unscaledDeltaTime / _fadeIn);

        UpdateLeftTime();
    }

    // 초가 바뀔 때만 문자열을 만든다.
    private void UpdateLeftTime()
    {
        if (_textAutoQuitLeftTime == null || _run == null) return;

        int seconds = Mathf.CeilToInt(_run.RespawnRemaining);
        if (seconds == _lastSeconds) return;

        _lastSeconds = seconds;
        _textAutoQuitLeftTime.text = string.Format(_autoQuitLeftTime, seconds);
    }

    private void SetAlpha(float alpha)
    {
        if (_group == null) return;

        _group.alpha = Mathf.Clamp01(alpha);
        _group.blocksRaycasts = false;
        _group.interactable = false;
    }
}
