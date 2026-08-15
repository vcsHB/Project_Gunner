/// <summary>
/// 게임 판정에 쓰는 난수원.
///
/// UnityEngine.Random은 프로세스 전역 상태라 클라이언트마다 결과가 갈린다.
/// 멀티에서 "내 화면에선 크리티컬이 떴는데 서버에선 안 떴다"가 되는 자리라,
/// 전투 판정과 절차적 생성은 전부 이 인터페이스를 통해 시드를 통제한다.
///
/// 재현은 <b>시드가 같고 호출 순서가 같을 때만</b> 보장된다.
/// 연출용 난수(파티클, 흩뿌리기)를 같은 소스에서 뽑으면 순서가 깨지므로 섞지 말 것.
/// </summary>
public interface IRandomSource
{
    /// <summary>0 이상 1 미만.</summary>
    float Value { get; }

    float Range(float min, float max);

    /// <summary>max는 포함하지 않는다. UnityEngine.Random.Range(int)와 같은 규칙이다.</summary>
    int Range(int minInclusive, int maxExclusive);
}
