using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ItemDataSO 파생 클래스를 긁어와 에셋 생성을 한 곳에서 처리한다.
/// 이름은 Item_{종류}_{이름} 규칙으로 자동으로 붙는다.
/// </summary>
public class ItemCreatorWindow : EditorWindow
{
    private const string RootFolderKey = "ProjectGunner.ItemCreator.RootFolder";
    private const string DefaultRootFolder = "Assets/06.Data/Items";

    [MenuItem("Tools/Item Creator")]
    public static void Open()
    {
        ItemCreatorWindow window = GetWindow<ItemCreatorWindow>("Item Creator");
        window.minSize = new Vector2(520f, 420f);
    }

    private string _rootFolder;
    private string _itemName = string.Empty;
    private Type _selectedType;
    private Vector2 _typeScroll;
    private Vector2 _renameScroll;

    private List<Type> _types;

    private void OnEnable()
    {
        _rootFolder = EditorPrefs.GetString(RootFolderKey, DefaultRootFolder);
        RefreshTypes();
    }

    private void RefreshTypes()
    {
        _types = new List<Type>();

        foreach (Type type in TypeCache.GetTypesDerivedFrom<ItemDataSO>())
        {
            if (type.IsAbstract) continue;

            _types.Add(type);
        }

        _types.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));

        if (_selectedType == null && _types.Count > 0)
            _selectedType = _types[0];
    }

    private void OnGUI()
    {
        DrawRootFolder();
        EditorGUILayout.Space();

        DrawTypeList();
        EditorGUILayout.Space();

        DrawCreate();

        EditorGUILayout.Space();
        DrawMaintenance();
    }

    #region Root Folder

    private void DrawRootFolder()
    {
        EditorGUILayout.BeginHorizontal();

        EditorGUI.BeginChangeCheck();
        _rootFolder = EditorGUILayout.TextField("루트 폴더", _rootFolder);
        if (EditorGUI.EndChangeCheck())
            EditorPrefs.SetString(RootFolderKey, _rootFolder);

        if (GUILayout.Button("...", GUILayout.Width(30f)))
            BrowseRootFolder();

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField(" ", "종류별 하위 폴더가 자동으로 만들어집니다.", EditorStyles.miniLabel);
    }

    private void BrowseRootFolder()
    {
        string absolute = EditorUtility.OpenFolderPanel("루트 폴더 선택", _rootFolder, string.Empty);
        if (string.IsNullOrEmpty(absolute)) return;

        string dataPath = Application.dataPath;
        if (!absolute.StartsWith(dataPath, StringComparison.Ordinal))
        {
            EditorUtility.DisplayDialog("Item Creator", "프로젝트 Assets 폴더 안이어야 합니다.", "확인");
            return;
        }

        _rootFolder = "Assets" + absolute.Substring(dataPath.Length);
        EditorPrefs.SetString(RootFolderKey, _rootFolder);
        GUI.FocusControl(null);
    }

    #endregion

    #region Create

    private void DrawTypeList()
    {
        EditorGUILayout.LabelField($"만들 종류 ({_types.Count}개)", EditorStyles.boldLabel);

        _typeScroll = EditorGUILayout.BeginScrollView(_typeScroll, GUILayout.Height(150f));

        foreach (Type type in _types)
        {
            bool selected = _selectedType == type;

            if (GUILayout.Toggle(selected, $"  {ItemAssetNaming.GetKindName(type)}   ({type.Name})",
                    EditorStyles.miniButton, GUILayout.Height(20f)) && !selected)
                _selectedType = type;
        }

        EditorGUILayout.EndScrollView();

        if (_types.Count == 0)
            EditorGUILayout.HelpBox("ItemDataSO를 상속한 클래스가 없습니다.", MessageType.Info);
    }

    private void DrawCreate()
    {
        _itemName = EditorGUILayout.TextField("아이템 이름", _itemName);

        bool nameValid = ItemAssetNaming.IsValidName(_itemName);
        bool typeValid = _selectedType != null;

        if (typeValid && nameValid)
        {
            string path = ItemAssetNaming.GetAssetPath(_rootFolder, _selectedType, _itemName);
            EditorGUILayout.LabelField(" ", path, EditorStyles.miniLabel);

            if (AssetDatabase.LoadAssetAtPath<ItemDataSO>(path) != null)
                EditorGUILayout.HelpBox("같은 경로에 이미 있습니다. 다른 이름을 쓰세요.", MessageType.Error);
        }
        else
        {
            EditorGUILayout.LabelField(" ", "종류와 이름을 지정하세요.", EditorStyles.miniLabel);
        }

        using (new EditorGUI.DisabledScope(!typeValid || !nameValid))
        {
            if (GUILayout.Button("생성", GUILayout.Height(28f)))
                Create();
        }
    }

    private void Create()
    {
        string folder = ItemAssetNaming.GetFolder(_rootFolder, _selectedType);
        ItemAssetNaming.EnsureFolder(folder);

        string path = ItemAssetNaming.GetAssetPath(_rootFolder, _selectedType, _itemName);
        if (AssetDatabase.LoadAssetAtPath<ItemDataSO>(path) != null) return;

        ItemDataSO asset = (ItemDataSO)CreateInstance(_selectedType);
        asset.itemName = _itemName;

        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();

        // 만들자마자 Id를 받아야 다른 곳에서 참조할 수 있다.
        ItemDatabaseSO database = ItemDatabaseBuilder.FindDatabase();
        if (database != null)
            ItemDatabaseBuilder.Rebuild(database);
        else
            Debug.LogWarning("[Item Creator] ItemDatabase가 없어 Id를 부여하지 못했습니다.", asset);

        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);

        _itemName = string.Empty;
        GUI.FocusControl(null);
    }

    #endregion

    #region Maintenance

    private void DrawMaintenance()
    {
        EditorGUILayout.LabelField("정리", EditorStyles.boldLabel);

        List<ItemDataSO> mismatched = FindMismatchedNames();

        if (mismatched.Count == 0)
        {
            EditorGUILayout.HelpBox("모든 아이템 에셋이 이름 규칙에 맞습니다.", MessageType.Info);
            return;
        }

        EditorGUILayout.HelpBox($"이름 규칙과 다른 에셋 {mismatched.Count}개", MessageType.Warning);

        _renameScroll = EditorGUILayout.BeginScrollView(_renameScroll, GUILayout.Height(100f));
        foreach (ItemDataSO item in mismatched)
            EditorGUILayout.LabelField($"{item.name}  →  {ItemAssetNaming.BuildAssetName(item)}", EditorStyles.miniLabel);

        EditorGUILayout.EndScrollView();

        if (GUILayout.Button("이름 규칙 일괄 적용", GUILayout.Height(24f)))
            RenameAll(mismatched);
    }

    private static List<ItemDataSO> FindMismatchedNames()
    {
        List<ItemDataSO> result = new();

        foreach (ItemDataSO item in ItemDatabaseBuilder.LoadAllItemAssets())
        {
            if (!ItemAssetNaming.IsConventional(item))
                result.Add(item);
        }

        return result;
    }

    private static void RenameAll(List<ItemDataSO> targets)
    {
        // 에셋 이름을 바꿔도 GUID는 그대로라 기존 참조는 유지된다.
        if (!EditorUtility.DisplayDialog("이름 규칙 일괄 적용",
                $"{targets.Count}개의 에셋 이름을 바꿉니다.\n참조는 GUID 기준이라 끊기지 않습니다.", "적용", "취소"))
            return;

        StringBuilder failures = new();

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (ItemDataSO item in targets)
            {
                string path = AssetDatabase.GetAssetPath(item);
                string error = AssetDatabase.RenameAsset(path, ItemAssetNaming.BuildAssetName(item));

                if (!string.IsNullOrEmpty(error))
                    failures.AppendLine($"- {item.name}: {error}");
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        if (failures.Length > 0)
            Debug.LogError($"[Item Creator] 이름 변경 실패\n{failures}");
        else
            Debug.Log($"[Item Creator] {targets.Count}개의 이름을 규칙에 맞게 바꿨습니다.");
    }

    #endregion
}
