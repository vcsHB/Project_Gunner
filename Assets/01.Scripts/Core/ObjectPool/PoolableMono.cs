using UnityEngine;

/// <summary>
/// 풀링되는 오브젝트의 공통 베이스. PoolListSO에는 이 타입만 등록할 수 있다.
/// </summary>
public abstract class PoolableMono : MonoBehaviour, IPoolable
{
    public GameObject SelfObject => gameObject;

    /// <summary>어느 풀에서 나왔는지. 풀이 스폰 시점에 넣어준다.</summary>
    public PoolType PoolType { get; private set; }

    /// <summary>풀 안에서 대기 중인지. 이중 반납을 막는 용도.</summary>
    public bool IsInPool { get; private set; } = true;

    internal void SetPoolType(PoolType poolType) => PoolType = poolType;
    internal void SetInPool(bool value) => IsInPool = value;

    public virtual void OnSpawn() { }

    public virtual void OnDespawn() { }

    /// <summary>자기 자신을 풀로 되돌린다.</summary>
    public void Release() => ObjectPool.Release(this);
}
