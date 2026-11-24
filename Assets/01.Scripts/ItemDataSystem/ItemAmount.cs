using UnityEngine;

/// <summary>"무엇을 몇 개". 재료 구성·분해 결과·레시피가 쓴다. 확률이 끼면 <see cref="LootEntry"/>.</summary>
[System.Serializable]
public struct ItemAmount
{
    public ItemDataSO item;
    [Min(1)] public int count;

    public bool IsValid => item != null && count > 0;
}
