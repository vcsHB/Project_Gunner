using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 기체를 구성하는 파츠. 코어·추진체가 여기서 파생된다.
/// 팔에 다는 무기는 <see cref="PlayerWeaponDataSO"/>라 이 계보에 속하지 않는다 —
/// 무기는 자기 스탯 체계(WeaponStatus)가 따로 있다.
///
/// 스탯은 여기(데이터)에, 비주얼과 소켓은 프리팹에 둔다.
/// <b>프리팹이 없어도 스탯은 적용되어야 한다.</b> 아트가 안 나온 파츠로도 밸런스를 봐야 한다.
/// </summary>
public abstract class PartDataSO : EquipableItemDataSO
{
    [Header("Part Stats")]
    [Tooltip("장착하면 기체 스탯에 붙는 변화. 해제하면 그대로 걷힌다.")]
    [SerializeField] private List<AgentStatDelta> _deltas = new();

    public IReadOnlyList<AgentStatDelta> Deltas => _deltas;
}
