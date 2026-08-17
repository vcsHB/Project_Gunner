using System;
using UnityEngine;

/// <summary>
/// 같은 아이템이라도 개체마다 달라지는 상태.
/// ItemStack은 (Id, 개수)뿐이라 모딩된 무기 두 정을 구분하지 못한다. 그 구분을 여기가 담당한다.
///
/// 겹치는 아이템(자원 등)은 인스턴스를 만들지 않는다. 수천 개에 객체를 붙일 이유가 없다.
/// </summary>
public abstract class ItemInstance
{
    /// <summary>(현재, 최대). 내구도가 바뀌면 슬롯 표시가 따라가야 한다.</summary>
    public event Action<int, int> OnDurabilityChangedEvent;

    private int _durability = -1;

    public uint InstanceId { get; internal set; }
    public uint ItemId { get; internal set; }

    public ItemDataSO Data
        => ItemDatabaseSO.Instance != null && ItemDatabaseSO.Instance.TryGet(ItemId, out ItemDataSO data)
            ? data
            : null;

    public int MaxDurability
    {
        get
        {
            ItemDataSO data = Data;
            return data != null ? data.MaxDurability : 0;
        }
    }

    /// <summary>
    /// 남은 내구도. 내구도가 없는 아이템은 항상 0이다.
    ///
    /// -1로 시작해서 처음 읽을 때 최대값으로 채운다. 생성 시점에는 아직 ItemId가
    /// 붙기 전이라 최대값을 알 수 없다(레지스트리가 나중에 넣어준다).
    /// </summary>
    public int Durability
    {
        get
        {
            if (_durability < 0)
                _durability = MaxDurability;

            return _durability;
        }
    }

    public bool HasDurability => MaxDurability > 0;

    /// <summary>내구도가 있는 아이템인데 다 닳았는지.</summary>
    public bool IsBroken => HasDurability && Durability <= 0;

    public float DurabilityNormalized
    {
        get
        {
            int max = MaxDurability;
            return max <= 0 ? 1f : Mathf.Clamp01((float)Durability / max);
        }
    }

    /// <summary>
    /// 내구도를 깎는다. 실제로 깎였으면 true.
    /// 내구도가 없는 아이템은 아무 일도 하지 않고 true를 돌려준다 — 닳지 않는 것이지 실패가 아니다.
    /// </summary>
    public bool ConsumeDurability(int amount = 1)
    {
        if (!HasDurability || amount <= 0) return true;
        if (Durability <= 0) return false;

        SetDurability(Durability - amount);
        return true;
    }

    public void RepairFull() => SetDurability(MaxDurability);

    /// <summary>세이브 복원에도 이걸 쓴다.</summary>
    public void SetDurability(int value)
    {
        int max = MaxDurability;
        int clamped = Mathf.Clamp(value, 0, max);

        if (_durability == clamped) return;

        _durability = clamped;
        OnDurabilityChangedEvent?.Invoke(clamped, max);
    }
}
