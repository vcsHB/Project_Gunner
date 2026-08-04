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

    public void Cast(Collider2D target)
    {
        if (_conditionType == ConditionType.None) return;
        if (_chance < 1f && Random.value > _chance) return;

        Agent agent = target.GetComponentInParent<Agent>();
        if (agent == null || agent.IsDead) return;

        Agent owner = _caster != null ? _caster.Owner : null;
        if (owner != null && owner == agent) return;

        AgentConditionEffector effector = agent.GetCompo<AgentConditionEffector>();
        if (effector == null) return;

        effector.AddCondition(_conditionType, _conditionLevel, _duration, owner);
    }
}
