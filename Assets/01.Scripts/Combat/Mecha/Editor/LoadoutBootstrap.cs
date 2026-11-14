using UnityEditor;
using UnityEngine;

/// <summary>
/// 고른 출격 구성으로 플레이 모드에 들어가는 길.
///
/// 씬의 Player를 열어 기본 로드아웃 칸을 바꾸는 것과 결과는 같지만,
/// <b>씬을 더럽히지 않는다.</b> 구성을 바꿔가며 돌려보는 일이 잦은데 그때마다 씬이
/// 수정 상태가 되면, 진짜 고친 것과 테스트로 건드린 것이 git에서 섞인다.
///
/// 고른 구성은 <see cref="SessionState"/>에 둔다. 플레이 모드에 들어갈 때 도메인 리로드가
/// 일어나 static이 전부 날아가므로, 리로드를 건너뛰는 저장소가 필요하다.
/// </summary>
public static class LoadoutBootstrap
{
    private const string LoadoutGuidKey = "Mecha.Bootstrap.LoadoutGuid";
    private const string EnabledKey = "Mecha.Bootstrap.Enabled";

    /// <summary>플레이 시 고른 구성으로 덮어쓸지.</summary>
    public static bool Enabled
    {
        get => SessionState.GetBool(EnabledKey, true);
        set => SessionState.SetBool(EnabledKey, value);
    }

    /// <summary>지금 고른 구성. 없으면 null.</summary>
    public static MechaLoadoutSO Loadout
    {
        get
        {
            string guid = SessionState.GetString(LoadoutGuidKey, string.Empty);
            if (string.IsNullOrEmpty(guid)) return null;

            string path = AssetDatabase.GUIDToAssetPath(guid);

            return string.IsNullOrEmpty(path)
                ? null
                : AssetDatabase.LoadAssetAtPath<MechaLoadoutSO>(path);
        }

        set
        {
            string guid = value == null
                ? string.Empty
                : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value));

            SessionState.SetString(LoadoutGuidKey, guid);
        }
    }

    public static void Play(MechaLoadoutSO loadout)
    {
        Loadout = loadout;
        Enabled = true;

        EditorApplication.isPlaying = true;
    }

    /// <summary>
    /// 도메인 리로드가 끝난 뒤, 씬의 Awake보다 먼저 불린다.
    /// <see cref="MechaPartController"/>는 Start에서 로드아웃을 읽으므로 늦지 않다.
    ///
    /// 에디터 어셈블리에 두는 이유 — 런타임 코드가 SessionState를 알면 안 된다.
    /// 주입은 에디터 편의이고, 빌드에는 이 경로가 아예 없어야 한다.
    /// </summary>
    [InitializeOnEnterPlayMode]
    private static void InjectOnEnterPlayMode()
    {
        MechaPartController.PendingLoadout = null;

        if (!Enabled) return;

        MechaLoadoutSO loadout = Loadout;
        if (loadout == null) return;

        MechaPartController.PendingLoadout = loadout;
        Debug.Log($"[Bootstrap] 출격 구성 '{loadout.name}'으로 들어갑니다.", loadout);
    }
}
