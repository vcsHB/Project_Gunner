using TMPro;
using UnityEngine;

/// <summary>
/// 기체 파괴(사망) 화면. "기체 파괴 — 비상 탈출"과 새 판까지 남은 시간을 띄운다.
///
/// <see cref="RunController"/>의 이벤트만 듣는다. 죽으면 서서히 나타나고, 새 판이 시작되면 사라진다.
/// 무엇을 잃었는지(기체·적재물 전부, 안전 슬롯만 남음)를 여기서 알려준다 — 처음 죽어 보는 사람이 "왜 다 없어졌지"가 되지 않게.
/// </summary>
public class DeathScreenUI : MonoBehaviour
{
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private TextMeshProUGUI _title;
    [SerializeField] private TextMeshProUGUI _detail;

    [SerializeField] private string _titleText = "기체 파괴 — 비상 탈출";
    [SerializeField] private string _detailFormat = "기체와 적재물을 잃었습니다. 안전 슬롯만 남습니다.\n재출격까지 {0}";

    [SerializeField, Min(0.01f)] private float _fadeIn = 0.6f;

    private RunController _run;
    private bool _shown;

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

        if (_title != null) _title.text = _titleText;
    }

    private void HandleRunStarted()
    {
        _shown = false;
        SetAlpha(0f);
    }

    private void Update()
    {
        if (!_shown || _group == null) return;

        if (_group.alpha < 1f)
            SetAlpha(_group.alpha + Time.unscaledDeltaTime / _fadeIn);

        if (_detail != null && _run != null)
            _detail.text = string.Format(_detailFormat, Mathf.CeilToInt(_run.RespawnRemaining));
    }

    private void SetAlpha(float alpha)
    {
        if (_group == null) return;

        _group.alpha = Mathf.Clamp01(alpha);
        _group.blocksRaycasts = false;
        _group.interactable = false;
    }
}
