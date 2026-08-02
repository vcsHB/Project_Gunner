using System;
using UnityEngine;

public abstract class CasterBase : MonoBehaviour
{
    protected const int MaxTargetCount = 32;

    public event Action OnCastEvent;
    [SerializeField] protected LayerMask _targetLayer;

    /// <summary>
    /// 이 캐스터를 발사한 주체. 투사체처럼 Agent 밑에 있지 않은 경우 스폰 시점에 넣어준다.
    /// </summary>
    public Agent Owner { get; set; }

    private ICastEffector[] _castEffectors;
    private readonly Collider2D[] _targetBuffer = new Collider2D[MaxTargetCount];
    private ContactFilter2D _contactFilter;

    protected virtual void Awake()
    {
        Owner = GetComponentInParent<Agent>();

        _contactFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = _targetLayer,
            useTriggers = true,
        };

        _castEffectors = GetComponentsInChildren<ICastEffector>();
        for (int i = 0; i < _castEffectors.Length; i++)
        {
            _castEffectors[i].Initialize();
        }
    }

    public void Cast()
    {
        int count = CastTarget(_contactFilter, _targetBuffer);
        ApplyEffectors(_targetBuffer, count);

        OnCastEvent?.Invoke();
    }

    /// <summary>
    /// 도형에 맞는 방식으로 대상을 찾아 buffer에 담고, 담은 개수를 반환한다.
    /// </summary>
    protected abstract int CastTarget(ContactFilter2D filter, Collider2D[] buffer);

    /// <summary>
    /// 탐색 없이 이미 알고 있는 대상에게 바로 적용한다.
    /// </summary>
    public void ForceCast(Collider2D[] targets) => ApplyEffectors(targets, targets.Length);

    private void ApplyEffectors(Collider2D[] targets, int count)
    {
        for (int i = 0; i < _castEffectors.Length; i++)
        {
            for (int j = 0; j < count; j++)
            {
                if (targets[j] == null) continue;

                _castEffectors[i].Cast(targets[j]);
            }
        }
    }
}
