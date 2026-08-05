using UnityEngine;

/// <summary>
/// Inventory를 Agent에 붙여주는 얇은 래퍼. 로직은 전부 Inventory에 있다.
/// </summary>
public class InventoryController : MonoBehaviour, IAgentComponent
{
    [SerializeField, Min(1)] private int _capacity = 30;

    public Inventory Inventory { get; private set; }
    public Agent Owner { get; private set; }

    public void Initialize(Agent owner)
    {
        Owner = owner;
        Inventory = new Inventory(_capacity);
    }

    public void AfterInitialize()
    {
    }

    public void Dispose()
    {
    }
}
