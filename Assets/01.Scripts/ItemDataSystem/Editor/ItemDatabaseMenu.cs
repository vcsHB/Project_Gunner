using UnityEditor;
using UnityEngine;

/// <summary>
/// 인스펙터 버튼으로만 할 수 있던 것을 메뉴에서도 부를 수 있게 한다.
///
/// 인스펙터는 사람이 눌러야 하므로 자동화에서 닿지 않는다. 스캔은 에셋을 추가할 때마다
/// 반드시 한 번 돌아야 하는 작업이라, 메뉴에도 길을 내 둔다.
///
/// <b>전체 재부여는 여기에 넣지 않는다.</b> Id 불변 규칙을 깨는 유일한 경로라
/// 실수로 눌리면 안 되고, 경고를 읽은 사람만 누를 수 있어야 한다.
/// </summary>
public static class ItemDatabaseMenu
{
    // 메뉴 경로는 ASCII로 둔다. &는 유니티가 단축키 지정자로 먹고,
    // 한글 경로는 MCP 브리지에서 매칭에 실패한다(INCIDENTS 참고).
    [MenuItem("Tools/Item Database/Rebuild")]
    public static void Rebuild()
    {
        ItemDatabaseSO database = ItemDatabaseBuilder.FindDatabase();
        if (database == null)
        {
            Debug.LogError("[ItemDatabase] 데이터베이스 에셋을 찾지 못했습니다.");
            return;
        }

        ItemDatabaseBuilder.Rebuild(database);
        Debug.Log($"[ItemDatabase] 스캔 완료 — {database.All.Count}개 등록됨.", database);
    }

    [MenuItem("Tools/Item Database/Select Asset")]
    public static void SelectDatabase()
    {
        ItemDatabaseSO database = ItemDatabaseBuilder.FindDatabase();
        if (database == null)
        {
            Debug.LogError("[ItemDatabase] 데이터베이스 에셋을 찾지 못했습니다.");
            return;
        }

        Selection.activeObject = database;
        EditorGUIUtility.PingObject(database);
    }
}
