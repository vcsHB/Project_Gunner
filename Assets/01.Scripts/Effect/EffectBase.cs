using UnityEngine;

public class EffectBase : MonoBehaviour, IPoolable
{
    [SerializeField] protected float _lifetime = 3f;
    public GameObject SelfObject => gameObject;

    public virtual void OnSpawn()
    {
        
    }

    public virtual void OnDespawn()
    {

    }

}