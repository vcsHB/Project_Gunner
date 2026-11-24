using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적이 부서지면 그 잔해에 <b>적재함</b>(<see cref="LootContainer"/>)을 단다. 적 유닛 루트(<see cref="PartBreakFeedback"/>와 같은 오브젝트)에 붙인다.
///
/// 무엇이 들어가나 (2026-11-25 확정)
/// <list type="bullet">
/// <item><b>코어의 재료 중 일부</b> — 달고 있던 코어의 <see cref="ItemDataSO.Composition"/>에서 재료마다 일정 비율. 코어가 없는 단일 기체(터렛)는 유닛 데이터의 재료 구성을 쓴다.</item>
/// <item><b>고철</b> 몇 개 — 분해하면 재료가 나온다.</item>
/// <item>유닛 데이터의 전리품 표(<see cref="UnitDataSO.RollLoot"/>) — 바닥에 떨구던 것을 잔해 안으로 옮겼다.</item>
/// </list>
/// 달고 있던 파츠·무기가 통째로 나오지는 않는다 — 재료로 돌아온다.
///
/// 굴림은 <see cref="GameRandom.World"/>다(전리품은 월드를 만드는 판정).
/// 잔해의 모양은 <see cref="PartBreakFeedback"/>가 만든다. 여기는 그 오브젝트에 적재함을 얹고, 비지 않은 동안 사라지지 않게 붙잡는다.
/// </summary>
public class WreckLoot : MonoBehaviour
{
    [Tooltip("코어 재료 하나하나에서 남는 비율(최소~최대). 0.5면 강철 6개짜리 코어에서 3개쯤.")]
    [SerializeField] private Vector2 _compositionShare = new(0.25f, 0.6f);

    [SerializeField] private ItemDataSO _scrap;
    [SerializeField] private Vector2Int _scrapCount = new(1, 3);

    private Agent _owner;
    private PartBreakFeedback _visual;

    private static readonly List<ItemStack> s_buffer = new();

    public bool HandlesLoot => isActiveAndEnabled;

    private void Start()
    {
        _owner = GetComponentInParent<Agent>();
        _visual = GetComponent<PartBreakFeedback>();

        if (_visual != null)
            _visual.OnWreckSpawnedEvent += HandleWreckSpawned;
        else
            Debug.LogWarning($"[WreckLoot] {name}에 PartBreakFeedback이 없어 잔해를 만들 수 없습니다.", this);
    }

    private void OnDestroy()
    {
        if (_visual != null)
            _visual.OnWreckSpawnedEvent -= HandleWreckSpawned;
    }

    private void HandleWreckSpawned(GameObject wreck)
    {
        s_buffer.Clear();
        Roll(s_buffer);

        if (s_buffer.Count == 0) return; // 빈 잔해는 원래대로 흐려져 사라진다

        PartDebris debris = wreck.GetComponent<PartDebris>();
        if (debris != null) debris.MakePersistent();

        LootContainer container = wreck.AddComponent<LootContainer>();
        container.Fill(s_buffer);

        s_buffer.Clear();
    }

    private void Roll(List<ItemStack> results)
    {
        ItemDataSO source = FindCompositionSource();
        if (source != null)
        {
            IReadOnlyList<ItemAmount> composition = source.Composition;
            for (int i = 0; i < composition.Count; i++)
            {
                ItemAmount part = composition[i];
                if (!part.IsValid || !part.item.IsRegistered) continue;

                float share = GameRandom.World.Range(_compositionShare.x, _compositionShare.y);
                int count = Mathf.RoundToInt(part.count * share);
                if (count > 0) results.Add(new ItemStack(part.item.Id, count));
            }
        }

        if (_scrap != null && _scrap.IsRegistered)
        {
            int scrap = GameRandom.World.Range(Mathf.Min(_scrapCount.x, _scrapCount.y), Mathf.Max(_scrapCount.x, _scrapCount.y) + 1);
            if (scrap > 0) results.Add(new ItemStack(_scrap.Id, scrap));
        }

        if (_owner is Enemy enemy && enemy.Data != null)
            enemy.Data.RollLoot(results);
    }

    /// <summary>달고 있던 코어. 없으면(단일 기체) 유닛 데이터.</summary>
    private ItemDataSO FindCompositionSource()
    {
        EquipmentController equipment = _owner != null ? _owner.GetCompo<EquipmentController>() : null;
        ItemDataSO core = equipment != null ? equipment.Get(EquipSlotType.Core) : null;
        if (core != null && core.Composition.Count > 0) return core;

        return _owner is Enemy enemy ? enemy.Data : null;
    }
}
