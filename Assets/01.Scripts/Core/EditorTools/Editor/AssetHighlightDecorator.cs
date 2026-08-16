using UnityEditor;
using UnityEngine;

/// <summary>
/// 프로젝트 창에 칠해둔 색을 그린다.
///
/// 유니티가 항목을 먼저 그리고 이 콜백을 부르므로 <b>덧칠밖에 못 합니다.</b>
/// 그래서 전체는 옅게만 깔고, 진한 색은 글자가 없는 가장자리에 막대로 세웁니다.
/// </summary>
[InitializeOnLoad]
public static class AssetHighlightDecorator
{
    private const float BarThickness = 3f;
    private const float TintAlpha = 0.20f;

    /// <summary>이 높이를 넘으면 아이콘(그리드) 보기다. 목록 보기는 16~20px 언저리.</summary>
    private const float GridViewThreshold = 24f;

    static AssetHighlightDecorator()
    {
        EditorApplication.projectWindowItemOnGUI -= OnItemGUI;
        EditorApplication.projectWindowItemOnGUI += OnItemGUI;
    }

    private static void OnItemGUI(string guid, Rect rect)
    {
        if (Event.current.type != EventType.Repaint) return;
        if (!AssetHighlightStore.TryGetColor(guid, out Color color)) return;

        Color tint = color;
        tint.a = TintAlpha;
        EditorGUI.DrawRect(rect, tint);

        Rect bar = rect.height > GridViewThreshold
            ? new Rect(rect.x, rect.y, rect.width, BarThickness)                    // 아이콘 보기 — 셀 위쪽
            : new Rect(rect.xMax - BarThickness, rect.y, BarThickness, rect.height); // 목록 보기 — 오른쪽 끝

        EditorGUI.DrawRect(bar, color);
    }
}
