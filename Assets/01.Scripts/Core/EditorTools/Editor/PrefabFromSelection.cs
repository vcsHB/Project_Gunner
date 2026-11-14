using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 씬에서 손으로 맞춘 오브젝트를 프리팹으로 굳힌다.
///
/// 씬에서 피벗·정렬·자식 배치를 맞춰보고, 마음에 들면 그대로 프리팹이 되는 흐름이
/// 가장 손이 적게 든다. 프리팹을 먼저 만들고 프리팹 모드에서 맞추면 주변(기체 본체,
/// 소켓 위치)이 안 보여서 결국 씬에 꺼내 보게 된다.
///
/// 메뉴로 열어두는 이유 — 자동화에서 부를 수 있어야 한다. 에디터 창 안의 버튼은 닿지 않는다.
/// </summary>
public static class PrefabFromSelection
{
    private const string FolderKey = "ProjectGunner.PrefabFromSelection.Folder";
    private const string DefaultFolder = "Assets/03.Prefabs/Mecha";

    [MenuItem("Tools/Cleanup/Save Selection As Prefab")]
    public static void Save()
    {
        GameObject target = Selection.activeGameObject;

        if (target == null)
        {
            Debug.LogWarning("[Prefab] 선택된 오브젝트가 없습니다.");
            return;
        }

        if (PrefabUtility.IsPartOfPrefabInstance(target))
        {
            Debug.LogWarning($"[Prefab] {target.name}은 이미 프리팹 인스턴스입니다. Apply를 쓰세요.", target);
            return;
        }

        string folder = EditorPrefs.GetString(FolderKey, DefaultFolder);
        EnsureFolder(folder);

        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{target.name}.prefab");

        // 씬 오브젝트를 그대로 프리팹으로 바꾸면 씬 쪽이 인스턴스가 되어
        // 이후 수정이 프리팹으로 번진다. 여기서는 복사본만 굳히고 씬은 건드리지 않는다.
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(target, path, out bool success);

        if (!success || saved == null)
        {
            Debug.LogError($"[Prefab] {target.name} 저장에 실패했습니다.", target);
            return;
        }

        Debug.Log($"[Prefab] {path} 로 저장했습니다.", saved);

        Selection.activeObject = saved;
        EditorGUIUtility.PingObject(saved);
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;

        string[] parts = folder.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";

            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }
    }
}
