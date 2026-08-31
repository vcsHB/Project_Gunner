using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전리품 테이블 한 줄. "무엇이, 얼마나, 얼마의 확률로".
/// </summary>
[System.Serializable]
public struct LootEntry
{
    public ItemDataSO item;

    [Tooltip("떨어질 확률. 1이면 반드시 떨어진다.")]
    [Range(0f, 1f)] public float chance;

    [Tooltip("떨어질 개수 범위. 최대값을 포함한다.")]
    [Min(1)] public int minCount;
    [Min(1)] public int maxCount;

    public bool IsValid => item != null && chance > 0f;

    /// <summary>인스펙터에서 min/max를 뒤집어 넣어도 동작하게 한다.</summary>
    public int MinCount => Mathf.Min(minCount, maxCount);
    public int MaxCount => Mathf.Max(minCount, maxCount);
}

/// <summary>
/// 유닛 하나의 데이터. 몬스터·NPC가 여기서 나온다.
///
/// 유닛도 <see cref="ItemDataSO"/> 파생이다. Id 하나로 식별되어야
/// 세이브·네트워크·로그에서 아이템과 같은 방식으로 다뤄진다.
/// (Id 대역 9000~9999, <see cref="ItemCategoryType.Unit"/>)
///
/// 스탯은 여기에 <b>기본값</b>으로만 적힌다. 버프·상태이상은 런타임에
/// <see cref="Status{T}"/>의 modifier로 얹히므로 이 값을 건드리지 않는다.
/// </summary>
[CreateAssetMenu(menuName = "SO/Item/UnitData")]
public class UnitDataSO : ItemDataSO
{
    [Header("Unit")]
    [Tooltip("스폰할 프리팹. 스포너가 이걸 찍어낸다.")]
    [SerializeField] private Agent _prefab;

    [Header("Stats")]
    [SerializeField, Min(1f)] private float _maxHealth = 30f;
    [SerializeField, Min(0f)] private float _moveSpeed = 3f;

    [Tooltip("공격 한 번의 위력. 실제 판정은 캐스터가 한다.")]
    [SerializeField, Min(0f)] private float _damage = 5f;

    [SerializeField, Min(0f)] private float _defense;

    [Header("전리품")]
    [Tooltip("죽을 때 떨구는 것. 줄마다 따로 굴린다 — 여러 줄이 동시에 나올 수 있다.")]
    [SerializeField] private List<LootEntry> _loot = new();

    public Agent Prefab => _prefab;
    public float MaxHealth => _maxHealth;
    public float MoveSpeed => _moveSpeed;
    public float Damage => _damage;
    public float Defense => _defense;

    public IReadOnlyList<LootEntry> Loot => _loot;

    /// <summary>
    /// 기본 스탯을 유닛에 적어 넣는다. 프리팹 인스펙터에 적힌 값을 덮어쓴다.
    ///
    /// modifier가 아니라 기본값을 바꾸는 것이므로, 이미 걸려 있는 버프는 그대로 살아남는다.
    /// </summary>
    public void ApplyTo(AgentStatus status)
    {
        if (status == null) return;

        status.Health.SetDefaultValue(_maxHealth);
        status.MoveSpeed.SetDefaultValue(_moveSpeed);
        status.Damage.SetDefaultValue(_damage);
        status.Defense.SetDefaultValue(_defense);
    }

    /// <summary>
    /// 전리품을 굴려 results에 담는다. 호출 전에 비워야 한다.
    ///
    /// <see cref="GameRandom.World"/>를 쓴다. 전리품은 월드를 만드는 판정이라
    /// 전투 판정(치명타·산탄)과 소스를 섞으면 호출 순서가 깨져 재현이 안 된다.
    /// </summary>
    public void RollLoot(List<ItemStack> results)
    {
        if (results == null) return;

        for (int i = 0; i < _loot.Count; i++)
        {
            LootEntry entry = _loot[i];
            if (!entry.IsValid) continue;

            // 확률은 항상 굴린다. 100%라고 건너뛰면 호출 횟수가 달라져 이후 굴림이 전부 어긋난다.
            if (GameRandom.World.Value >= entry.chance) continue;

            if (!entry.item.IsRegistered)
            {
                Debug.LogWarning(
                    $"[UnitData] {EditorName}의 전리품 '{entry.item.EditorName}'이 아직 Id를 받지 못했습니다.", this);
                continue;
            }

            int count = GameRandom.World.Range(entry.MinCount, entry.MaxCount + 1);
            if (count <= 0) continue;

            AddLoot(results, entry.item, count);
        }
    }

    /// <summary>
    /// 개체 상태를 갖는 아이템(내구도 있는 무기 등)은 겹칠 수 없으므로 하나씩 따로 만든다.
    /// </summary>
    private static void AddLoot(List<ItemStack> results, ItemDataSO item, int count)
    {
        if (!item.RequiresInstance)
        {
            results.Add(ItemInstanceRegistry.CreateStack(item, count));
            return;
        }

        for (int i = 0; i < count; i++)
            results.Add(ItemInstanceRegistry.CreateStack(item, 1));
    }
}
