using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 스크립트를 지우면 그걸 붙여둔 오브젝트에 "The associated script can not be loaded"가 남는다.
/// 인스펙터에서 하나씩 지울 수 있지만, 어느 오브젝트에 남았는지 찾는 것부터가 일이다.
///
/// 비활성 오브젝트와 프리팹 안쪽까지 훑으므로 눈으로 찾는 것보다 확실하다.
///
/// 게임 코드에 아무것도 남기지 않는 작업 편의 도구다. (CONVENTIONS.md 에디터 툴)
/// </summary>
public static class MissingScriptCleaner
{
    [MenuItem("Tools/Cleanup/Remove Missing Scripts In Scene")]
    public static void CleanLoadedScenes()
    {
        List<GameObject> roots = new();

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            roots.AddRange(scene.GetRootGameObjects());
        }

        int removed = Clean(roots, out string report);

        if (removed == 0)
        {
            Debug.Log("[Cleanup] 씬에 끊어진 스크립트가 없습니다.");
            return;
        }

        // 씬을 더럽혀야 저장 대상이 된다. 안 하면 지운 것이 다음 로드에서 되살아난다.
        for (int i = 0; i < SceneManager.sceneCount; i++)
            EditorSceneManager.MarkSceneDirty(SceneManager.GetSceneAt(i));

        Debug.Log($"[Cleanup] 끊어진 스크립트 {removed}개를 지웠습니다.\n{report}");
    }

    [MenuItem("Tools/Cleanup/Remove Missing Scripts In Selection")]
    public static void CleanSelection()
    {
        List<GameObject> roots = new(Selection.gameObjects);

        if (roots.Count == 0)
        {
            Debug.LogWarning("[Cleanup] 선택된 오브젝트가 없습니다.");
            return;
        }

        int removed = Clean(roots, out string report);

        if (removed == 0)
        {
            Debug.Log("[Cleanup] 선택한 오브젝트에 끊어진 스크립트가 없습니다.");
            return;
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[Cleanup] 끊어진 스크립트 {removed}개를 지웠습니다.\n{report}");
    }

    /// <summary>어디서 몇 개를 지웠는지 함께 돌려준다. 지운 뒤에는 추적할 방법이 없다.</summary>
    private static int Clean(List<GameObject> roots, out string report)
    {
        StringBuilder log = new();
        int total = 0;

        for (int i = 0; i < roots.Count; i++)
        {
            if (roots[i] == null) continue;

            // 비활성 오브젝트도 포함한다. 꺼둔 UI에 남아 있는 경우가 가장 찾기 어렵다.
            Transform[] all = roots[i].GetComponentsInChildren<Transform>(true);

            for (int j = 0; j < all.Length; j++)
            {
                GameObject target = all[j].gameObject;

                int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(target);
                if (count == 0) continue;

                Undo.RegisterCompleteObjectUndo(target, "Remove Missing Scripts");
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(target);

                total += count;
                log.AppendLine($"  {GetPath(all[j])} — {count}개");
            }
        }

        report = log.ToString();
        return total;
    }

    private static string GetPath(Transform target)
    {
        string path = target.name;

        for (Transform parent = target.parent; parent != null; parent = parent.parent)
            path = $"{parent.name}/{path}";

        return path;
    }
}
