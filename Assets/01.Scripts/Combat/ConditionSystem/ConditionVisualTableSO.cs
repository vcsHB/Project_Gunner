using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상태이상을 화면에 어떻게 보여줄지. 로직과 표현을 섞지 않기 위해 따로 둔다.
/// </summary>
[CreateAssetMenu(fileName = "ConditionVisualTable", menuName = "SO/Combat/ConditionVisualTable")]
public class ConditionVisualTableSO : ScriptableObject
{
#pragma warning disable CS0649 // 인스펙터에서만 채워진다
    [Serializable]
    private struct Entry
    {
        public ConditionType type;
        public Sprite icon;
        public string displayName;
        public Color tint;
    }
#pragma warning restore CS0649

    [SerializeField] private List<Entry> _entries = new();

    private Dictionary<ConditionType, Entry> _map;

    private void OnEnable() => _map = null;

    private Dictionary<ConditionType, Entry> Map
    {
        get
        {
            if (_map != null) return _map;

            _map = new Dictionary<ConditionType, Entry>(_entries.Count);
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].type == ConditionType.None) continue;

                if (!_map.TryAdd(_entries[i].type, _entries[i]))
                    Debug.LogError($"[ConditionVisual] {_entries[i].type}이 중복 등록되었습니다.", this);
            }

            return _map;
        }
    }

    public Sprite GetIcon(ConditionType type) => Map.TryGetValue(type, out Entry entry) ? entry.icon : null;

    public string GetDisplayName(ConditionType type)
        => Map.TryGetValue(type, out Entry entry) && !string.IsNullOrEmpty(entry.displayName)
            ? entry.displayName
            : type.ToString();

    public Color GetTint(ConditionType type)
        => Map.TryGetValue(type, out Entry entry) && entry.tint.a > 0f ? entry.tint : Color.white;
}
