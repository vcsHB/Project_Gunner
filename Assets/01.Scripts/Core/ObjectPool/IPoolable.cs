using UnityEngine;
using UnityEngine.Rendering;

public interface IPoolable
{

    public GameObject SelfObject { get; } // TODO 하위 구현하지 않아도 되도록 기술적으로 가능한지 확인

    public void OnSpawn();
    public void OnDespawn();
}