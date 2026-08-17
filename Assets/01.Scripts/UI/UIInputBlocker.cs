using System;
using UnityEngine;

/// <summary>
/// UI가 떠 있는 동안 게임플레이 입력을 막는다. 인벤토리를 열어두고 총이 나가면 안 된다.
///
/// <b>세는 방식이다.</b> 인벤토리 위에 확인 창이 또 뜨는 식으로 겹칠 수 있고,
/// 그때 하나만 닫혔다고 조작이 풀리면 안 된다.
///
/// 막는 쪽(패널)과 막히는 쪽(조준·발사)이 서로를 몰라도 되도록 가운데에 둔다.
/// 패널이 무기를 직접 붙들면 UI가 게임플레이를 알게 되어 나중에 떼기 어렵다.
/// </summary>
public static class UIInputBlocker
{
    /// <summary>막힘 상태가 바뀌었을 때. 누르고 있던 입력을 놓아주는 데 쓴다.</summary>
    public static event Action<bool> OnBlockedChangedEvent;

    private static int s_count;

    public static bool IsBlocked => s_count > 0;

    public static void Push()
    {
        s_count++;

        if (s_count == 1)
            OnBlockedChangedEvent?.Invoke(true);
    }

    public static void Pop()
    {
        if (s_count <= 0) return;

        s_count--;

        if (s_count == 0)
            OnBlockedChangedEvent?.Invoke(false);
    }

    /// <summary>씬을 갈아탈 때. 닫히지 못하고 파괴된 패널이 있으면 영영 막힌 채로 남는다.</summary>
    public static void Clear()
    {
        if (s_count == 0) return;

        s_count = 0;
        OnBlockedChangedEvent?.Invoke(false);
    }

    // 정적이라 플레이 모드를 나가도 값이 남는다. 도메인 리로드를 꺼도 새 플레이에서 0부터 시작하게 한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        s_count = 0;
        OnBlockedChangedEvent = null;
    }
}
