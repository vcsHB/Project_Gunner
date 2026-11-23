using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 에셋 북마크 창의 저장소. 탭(그룹)마다 에셋 목록을 들고 EditorPrefs에 JSON으로 넣는다.
///
/// 에셋을 <b>GUID + 로컬 Id</b>로 기억합니다. 경로로 두면 파일을 옮기거나 이름을 바꾸는 순간 끊어집니다.
/// 로컬 Id는 스프라이트 같은 서브에셋용이고, 메인 에셋이면 0입니다.
/// </summary>
public static class AssetBookmarkStore
{
    private const string KeyPrefix = "AssetBookmarks.";
    private const string DefaultTabName = "기본";

    [Serializable]
    public class Entry
    {
        public string Guid;
        public long LocalId;

        public bool Matches(Entry other) => other != null && Guid == other.Guid && LocalId == other.LocalId;
    }

    [Serializable]
    public class Tab
    {
        public string Name;
        public List<Entry> Entries = new();
    }

    [Serializable]
    private class SaveData
    {
        public List<Tab> Tabs = new();
        public int Selected;
    }

    private static SaveData _data;
    private static string _key;

    public static IReadOnlyList<Tab> Tabs
    {
        get
        {
            EnsureLoaded();
            return _data.Tabs;
        }
    }

    public static int SelectedIndex
    {
        get
        {
            EnsureLoaded();
            return Mathf.Clamp(_data.Selected, 0, _data.Tabs.Count - 1);
        }
        set
        {
            EnsureLoaded();

            int clamped = Mathf.Clamp(value, 0, _data.Tabs.Count - 1);
            if (clamped == _data.Selected) return;

            _data.Selected = clamped;
            Save();
        }
    }

    /// <summary>탭은 항상 하나 이상이므로 null이 아니다.</summary>
    public static Tab SelectedTab => Tabs[SelectedIndex];

    public static Tab AddTab(string name)
    {
        EnsureLoaded();

        Tab tab = new() { Name = string.IsNullOrWhiteSpace(name) ? MakeUniqueName("새 탭") : name.Trim() };
        _data.Tabs.Add(tab);
        _data.Selected = _data.Tabs.Count - 1;

        Save();
        return tab;
    }

    public static void RenameTab(Tab tab, string name)
    {
        if (tab == null || string.IsNullOrWhiteSpace(name)) return;

        tab.Name = name.Trim();
        Save();
    }

    /// <summary>마지막 하나를 지우면 빈 기본 탭을 새로 만든다. 탭이 없는 상태를 만들지 않는다.</summary>
    public static void RemoveTab(Tab tab)
    {
        EnsureLoaded();

        int index = _data.Tabs.IndexOf(tab);
        if (index < 0) return;

        _data.Tabs.RemoveAt(index);

        if (_data.Tabs.Count == 0)
            _data.Tabs.Add(new Tab { Name = DefaultTabName });

        _data.Selected = Mathf.Clamp(index - 1, 0, _data.Tabs.Count - 1);
        Save();
    }

    /// <summary>프로젝트 에셋만 받는다. 씬 오브젝트·중복은 건너뛰고, 실제로 넣은 개수를 반환한다.</summary>
    public static int AddAssets(Tab tab, IList<UnityEngine.Object> objects)
    {
        if (tab == null || objects == null) return 0;

        int added = 0;

        for (int i = 0; i < objects.Count; i++)
        {
            if (!TryCreateEntry(objects[i], out Entry entry)) continue;
            if (Contains(tab, entry)) continue;

            tab.Entries.Add(entry);
            added++;
        }

        if (added > 0) Save();
        return added;
    }

    public static bool Contains(Tab tab, Entry entry)
    {
        for (int i = 0; i < tab.Entries.Count; i++)
        {
            if (tab.Entries[i].Matches(entry)) return true;
        }

        return false;
    }

    /// <summary>
    /// 지워진 에셋을 걷어낸다. 지운 개수를 반환한다.
    /// 자동으로 하지 않는 이유 — 브랜치를 잠깐 옮긴 사이에 에셋이 안 보이면 돌아왔을 때 목록이 비어 있게 됩니다.
    /// </summary>
    public static int PruneMissing(Tab tab)
    {
        if (tab == null) return 0;

        int removed = tab.Entries.RemoveAll(e => Resolve(e) == null);
        if (removed > 0) Save();
        return removed;
    }

    public static bool TryCreateEntry(UnityEngine.Object obj, out Entry entry)
    {
        entry = null;

        if (obj == null || !AssetDatabase.Contains(obj)) return false;
        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out string guid, out long localId)) return false;

        entry = new Entry
        {
            Guid = guid,
            LocalId = AssetDatabase.IsMainAsset(obj) ? 0 : localId,
        };

        return true;
    }

    /// <summary>에셋이 없으면 null. 창은 매 프레임 부르지 말고 캐시해서 쓸 것.</summary>
    public static UnityEngine.Object Resolve(Entry entry)
    {
        if (entry == null) return null;

        string path = AssetDatabase.GUIDToAssetPath(entry.Guid);
        if (string.IsNullOrEmpty(path)) return null;

        if (entry.LocalId == 0) return AssetDatabase.LoadMainAssetAtPath(path);

        UnityEngine.Object[] all = AssetDatabase.LoadAllAssetsAtPath(path);

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null) continue;
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(all[i], out _, out long localId)) continue;
            if (localId == entry.LocalId) return all[i];
        }

        return null;
    }

    /// <summary>창이 목록을 직접 고친 뒤(순서 변경, 삭제) 부른다.</summary>
    public static void Save()
    {
        EnsureLoaded();
        EditorPrefs.SetString(Key, JsonUtility.ToJson(_data));
    }

    private static string MakeUniqueName(string baseName)
    {
        string name = baseName;
        int n = 2;

        while (_data.Tabs.Exists(t => t.Name == name))
            name = $"{baseName} {n++}";

        return name;
    }

    private static void EnsureLoaded()
    {
        if (_data != null) return;

        string json = EditorPrefs.GetString(Key, string.Empty);
        _data = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<SaveData>(json);

        _data ??= new SaveData();
        _data.Tabs ??= new List<Tab>();

        foreach (Tab tab in _data.Tabs)
            tab.Entries ??= new List<Entry>();

        if (_data.Tabs.Count == 0)
            _data.Tabs.Add(new Tab { Name = DefaultTabName });
    }

    private static string Key => _key ??= BuildKey();

    /// <summary>
    /// EditorPrefs는 유니티 전체가 공유하므로 프로젝트 Id(PlayerSettings.productGUID)로 키를 나눈다.
    /// 경로가 아니라 Id라서 다른 프로젝트가 같은 경로에 있어도 섞이지 않고, 폴더를 옮겨도 따라온다.
    /// Id가 비어 있는 예외 상황에서만 데이터 경로 해시(FNV-1a)로 물러선다.
    /// </summary>
    private static string BuildKey()
    {
        Guid id = PlayerSettings.productGUID;
        if (id != Guid.Empty) return KeyPrefix + id.ToString("N");

        string path = Application.dataPath;
        uint hash = 2166136261u;

        for (int i = 0; i < path.Length; i++)
        {
            hash ^= path[i];
            hash *= 16777619u;
        }

        return KeyPrefix + "path_" + hash.ToString("X8");
    }
}
