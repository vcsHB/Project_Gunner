/// <summary>
/// 팔에 달 수 있는 아이템 데이터. 달았을 때 만들어질 프리팹을 알려준다.
///
/// 인터페이스인 이유는 무기와 도구가 상속 계보를 공유하지 않기 때문이다.
/// 공통 베이스로 올리면 팔에 달 일이 없는 자원·블럭까지 프리팹 칸을 갖게 된다.
/// </summary>
public interface IHandItemData
{
    /// <summary>팔에 만들어질 프리팹. 없으면 그 아이템은 달 수 없다.</summary>
    HandActionBase HandPrefab { get; }
}
