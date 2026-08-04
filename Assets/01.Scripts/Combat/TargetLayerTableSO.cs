using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// TargetType <-> 물리 레이어 대응표. 이 대응이 정의되는 유일한 곳이다.
/// Resources 폴더에 "TargetLayerTable" 이름으로 하나만 두고 전역으로 쓴다.
/// (캐스터/타겟마다 참조를 물리면 하나만 빠뜨려도 조용히 안 맞기 때문)
/// </summary>
[CreateAssetMenu(fileName = ResourceName, menuName = "SO/Combat/TargetLayerTable")]
public class TargetLayerTableSO : ScriptableObject
{
    public const string ResourceName = "TargetLayerTable";

    // 인스펙터에서만 채워지는 필드라 "할당된 적 없음" 경고가 뜬다.
#pragma warning disable CS0649
    [Serializable]
    private struct Entry
    {
        public TargetType type;
        [Layer] public int layer;
    }
#pragma warning restore CS0649

    [SerializeField] private List<Entry> _entries = new();

    private Dictionary<TargetType, int> _layerByType;

    #region Static Access

    private static TargetLayerTableSO s_instance;
    private static bool s_loadFailed;

    public static TargetLayerTableSO Instance
    {
        get
        {
            if (s_instance != null) return s_instance;
            if (s_loadFailed) return null;

            s_instance = Resources.Load<TargetLayerTableSO>(ResourceName);
            if (s_instance == null)
            {
                s_loadFailed = true;
                Debug.LogError($"[TargetLayerTable] Resources/{ResourceName}.asset 이 없습니다. " +
                               "SO/Combat/TargetLayerTable로 만들어 Resources 폴더에 두세요.");
            }

            return s_instance;
        }
    }

    /// <summary>대응 레이어가 없으면 -1.</summary>
    public static int LayerOf(TargetType type)
        => Instance != null ? Instance.GetLayer(type) : -1;

    /// <summary>표가 없으면 전체 레이어를 반환한다. (필터가 조용히 아무것도 안 맞히는 것보다 낫다)</summary>
    public static LayerMask MaskOf(TargetType types)
        => Instance != null ? Instance.GetMask(types) : ~0;

    #endregion

    private void OnEnable() => _layerByType = null;

    private Dictionary<TargetType, int> Map
    {
        get
        {
            if (_layerByType != null) return _layerByType;

            _layerByType = new Dictionary<TargetType, int>();
            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                if (entry.type == TargetType.None) continue;

                if (!_layerByType.TryAdd(entry.type, entry.layer))
                    Debug.LogError($"[TargetLayerTable] {entry.type}이 중복 등록되었습니다.", this);
            }

            return _layerByType;
        }
    }

    public int GetLayer(TargetType type)
    {
        if (type == TargetType.None) return -1;

        if (Map.TryGetValue(type, out int layer))
            return layer;

        Debug.LogError($"[TargetLayerTable] {type}에 대응하는 레이어가 없습니다.", this);
        return -1;
    }

    /// <summary>플래그 조합을 합쳐 LayerMask로 만든다.</summary>
    public LayerMask GetMask(TargetType types)
    {
        int mask = 0;

        foreach (KeyValuePair<TargetType, int> pair in Map)
        {
            if ((types & pair.Key) == 0) continue;

            mask |= 1 << pair.Value;
        }

        return mask;
    }
}
