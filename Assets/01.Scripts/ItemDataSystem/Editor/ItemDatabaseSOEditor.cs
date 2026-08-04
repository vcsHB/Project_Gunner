using System.Text;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ItemDatabaseSO))]
public class ItemDatabaseSOEditor : Editor
{
    public override void OnInspectorGUI()
    {
        ItemDatabaseSO database = (ItemDatabaseSO)target;

        EditorGUILayout.HelpBox(
            $"등록된 아이템 {database.All.Count}개 / 다음 Id {database.NextId}", MessageType.None);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("프로젝트 스캔 & 갱신", GUILayout.Height(28f)))
            ItemDatabaseBuilder.Rebuild(database);

        if (GUILayout.Button("Item Creator 열기", GUILayout.Height(28f), GUILayout.Width(140f)))
            ItemCreatorWindow.Open();

        EditorGUILayout.EndHorizontal();

        DrawValidation(database);

        EditorGUILayout.Space();

        // Id가 툴 밖에서 바뀌면 세이브가 깨지므로 인스펙터에서는 읽기 전용으로만 보여준다.
        using (new EditorGUI.DisabledScope(true))
            DrawDefaultInspector();
    }

    private static void DrawValidation(ItemDatabaseSO database)
    {
        StringBuilder problems = new();

        foreach (ItemDataSO item in database.All)
        {
            if (item == null)
            {
                problems.AppendLine("- 목록에 빈 항목이 있습니다. 스캔을 다시 실행하세요.");
                continue;
            }

            if (!item.IsRegistered)
                problems.AppendLine($"- {item.name}: Id가 없습니다.");

            if (item.ItemCategory == ItemCategoryType.None)
                problems.AppendLine($"- {item.name}: 카테고리가 None 입니다.");

            if (item.IconSprite == null)
                problems.AppendLine($"- {item.name}: 아이콘이 없습니다.");
        }

        if (problems.Length > 0)
            EditorGUILayout.HelpBox(problems.ToString().TrimEnd(), MessageType.Warning);
    }
}
