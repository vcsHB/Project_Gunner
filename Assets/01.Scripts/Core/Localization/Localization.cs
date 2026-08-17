using System;
using UnityEngine;

/// <summary>
/// 화면에 나가는 문자열의 단일 창구.
///
/// 표가 없어도 지금부터 이걸 통해서 읽습니다. 표를 갈아끼워도 호출부는 바뀌지 않습니다.
/// 반대로 키를 나중에 끼우려 하면 모든 호출부를 다시 찾아야 합니다.
///
/// 정적이라 플레이 모드를 나가도 표와 언어가 남습니다. 도메인 리로드가 정리하지만,
/// 리로드를 끄고 쓰는 경우 새 게임에서 다시 지정하세요.
/// </summary>
public static class Localization
{
    /// <summary>Resources에 둘 표 에셋 이름.</summary>
    public const string TableResourceName = "LocalizationTable";

    private static ILocalizationTable s_table;
    private static bool s_loadAttempted;

    /// <summary>표나 언어가 갈렸을 때. 떠 있는 UI는 다시 그려야 합니다.</summary>
    public static event Action OnChangedEvent;

    public static LanguageType Language { get; private set; } = LanguageType.Korean;

    /// <summary>
    /// 지정된 표가 없으면 Resources에서 한 번 찾아본다.
    /// ItemDatabase와 같은 방식이라 부트스트랩 코드를 따로 두지 않아도 된다.
    /// </summary>
    public static ILocalizationTable Table
    {
        get
        {
            if (s_table != null || s_loadAttempted) return s_table;

            s_loadAttempted = true;
            s_table = ResourceLocator.LoadSingle<LocalizationTableSO>(TableResourceName);

            return s_table;
        }
    }

    /// <summary>시트에서 받아온 표로 갈아끼운다. null이면 다음 조회에서 Resources를 다시 찾는다.</summary>
    public static void SetTable(ILocalizationTable table)
    {
        s_table = table;
        s_loadAttempted = table != null;

        OnChangedEvent?.Invoke();
    }

    public static void SetLanguage(LanguageType language)
    {
        if (Language == language) return;

        Language = language;
        OnChangedEvent?.Invoke();
    }

    /// <summary>
    /// 키에 해당하는 문자열. 키가 없거나 표에 없으면 fallback을 돌려줍니다.
    ///
    /// 화면에 키 문자열이 그대로 뜨는 것보다 원문이라도 보이는 편이 훨씬 낫습니다.
    /// 빠진 키는 에디터 검증이 잡습니다.
    /// </summary>
    public static string Get(string key, string fallback)
    {
        if (string.IsNullOrEmpty(key)) return fallback;

        ILocalizationTable table = Table;
        if (table == null) return fallback;

        // 시트에 행은 있는데 그 언어 칸만 비어 있는 경우가 흔하다. 그때도 폴백으로 내려간다.
        return table.TryGet(key, Language, out string value) && !string.IsNullOrEmpty(value)
            ? value
            : fallback;
    }

    /// <summary>
    /// 값이 끼어드는 문구. 표에는 <b>포맷 문자열</b>이 들어간다. ("남은 시간 {0:0.0}초")
    ///
    /// 코드에서 문자열을 이어붙이면("남은 시간 " + t + "초") 번역이 아예 불가능해진다.
    /// 언어마다 어순도 단위 위치도 다르기 때문이다. 조각이 아니라 문장 전체를 번역하게 한다.
    /// </summary>
    public static string Format(string key, string fallbackFormat, object arg0)
        => FormatInternal(Get(key, fallbackFormat), arg0);

    public static string Format(string key, string fallbackFormat, params object[] args)
        => FormatInternal(Get(key, fallbackFormat), args);

    private static string FormatInternal(string format, params object[] args)
    {
        if (string.IsNullOrEmpty(format)) return string.Empty;

        try
        {
            return string.Format(format, args);
        }
        catch (FormatException)
        {
            // 번역자가 중괄호를 깨먹는 일은 반드시 생긴다. 게임이 죽는 것보다 원문이 뜨는 게 낫다.
            Debug.LogError($"[Localization] 포맷이 잘못되었습니다: \"{format}\"");
            return format;
        }
    }
}
