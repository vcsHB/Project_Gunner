using System;
using System.Collections.Generic;

/// <summary>우클릭 메뉴 한 줄. 할 수 없으면 회색으로 보이고 이유가 붙는다 — 아예 숨기면 "이걸 왜 못 하지"를 모른다.</summary>
public struct ItemContextAction
{
    public string label;
    public bool enabled;
    public string reason;
    public Action run;
}

/// <summary>
/// 칸 하나에 할 수 있는 추가 작업 목록. 우클릭 메뉴(<see cref="PopupItemContext"/>)는 이걸 그리기만 한다.
///
/// | 작업 | 뜨는 조건 |
/// |---|---|
/// | 사용 | 쓸 수 있는 아이템(지금은 수리 키트) |
/// | 분할 | 겹친 것(2개 이상), 적재함 칸 |
/// | 버리기 | 언제나(못 빼는 칸이면 회색) |
///
/// 새 작업은 여기에 한 갈래를 더한다. <b>분해</b>는 분해 데이터가, <b>자세히 보기</b>는 도감 패널이,
/// <b>모듈</b>은 파츠 상세 화면이 생길 때 붙는다.
/// </summary>
public static class ItemContextActions
{
    public static void Collect(CellInventorySlot cell, Agent owner, List<ItemContextAction> actions)
    {
        actions.Clear();
        if (cell == null || cell.IsEmpty || cell.Container == null) return;

        IItemSlotContainer container = cell.Container;
        int slotKey = cell.SlotKey;
        ItemStack stack = cell.Stack;
        ItemDataSO data = stack.Resolve();
        if (data == null) return;

        bool canTake = container.CanTake(slotKey, out string takeReason);

        // 사용 — 수리 키트. 쓰는 규칙(맞으면 끊김 등)은 MechaRepair가 정한다. 여기서는 어느 칸의 키트를 쓸지만 넘긴다.
        if (data is RepairKitDataSO && container is Inventory)
        {
            MechaRepair repair = owner != null ? owner.GetCompo<MechaRepair>() : null;
            bool busy = repair != null && repair.IsRepairing;

            actions.Add(new ItemContextAction
            {
                label = ItemText.Label("action.use", "사용"),
                enabled = repair != null && !busy,
                reason = busy ? ItemText.Label("action.use.busy", "수리 중") : null,
                run = () => repair.TryStartRepair(slotKey),
            });
        }

        // 분할 — 같은 적재함의 빈 칸으로 떼어 놓는다.
        if (stack.count > 1 && container is Inventory inventory)
        {
            bool hasRoom = inventory.FindEmptySlot() >= 0;

            actions.Add(new ItemContextAction
            {
                label = ItemText.Label("action.split", "분할"),
                enabled = canTake && hasRoom,
                reason = !hasRoom ? ItemText.Label("action.split.noRoom", "빈 칸 없음") : takeReason,
                run = () =>
                {
                    PopupItemSplit split = PopupItemSplit.Instance;
                    if (split != null) split.Open(inventory, slotKey);
                },
            });
        }

        // 버리기 — 통째로. 일부만 버리려면 분할하거나 Alt 드래그로 패널 밖에.
        actions.Add(new ItemContextAction
        {
            label = ItemText.Label("action.discard", "버리기"),
            enabled = canTake && owner != null,
            reason = canTake ? null : takeReason,
            run = () => ItemDiscard.Discard(container, slotKey, stack.count, owner),
        });
    }
}
