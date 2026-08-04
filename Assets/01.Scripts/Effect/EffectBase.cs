using UnityEngine;

public class EffectBase : PoolableMono
{
    [Tooltip("0 이하면 자동으로 반납하지 않는다.")]
    [SerializeField] protected float _lifetime = 3f;

    private float _remainTime;

    public override void OnSpawn()
    {
        _remainTime = _lifetime;
    }

    public override void OnDespawn()
    {
        _remainTime = 0f;
    }

    protected virtual void Update()
    {
        if (_lifetime <= 0f) return;

        _remainTime -= Time.deltaTime;
        if (_remainTime <= 0f)
            Release();
    }
}
