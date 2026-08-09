using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Inventory를 Agent에 붙여주는 얇은 래퍼. 로직은 전부 Inventory에 있다.
/// 칸 수는 AgentStatus의 InventorySize를 따라간다.
/// </summary>
public class InventoryController : MonoBehaviour, IAgentComponent
{
    [Tooltip("AgentStatus가 없을 때 쓸 칸 수.")]
    [SerializeField, Min(1)] private int _fallbackCapacity = 30;

    public Inventory Inventory { get; private set; }
    public Agent Owner { get; private set; }

    private AgentStatus _status;
    private readonly List<ItemStack> _overflowBuffer = new();

    public void Initialize(Agent owner)
    {
        Owner = owner;
    }

    public void AfterInitialize()
    {
        _status = Owner.GetCompo<AgentStatus>();

        int capacity = _status != null ? _status.InventorySize.TotalValue : _fallbackCapacity;
        Inventory = new Inventory(ClampCapacity(capacity));

        if (_status != null)
            _status.InventorySize.OnChangedEvent += HandleCapacityChanged;
    }

    /// <summary>
    /// 앞 7칸은 핫바라 절대 줄어들면 안 된다.
    /// 배낭을 벗어서 스탯이 그 아래로 내려가도 손에 든 것까지 사라지지는 않게 한다.
    /// </summary>
    private static int ClampCapacity(int capacity) => Mathf.Max(InventoryLayout.HotbarSize, capacity);

    public void Dispose()
    {
        if (_status != null)
            _status.InventorySize.OnChangedEvent -= HandleCapacityChanged;
    }

    private void HandleCapacityChanged(int newCapacity)
    {
        _overflowBuffer.Clear();
        Inventory.Resize(ClampCapacity(newCapacity), _overflowBuffer);

        if (_overflowBuffer.Count == 0) return;

        // 칸이 줄어서 자리를 잃은 아이템은 발밑에 떨군다. 조용히 사라지면 안 된다.
        for (int i = 0; i < _overflowBuffer.Count; i++)
            WorldItemSpawner.Spawn(_overflowBuffer[i], transform.position);
    }
}
