using UnityEngine;

public interface IPoolable
{
    // SelfObject를 매번 구현하지 않으려면 PoolableMono를 상속할 것.
    // (인터페이스에는 필드가 없어 gameObject에 접근할 수 없으므로 인터페이스만으로는 불가능하다)
    public GameObject SelfObject { get; }

    public void OnSpawn();
    public void OnDespawn();
}
