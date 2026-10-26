using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이 모드에 들어갈 때 설정된 파츠로 기체를 채운다.
///
/// 핫바가 없어지면 <see cref="EquipmentController"/>가 비어있는 한 무장이 아예 생기지 않는다.
/// 누군가는 채워야 하고, 그게 여기다.
///
/// <b>여기가 "새 판 시작" 지점이다.</b> 월드 상태를 되돌릴 유일한 자리이기도 해서
/// ItemInstanceRegistry.Clear와 GameRandom.Reseed를 같이 부른다.
/// 안 부르면 플레이 모드를 들락거릴 때마다 이전 판의 인스턴스가 쌓인다(정적이라 남는다).
/// </summary>
public class MechaLoadoutApplier : MonoBehaviour, IAgentComponent
{
    /// <summary>
    /// 격납고가 정한 구성. 있으면 에셋 기본값보다 우선한다.
    ///
    /// 정적인 것은 <b>임시다.</b> 로컬 1인 기준이라 지금은 문제가 없지만,
    /// 플레이어별 상태를 전역에 두면 서버 권한 구조로 못 옮긴다. (RULES.md 멀티플레이 대비)
    /// 플레이어 스폰 흐름이 생기면 스폰하는 쪽이 SetLoadout으로 넘기도록 바꿀 것.
    /// </summary>
    public static MechaLoadoutSO PendingLoadout { get; set; }

    [Tooltip("격납고에서 넘어온 구성이 없을 때 쓸 기본값. 디버그용이다.")]
    [SerializeField] private MechaLoadoutSO _defaultLoadout;

    [Tooltip("새 판으로 취급해 월드 상태를 되돌린다. " +
             "이걸 켠 Applier가 씬에 둘 이상이면 서로의 아이템 인스턴스를 지운다.")]
    [SerializeField] private bool _resetWorldState = true;

    [Tooltip("켜면 매번 같은 시드로 시작한다. 재현이 필요한 테스트에 쓴다.")]
    [SerializeField] private bool _useFixedSeed;

    [SerializeField] private int _seed;

    private Agent _owner;
    private EquipmentController _equipment;

    /// <summary>지금 적용된 구성. 없으면 null.</summary>
    public MechaLoadoutSO Applied { get; private set; }

    public void Initialize(Agent owner)
    {
        _owner = owner;
    }

    public void AfterInitialize()
    {
        _equipment = _owner.GetCompo<EquipmentController>();
    }

    public void Dispose()
    {
        _equipment = null;
    }

    /// <summary>
    /// AfterInitialize가 아니라 Start에서 적용한다.
    /// 컴포넌트 초기화 순서는 보장되지 않아서, AfterInitialize에서 장착하면
    /// 아직 구독하지 않은 컴포넌트가 장착 이벤트를 통째로 놓친다.
    /// Start는 모든 Awake(= Agent의 초기화 전체)가 끝난 뒤다.
    /// </summary>
    private void Start()
    {
        Apply(PendingLoadout != null ? PendingLoadout : _defaultLoadout);
    }

    public void SetLoadout(MechaLoadoutSO loadout) => Apply(loadout);

    private void Apply(MechaLoadoutSO loadout)
    {
        if (_equipment == null)
        {
            Debug.LogError("[Loadout] EquipmentController가 없어 구성을 적용할 수 없습니다.", this);
            return;
        }

        if (_resetWorldState)
            ResetWorldState();

        Applied = loadout;
        if (loadout == null) return;

        IReadOnlyList<MechaLoadoutSO.Entry> entries = loadout.Entries;
        for (int i = 0; i < entries.Count; i++)
            Equip(entries[i], loadout);
    }

    private void Equip(MechaLoadoutSO.Entry entry, MechaLoadoutSO loadout)
    {
        if (!entry.IsValid) return;

        if (!entry.item.CanEquipTo(entry.slot))
        {
            Debug.LogWarning(
                $"[Loadout] {entry.item.DisplayName}은 {entry.slot} 슬롯에 들어가지 않습니다.", loadout);
            return;
        }

        // 무기처럼 개체 상태가 있는 아이템은 인스턴스를 만들어야 모딩·내구도가 붙는다.
        // CreateStack이 RequiresInstance를 보고 알아서 갈라준다.
        ItemStack stack = ItemInstanceRegistry.CreateStack(entry.item);
        if (stack.IsEmpty)
        {
            Debug.LogWarning(
                $"[Loadout] {entry.item.DisplayName}을 만들지 못했습니다. " +
                "ItemDatabase에 등록되어 있는지 확인하세요.", loadout);
            return;
        }

        _equipment.Equip(entry.slot, stack);
    }

    /// <summary>
    /// 이전 판의 흔적을 지운다. ItemInstanceRegistry는 정적이라
    /// 플레이 모드를 나가도 살아남는다.
    /// </summary>
    private void ResetWorldState()
    {
        ItemInstanceRegistry.Clear();
        GameRandom.Reseed(_useFixedSeed ? _seed : Environment.TickCount);
    }
}
