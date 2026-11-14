using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 기체 파츠를 <b>만들고, 설정하고, 출격시키는</b> 한 창.
///
/// 셋을 나누지 않은 이유 — 파츠 하나를 굴려보려면 에셋 생성 → 프리팹 연결 → Id 발급 →
/// 로드아웃 등록 → 플레이까지 다섯 군데를 거쳐야 한다. 중간에 하나만 빠져도
/// "왜 안 나오지"가 되고, 어디서 빠졌는지는 다섯 곳을 다시 봐야 알 수 있다.
///
/// <see cref="ItemCreatorWindow"/>와 겹치는 것처럼 보이지만 다루는 단위가 다르다.
/// 저기는 아이템 SO 하나, 여기는 <b>SO + 프리팹 + 소켓</b> 한 묶음이다.
/// </summary>
public class PartForgeWindow : EditorWindow
{
    private const string RootFolderKey = "ProjectGunner.PartForge.RootFolder";
    private const string DefaultRootFolder = "Assets/08.SO/Items";
    private const string PrefabFolderKey = "ProjectGunner.PartForge.PrefabFolder";
    private const string DefaultPrefabFolder = "Assets/03.Prefabs/Mecha";

    /// <summary>파츠 타입별 기본 소켓 배치. 아트를 보기 전의 출발점일 뿐이고 이후엔 씬에서 맞춘다.</summary>
    private static readonly Dictionary<EquipSlotType, Vector2> CoreSockets = new()
    {
        { EquipSlotType.ArmLeft, new Vector2(-0.32f, 0.12f) },
        { EquipSlotType.ArmRight, new Vector2(0.32f, 0.12f) },
        { EquipSlotType.Booster, new Vector2(0f, -0.34f) },
    };

    private enum PartKind { Core, Booster }

    private PartKind _kind = PartKind.Core;
    private string _rootFolder;
    private string _prefabFolder;

    private Vector2 _scroll;
    private bool _showCreate = true;
    private bool _showParts = true;
    private bool _showLoadout = true;

    private PartDataSO _selected;
    private Editor _selectedEditor;
    private MechaLoadoutSO _loadout;
    private Editor _loadoutEditor;

    [MenuItem("Tools/Mecha/Part Forge")]
    public static void Open()
    {
        PartForgeWindow window = GetWindow<PartForgeWindow>();
        window.titleContent = new GUIContent("Part Forge");
        window.minSize = new Vector2(380f, 420f);
    }

    private void OnEnable()
    {
        _rootFolder = EditorPrefs.GetString(RootFolderKey, DefaultRootFolder);
        _prefabFolder = EditorPrefs.GetString(PrefabFolderKey, DefaultPrefabFolder);
        _loadout = LoadoutBootstrap.Loadout;
    }

    private void OnDisable()
    {
        DestroyEditor(ref _selectedEditor);
        DestroyEditor(ref _loadoutEditor);
    }

    private static void DestroyEditor(ref Editor editor)
    {
        if (editor == null) return;

        DestroyImmediate(editor);
        editor = null;
    }

    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        DrawCreate();
        EditorGUILayout.Space();
        DrawParts();
        EditorGUILayout.Space();
        DrawLoadout();

        EditorGUILayout.EndScrollView();
    }

    #region 1. 만들기

    private void DrawCreate()
    {
        _showCreate = EditorGUILayout.BeginFoldoutHeaderGroup(_showCreate, "1. 만들기");
        EditorGUILayout.EndFoldoutHeaderGroup();

        if (!_showCreate) return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            _kind = (PartKind)EditorGUILayout.EnumPopup("파츠 종류", _kind);

            DrawFolderField("SO 폴더", ref _rootFolder, RootFolderKey);
            DrawFolderField("프리팹 폴더", ref _prefabFolder, PrefabFolderKey);

            Sprite[] sprites = Selection.GetFiltered<Sprite>(SelectionMode.Assets);

            EditorGUILayout.HelpBox(
                sprites.Length == 0
                    ? "프로젝트 창에서 스프라이트를 고르세요. 여러 개 골라도 됩니다."
                    : $"{sprites.Length}개 선택됨 — 하나당 SO + 프리팹 한 쌍이 생깁니다.",
                sprites.Length == 0 ? MessageType.Info : MessageType.None);

            using (new EditorGUI.DisabledScope(sprites.Length == 0))
            {
                if (GUILayout.Button($"{sprites.Length}개 파츠 만들기", GUILayout.Height(26f)))
                    CreateParts(sprites);
            }
        }
    }

    private void DrawFolderField(string label, ref string folder, string prefsKey)
    {
        EditorGUI.BeginChangeCheck();
        folder = EditorGUILayout.TextField(label, folder);

        if (EditorGUI.EndChangeCheck())
            EditorPrefs.SetString(prefsKey, folder);
    }

    /// <summary>
    /// 스프라이트 하나당 SO + 프리팹을 만들고 서로 연결한다.
    /// 마지막에 Id 스캔까지 돌려서 "만들었는데 Id가 없어 장착이 안 되는" 상태를 남기지 않는다.
    /// </summary>
    private void CreateParts(Sprite[] sprites)
    {
        Type soType = _kind == PartKind.Core ? typeof(CoreDataSO) : typeof(BoosterDataSO);

        ItemAssetNaming.EnsureFolder(ItemAssetNaming.GetFolder(_rootFolder, soType));
        ItemAssetNaming.EnsureFolder(_prefabFolder);

        PartDataSO last = null;

        try
        {
            AssetDatabase.StartAssetEditing();

            for (int i = 0; i < sprites.Length; i++)
                last = CreateOne(sprites[i], soType) ?? last;
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        ItemDatabaseMenu.Rebuild();

        _selected = last;
        DestroyEditor(ref _selectedEditor);

        Debug.Log($"[PartForge] 파츠 {sprites.Length}개를 만들었습니다.");
    }

    private PartDataSO CreateOne(Sprite sprite, Type soType)
    {
        string name = Sanitize(sprite.name);

        string soPath = AssetDatabase.GenerateUniqueAssetPath(
            ItemAssetNaming.GetAssetPath(_rootFolder, soType, name));

        PartDataSO asset = (PartDataSO)CreateInstance(soType);
        AssetDatabase.CreateAsset(asset, soPath);

        MechaPartView view = CreateView(sprite, name);

        SerializedObject serialized = new(asset);
        serialized.FindProperty("_editorName").stringValue = name;
        serialized.FindProperty("_displayName").stringValue = name;
        serialized.FindProperty("_localizationKey").stringValue =
            ItemAssetNaming.BuildLocalizationKey(asset);
        serialized.FindProperty("<IconSprite>k__BackingField").objectReferenceValue = sprite;
        serialized.FindProperty("_viewPrefab").objectReferenceValue = view;

        // 기체 파츠는 한 칸에 하나씩. 겹쳐지면 어느 것을 장착했는지 알 수 없다.
        serialized.FindProperty("_maxStackCount").intValue = 1;
        serialized.FindProperty("<ItemCategory>k__BackingField").enumValueIndex =
            (int)ItemCategoryType.MechaPart;

        SerializedProperty slots = serialized.FindProperty("_allowedSlots");
        slots.arraySize = 1;
        slots.GetArrayElementAtIndex(0).enumValueIndex =
            IndexOfSlot(_kind == PartKind.Core ? EquipSlotType.Core : EquipSlotType.Booster);

        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(asset);

        return asset;
    }

    /// <summary>
    /// 비주얼 프리팹. 스프라이트는 <b>자식</b>에 둔다 —
    /// 루트는 조립기가 잡는 자리라 비워둬야 나중에 피벗 보정을 넣을 수 있다.
    /// </summary>
    private MechaPartView CreateView(Sprite sprite, string name)
    {
        GameObject root = new($"PartView_{name}");
        root.AddComponent<MechaPartView>();

        GameObject visual = new("Visual");
        visual.transform.SetParent(root.transform, false);
        visual.AddComponent<SpriteRenderer>().sprite = sprite;

        if (_kind == PartKind.Core)
        {
            foreach (KeyValuePair<EquipSlotType, Vector2> pair in CoreSockets)
                AddSocket(root.transform, pair.Key, pair.Value);
        }

        string path = AssetDatabase.GenerateUniqueAssetPath($"{_prefabFolder}/PartView_{name}.prefab");
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);

        DestroyImmediate(root);

        return saved != null ? saved.GetComponent<MechaPartView>() : null;
    }

    private static void AddSocket(Transform parent, EquipSlotType slot, Vector2 position)
    {
        GameObject socket = new($"Socket_{slot}");
        socket.transform.SetParent(parent, false);
        socket.transform.localPosition = position;

        SerializedObject serialized = new(socket.AddComponent<MechaSocket>());
        serialized.FindProperty("_slot").enumValueIndex = IndexOfSlot(slot);
        serialized.ApplyModifiedProperties();
    }

    /// <summary>
    /// enumValueIndex는 값이 아니라 <b>선언 순서</b>다. EquipSlotType은 값에 구멍이 있어서
    /// (int)slot을 그대로 넣으면 엉뚱한 슬롯이 된다.
    /// </summary>
    private static int IndexOfSlot(EquipSlotType slot)
        => Array.IndexOf((EquipSlotType[])Enum.GetValues(typeof(EquipSlotType)), slot);

    private static string Sanitize(string value)
        => new(value.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());

    #endregion

    #region 2. 설정

    private void DrawParts()
    {
        _showParts = EditorGUILayout.BeginFoldoutHeaderGroup(_showParts, "2. 설정");
        EditorGUILayout.EndFoldoutHeaderGroup();

        if (!_showParts) return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUI.BeginChangeCheck();
            _selected = (PartDataSO)EditorGUILayout.ObjectField(
                "파츠", _selected, typeof(PartDataSO), false);

            if (EditorGUI.EndChangeCheck())
                DestroyEditor(ref _selectedEditor);

            if (_selected == null)
            {
                EditorGUILayout.LabelField("만든 파츠를 넣으면 여기서 바로 고칠 수 있습니다.",
                    EditorStyles.miniLabel);
                return;
            }

            DrawPartWarnings(_selected);

            if (_selectedEditor == null)
                _selectedEditor = Editor.CreateEditor(_selected);

            _selectedEditor.OnInspectorGUI();

            if (_selected.ViewPrefab != null && GUILayout.Button("프리팹 열기 (소켓 맞추기)"))
                AssetDatabase.OpenAsset(_selected.ViewPrefab.gameObject);
        }
    }

    /// <summary>플레이를 눌러보고서야 알게 되면 안 되는 것들.</summary>
    private static void DrawPartWarnings(PartDataSO part)
    {
        List<string> problems = new();

        if (part.Id == 0)
            problems.Add("Id가 없습니다. Tools > Item Database > Rebuild를 돌리세요.");

        if (part.ViewPrefab == null)
            problems.Add("비주얼 프리팹이 없습니다. 스탯만 적용되고 모양은 안 바뀝니다.");
        else if (part is CoreDataSO && !part.ViewPrefab.HasSocket(EquipSlotType.ArmLeft))
            problems.Add("코어인데 ArmLeft 소켓이 없습니다. 무기가 폴백 위치에 달립니다.");

        if (part.AllowedSlots.Count == 0)
            problems.Add("들어갈 슬롯이 지정되지 않았습니다.");

        if (problems.Count > 0)
            EditorGUILayout.HelpBox(string.Join("\n", problems), MessageType.Warning);
    }

    #endregion

    #region 3. 출격

    private void DrawLoadout()
    {
        _showLoadout = EditorGUILayout.BeginFoldoutHeaderGroup(_showLoadout, "3. 출격");
        EditorGUILayout.EndFoldoutHeaderGroup();

        if (!_showLoadout) return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUI.BeginChangeCheck();
            _loadout = (MechaLoadoutSO)EditorGUILayout.ObjectField(
                "Loadout", _loadout, typeof(MechaLoadoutSO), false);

            if (EditorGUI.EndChangeCheck())
            {
                DestroyEditor(ref _loadoutEditor);
                LoadoutBootstrap.Loadout = _loadout;
            }

            if (_loadout == null)
            {
                EditorGUILayout.LabelField("출격 구성을 지정하세요.", EditorStyles.miniLabel);
                return;
            }

            DrawAssignButton();

            if (_loadoutEditor == null)
                _loadoutEditor = Editor.CreateEditor(_loadout);

            _loadoutEditor.OnInspectorGUI();

            EditorGUILayout.Space(4f);
            DrawPlayButton();
        }
    }

    /// <summary>2번에서 고른 파츠를 그 자리에 바로 꽂는다. 구성표를 손으로 여는 단계를 없앤다.</summary>
    private void DrawAssignButton()
    {
        if (_selected == null) return;

        EquipSlotType slot = _selected.AllowedSlots.Count > 0
            ? _selected.AllowedSlots[0]
            : EquipSlotType.None;

        using (new EditorGUI.DisabledScope(slot == EquipSlotType.None))
        {
            if (GUILayout.Button($"'{_selected.name}'을 {slot} 칸에 넣기"))
                Assign(_loadout, slot, _selected);
        }
    }

    private static void Assign(MechaLoadoutSO loadout, EquipSlotType slot, PartDataSO part)
    {
        SerializedObject serialized = new(loadout);
        SerializedProperty entries = serialized.FindProperty("_entries");

        // 같은 슬롯이 이미 있으면 덮어쓴다. 두 개가 되면 뒤엣것이 조용히 이긴다.
        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);

            if (entry.FindPropertyRelative("slot").enumValueIndex != IndexOfSlot(slot)) continue;

            entry.FindPropertyRelative("item").objectReferenceValue = part;
            serialized.ApplyModifiedProperties();
            return;
        }

        entries.arraySize++;

        SerializedProperty added = entries.GetArrayElementAtIndex(entries.arraySize - 1);
        added.FindPropertyRelative("slot").enumValueIndex = IndexOfSlot(slot);
        added.FindPropertyRelative("item").objectReferenceValue = part;

        serialized.ApplyModifiedProperties();
    }

    private void DrawPlayButton()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            if (GUILayout.Button("정지", GUILayout.Height(28f)))
                EditorApplication.isPlaying = false;

            return;
        }

        if (GUILayout.Button("이 구성으로 Play", GUILayout.Height(28f)))
            LoadoutBootstrap.Play(_loadout);
    }

    #endregion
}
