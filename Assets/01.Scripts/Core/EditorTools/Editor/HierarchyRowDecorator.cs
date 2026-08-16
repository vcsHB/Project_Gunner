using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 하이어라키 행에 계층선과 토글, 컴포넌트 아이콘을 얹는다.
///
/// 항상 그리는 것은 <b>계층선과 활성 체크박스</b>뿐입니다.
/// 컴포넌트 아이콘과 RaycastTarget 토글은 <b>마우스를 올린 행에만</b> 나옵니다 —
/// 행 폭이 좁아서 전부 상시로 그리면 이름이 가려집니다.
/// </summary>
[InitializeOnLoad]
public static class HierarchyRowDecorator
{
    private const float IndentWidth = 14f;
    private const float IconSize = 16f;
    private const float ToggleWidth = 15f;
    private const float RaycastWidth = 20f;
    private const float RightMargin = 2f;

    /// <summary>행 폭이 좁아서 이 이상은 못 그린다. 넘치면 +N으로 알린다.</summary>
    private const int MaxIcons = 3;

    private static readonly List<Component> _components = new();
    private static readonly List<Component> _shown = new();

    /// <summary>스킨을 바꾸면 바로 따라와야 하므로 매번 읽는다.</summary>
    private static Color LineColor => EditorGUIUtility.isProSkin
        ? new Color(1f, 1f, 1f, 0.13f)
        : new Color(0f, 0f, 0f, 0.18f);

    static HierarchyRowDecorator()
    {
        EditorApplication.hierarchyWindowItemOnGUI -= OnItemGUI;
        EditorApplication.hierarchyWindowItemOnGUI += OnItemGUI;
    }

    private static void OnItemGUI(int instanceId, Rect rect)
    {
        // 씬 헤더 행은 GameObject가 아니다.
        if (EditorUtility.EntityIdToObject(instanceId) is not GameObject go) return;

        DrawTreeLines(go, rect);

        float x = rect.xMax - RightMargin;
        DrawActiveToggle(go, rect, ref x);

        if (!rect.Contains(Event.current.mousePosition)) return;

        DrawRaycastToggle(go, rect, ref x);
        DrawComponentIcons(go, rect, x);
    }

    /// <summary>
    /// 부모에서 내려오는 줄기와 자신을 잇는다.
    /// 들여쓰기 폭을 rect.x에서 역산하지 않고 실제 부모를 타고 올라가는 이유는,
    /// 검색·정렬 상태에서 rect.x가 계층과 어긋나기 때문입니다.
    /// </summary>
    private static void DrawTreeLines(GameObject go, Rect rect)
    {
        if (Event.current.type != EventType.Repaint) return;

        Transform current = go.transform;
        if (current.parent == null) return;

        float center = rect.y + rect.height * 0.5f;
        float x = rect.x - IndentWidth * 0.5f;

        // 자기 레벨 — 막내면 세로선을 자기 높이의 절반에서 끊는다.
        float height = IsLastSibling(current) ? center - rect.y : rect.height;
        EditorGUI.DrawRect(new Rect(x, rect.y, 1f, height), LineColor);
        EditorGUI.DrawRect(new Rect(x, center, IndentWidth * 0.5f - 1f, 1f), LineColor);

        // 조상 레벨 — 아직 형제가 남은 조상만 세로선을 이어 준다.
        current = current.parent;
        x -= IndentWidth;

        while (current != null && current.parent != null)
        {
            if (!IsLastSibling(current))
                EditorGUI.DrawRect(new Rect(x, rect.y, 1f, rect.height), LineColor);

            current = current.parent;
            x -= IndentWidth;
        }
    }

    private static bool IsLastSibling(Transform transform)
    {
        Transform parent = transform.parent;
        if (parent == null) return true;

        return transform.GetSiblingIndex() == parent.childCount - 1;
    }

    private static void DrawActiveToggle(GameObject go, Rect rect, ref float x)
    {
        x -= ToggleWidth;

        Rect area = new(x, rect.y + (rect.height - ToggleWidth) * 0.5f, ToggleWidth, ToggleWidth);

        EditorGUI.BeginChangeCheck();
        bool active = GUI.Toggle(area, go.activeSelf, GUIContent.none);

        if (!EditorGUI.EndChangeCheck()) return;

        Undo.RecordObject(go, active ? "Activate GameObject" : "Deactivate GameObject");
        go.SetActive(active);
    }

    /// <summary>UI 오브젝트만 나온다. Graphic이 없으면 RaycastTarget 자체가 없다.</summary>
    private static void DrawRaycastToggle(GameObject go, Rect rect, ref float x)
    {
        if (!go.TryGetComponent(out Graphic graphic)) return;

        x -= RaycastWidth;

        Rect area = new(x, rect.y + (rect.height - IconSize) * 0.5f, RaycastWidth, IconSize);
        GUIContent label = new("R", "Raycast Target");

        EditorGUI.BeginChangeCheck();
        bool on = GUI.Toggle(area, graphic.raycastTarget, label, EditorStyles.miniButton);

        if (!EditorGUI.EndChangeCheck()) return;

        Undo.RecordObject(graphic, "Toggle Raycast Target");
        graphic.raycastTarget = on;
    }

    /// <summary>붙어 있는 컴포넌트를 아이콘으로 미리 보여준다. 툴팁에 타입 이름이 뜬다.</summary>
    private static void DrawComponentIcons(GameObject go, Rect rect, float rightEdge)
    {
        go.GetComponents(_components);
        _shown.Clear();

        int extra = 0;

        for (int i = 0; i < _components.Count; i++)
        {
            Component component = _components[i];

            // 스크립트가 빠진 칸은 null로 들어온다. Transform은 모든 행에 있어서 알려주는 게 없다.
            if (component == null) continue;
            if (component is Transform) continue;

            if (_shown.Count < MaxIcons) _shown.Add(component);
            else extra++;
        }

        _components.Clear();
        if (_shown.Count == 0) return;

        float width = _shown.Count * IconSize + (extra > 0 ? 22f : 0f);
        float x = rightEdge - width;
        float y = rect.y + (rect.height - IconSize) * 0.5f;

        if (extra > 0)
        {
            GUI.Label(new Rect(x, rect.y, 22f, rect.height), $"+{extra}", EditorStyles.centeredGreyMiniLabel);
            x += 22f;
        }

        for (int i = 0; i < _shown.Count; i++)
        {
            Texture icon = AssetPreview.GetMiniThumbnail(_shown[i]);

            if (icon != null)
                GUI.Label(new Rect(x, y, IconSize, IconSize), new GUIContent(icon, _shown[i].GetType().Name));

            x += IconSize;
        }

        _shown.Clear();
    }
}
