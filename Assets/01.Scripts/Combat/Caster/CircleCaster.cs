using UnityEngine;

public class CircleCaster : CasterBase
{
    [SerializeField] private float _radius = 1f;

    /// <summary>폭발 반경처럼 스탯에서 오는 값이면 발사 시점에 덮어쓴다.</summary>
    public float Radius
    {
        get => _radius;
        set => _radius = Mathf.Max(0f, value);
    }

    protected override int CastTarget(ContactFilter2D filter, Collider2D[] buffer)
        => Physics2D.OverlapCircle(transform.position, _radius, filter, buffer);

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _radius);
    }
#endif
}
