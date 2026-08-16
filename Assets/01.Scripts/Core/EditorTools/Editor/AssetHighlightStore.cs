using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 프로젝트 창 에셋 하이라이트의 저장소. EditorPrefs에 JSON으로 넣는다.
///
/// 프로젝트 에셋(SO)이 아니라 EditorPrefs를 쓰는 이유 — 하이라이트는 <b>지금 내가 보고 있는 것</b>을
/// 표시하는 개인 메모입니다. 에셋으로 두면 색 하나 칠할 때마다 diff가 생기고 팀원과 색이 충돌합니다.
/// 대신 다른 PC로 옮기면 따라가지 않습니다.
/// </summary>
public static class AssetHighlightStore
{
    private const string KeyPrefix = "ProjectGunner.AssetHighlight.";

    /// <summary>팔레트 한 칸. 이름은 팝업에서 사람이 읽는 용도로만 쓴다.</summary>
    [Serializable]
    public class PaletteEntry
    {
        public string Name;
        public Color Color;

        public PaletteEntry() { }

        public PaletteEntry(string name, Color color)
        {
            Name = name;
            Color = color;
        }
    }

    /// <summary>
    /// 에셋 하나에 칠해진 색. 팔레트 인덱스가 아니라 색 값을 그대로 들고 있습니다.
    /// 인덱스로 두면 팔레트에서 한 칸을 지우는 순간 칠해둔 것들이 통째로 밀립니다.
    /// </summary>
    [Serializable]
    private class Assignment
    {
        public string Guid;
        public Color Color;

        public Assignment() { }

        public Assignment(string guid, Color color)
        {
            Guid = guid;
            Color = color;
        }
    }

    [Serializable]
    private class SaveData
    {
        public List<PaletteEntry> Palette = new();
        public List<Assignment> Assignments = new();
    }

    private static SaveData _data;
    private static Dictionary<string, Color> _lookup;
    private static string _key;

    /// <summary>팔레트나 색 지정이 바뀌었을 때. 열려 있는 설정 창이 듣는다.</summary>
    public static event Action OnChangedEvent;

    public static IReadOnlyList<PaletteEntry> Palette
    {
        get
        {
            EnsureLoaded();
            return _data.Palette;
        }
    }

    public static int AssignedCount
    {
        get
        {
            EnsureLoaded();
            return _data.Assignments.Count;
        }
    }

    /// <summary>이 에셋에 칠해둔 색. 프로젝트 창이 행마다 부르므로 딕셔너리 조회로 끝낸다.</summary>
    public static bool TryGetColor(string guid, out Color color)
    {
        EnsureLoaded();
        return _lookup.TryGetValue(guid, out color);
    }

    /// <summary>고른 에셋들에 색을 칠한다.</summary>
    public static void Assign(IList<string> guids, Color color)
    {
        if (guids == null || guids.Count == 0) return;

        EnsureLoaded();

        for (int i = 0; i < guids.Count; i++)
        {
            string guid = guids[i];
            if (string.IsNullOrEmpty(guid)) continue;

            int found = IndexOf(guid);
            if (found >= 0) _data.Assignments[found].Color = color;
            else _data.Assignments.Add(new Assignment(guid, color));
        }

        Save();
    }

    /// <summary>고른 에셋들의 색을 지운다.</summary>
    public static void Clear(IList<string> guids)
    {
        if (guids == null || guids.Count == 0) return;

        EnsureLoaded();

        bool changed = false;

        for (int i = 0; i < guids.Count; i++)
        {
            int found = IndexOf(guids[i]);
            if (found < 0) continue;

            _data.Assignments.RemoveAt(found);
            changed = true;
        }

        if (changed) Save();
    }

    public static void ClearAll()
    {
        EnsureLoaded();

        if (_data.Assignments.Count == 0) return;

        _data.Assignments.Clear();
        Save();
    }

    /// <summary>
    /// 지워진 에셋에 남은 지정을 걷어낸다. 지운 개수를 반환한다.
    /// 자동으로 하지 않는 이유 — 브랜치를 잠깐 옮긴 사이에 에셋이 안 보이면
    /// 돌아왔을 때 칠해둔 것이 통째로 사라집니다. 사람이 눌러야 합니다.
    /// </summary>
    public static int PruneMissing()
    {
        EnsureLoaded();

        int removed = 0;

        for (int i = _data.Assignments.Count - 1; i >= 0; i--)
        {
            string path = AssetDatabase.GUIDToAssetPath(_data.Assignments[i].Guid);
            if (!string.IsNullOrEmpty(path)) continue;

            _data.Assignments.RemoveAt(i);
            removed++;
        }

        if (removed > 0) Save();
        return removed;
    }

    /// <summary>설정 창이 목록을 그리려고 부른다. 경로 순으로 고정해야 볼 때마다 순서가 안 바뀐다.</summary>
    public static void CollectAssignments(List<KeyValuePair<string, Color>> buffer)
    {
        if (buffer == null) return;

        EnsureLoaded();
        buffer.Clear();

        foreach (Assignment assignment in _data.Assignments)
            buffer.Add(new KeyValuePair<string, Color>(assignment.Guid, assignment.Color));

        buffer.Sort((a, b) => string.CompareOrdinal(
            AssetDatabase.GUIDToAssetPath(a.Key), AssetDatabase.GUIDToAssetPath(b.Key)));
    }

    /// <summary>팔레트를 통째로 교체한다. 설정 창이 편집한 복사본을 넘긴다.</summary>
    public static void SetPalette(List<PaletteEntry> palette)
    {
        EnsureLoaded();

        _data.Palette = palette ?? new List<PaletteEntry>();

        if (_data.Palette.Count == 0)
            _data.Palette = BuildDefaultPalette();

        Save();
    }

    public static void ResetPalette()
    {
        EnsureLoaded();

        _data.Palette = BuildDefaultPalette();
        Save();
    }

    public static List<PaletteEntry> BuildDefaultPalette()
    {
        return new List<PaletteEntry>
        {
            new("빨강", new Color(0.90f, 0.27f, 0.27f)),
            new("초록", new Color(0.31f, 0.78f, 0.37f)),
            new("파랑", new Color(0.29f, 0.55f, 0.95f)),
            new("노랑", new Color(0.95f, 0.79f, 0.22f)),
        };
    }

    private static int IndexOf(string guid)
    {
        for (int i = 0; i < _data.Assignments.Count; i++)
        {
            if (_data.Assignments[i].Guid == guid) return i;
        }

        return -1;
    }

    private static void EnsureLoaded()
    {
        if (_data != null) return;

        string json = EditorPrefs.GetString(Key, string.Empty);
        _data = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<SaveData>(json);

        _data ??= new SaveData();
        _data.Assignments ??= new List<Assignment>();

        if (_data.Palette == null || _data.Palette.Count == 0)
            _data.Palette = BuildDefaultPalette();

        RebuildLookup();
    }

    private static void Save()
    {
        RebuildLookup();

        EditorPrefs.SetString(Key, JsonUtility.ToJson(_data));

        OnChangedEvent?.Invoke();
        EditorApplication.RepaintProjectWindow();
    }

    private static void RebuildLookup()
    {
        _lookup ??= new Dictionary<string, Color>();
        _lookup.Clear();

        foreach (Assignment assignment in _data.Assignments)
        {
            if (string.IsNullOrEmpty(assignment.Guid)) continue;
            _lookup[assignment.Guid] = assignment.Color;
        }
    }

    private static string Key => _key ??= BuildKey();

    /// <summary>
    /// EditorPrefs는 유니티 전체가 공유하므로 프로젝트 경로로 키를 나눈다.
    /// string.GetHashCode는 런타임에 따라 값이 달라질 수 있어서 직접 고정 해시(FNV-1a)를 만듭니다.
    /// 그러지 않으면 에디터 버전을 올린 순간 칠해둔 것이 사라진 것처럼 보입니다.
    /// </summary>
    private static string BuildKey()
    {
        string path = Application.dataPath;
        uint hash = 2166136261u;

        for (int i = 0; i < path.Length; i++)
        {
            hash ^= path[i];
            hash *= 16777619u;
        }

        return KeyPrefix + hash.ToString("X8");
    }
}
