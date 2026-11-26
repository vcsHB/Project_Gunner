using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 조종권. <b>플레이어 = 이것</b>, 기체(<see cref="Player"/>)는 타는 것이다. 플레이어마다 하나.
///
/// 기체 밖의 플레이어는 없다 — 늘 어떤 기체 안에 있거나, 기체가 없으면 아무것도 조종하지 않는다(조립기 화면).
/// 입력(<see cref="PlayerInput"/>)은 여기 붙어 있고, 탄 기체가 <see cref="Player.Input"/>으로 빌려 쓴다.
/// 내린 기체는 입력이 끊겨 서 있는다 — 월드에 그대로 남는다(격납고에 세워 둔 기체).
///
/// HUD·카메라·인벤토리 화면은 기체를 직접 찾지 않고 <see cref="LocalMecha"/>를 보고,
/// 갈아타면 <see cref="OnLocalMechaChangedEvent"/>로 다시 붙는다.
///
/// <see cref="Local"/>은 "이 화면의 주인" 하나를 가리키는 표시용 포인터다. 상태는 각 조종권 인스턴스에 있다(RULES — 싱글턴에 플레이어별 상태 금지).
/// 멀티에서는 조종권이 여럿이고 <see cref="Local"/>만 하나다.
/// </summary>
public class PlayerPilot : MonoBehaviour
{
    /// <summary>이 화면의 조종권.</summary>
    public static PlayerPilot Local { get; private set; }

    private static readonly List<PlayerPilot> s_all = new();

    /// <summary>살아 있는 조종권 전부. 멀티에서는 여럿.</summary>
    public static IReadOnlyList<PlayerPilot> All => s_all;

    /// <summary>이 화면의 조종권이 탄 기체. 없으면 null.</summary>
    public static Player LocalMecha => Local != null ? Local.Mecha : null;

    /// <summary>이 화면의 조종권이 갈아탔다. 내렸으면 null. 표시 쪽이 다시 붙을 때 쓴다.</summary>
    public static event Action<Player> OnLocalMechaChangedEvent;

    /// <summary>이 조종권이 갈아탔다. 내렸으면 null.</summary>
    public event Action<Player> OnMechaChangedEvent;

    [SerializeField] private PlayerInput _input;

    [Tooltip("시작할 때 탈 기체. 비우면 씬에서 찾는다.")]
    [SerializeField] private Player _startMecha;

    [Tooltip("이 화면의 주인인가. 멀티에서 남의 조종권은 끈다.")]
    [SerializeField] private bool _isLocal = true;

    public PlayerInput Input => _input;

    public Player Mecha { get; private set; }

    /// <summary>판 밖 상태 — 창고·격납고. 판이 끝나도 남는다.</summary>
    public PlayerProfile Profile { get; } = new();

    public bool IsLocal => _isLocal;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        Local = null;
        OnLocalMechaChangedEvent = null;
        s_all.Clear();
    }

    /// <summary>
    /// 모든 플레이어의 판 밖 상태가 붙잡은 인스턴스. 새 판에 레지스트리를 비울 때 남긴다 — 창고의 무기·세워 둔 기체의 파츠가 날아가지 않게.
    /// </summary>
    public static void CollectPersistentInstances(ICollection<uint> into)
    {
        for (int i = 0; i < s_all.Count; i++)
            s_all[i].Profile.CollectInstanceIds(into);
    }

    // 기체의 컴포넌트 초기화(Agent.Awake)보다 먼저일 수도, 나중일 수도 있다. 기체 쪽은 둘 다 받는다 —
    // 초기화 때 지금 입력을 붙이고, 그 뒤의 교체는 이벤트로 받는다(Player.OnInputChangedEvent).
    private void Awake()
    {
        if (_input == null)
            Debug.LogError("[PlayerPilot] PlayerInput이 지정되지 않았습니다.", this);

        if (!s_all.Contains(this)) s_all.Add(this);

        if (_isLocal)
        {
            if (Local != null && Local != this)
                Debug.LogWarning("[PlayerPilot] 로컬 조종권이 둘입니다. 나중 것을 씁니다.", this);

            Local = this;
        }

        Board(_startMecha != null ? _startMecha : FindAnyObjectByType<Player>());
    }

    private void OnDestroy()
    {
        if (Mecha != null && Mecha.Pilot == this)
            Mecha.SetPilot(null);

        s_all.Remove(this);
        if (Local == this) Local = null;
    }

    /// <summary>이 기체에 탄다. 타고 있던 기체에서는 내린다. null이면 내리기만 한다.</summary>
    public bool Board(Player mecha)
    {
        if (mecha == Mecha) return true;

        if (mecha != null && mecha.Pilot != null && mecha.Pilot != this)
        {
            Debug.LogWarning($"[PlayerPilot] {mecha.name}에는 이미 다른 조종권이 타고 있습니다.", mecha);
            return false;
        }

        if (Mecha != null && Mecha.Pilot == this)
            Mecha.SetPilot(null);

        Mecha = mecha;

        if (Mecha != null)
            Mecha.SetPilot(this);

        OnMechaChangedEvent?.Invoke(Mecha);
        if (_isLocal && Local == this) OnLocalMechaChangedEvent?.Invoke(Mecha);

        return true;
    }

    /// <summary>내린다. 기체는 그 자리에 남는다.</summary>
    public void Disembark() => Board(null);
}
