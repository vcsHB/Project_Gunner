/// <summary>
/// 키 → 표시 문자열 조회. 구글 스프레드시트에서 받아온 표가 이걸 구현해서 끼워집니다.
///
/// 인터페이스로 둔 이유는 출처가 실제로 갈릴 것이기 때문입니다 —
/// 프로젝트에 구워둔 표, 시트에서 받아온 표, 테스트용 더미가 같은 자리에 들어갑니다.
/// </summary>
public interface ILocalizationTable
{
    /// <summary>키와 언어에 해당하는 문자열. 없으면 false.</summary>
    bool TryGet(string key, LanguageType language, out string value);
}
