using UnityEngine;

public class ConditionCastEffector : MonoBehaviour, ICastEffector
{
    [SerializeField] private ConditionType _conditionType = ConditionType.Burn;
    [SerializeField, Min(1)] private int _conditionLevel = 1;

    [Tooltip("0 이하면 직접 제거할 때까지 유지된다.")]
    [SerializeField] private float _duration = 3f;

    [SerializeField, Range(0f, 1f)] private float _chance = 1f;

    private CasterBase _caster;

    public void Initialize()
    {
        _caster = GetComponentInParent<CasterBase>();
    }

    public void Cast(in CastHit hit)
    {
        if (_conditionType == ConditionType.None) return;
        if (_chance < 1f && Random.value > _chance) return;

        // 상태이상은 Agent에게만 걸린다. (폭발통 같은 단순 대상은 대상 외)
        Agent agent = hit.Target.Owner;
        if (agent == null) return;

        AgentConditionEffector effector = agent.GetCompo<AgentConditionEffector>();
        if (effector == null) return;

        Agent owner = _caster != null ? _caster.Owner : null;
        effector.AddCondition(_conditionType, _conditionLevel, _duration, owner);
    }
}
