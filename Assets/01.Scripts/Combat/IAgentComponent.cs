using UnityEngine;

public interface IAgentComponent
{   
    public void Initialize(Agent owner);
    public void AfterInitialize();
    public void Dispose();
}