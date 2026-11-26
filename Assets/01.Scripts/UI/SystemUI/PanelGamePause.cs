using UnityEngine;

/// <summary>
/// 일시정지 메뉴. ESC(<see cref="PlayerInput.OnPauseEvent"/>)로 뜬다 — <b>다른 창이 떠 있으면 그 창이 먼저 닫힌다</b>
/// (<see cref="UIPanelBase.TryCloseTop"/>). 자기도 ESC로 닫히는 창이라 한 번 더 누르면 돌아간다.
///
/// 시간은 직접 멈추지 않고 <see cref="GameTime"/>에 요청한다. 싱글에서는 멈추고, 멀티에서는 메뉴만 뜨고 게임은 흐른다.
///
/// 버튼은 임시 UI다. 씬의 Button(UnityEngine.UI)이 아래 public 메서드를 OnClick으로 부른다.
/// </summary>
public class PanelGamePause : UIPanelBase
{
    private PlayerInput _input;

    /// <summary>메뉴가 떠 있는가. 시간이 멈췄는지는 <see cref="GameTime.IsPaused"/>.</summary>
    public static bool IsOpen { get; private set; }

    private void Start()
    {
        _input = PlayerPilot.Local != null ? PlayerPilot.Local.Input : null; // 입력은 조종권의 것 — 갈아타도 그대로다

        if (_input != null)
            _input.OnPauseEvent += HandlePause;
    }

    private void OnDestroy()
    {
        if (_input != null)
            _input.OnPauseEvent -= HandlePause;

        // 떠 있는 채로 파괴되면 아무도 요청을 거두지 않는다.
        IsOpen = false;
        GameTime.ReleasePause(this);
    }

    private void HandlePause()
    {
        // 맨 위 창을 닫았으면 거기서 끝. 이 메뉴 자신도 이 경로로 닫힌다.
        if (UIPanelBase.TryCloseTop()) return;

        Show();
    }

    protected override void OnShow()
    {
        IsOpen = true;
        GameTime.RequestPause(this);
    }

    protected override void OnHide()
    {
        IsOpen = false;
        GameTime.ReleasePause(this);
    }

    /// <summary>버튼: 계속하기.</summary>
    public void OnClickResume() => Hide();

    /// <summary>버튼: 게임 종료. 에디터에서는 플레이 모드를 끝낸다.</summary>
    public void OnClickQuit()
    {
        Hide();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay() => IsOpen = false;
}
