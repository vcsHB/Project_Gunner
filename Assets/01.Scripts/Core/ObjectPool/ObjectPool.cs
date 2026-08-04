using UnityEngine;

/// <summary>
/// PoolManager로 가는 정적 진입점.
/// 타입이 유일하면 Get&lt;T&gt;(), 같은 컴포넌트를 쓰는 프리팹이 여럿이면 Get(PoolType)을 쓴다.
/// </summary>
public static class ObjectPool
{
    public static T Get<T>() where T : PoolableMono
    {
        PoolManager manager = PoolManager.Instance;
        if (manager == null)
        {
            Debug.LogError("[ObjectPool] 씬에 PoolManager가 없습니다.");
            return null;
        }

        return manager.Get<T>();
    }

    public static T Get<T>(Vector3 position, Quaternion rotation) where T : PoolableMono
        => Place(Get<T>(), position, rotation);

    public static PoolableMono Get(PoolType type)
    {
        PoolManager manager = PoolManager.Instance;
        if (manager == null)
        {
            Debug.LogError("[ObjectPool] 씬에 PoolManager가 없습니다.");
            return null;
        }

        return manager.Get(type);
    }

    public static PoolableMono Get(PoolType type, Vector3 position, Quaternion rotation)
        => Place(Get(type), position, rotation);

    public static void Release(PoolableMono item)
    {
        if (item == null) return;

        PoolManager manager = PoolManager.Instance;
        if (manager == null)
        {
            Object.Destroy(item.gameObject);
            return;
        }

        manager.Release(item);
    }

    private static T Place<T>(T item, Vector3 position, Quaternion rotation) where T : PoolableMono
    {
        if (item != null)
            item.transform.SetPositionAndRotation(position, rotation);

        return item;
    }
}
