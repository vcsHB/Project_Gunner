using UnityEngine;

/// <summary>
/// 재료. 강철·회로·고분자·연료 전지 같은 것과 <b>고철</b>. 잔해에서 나오고, 분해하면 나오고, 나중에 제작에 들어간다.
///
/// 고철도 이 타입이다 — 분해할 수 있게(<see cref="ItemDataSO.CanDisassemble"/>) 켜고 재료 구성(<see cref="ItemDataSO.Composition"/>)을 적은 재료일 뿐이다.
/// 타입을 따로 두지 않는 이유: 분해는 재료만의 일이 아니다(나중에 파츠·무기도 분해된다). 판정은 데이터로 한다.
///
/// Id 대역: 7000~7999 (<see cref="ItemDatabaseSO"/>).
/// </summary>
[CreateAssetMenu(menuName = "SO/Item/MaterialData")]
public class MaterialDataSO : ItemDataSO
{
    /// <summary>재료는 개체마다 다른 상태가 없다. 겹쳐 쌓인다.</summary>
    public override bool RequiresInstance => false;
}
