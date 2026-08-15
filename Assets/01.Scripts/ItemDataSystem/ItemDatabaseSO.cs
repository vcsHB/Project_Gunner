using System;
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

    [Tooltip("타입별 Id 대역. 번호만 보고 종류를 알 수 있게 나눈다.")]
    [SerializeField] private List<ItemIdRange> _idRanges = new();

    // 대역이 지정되지 않은 타입이 쓰는 커서. 대역 표가 비어있던 시절의 경로다.
    // 삭제된 아이템의 Id를 재사용하지 않기 위해 따로 들고 간다.
    // (재사용하면 옛 세이브가 엉뚱한 아이템을 가리키게 된다)
    [SerializeField] private uint _nextId = 1;
    [SerializeField] private List<ItemDataSO> _items = new();

    private Dictionary<uint, ItemDataSO> _byId;

    public IReadOnlyList<ItemDataSO> All => _items;
    public IReadOnlyList<ItemIdRange> IdRanges => _idRanges;
    public uint NextId => _nextId;

    /// <summary>
    /// 새 프로젝트나 대역 표가 비었을 때 채워 넣을 기본값.
    /// 1000 단위라 앞자리만 봐도 종류가 읽힌다. 인스펙터에서 언제든 고칠 수 있다.
    /// </summary>
    public static readonly ItemIdRange[] DefaultRanges =
    {
        // 모든 타입의 조상이라 대역을 못 찾은 아이템이 여기로 떨어진다.
        new(nameof(ItemDataSO), "기타 / 미분류", 1, 999),

        new(nameof(PlayerWeaponDataSO), "무기", 1000, 1999),
        new(nameof(WeaponPartDataSO), "무기 파츠", 2000, 2999),
        new(nameof(AmmoDataSO), "탄약", 3000, 3999),

        // 아직 타입이 없는 예약 대역. 클래스를 만들면 typeName만 채우면 된다.
        new(string.Empty, "소모품", 4000, 4999),
        new(string.Empty, "도구", 5000, 5999),
        new(string.Empty, "방어구", 6000, 6999),
        new(string.Empty, "자원", 7000, 7999),
        new(string.Empty, "블럭", 8000, 8999),
        new(string.Empty, "유닛", 9000, 9999),
    };

    #region Static Access

    private static ItemDatabaseSO s_instance;
    private static bool s_loadFailed;

    public static ItemDatabaseSO Instance
    {
        get
        {
            if (s_instance != null) return s_instance;
            if (s_loadFailed) return null;

            s_instance = ResourceLocator.LoadSingle<ItemDatabaseSO>(ResourceName);
            if (s_instance == null)
            {
                s_loadFailed = true;
                Debug.LogError($"[ItemDatabase] Resources 안에 {ResourceName} 에셋이 없습니다. " +
                               "SO/Item/ItemDatabase로 만들어 Resources 폴더(하위 폴더 가능)에 두세요.");
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

    #region Id 대역

    /// <summary>
    /// 이 타입이 쓸 대역의 인덱스. 없으면 -1.
    ///
    /// 자기 타입에 설정이 없으면 부모로 거슬러 올라간다. ItemDataSO에 대역이 있으면
    /// 결국 거기서 잡히므로, 새 파생 타입을 만들어도 Id를 못 받는 일은 없다.
    /// </summary>
    public int FindRangeIndex(Type type)
    {
        if (type == null) return -1;

        for (Type current = type;
             current != null && typeof(ItemDataSO).IsAssignableFrom(current);
             current = current.BaseType)
        {
            for (int i = 0; i < _idRanges.Count; i++)
            {
                // 예약 대역(타입 미지정)은 아무것도 받지 않는다. 빈 문자열끼리 맞아버리면 안 된다.
                if (_idRanges[i].IsReservation) continue;

                if (_idRanges[i].typeName == current.Name)
                    return i;
            }
        }

        return -1;
    }

    public bool TryGetRange(Type type, out ItemIdRange range)
    {
        int index = FindRangeIndex(type);
        if (index < 0)
        {
            range = default;
            return false;
        }

        range = _idRanges[index];
        return true;
    }

    /// <summary>이 Id가 들어있는 대역의 이름. 로그와 디버그 표시용. 없으면 빈 문자열.</summary>
    public string GetRangeLabel(uint id)
    {
        for (int i = 0; i < _idRanges.Count; i++)
        {
            if (_idRanges[i].Contains(id))
                return _idRanges[i].DisplayName;
        }

        return string.Empty;
    }

    #endregion

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
