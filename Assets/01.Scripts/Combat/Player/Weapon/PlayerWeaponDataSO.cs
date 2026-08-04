using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "SO/Item/PlayerWeaponData")]
public class PlayerWeaponDataSO : ItemDataSO
{
    [Header("Player WeaponData Settings")]
    public PlayerWeaponBase playerWeaponPrefab;

    [Tooltip("공격 방식 분류. 파츠 호환 판정에 쓴다.")]
    [SerializeField] private PlayerWeaponCategory _category = PlayerWeaponCategory.None;

    [Tooltip("이 무기가 가진 모딩 슬롯. 여기 없는 슬롯의 파츠는 끼울 수 없다.")]
    [SerializeField] private List<WeaponPartSlotType> _partSlots = new();

    [SerializeField] private WeaponStatDefaults _stats = new();

    public PlayerWeaponCategory Category => _category;
    public IReadOnlyList<WeaponPartSlotType> PartSlots => _partSlots;
    public WeaponStatDefaults Stats => _stats;

    public bool HasSlot(WeaponPartSlotType slot) => _partSlots.Contains(slot);
}
