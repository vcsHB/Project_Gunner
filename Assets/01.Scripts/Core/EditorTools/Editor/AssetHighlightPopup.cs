using System.Collections.Generic;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

/// <summary>
/// Ctrl+/ 로 뜨는 색 고르기 팝업. 화살표키 + Enter, 숫자키, 마우스 클릭 전부 받는다.
///
/// 단축키는 Edit &gt; Shortcuts 에서 "Asset Highlight/Pick Color"로 바꿀 수 있습니다.
/// </summary>
public class AssetHighlightPopup : EditorWindow
{
    private const float Width = 208f;
    private const float RowHeight = 22f;
    private const float HeaderHeight = 20f;
    private const float Padding = 4f;
    private const float SwatchWidth = 26f;

    private string[] _guids;
    private int _index;

    /// <summary>고른 행을 그리는 도중에 실행하면 창을 닫으면서 레이아웃이 깨진다. OnGUI 끝에서 한 번에 처리한다.</summary>
    private int _pending = -1;

    [Shortcut("Asset Highlight/Pick Color", KeyCode.Slash, ShortcutModifiers.Action)]
    private static void OpenFromShortcut()
    {
        string[] guids = Selection.assetGUIDs;

        // 하이어라키 오브젝트만 골라둔 상태면 칠할 대상이 없다.
        if (guids == null || guids.Length == 0) return;

        AssetHighlightPopup popup = CreateInstance<AssetHighlightPopup>();
        popup._guids = (string[])guids.Clone();

        int rows = AssetHighlightStore.Palette.Count + 2;
        Vector2 size = new(Width, HeaderHeight + rows * RowHeight + Padding * 2f);

        popup.ShowAsDropDown(GetActivatorRect(), size);
    }

    /// <summary>마우스 옆에 띄운다. 단축키가 GUI 밖에서 불릴 수도 있어서 창 중앙으로 물러설 길을 둔다.</summary>
    private static Rect GetActivatorRect()
    {
        EditorWindow focused = focusedWindow;

        Vector2 point;

        if (Event.current != null)
            point = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
        else if (focused != null)
            point = focused.position.center;
        else
            point = EditorGUIUtility.GetMainWindowPosition().center;

        return new Rect(point, Vector2.zero);
    }

    private void OnEnable()
    {
        // 마우스를 올리기만 해도 선택이 따라오게 하려면 필요하다.
        wantsMouseMove = true;
    }

    private void OnGUI()
    {
        IReadOnlyList<AssetHighlightStore.PaletteEntry> palette = AssetHighlightStore.Palette;
        int rowCount = palette.Count + 2;

        HandleKeys(rowCount);

        Rect header = new(Padding, Padding, position.width - Padding * 2f, HeaderHeight);
        EditorGUI.LabelField(header, $"에셋 {_guids.Length}개", EditorStyles.miniLabel);

        float y = header.yMax;

        for (int i = 0; i < palette.Count; i++)
        {
            DrawRow(new Rect(Padding, y, position.width - Padding * 2f, RowHeight), i,
                palette[i].Name, palette[i].Color);

            y += RowHeight;
        }

        DrawRow(new Rect(Padding, y, position.width - Padding * 2f, RowHeight), palette.Count,
            "하이라이트 지우기", null);

        y += RowHeight;

        DrawRow(new Rect(Padding, y, position.width - Padding * 2f, RowHeight), palette.Count + 1,
            "색상 설정...", null);

        if (_pending < 0) return;

        int pending = _pending;
        _pending = -1;
        Apply(pending, palette);
    }

    private void DrawRow(Rect rect, int index, string label, Color? color)
    {
        Event e = Event.current;
        bool hover = rect.Contains(e.mousePosition);

        if (hover && e.type == EventType.MouseMove && _index != index)
        {
            _index = index;
            Repaint();
        }

        if (e.type == EventType.Repaint && _index == index)
            EditorGUI.DrawRect(rect, new Color(0.35f, 0.55f, 0.85f, 0.45f));

        Rect swatch = new(rect.x + 3f, rect.y + 4f, SwatchWidth, rect.height - 8f);

        if (color.HasValue)
        {
            EditorGUI.DrawRect(swatch, color.Value);
        }
        else if (index == _index || hover)
        {
            // 색이 없는 행(지우기 · 설정)은 비워두면 어디까지가 행인지 안 보인다.
            EditorGUI.DrawRect(swatch, new Color(1f, 1f, 1f, 0.06f));
        }

        Rect text = new(swatch.xMax + 6f, rect.y, rect.width - SwatchWidth - 12f, rect.height);
        EditorGUI.LabelField(text, label);

        // 숫자키 안내. 팔레트가 아무리 늘어도 1~9까지만 의미가 있다.
        if (color.HasValue && index < 9)
        {
            Rect hint = new(rect.xMax - 18f, rect.y, 16f, rect.height);
            EditorGUI.LabelField(hint, (index + 1).ToString(), EditorStyles.centeredGreyMiniLabel);
        }

        if (e.type == EventType.MouseDown && e.button == 0 && hover)
        {
            _pending = index;
            e.Use();
        }
    }

    private void HandleKeys(int rowCount)
    {
        Event e = Event.current;
        if (e.type != EventType.KeyDown) return;

        switch (e.keyCode)
        {
            case KeyCode.DownArrow:
            case KeyCode.RightArrow:
                _index = (_index + 1) % rowCount;
                e.Use();
                Repaint();
                return;

            case KeyCode.UpArrow:
            case KeyCode.LeftArrow:
                _index = (_index - 1 + rowCount) % rowCount;
                e.Use();
                Repaint();
                return;

            case KeyCode.Return:
            case KeyCode.KeypadEnter:
                _pending = _index;
                e.Use();
                return;

            case KeyCode.Escape:
                e.Use();
                Close();
                return;
        }

        // 숫자키로 바로 고르기. 팔레트 범위를 벗어나면 무시한다.
        if (e.keyCode >= KeyCode.Alpha1 && e.keyCode <= KeyCode.Alpha9)
        {
            int picked = e.keyCode - KeyCode.Alpha1;
            if (picked >= rowCount - 2) return;

            _pending = picked;
            e.Use();
        }
    }

    private void Apply(int index, IReadOnlyList<AssetHighlightStore.PaletteEntry> palette)
    {
        if (index < palette.Count) AssetHighlightStore.Assign(_guids, palette[index].Color);
        else if (index == palette.Count) AssetHighlightStore.Clear(_guids);

        bool openSettings = index == palette.Count + 1;

        Close();

        if (openSettings) AssetHighlightSettingsWindow.Open();
    }
}
