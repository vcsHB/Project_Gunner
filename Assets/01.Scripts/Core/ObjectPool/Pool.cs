using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PoolType 하나에 대응하는 실제 큐. PoolManager가 소유한다.
/// </summary>
public class Pool
{
    private readonly PoolType _type;
    private readonly PoolableMono _prefab;
    private readonly string _objectName;
    private readonly Transform _root;
    private readonly Stack<PoolableMono> _idle = new();

    /// <summary>지금까지 만들어진 총 개수. (대여 중 + 대기 중)</summary>
    public int TotalCount { get; private set; }
    public int IdleCount => _idle.Count;

    public Pool(PoolItem item, Transform root)
    {
        _type = item.Type;
        _prefab = item.Prefab;
        _objectName = item.ObjectName;
        _root = root;

        for (int i = 0; i < item.PrewarmCount; i++)
            _idle.Push(CreateNew());
    }

    public PoolableMono Get()
    {
        PoolableMono item = _idle.Count > 0 ? _idle.Pop() : CreateNew();

        item.SetInPool(false);
        item.gameObject.SetActive(true);
        item.OnSpawn();

        return item;
    }

    public void Release(PoolableMono item)
    {
        if (item.IsInPool)
        {
            Debug.LogWarning($"[Pool] {_type} 오브젝트가 이미 반납되어 있습니다.", item);
            return;
        }

        item.OnDespawn();
        item.SetInPool(true);
        item.gameObject.SetActive(false);
        item.transform.SetParent(_root, false);

        _idle.Push(item);
    }

    /// <summary>씬 정리용. 대기 중인 것만 실제로 파괴한다.</summary>
    public void Clear()
    {
        while (_idle.Count > 0)
        {
            PoolableMono item = _idle.Pop();
            if (item != null)
                Object.Destroy(item.gameObject);
        }

        TotalCount = 0;
    }

    private PoolableMono CreateNew()
    {
        PoolableMono item = Object.Instantiate(_prefab, _root);
        item.gameObject.name = $"{_objectName}_{TotalCount}";
        item.SetPoolType(_type);
        item.SetInPool(true);
        item.gameObject.SetActive(false);

        TotalCount++;
        return item;
    }
}
