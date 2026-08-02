using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Agent : MonoBehaviour
{
    [field: SerializeField] public Health AgentHealth { get; set; }
    [field: SerializeField] public AgentStatus AgentStatus { get; set; }

    public bool IsDead { get; protected set; }
    private Dictionary<Type, IAgentComponent> _components = new Dictionary<Type, IAgentComponent>();


    protected virtual void Awake()
    {

        AddComponentToDictionary();
        ComponentInitialize();
        AfterInit();
        AgentHealth = GetCompo<Health>();
        AgentStatus = GetCompo<AgentStatus>();

        if (AgentHealth != null)
            AgentHealth.OnDeadEvent += HandleDead;
    }

    protected virtual void Start()
    {

    }

    protected virtual void OnDestroy()
    {
        if (AgentHealth != null)
            AgentHealth.OnDeadEvent -= HandleDead;

        _components.Values.ToList().ForEach(compo => compo.Dispose());
    }

    protected virtual void HandleDead()
    {
        IsDead = true;
    }

    private void AddComponentToDictionary()
    {
        // 같은 타입이 여러 개 붙어있으면 첫 번째 것만 등록한다.
        GetComponentsInChildren<IAgentComponent>(true)
            .ToList().ForEach(compo => _components.TryAdd(compo.GetType(), compo));
    }

    private void ComponentInitialize()
    {
        _components.Values.ToList().ForEach(compo => compo.Initialize(this));
    }

    private void AfterInit()
    {
        _components.Values.ToList().ForEach(compo => compo.AfterInitialize());
    }

    public T GetCompo<T>(bool isDerived = false) where T : class
    {
        if (_components.TryGetValue(typeof(T), out IAgentComponent compo))
        {
            return compo as T;
        }
        else
        {
            // 상속구조의 예외 발생 가능 
            //Debug.Log("Not Exist In components dictionary");
            T newComponent = GetComponentInChildren<T>();
            if (newComponent is IAgentComponent)
            {

                _components.TryAdd(typeof(T), newComponent as IAgentComponent);
                //Debug.Log("Insert In dictionary");
                return newComponent;
            }
        }

        if (!isDerived) return default;

        Type findType = _components.Keys.FirstOrDefault(x => x.IsSubclassOf(typeof(T)));
        if (findType != null)
            return _components[findType] as T;

        return default(T);
    }


}
