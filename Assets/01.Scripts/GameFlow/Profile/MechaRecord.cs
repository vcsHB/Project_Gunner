using System.Collections.Generic;

/// <summary>
/// 기체 한 대를 <b>오브젝트 없이</b> 적어 둔 것. 씬이 바뀌어도(주둔지 ↔ 필드), 격납고에 세워 둬도 이걸로 다시 세운다.
///
/// 적는 것은 아이템 스택뿐이다 — 장착(슬롯별)·적재함·안전 슬롯. 손상·개량·내구도는 스택이 가리키는
/// 인스턴스(<see cref="ItemInstance"/>)에 이미 있다(<see cref="ItemInstance.HealthRatio"/>). 그래서 인스턴스를 레지스트리에서 지우면 안 된다 —
/// <see cref="CollectInstanceIds"/>가 지키는 목록에 넣는다.
///
/// 실드·전력·연료·상태이상은 적지 않는다 — 판마다 차서 시작한다.
/// 세이브(M7)도 이 모양을 그대로 쓴다: (슬롯, itemId, instanceId) + 인스턴스 상태.
/// </summary>
public class MechaRecord
{
    /// <summary>적고 다시 입히는 순서. <b>코어가 먼저</b> — 팔·추진체 소켓과 적재 칸 수가 코어에 달려 있다.</summary>
    public static readonly EquipSlotType[] Slots =
    {
        EquipSlotType.Core,
        EquipSlotType.ArmLeft,
        EquipSlotType.ArmRight,
        EquipSlotType.Booster,
    };

    private readonly Dictionary<EquipSlotType, ItemStack> _equipped = new();
    private readonly List<ItemStack> _cargo = new();
    private readonly List<ItemStack> _safeSlot = new();

    public IReadOnlyList<ItemStack> Cargo => _cargo;

    public bool HasCore => !GetEquipped(EquipSlotType.Core).IsEmpty;

    public ItemStack GetEquipped(EquipSlotType slot)
        => _equipped.TryGetValue(slot, out ItemStack stack) ? stack : ItemStack.Empty;

    /// <summary>지금 기체를 적는다. 기체는 건드리지 않는다.</summary>
    public static MechaRecord Capture(Agent mecha)
    {
        MechaRecord record = new();
        if (mecha == null) return record;

        EquipmentController equipment = mecha.GetCompo<EquipmentController>();
        if (equipment != null)
        {
            for (int i = 0; i < Slots.Length; i++)
            {
                ItemStack stack = equipment.GetStack(Slots[i]);
                if (!stack.IsEmpty) record._equipped[Slots[i]] = stack;
            }
        }

        InventoryController inventory = mecha.GetCompo<InventoryController>();
        if (inventory != null)
        {
            CopyNonEmpty(inventory.Inventory, record._cargo);
            CopyNonEmpty(inventory.SafeSlot, record._safeSlot);
        }

        return record;
    }

    /// <summary>
    /// 이 기록대로 기체를 입힌다. 기체는 <b>비어 있어야 한다</b>(장비·적재함·안전 슬롯) — 남아 있던 것은 덮어쓰지 않고 그 위에 더한다.
    /// 적재함은 장착 <b>뒤에</b> 싣는다 — 칸 수가 파츠 스탯을 따른다. 못 실은 것은 돌려준다(부르는 쪽이 창고로).
    /// </summary>
    public void ApplyTo(Agent mecha, List<ItemStack> leftover = null)
    {
        if (mecha == null) return;

        EquipmentController equipment = mecha.GetCompo<EquipmentController>();
        if (equipment != null)
        {
            for (int i = 0; i < Slots.Length; i++)
            {
                ItemStack stack = GetEquipped(Slots[i]);
                if (!stack.IsEmpty && !equipment.Equip(Slots[i], stack))
                    leftover?.Add(stack);
            }
        }

        InventoryController inventory = mecha.GetCompo<InventoryController>();
        Load(inventory != null ? inventory.Inventory : null, _cargo, leftover);
        Load(inventory != null ? inventory.SafeSlot : null, _safeSlot, leftover);
    }

    /// <summary>이 기록이 붙잡고 있는 인스턴스. 새 판에 레지스트리를 비울 때 남긴다.</summary>
    public void CollectInstanceIds(ICollection<uint> into)
    {
        foreach (ItemStack stack in _equipped.Values)
            AddId(stack, into);

        for (int i = 0; i < _cargo.Count; i++) AddId(_cargo[i], into);
        for (int i = 0; i < _safeSlot.Count; i++) AddId(_safeSlot[i], into);
    }

    private static void CopyNonEmpty(Inventory from, List<ItemStack> into)
    {
        if (from == null) return;

        for (int i = 0; i < from.Capacity; i++)
        {
            if (!from[i].IsEmpty) into.Add(from[i]);
        }
    }

    private static void Load(Inventory into, List<ItemStack> stacks, List<ItemStack> leftover)
    {
        for (int i = 0; i < stacks.Count; i++)
        {
            ItemStack stack = stacks[i];

            if (into == null)
            {
                leftover?.Add(stack);
                continue;
            }

            if (stack.HasInstance)
            {
                if (!into.AddStack(stack)) leftover?.Add(stack);
                continue;
            }

            // 겹치는 것은 일부만 들어갈 수 있다. 남은 개수만 돌려준다.
            int left = into.Add(stack.itemId, stack.count);
            if (left > 0) leftover?.Add(new ItemStack(stack.itemId, left));
        }
    }

    private static void AddId(ItemStack stack, ICollection<uint> into)
    {
        if (!stack.IsEmpty && stack.instanceId != 0) into.Add(stack.instanceId);
    }
}
