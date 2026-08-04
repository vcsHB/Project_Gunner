public interface ICastEffector
{
    public void Initialize();

    /// <summary>
    /// 대상 하나에 효과를 적용한다.
    /// 생존 여부, 자기 자신 제외, 히트박스 중복 제거는 CasterBase가 이미 처리한 상태로 들어온다.
    /// </summary>
    public void Cast(in CastHit hit);
}
