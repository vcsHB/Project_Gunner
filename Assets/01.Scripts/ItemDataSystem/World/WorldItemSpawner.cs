using UnityEngine;

/// <summary>
/// 바닥에 아이템을 떨군다. 풀에 WorldItem이 등록되어 있어야 한다.
/// </summary>
public static class WorldItemSpawner
{
    public static WorldItem Spawn(ItemStack stack, Vector2 position, float scatter = 0.3f)
    {
        if (stack.IsEmpty) return null;

        WorldItem item = ObjectPool.Get<WorldItem>();
        if (item == null)
        {
            Debug.LogError($"[WorldItem] 풀에서 꺼내지 못했습니다. 아이템을 잃었습니다. (Id {stack.itemId} x{stack.count})");
            return null;
        }

        // 흩뿌리기는 연출이라 시드를 쓰지 않는다. 멀티에서는 서버가 위치를 정하게 될 자리다.
        if (scatter > 0f)
            position += Random.insideUnitCircle * scatter;

        item.transform.position = position;
        item.Setup(stack);

        return item;
    }

    /// <summary>여러 개를 한 번에 떨군다.</summary>
    public static void SpawnAll(System.Collections.Generic.IReadOnlyList<ItemStack> stacks, Vector2 position, float scatter = 0.3f)
    {
        for (int i = 0; i < stacks.Count; i++)
            Spawn(stacks[i], position, scatter);
    }
}
