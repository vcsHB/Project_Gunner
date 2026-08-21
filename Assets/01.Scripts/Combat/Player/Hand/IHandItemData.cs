/// <summary>
/// 손에 들 수 있는 아이템 데이터. 들었을 때 만들어질 프리팹을 알려준다.
///
/// 인터페이스인 이유는 무기(<see cref="PlayerWeaponDataSO"/>)와 소모품(<see cref="ConsumableDataSO"/>)이
/// 상속 계보가 이미 갈려 있기 때문이다. 공통 베이스로 올리면 손에 들 일이 없는
/// 자원·블럭까지 프리팹 칸을 갖게 된다.
/// </summary>
public interface IHandItemData
{
    /// <summary>손에 만들어질 프리팹. null이면 컨트롤러의 기본 손을 쓴다.</summary>
    HandActionBase HandPrefab { get; }
}
