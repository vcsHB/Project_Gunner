using UnityEngine;

/// <summary>
/// 일시정지. ESC(<see cref="PlayerInput.OnPauseEvent"/>)로 뜬다 — <b>다른 창이 떠 있으면 그 창이 먼저 닫힌다</b>
/// (<see cref="UIPanelBase.TryCloseTop"/>). 자기도 ESC로 닫히는 창이라 한 번 더 누르면 돌아간다.
///
/// 떠 있는 동안 시간을 멈춘다(<see cref="Time.timeScale"/> = 0). 시간을 건드리는 곳은 여기 하나뿐이다 —
/// 둘이 되면 "누가 마지막에 1로 돌렸나" 싸움이 난다. 슬로우모션 같은 게 생기면 그때 한 곳으로 모은다.
///
/// 멀티(M8)에서는 시간을 멈출 수 없다. 그때는 메뉴만 띄우고 시간은 흐르게 바꾼다.
///
/// 버튼은 임시 UI다. 씬의 Button(UnityEngine.UI)이 아래 public 메서드를 OnClick으로 부른다.
/// </summary>
public class PanelGamePause : UIPanelBase
{
    private PlayerInput _input;
    private float _timeScaleBeforePause = 1f;

    public static bool IsPaused { get; private set; }

    private void Start()
    {
        Player player = FindAnyObjectByType<Player>();
        _input = player != null ? player.Input : null;

        if (_input != null)
            _input.OnPauseEvent += HandlePause;
    }

    private void OnDestroy()
    {
        if (_input != null)
            _input.OnPauseEvent -= HandlePause;

        // 떠 있는 채로 씬이 내려가면 다음 씬이 멈춘 채로 시작한다.
        if (IsPaused) Resume();
    }

    private void HandlePause()
    {
        // 맨 위 창을 닫았으면 거기서 끝. 일시정지 창 자신도 이 경로로 닫힌다.
        if (UIPanelBase.TryCloseTop()) return;

        Show();
    }

    protected override void OnShow()
    {
        IsPaused = true;
        _timeScaleBeforePause = Time.timeScale;
        Time.timeScale = 0f;
    }

    protected override void OnHide() => Resume();

    private void Resume()
    {
        if (!IsPaused) return;

        IsPaused = false;
        Time.timeScale = _timeScaleBeforePause > 0f ? _timeScaleBeforePause : 1f;
    }

    /// <summary>버튼: 계속하기.</summary>
    public void OnClickResume() => Hide();

    /// <summary>버튼: 게임 종료. 에디터에서는 플레이 모드를 끝낸다.</summary>
    public void OnClickQuit()
    {
        Resume();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay() => IsPaused = false;
}
