using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 출격할 때 무엇을 달고 들어가는가. 슬롯 하나당 아이템 하나다.
///
/// 아이템이 아니라 <b>구성표</b>라서 ItemDataSO 계보에 넣지 않는다 — Id도 인벤토리 칸도 필요 없다.
///
/// 지금은 디버그용 기본값으로만 쓰인다. 격납고가 생기면 거기서 만든 구성이
/// <see cref="MechaLoadoutApplier"/>를 통해 이걸 덮는다.
/// </summary>
[CreateAssetMenu(menuName = "SO/Mecha/Loadout")]
public class MechaLoadoutSO : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public EquipSlotType slot;

        [Tooltip("비워두면 그 슬롯은 빈 채로 시작한다.")]
        public EquipableItemDataSO item;

        public bool IsValid => slot != EquipSlotType.None && item != null;
    }

    [SerializeField] private List<Entry> _entries = new();

    public IReadOnlyList<Entry> Entries => _entries;
}
