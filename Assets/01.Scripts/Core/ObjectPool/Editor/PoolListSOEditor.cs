using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PoolListSO))]
public class PoolListSOEditor : Editor
{
    public override void OnInspectorGUI()
    {
        PoolListSO list = (PoolListSO)target;

        int groupCount = list.Groups.Count;
        int itemCount = 0;
        foreach (PoolItem _ in list.AllItems)
            itemCount++;

        EditorGUILayout.HelpBox($"탭 {groupCount}개 / 항목 {itemCount}개", MessageType.None);

        if (!PoolEnumGenerator.IsUpToDate(list))
            EditorGUILayout.HelpBox("PoolType이 목록과 다릅니다. Pool Manager에서 Generate Enum을 실행하세요.", MessageType.Warning);

        if (GUILayout.Button("Pool Manager 열기", GUILayout.Height(28f)))
            PoolManagerWindow.Open();

        EditorGUILayout.Space();

        // 창에서 편집하는 것이 정상 경로라 인스펙터에서는 읽기 전용으로만 보여준다.
        using (new EditorGUI.DisabledScope(true))
            DrawDefaultInspector();
    }
}
