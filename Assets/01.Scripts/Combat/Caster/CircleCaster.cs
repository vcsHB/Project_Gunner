using UnityEngine;

public class CircleCaster : CasterBase
{
    [SerializeField] private float _radius = 1f;

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
