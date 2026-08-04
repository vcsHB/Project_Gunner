using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 모든 ItemDataSO의 레지스트리. Id로 실제 데이터를 찾는 유일한 경로다.
/// 도감은 All을 순회하면 되고, 아이템이 몇 개로 늘어나도 코드는 그대로다.
/// Resources 폴더에 "ItemDatabase" 이름으로 하나만 둔다.
/// </summary>
[CreateAssetMenu(fileName = ResourceName, menuName = "SO/Item/ItemDatabase")]
public class ItemDatabaseSO : ScriptableObject
{
    public const string ResourceName = "ItemDatabase";

    // 삭제된 아이템의 Id를 재사용하지 않기 위해 따로 들고 간다.
    // (재사용하면 옛 세이브가 엉뚱한 아이템을 가리키게 된다)
    [SerializeField] private uint _nextId = 1;
    [SerializeField] private List<ItemDataSO> _items = new();

    private Dictionary<uint, ItemDataSO> _byId;

    public IReadOnlyList<ItemDataSO> All => _items;
    public uint NextId => _nextId;

    #region Static Access

    private static ItemDatabaseSO s_instance;
    private static bool s_loadFailed;

    public static ItemDatabaseSO Instance
    {
        get
        {
            if (s_instance != null) return s_instance;
            if (s_loadFailed) return null;

            s_instance = Resources.Load<ItemDatabaseSO>(ResourceName);
            if (s_instance == null)
            {
                s_loadFailed = true;
                Debug.LogError($"[ItemDatabase] Resources/{ResourceName}.asset 이 없습니다. " +
                               "SO/Item/ItemDatabase로 만들어 Resources 폴더에 두세요.");
            }

            return s_instance;
        }
    }

    #endregion

    private void OnEnable() => _byId = null;

    private Dictionary<uint, ItemDataSO> Map
    {
        get
        {
            if (_byId != null) return _byId;

            _byId = new Dictionary<uint, ItemDataSO>(_items.Count);
            for (int i = 0; i < _items.Count; i++)
            {
                ItemDataSO item = _items[i];
                if (item == null || !item.IsRegistered) continue;

                if (!_byId.TryAdd(item.Id, item))
                    Debug.LogError($"[ItemDatabase] Id {item.Id}가 중복입니다. ({item.name})", this);
            }

            return _byId;
        }
    }

    public bool TryGet(uint id, out ItemDataSO item) => Map.TryGetValue(id, out item);

    public ItemDataSO Get(uint id)
    {
        if (Map.TryGetValue(id, out ItemDataSO item))
            return item;

        Debug.LogError($"[ItemDatabase] Id {id}에 해당하는 아이템이 없습니다.", this);
        return null;
    }

    /// <summary>타입까지 맞는지 확인하며 가져온다. 맞지 않으면 null.</summary>
    public T Get<T>(uint id) where T : ItemDataSO => Get(id) as T;

    /// <summary>도감/인벤토리 필터용. results를 비우고 채운다.</summary>
    public void GetByCategory(ItemCategoryType category, List<ItemDataSO> results)
    {
        results.Clear();

        for (int i = 0; i < _items.Count; i++)
        {
            if (_items[i] == null || _items[i].ItemCategory != category) continue;

            results.Add(_items[i]);
        }
    }
}
