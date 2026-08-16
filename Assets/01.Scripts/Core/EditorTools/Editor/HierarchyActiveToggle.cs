using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 하이어라키에서 휠(가운데) 클릭으로 오브젝트를 껐다 켠다.
///
/// 여러 개를 골라둔 상태에서 그 중 하나를 누르면 <b>고른 것 전부</b>가 같이 바뀝니다.
/// 누른 오브젝트의 상태를 기준으로 뒤집으므로, 섞여 있어도 한 번에 정리됩니다.
/// </summary>
[InitializeOnLoad]
public static class HierarchyActiveToggle
{
    private static readonly List<Object> _targets = new();

    static HierarchyActiveToggle()
    {
        EditorApplication.hierarchyWindowItemOnGUI -= OnItemGUI;
        EditorApplication.hierarchyWindowItemOnGUI += OnItemGUI;
    }

    private static void OnItemGUI(int instanceId, Rect rect)
    {
        Event e = Event.current;

        if (e.type != EventType.MouseDown || e.button != 2) return;
        if (!rect.Contains(e.mousePosition)) return;

        if (EditorUtility.EntityIdToObject(instanceId) is not GameObject clicked) return;

        CollectTargets(clicked);

        bool next = !clicked.activeSelf;

        Undo.RecordObjects(_targets.ToArray(), next ? "Activate GameObjects" : "Deactivate GameObjects");

        for (int i = 0; i < _targets.Count; i++)
            ((GameObject)_targets[i]).SetActive(next);

        _targets.Clear();
        e.Use();
    }

    /// <summary>누른 것이 선택에 포함돼 있으면 선택 전체가 대상. 아니면 누른 것 하나만.</summary>
    private static void CollectTargets(GameObject clicked)
    {
        _targets.Clear();

        GameObject[] selection = Selection.gameObjects;
        bool inSelection = false;

        for (int i = 0; i < selection.Length; i++)
        {
            if (selection[i] != clicked) continue;

            inSelection = true;
            break;
        }

        if (!inSelection)
        {
            _targets.Add(clicked);
            return;
        }

        for (int i = 0; i < selection.Length; i++)
        {
            // 프리팹 에셋을 고른 상태면 씬 오브젝트가 아니라 건드리면 안 된다.
            if (selection[i] == null || selection[i].scene.IsValid() == false) continue;

            _targets.Add(selection[i]);
        }

        if (_targets.Count == 0) _targets.Add(clicked);
    }
}
