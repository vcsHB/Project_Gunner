/// <summary>
/// 지원 언어. 표의 값 배열 인덱스로 쓰므로 <b>0부터 빈틈없이 이어져야 한다.</b>
/// 중간에 끼워 넣지 말고 뒤에 추가할 것 — 이미 저장된 표의 값이 통째로 밀린다.
///
/// 구글 시트의 열 이름과 1:1로 맞춘다.
/// </summary>
public enum LanguageType
{
    Korean = 0,
    English,
}
