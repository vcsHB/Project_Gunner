using UnityEngine;

/// <summary>
/// 콜라이더가 달린 오브젝트에 붙여 소속 TargetBase를 직접 물려둔다.
/// 캐스터가 부모로 거슬러 올라가며 탐색하지 않아도 되게 하는 것이 목적이다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class HitBox : MonoBehaviour
{
    [SerializeField] private TargetBase _target;

    [Tooltip("부위별 피해 배율. 머리 2배 같은 것.")]
    [SerializeField, Min(0f)] private float _damageMultiplier = 1f;

    public TargetBase Target => _target;
    public float DamageMultiplier => _damageMultiplier;

    // 컴포넌트를 붙이는 순간 인스펙터에서 자동으로 채워준다.
    private void Reset() => _target = GetComponentInParent<TargetBase>();

    private void Awake()
    {
        if (_target == null)
            _target = GetComponentInParent<TargetBase>();

        if (_target == null)
            Debug.LogError($"[HitBox] {name}이 소속될 TargetBase를 찾지 못했습니다.", this);
    }
}
