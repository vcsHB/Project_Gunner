using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class CasterBase : MonoBehaviour
{
    protected const int MaxTargetCount = 32;

    public event Action OnCastEvent;

    [Tooltip("때릴 수 있는 대상 분류. 물리 LayerMask는 여기서 자동으로 만들어진다.")]
    [SerializeField] private TargetType _targetTypes = TargetType.Ground | TargetType.Air;

    /// <summary>
    /// 이 캐스터를 발사한 주체. 투사체처럼 Agent 밑에 있지 않은 경우 스폰 시점에 넣어준다.
    /// </summary>
    public Agent Owner { get; set; }

    private ICastEffector[] _castEffectors;
    private readonly Collider2D[] _colliderBuffer = new Collider2D[MaxTargetCount];
    private ContactFilter2D _contactFilter;

    // 같은 TargetBase가 히트박스 여러 개로 잡히는 걸 접기 위한 것
    private readonly List<CastHit> _hits = new();
    private readonly Dictionary<TargetBase, int> _hitIndices = new();

    protected virtual void Awake()
    {
        Owner = GetComponentInParent<Agent>();

        _contactFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = TargetLayerTableSO.MaskOf(_targetTypes),
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
        int count = CastTarget(_contactFilter, _colliderBuffer);

        ResolveHits(_colliderBuffer, count);
        ApplyEffectors();

        OnCastEvent?.Invoke();
    }

    /// <summary>
    /// 도형에 맞는 방식으로 콜라이더를 찾아 buffer에 담고, 담은 개수를 반환한다.
    /// </summary>
    protected abstract int CastTarget(ContactFilter2D filter, Collider2D[] buffer);

    /// <summary>탐색 없이 이미 알고 있는 콜라이더에 바로 적용한다.</summary>
    public void ForceCast(Collider2D[] colliders)
    {
        ResolveHits(colliders, colliders.Length);
        ApplyEffectors();
    }

    /// <summary>
    /// 콜라이더 목록을 대상 목록으로 접는다.
    /// 한 유닛이 히트박스를 여럿 갖고 있어도 효과는 한 번만 들어가야 한다.
    /// </summary>
    private void ResolveHits(Collider2D[] colliders, int count)
    {
        _hits.Clear();
        _hitIndices.Clear();

        for (int i = 0; i < count; i++)
        {
            if (!TryResolve(colliders[i], out CastHit hit)) continue;
            if (!hit.Target.IsTargetable) continue;
            if (Owner != null && hit.Target.Owner == Owner) continue;

            if (_hitIndices.TryGetValue(hit.Target, out int index))
            {
                // 같은 대상이 여러 부위로 걸리면 배율이 높은 쪽을 남긴다.
                if (_hits[index].DamageMultiplier < hit.DamageMultiplier)
                    _hits[index] = hit;

                continue;
            }

            _hitIndices.Add(hit.Target, _hits.Count);
            _hits.Add(hit);
        }
    }

    private static bool TryResolve(Collider2D collider, out CastHit hit)
    {
        hit = default;
        if (collider == null) return false;

        // 빠른 경로. 히트박스가 소속 대상을 직접 물고 있다.
        if (collider.TryGetComponent(out HitBox hitBox))
        {
            if (hitBox.Target == null) return false;

            hit = new CastHit(hitBox.Target, hitBox, collider);
            return true;
        }

        // 히트박스를 나누지 않은 단순한 대상
        TargetBase target = collider.GetComponentInParent<TargetBase>();
        if (target == null) return false;

        hit = new CastHit(target, null, collider);
        return true;
    }

    private void ApplyEffectors()
    {
        for (int i = 0; i < _castEffectors.Length; i++)
        {
            for (int j = 0; j < _hits.Count; j++)
                _castEffectors[i].Cast(_hits[j]);
        }
    }
}
