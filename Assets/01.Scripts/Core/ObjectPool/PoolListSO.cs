using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PoolItem
{
    // Id는 생성된 PoolType의 enum 값과 1:1로 대응한다.
    // 한 번 부여되면 바뀌지 않기 때문에, 이름을 고치거나 순서를 바꿔도
    // 이미 저장된 PoolType 필드(프리셋 등)가 깨지지 않는다.
    [SerializeField] private int _id;
    [SerializeField] private string _enumName;
    [SerializeField] private string _objectName;
    [SerializeField] private PoolableMono _prefab;
    [SerializeField, Min(0)] private int _prewarmCount = 8;

    public int Id => _id;
    public string EnumName => _enumName;
    public PoolableMono Prefab => _prefab;
    public int PrewarmCount => _prewarmCount;
    public PoolType Type => (PoolType)_id;

    /// <summary>비워두면 프리팹 이름을 쓴다.</summary>
    public string ObjectName
        => string.IsNullOrEmpty(_objectName) ? (_prefab != null ? _prefab.name : string.Empty) : _objectName;

    public bool IsValid => _id > 0 && _prefab != null && !string.IsNullOrEmpty(_enumName);
}

/// <summary>Pool Manager 창의 탭 하나에 해당한다.</summary>
[Serializable]
public class PoolGroup
{
    [SerializeField] private string _name = "New Group";
    [SerializeField] private List<PoolItem> _items = new();

    public string Name => string.IsNullOrEmpty(_name) ? "Unnamed" : _name;
    public IReadOnlyList<PoolItem> Items => _items;
}

/// <summary>
/// 풀에 등록될 프리팹 전체 목록. Tools > Pool Manager 창에서 편집한다.
/// </summary>
[CreateAssetMenu(fileName = "PoolListSO", menuName = "SO/Core/PoolList")]
public class PoolListSO : ScriptableObject
{
    [SerializeField] private List<PoolGroup> _groups = new();

    public IReadOnlyList<PoolGroup> Groups => _groups;

    public IEnumerable<PoolItem> AllItems
    {
        get
        {
            for (int g = 0; g < _groups.Count; g++)
            {
                IReadOnlyList<PoolItem> items = _groups[g].Items;
                for (int i = 0; i < items.Count; i++)
                    yield return items[i];
            }
        }
    }
}
