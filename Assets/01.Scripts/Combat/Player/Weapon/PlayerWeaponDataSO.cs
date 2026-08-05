using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "SO/Item/PlayerWeaponData")]
public class PlayerWeaponDataSO : EquipableItemDataSO
{
    [Header("Player WeaponData Settings")]
    public PlayerWeaponBase playerWeaponPrefab;

    [Tooltip("공격 방식 분류. 파츠 호환 판정에 쓴다.")]
    [SerializeField] private PlayerWeaponCategory _category = PlayerWeaponCategory.None;

    [Tooltip("이 무기가 가진 모딩 슬롯. 여기 없는 슬롯의 파츠는 끼울 수 없다.")]
    [SerializeField] private List<WeaponPartSlotType> _partSlots = new();

    [Tooltip("이 무기가 할 수 있는 개량. 무기마다 다르다.")]
    [SerializeField] private List<WeaponUpgradeSO> _allowedUpgrades = new();

    [Tooltip("기본으로 쓸 수 있는 발사 모드. 첫 번째가 시작 모드다. 파츠가 여기에 더할 수 있다.")]
    [SerializeField] private List<WeaponFireMode> _fireModes = new() { WeaponFireMode.Single };

    [SerializeField] private WeaponStatDefaults _stats = new();

    public PlayerWeaponCategory Category => _category;
    public IReadOnlyList<WeaponPartSlotType> PartSlots => _partSlots;
    public IReadOnlyList<WeaponUpgradeSO> AllowedUpgrades => _allowedUpgrades;
    public IReadOnlyList<WeaponFireMode> FireModes => _fireModes;

    public WeaponFireMode DefaultFireMode
        => _fireModes.Count > 0 ? _fireModes[0] : WeaponFireMode.Single;
    public WeaponStatDefaults Stats => _stats;

    // 무기는 파츠와 개량 때문에 개체마다 상태가 다르다.
    public override bool RequiresInstance => true;
    public override ItemInstance CreateRuntimeInstance() => new WeaponItemInstance();

    public bool HasSlot(WeaponPartSlotType slot) => _partSlots.Contains(slot);

    public bool CanUpgrade(WeaponUpgradeSO upgrade) => _allowedUpgrades.Contains(upgrade);
}
