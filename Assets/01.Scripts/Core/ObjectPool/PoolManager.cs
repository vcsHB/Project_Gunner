using System;
using System.Collections.Generic;
using UnityEngine;

public class PoolManager : Singleton<PoolManager>
{
    [Header("등록된 프리팹 전체 목록")]
    [SerializeField] private PoolListSO _poolList;

    [Header("이 씬/세션에서 미리 만들어 둘 것들")]
    [SerializeField] private List<PoolPresetSO> _presets = new();

    [Tooltip("프리셋에 없는 PoolType을 Get하면 그때 풀을 만든다. 끄면 에러만 내고 null을 반환한다.")]
    [SerializeField] private bool _createOnDemand = true;

    // PoolType -> 등록 정보 (프리셋에 없어도 카탈로그에는 다 들어있다)
    private readonly Dictionary<PoolType, PoolItem> _catalog = new();
    private readonly Dictionary<PoolType, Pool> _pools = new();

    // Get<T>()용. 프리팹 컴포넌트 타입 -> PoolType
    private readonly Dictionary<Type, PoolType> _typeToPoolType = new();

    protected override void Awake()
    {
        base.Awake();

        // 중복 싱글턴이면 base.Awake에서 파괴 예약된 상태다.
        if (Instance != this) return;

        BuildCatalog();
        PreparePresets();
    }

    private void BuildCatalog()
    {
        if (_poolList == null)
        {
            Debug.LogError("[PoolManager] PoolListSO가 비어있습니다.", this);
            return;
        }

        foreach (PoolItem item in _poolList.AllItems)
        {
            if (!item.IsValid)
            {
                Debug.LogError($"[PoolManager] 잘못된 풀 항목입니다. (id={item.Id}, enum={item.EnumName}, prefab={item.Prefab})", _poolList);
                continue;
            }

            PoolType type = item.Type;

            // Id와 생성된 enum이 어긋났다는 건 Generate Enum을 안 눌렀다는 뜻이다.
            if (!Enum.IsDefined(typeof(PoolType), type) || type.ToString() != item.EnumName)
            {
                Debug.LogError(
                    $"[PoolManager] '{item.EnumName}'이 PoolType에 반영되지 않았습니다. " +
                    "Tools > Pool Manager에서 Generate Enum을 실행하세요.", _poolList);
                continue;
            }

            if (!_catalog.TryAdd(type, item))
            {
                Debug.LogError($"[PoolManager] {type}이 중복 등록되었습니다.", _poolList);
                continue;
            }

            // 같은 컴포넌트 타입이 여러 PoolType에 물리면 Get<T>()로는 구분할 수 없다.
            Type prefabType = item.Prefab.GetType();
            if (!_typeToPoolType.TryAdd(prefabType, type))
            {
                Debug.LogWarning(
                    $"[PoolManager] {prefabType.Name}이 이미 {_typeToPoolType[prefabType]}에 등록되어 있어 " +
                    $"{type}은 Get<{prefabType.Name}>()로 꺼낼 수 없습니다. Get(PoolType)을 쓰세요.", _poolList);
            }
        }
    }

    private void PreparePresets()
    {
        for (int p = 0; p < _presets.Count; p++)
        {
            PoolPresetSO preset = _presets[p];
            if (preset == null) continue;

            IReadOnlyList<PoolType> types = preset.Types;
            for (int i = 0; i < types.Count; i++)
            {
                if (types[i] == PoolType.None) continue;

                CreatePool(types[i], preset);
            }
        }
    }

    private Pool CreatePool(PoolType type, UnityEngine.Object context)
    {
        if (_pools.TryGetValue(type, out Pool exist)) return exist;

        if (!_catalog.TryGetValue(type, out PoolItem item))
        {
            Debug.LogError($"[PoolManager] {type}이 PoolListSO에 등록되어 있지 않습니다.", context);
            return null;
        }

        Transform root = new GameObject($"Pool_{type}").transform;
        root.SetParent(transform, false);

        Pool pool = new Pool(item, root);
        _pools.Add(type, pool);

        return pool;
    }

    public PoolableMono Get(PoolType type)
    {
        if (_pools.TryGetValue(type, out Pool pool))
            return pool.Get();

        if (!_createOnDemand)
        {
            Debug.LogError($"[PoolManager] {type}이 이 씬의 프리셋에 등록되어 있지 않습니다.", this);
            return null;
        }

        Debug.LogWarning($"[PoolManager] {type}이 프리셋에 없어 실행 중에 생성합니다. 프리셋에 추가하는 것이 좋습니다.", this);

        pool = CreatePool(type, this);
        return pool?.Get();
    }

    public T Get<T>() where T : PoolableMono
    {
        if (!_typeToPoolType.TryGetValue(typeof(T), out PoolType type))
        {
            Debug.LogError($"[PoolManager] {typeof(T).Name} 타입으로 등록된 풀이 없습니다.");
            return null;
        }

        return Get(type) as T;
    }

    public void Release(PoolableMono item)
    {
        if (item == null) return;

        if (_pools.TryGetValue(item.PoolType, out Pool pool))
        {
            pool.Release(item);
            return;
        }

        Debug.LogError($"[PoolManager] {item.name}의 풀({item.PoolType})을 찾을 수 없어 파괴합니다.", item);
        Destroy(item.gameObject);
    }

    protected override void OnDestroy()
    {
        foreach (Pool pool in _pools.Values)
            pool.Clear();

        _pools.Clear();
        _catalog.Clear();
        _typeToPoolType.Clear();

        base.OnDestroy();
    }
}
