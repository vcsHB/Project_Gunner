using UnityEngine;

/// <summary>
/// 콜라이더가 달린 오브젝트에 붙여 소속 TargetBase를 직접 물려둔다.
/// 캐스터가 부모로 거슬러 올라가며 탐색하지 않아도 되게 하는 것이 목적이다.
///
/// <b>어느 부위인지</b>(<see cref="Part"/>)는 프리팹이 아니라 <b>붙은 자리</b>가 정한다 —
/// 가장 가까운 상위 <see cref="MechaSocket"/>의 슬롯. 같은 무기 프리팹이 왼팔에도 오른팔에도 붙기 때문이다.
/// 소켓 아래가 아니면(코어 자체, 실드, 메카가 아닌 유닛) 본체(<see cref="EquipSlotType.Core"/>)다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class HitBox : MonoBehaviour
{
    [SerializeField] private TargetBase _target;

    [Tooltip("부위별 피해 배율. 머리 2배 같은 것.")]
    [SerializeField, Min(0f)] private float _damageMultiplier = 1f;

    private bool _partResolved;
    private EquipSlotType _part;
    private Transform _resolvedParent;

    /// <summary>
    /// 소속 대상. Awake에서 못 찾았으면(꺼진 채로 생성되어 Awake가 늦은 경우 등) 물을 때 다시 찾는다.
    /// 비어 있으면 캐스터가 이 콜라이더를 조용히 무시한다.
    /// </summary>
    public TargetBase Target => _target != null ? _target : (_target = GetComponentInParent<TargetBase>());
    public float DamageMultiplier => _damageMultiplier;

    /// <summary>
    /// 맞으면 피해를 받을 부위. 처음 물을 때 계층에서 찾고, 부모가 바뀌면(코어 교체로 무기가 옮겨감) 다시 찾는다.
    /// </summary>
    public EquipSlotType Part
    {
        get
        {
            if (!_partResolved || _resolvedParent != transform.parent)
                ResolvePart();

            return _part;
        }
    }

    // 컴포넌트를 붙이는 순간 인스펙터에서 자동으로 채워준다.
    private void Reset() => _target = GetComponentInParent<TargetBase>();

    private void Awake()
    {
        if (_target == null)
            _target = GetComponentInParent<TargetBase>();

        if (_target == null)
            Debug.LogError($"[HitBox] {name}이 소속될 TargetBase를 찾지 못했습니다.", this);
    }

    private void ResolvePart()
    {
        _partResolved = true;
        _resolvedParent = transform.parent;

        MechaSocket socket = GetComponentInParent<MechaSocket>();
        _part = socket != null && socket.Slot != EquipSlotType.None ? socket.Slot : EquipSlotType.Core;
    }
}
