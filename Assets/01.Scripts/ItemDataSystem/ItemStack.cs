using System;

/// <summary>
/// 인벤토리 한 칸. 저장·전송되는 최소 단위다.
///
/// instanceId가 0이 아니면 그 개체만의 상태(무기 파츠·개량 등)가 따로 있고,
/// 이 경우 개수는 항상 1이며 다른 칸과 겹쳐지지 않는다.
/// </summary>
[Serializable]
public struct ItemStack
{
    public uint itemId;
    public int count;
    public uint instanceId;

    public ItemStack(uint itemId, int count, uint instanceId = 0)
    {
        this.itemId = itemId;
        this.count = count;
        this.instanceId = instanceId;
    }

    public bool IsEmpty => itemId == 0 || count <= 0;
    public bool HasInstance => instanceId != 0;

    public static ItemStack Empty => default;

    /// <summary>Id를 실제 데이터로. 등록되지 않은 Id면 null.</summary>
    public ItemDataSO Resolve()
    {
        if (IsEmpty || ItemDatabaseSO.Instance == null) return null;

        return ItemDatabaseSO.Instance.TryGet(itemId, out ItemDataSO data) ? data : null;
    }

    public T Resolve<T>() where T : ItemDataSO => Resolve() as T;

    public ItemInstance ResolveInstance()
        => ItemInstanceRegistry.TryGet(instanceId, out ItemInstance instance) ? instance : null;

    public T ResolveInstance<T>() where T : ItemInstance => ItemInstanceRegistry.Get<T>(instanceId);

    /// <summary>개체 상태를 가진 아이템은 서로 겹칠 수 없다.</summary>
    public bool CanMergeWith(ItemStack other)
        => !IsEmpty && !other.IsEmpty && itemId == other.itemId && !HasInstance && !other.HasInstance;
}
