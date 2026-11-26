using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

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
///
/// <b>씬을 건넌다</b>(주둔지 ↔ 필드, <see cref="SceneFlow"/>). 처음 뜬 조종권이 씬 밖(DontDestroyOnLoad)으로 나가 판 밖 상태(<see cref="Profile"/>)를 계속 든다.
/// 씬마다 조종권을 하나씩 놓아 둬도 된다 — 어느 씬에서 플레이를 시작해도 되게. 이미 건너온 조종권이 있으면 씬의 것은
/// 자기 <see cref="_startMecha"/>만 넘겨주고 사라진다. 도착한 씬의 기체에는 들고 온 기록(<see cref="PlayerProfile.Sortie"/>)을 입힌다.
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

    [Tooltip("씬을 건너 살아남는다(판 밖 상태를 든다). 끄면 씬과 함께 사라진다 — 테스트용.")]
    [SerializeField] private bool _persistent = true;

    // 건너온 뒤 탈 기체. 도착한 씬에 놓인 조종권 사본이 알려준다. 없으면 씬에서 찾는다.
    private Player _sceneStartMecha;

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
        // 건너온 조종권이 이미 있다 — 이 씬의 것은 탈 기체만 알려주고 빠진다.
        PlayerPilot carried = FindCarried();
        if (carried != null)
        {
            carried._sceneStartMecha = _startMecha;
            Destroy(gameObject);
            return;
        }

        if (_input == null)
            Debug.LogError("[PlayerPilot] PlayerInput이 지정되지 않았습니다.", this);

        if (!s_all.Contains(this)) s_all.Add(this);

        if (_isLocal)
        {
            if (Local != null && Local != this)
                Debug.LogWarning("[PlayerPilot] 로컬 조종권이 둘입니다. 나중 것을 씁니다.", this);

            Local = this;
        }

        if (_persistent)
        {
            transform.SetParent(null, false);
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        EnterScene(_startMecha);
    }

    private PlayerPilot FindCarried()
    {
        for (int i = 0; i < s_all.Count; i++)
        {
            PlayerPilot other = s_all[i];
            if (other != null && other != this && other._persistent && other._isLocal == _isLocal) return other;
        }

        return null;
    }

    private void OnDestroy()
    {
        if (!s_all.Contains(this)) return; // 건너온 조종권에 자리를 내준 씬의 사본

        SceneManager.sceneLoaded -= HandleSceneLoaded;

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

    #region 씬 건너기

    /// <summary>
    /// 씬을 떠난다. <paramref name="carryMecha"/>면 탄 기체를 기록으로 적어 들고 간다 — 기체 오브젝트는 씬과 함께 사라진다.
    /// 아니면(부서짐) 기체를 잃는다. 기체를 비우지 않는다 — 비우면 적재 칸이 줄며 넘친 짐이 바닥에 떨어진다. 기록이 같은 스택을 들고 있어도 기체가 곧 사라지니 괜찮다.
    /// </summary>
    public void LeaveScene(bool carryMecha)
    {
        Profile.Sortie = carryMecha && Mecha != null && !Mecha.IsDead ? MechaRecord.Capture(Mecha) : null;
        Disembark();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;

        // 첫 씬은 Awake에서 이미 탔다. 같은 씬에 대해 한 번 더 오는 알림이다.
        if (Mecha != null && Mecha.gameObject.scene == scene) return;

        Player start = _sceneStartMecha;
        _sceneStartMecha = null;

        EnterScene(start);
    }

    /// <summary>
    /// 이 씬의 기체에 탄다. 들고 온 기록이 있으면 먼저 입힌다 — 씬이 열린 직후(<c>sceneLoaded</c>)라 기체의 초기화는 끝났고
    /// <see cref="MechaPartController"/>의 Start(기본 구성 입히기)는 아직이다. 기록을 입히면 그쪽은 건너뛴다.
    /// </summary>
    private void EnterScene(Player mecha)
    {
        if (mecha == null) mecha = FindAnyObjectByType<Player>();

        MechaRecord sortie = Profile.Sortie;
        if (mecha != null && sortie != null)
        {
            MechaPartController parts = mecha.GetCompo<MechaPartController>();
            if (parts != null)
            {
                List<ItemStack> leftover = new();
                parts.BeginFromRecord(sortie, leftover);

                // 못 실은 것은 창고로. 창고도 차면 잃는다 — 경고는 남긴다.
                for (int i = 0; i < leftover.Count; i++)
                {
                    if (!Profile.Storage.AddStack(leftover[i]))
                        Debug.LogWarning($"[PlayerPilot] 기체에도 창고에도 못 넣은 아이템 {leftover[i].itemId}을 잃었습니다.", this);
                }
            }
        }

        Profile.Sortie = null;
        Board(mecha);
    }

    #endregion
}
