using UnityEngine;

/// <summary>
/// 동작이 필요한 파츠의 런타임 표현. 프리팹으로 만들어 WeaponPartDataSO에 등록한다.
///
/// 스탯 변화는 여기서 건드리지 않는다. WeaponModController가 파츠 종류와 무관하게
/// 한 곳에서 처리한다. 여기는 동작과 비주얼만 담당한다.
/// </summary>
public abstract class WeaponPartBehaviour : MonoBehaviour
{
    public WeaponPartDataSO Data { get; private set; }
    public PlayerWeaponBase Weapon { get; private set; }

    /// <summary>오버라이드할 때 base를 먼저 호출할 것.</summary>
    public virtual void OnAttached(PlayerWeaponBase weapon, WeaponPartDataSO data)
    {
        Weapon = weapon;
        Data = data;
    }

    /// <summary>이 직후 오브젝트가 파괴된다. 구독 해제 등을 여기서.</summary>
    public virtual void OnDetached()
    {
    }
}
