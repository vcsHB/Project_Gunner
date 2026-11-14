using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 출격할 때 무엇을 달고, 무엇을 싣고 들어가는가.
///
/// 아이템이 아니라 <b>구성표</b>라서 ItemDataSO 계보에 넣지 않는다 — Id도 인벤토리 칸도 필요 없다.
///
/// <b>적재도 여기 들어간다.</b> "들고 들어간 것만 쓴다"가 기획이라 탄약과 보급품도
/// 출격 구성의 일부다. 따로 두면 "무장은 로드아웃, 탄약은 다른 데"가 되어 한 판을
/// 세팅하는 데 두 곳을 봐야 한다.
///
/// 지금은 디버그용 기본값으로만 쓰인다. 격납고가 생기면 거기서 만든 구성이
/// <see cref="MechaPartController"/>를 통해 이걸 덮는다.
/// </summary>
[CreateAssetMenu(menuName = "SO/Mecha/Loadout")]
public class MechaLoadoutSO : ScriptableObject
{
    /// <summary>슬롯에 다는 것.</summary>
    [Serializable]
    public struct Entry
    {
        public EquipSlotType slot;

        [Tooltip("비워두면 그 슬롯은 빈 채로 시작한다.")]
        public EquipableItemDataSO item;

        public bool IsValid => slot != EquipSlotType.None && item != null;
    }

    /// <summary>적재함에 싣는 것. 탄약·보급품.</summary>
    [Serializable]
    public struct Cargo
    {
        public ItemDataSO item;

        [Min(1)] public int count;

        public bool IsValid => item != null && count > 0;
    }

    [Tooltip("기체 슬롯에 다는 것. 코어 / 좌팔 / 우팔 / 추진체.")]
    [SerializeField] private List<Entry> _entries = new();

    [Tooltip("적재함에 싣고 들어가는 것. 칸이 모자라면 들어간 만큼만 실린다.")]
    [SerializeField] private List<Cargo> _cargo = new();

    public IReadOnlyList<Entry> Entries => _entries;
    public IReadOnlyList<Cargo> CargoItems => _cargo;
}
