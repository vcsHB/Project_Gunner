using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 살아있는 아이템 인스턴스 저장소.
///
/// 월드 상태라 프로세스당 하나면 충분해서 정적으로 둔다.
/// 새 게임을 시작하거나 월드를 다시 불러올 때 반드시 Clear를 부를 것.
/// (플레이 모드를 나가도 정적 상태는 남는다)
/// </summary>
public static class ItemInstanceRegistry
{
    private static readonly Dictionary<uint, ItemInstance> Instances = new();
    private static uint s_nextId = 1;

    public static int Count => Instances.Count;

    /// <summary>월드 전환 시 호출. 안 부르면 이전 판의 인스턴스가 남는다.</summary>
    public static void Clear()
    {
        Instances.Clear();
        s_nextId = 1;
    }

    /// <summary>
    /// 아이템 하나를 인벤토리에 넣을 수 있는 형태로 만든다.
    /// 인스턴스가 필요한 아이템이면 함께 만들어 붙인다.
    /// </summary>
    public static ItemStack CreateStack(ItemDataSO data, int count = 1)
    {
        if (data == null || !data.IsRegistered || count <= 0) return ItemStack.Empty;

        if (!data.RequiresInstance)
            return new ItemStack(data.Id, count);

        ItemInstance instance = Create(data);
        return instance == null
            ? new ItemStack(data.Id, 1)
            : new ItemStack(data.Id, 1, instance.InstanceId);
    }

    public static ItemInstance Create(ItemDataSO data)
    {
        if (data == null || !data.IsRegistered) return null;

        ItemInstance instance = data.CreateRuntimeInstance();
        if (instance == null)
        {
            Debug.LogError($"[ItemInstance] {data.DisplayName}이 인스턴스를 만들지 못했습니다.", data);
            return null;
        }

        instance.InstanceId = s_nextId++;
        instance.ItemId = data.Id;
        Instances.Add(instance.InstanceId, instance);

        return instance;
    }

    public static bool TryGet(uint instanceId, out ItemInstance instance)
    {
        instance = null;
        return instanceId != 0 && Instances.TryGetValue(instanceId, out instance);
    }

    public static T Get<T>(uint instanceId) where T : ItemInstance
        => TryGet(instanceId, out ItemInstance instance) ? instance as T : null;

    /// <summary>아이템이 완전히 사라질 때. 인벤토리에서 옮기는 정도로는 부르지 않는다.</summary>
    public static bool Release(uint instanceId) => Instances.Remove(instanceId);
}
