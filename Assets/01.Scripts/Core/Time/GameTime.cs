using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임 시간의 관리자. <b><see cref="Time.timeScale"/>과 <see cref="AudioListener.pause"/>는 여기서만 바꾼다.</b>
/// 바꾸는 곳이 둘이 되면 "누가 마지막에 1로 돌렸나" 싸움이 나고, 일시정지가 풀린 채 메뉴가 떠 있는 일이 생긴다.
///
/// <b>멈춤은 요청을 모아서 정한다.</b> 일시정지 메뉴·설정 창·컷신이 각자 요청하고 각자 푼다(<see cref="RequestPause"/> /
/// <see cref="ReleasePause"/>). 하나라도 남아 있으면 멈춰 있다 — 메뉴 위에 설정 창을 열었다 닫아도 시간이 새지 않는다.
///
/// <b>멀티에서는 멈추지 않는다.</b> 세션이 시작될 때 <see cref="SetPauseAllowed"/>(false). 그때도 요청은 받아 두지만
/// 시간은 흐른다 — 메뉴는 뜨고 게임은 계속된다. 싱글 캠페인에서는 의도대로 멈춘다.
///
/// 정적인 이유 — 시간은 프로세스에 하나라 플레이어별 상태가 아니다(<see cref="GameRandom"/>과 같은 판단).
/// 슬로우모션 같은 배율이 생기면 여기에 요청으로 붙인다. 다른 곳에서 timeScale을 직접 만지지 말 것.
/// </summary>
public static class GameTime
{
    /// <summary>멈춤 상태가 바뀌었다. true = 멈춤.</summary>
    public static event Action<bool> OnPausedChangedEvent;

    private static readonly HashSet<object> s_pauseRequests = new();
    private static bool s_pauseAllowed = true;
    private static bool s_applied;

    /// <summary>지금 멈춰 있는가. 멀티에서는 요청이 있어도 false.</summary>
    public static bool IsPaused => s_pauseAllowed && s_pauseRequests.Count > 0;

    /// <summary>이 판에서 시간을 멈출 수 있는가. 싱글 = true, 멀티 = false.</summary>
    public static bool PauseAllowed => s_pauseAllowed;

    /// <summary>멈춰 달라. 같은 요청자가 두 번 불러도 하나로 센다.</summary>
    public static void RequestPause(object requester)
    {
        if (requester == null) return;

        s_pauseRequests.Add(requester);
        Apply();
    }

    /// <summary>멈춤 요청을 거둔다. 남은 요청이 없으면 다시 흐른다.</summary>
    public static void ReleasePause(object requester)
    {
        if (requester == null) return;

        s_pauseRequests.Remove(requester);
        Apply();
    }

    /// <summary>멀티 세션이 시작되면 false. 멈춰 있었으면 바로 풀린다.</summary>
    public static void SetPauseAllowed(bool allowed)
    {
        s_pauseAllowed = allowed;
        Apply();
    }

    private static void Apply()
    {
        bool paused = IsPaused;
        if (paused == s_applied) return;

        s_applied = paused;

        Time.timeScale = paused ? 0f : 1f;

        // 소리도 같이 멈춘다. 멈춘 화면에서 총소리·엔진음이 이어지면 멈춘 것 같지 않다.
        // 메뉴 소리처럼 멈춤 중에도 나야 하는 것은 AudioSource.ignoreListenerPause를 켠다.
        AudioListener.pause = paused;

        OnPausedChangedEvent?.Invoke(paused);
    }

    // 정적이라 플레이 모드를 나가도 남는다. 멈춘 채로 나가면 다음 플레이가 멈춘 채로 시작한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        s_pauseRequests.Clear();
        s_pauseAllowed = true;
        s_applied = false;
        OnPausedChangedEvent = null;

        Time.timeScale = 1f;
        AudioListener.pause = false;
    }
}
