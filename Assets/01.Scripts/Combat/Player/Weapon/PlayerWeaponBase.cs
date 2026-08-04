using UnityEngine;

public abstract class PlayerWeaponBase : MonoBehaviour
{
    public PlayerWeaponDataSO Data { get; private set; }

    /// <summary>이 무기 인스턴스만의 스탯. 파츠 modifier가 여기에 붙는다.</summary>
    public WeaponStatus Status { get; private set; }

    /// <summary>이 무기를 들고 있는 주체.</summary>
    public Agent Owner { get; private set; }

    public bool IsEquipped { get; private set; }

    /// <summary>
    /// 스폰 직후 한 번. 장착/해제와는 별개다.
    /// 오버라이드할 때 반드시 base를 먼저 호출할 것.
    /// </summary>
    public virtual void Initialize(PlayerWeaponDataSO data, Agent owner)
    {
        Data = data;
        Owner = owner;
        Status = new WeaponStatus(data.Stats);
    }

    /// <summary>슬롯에 장착되어 손에 들렸을 때.</summary>
    public virtual void OnEquipped()
    {
        IsEquipped = true;
    }

    /// <summary>슬롯에서 내려놓았을 때. 걸어둔 modifier나 예약된 상태를 여기서 되돌린다.</summary>
    public virtual void OnUnequipped()
    {
        IsEquipped = false;
    }
}
