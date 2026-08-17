using UnityEditor;
using UnityEngine;

/// <summary>
/// 씬에 흩어진 LocalizedText를 한 번에 다루는 메뉴.
/// 텍스트 하나하나 눌러가며 등록하면 반드시 빠뜨린다.
/// </summary>
public static class LocalizationMenu
{
    private const string MenuRoot = "Tools/Localization/";

    [MenuItem(MenuRoot + "키 없는 텍스트에 키 채우기")]
    private static void FillMissingKeys()
    {
        LocalizedText[] targets = FindAll();
        int filled = 0;

        foreach (LocalizedText localized in targets)
        {
            if (!string.IsNullOrEmpty(localized.Key)) continue;

            Undo.RecordObject(localized, "Fill Localization Key");
            localized.SetKey(LocalizationEditorUtil.BuildKeyFromHierarchy(localized.transform));
            EditorUtility.SetDirty(localized);
            filled++;
        }

        Debug.Log($"[Localization] 키가 없던 {filled}개에 키를 채웠습니다. (전체 {targets.Length}개)");
    }

    [MenuItem(MenuRoot + "씬 텍스트 표에 일괄 등록")]
    private static void RegisterAll()
    {
        LocalizationTableSO table = LocalizationEditorUtil.FindTable();
        if (table == null)
        {
            Debug.LogError("[Localization] LocalizationTable 에셋이 없습니다. " +
                           "SO/Localization/LocalizationTable로 만들어 Resources에 두세요.");
            return;
        }

        LocalizedText[] targets = FindAll();
        int written = 0;
        int skipped = 0;

        foreach (LocalizedText localized in targets)
        {
            if (string.IsNullOrEmpty(localized.Key))
            {
                skipped++;
                continue;
            }

            // 원문이 비어 있으면 행을 만들지 않는다. 번역할 게 없는 빈 행은 시트를 어지럽힐 뿐이다.
            if (Write(table, localized.Key, localized.SourceText))
                written++;
        }

        AssetDatabase.SaveAssets();

        string tail = skipped > 0 ? $" 키가 없어 건너뛴 것 {skipped}개." : string.Empty;
        Debug.Log($"[Localization] {written}개를 표에 등록했습니다.{tail}", table);
    }

    /// <summary>
    /// 아이템의 이름·설명을 표에 넣는다. 아이템은 씬이 아니라 에셋에 있어서
    /// 씬을 훑는 등록으로는 절대 걸리지 않는다.
    /// </summary>
    [MenuItem(MenuRoot + "아이템 이름·설명 표에 일괄 등록")]
    private static void RegisterItems()
    {
        LocalizationTableSO table = LocalizationEditorUtil.FindTable();
        if (table == null)
        {
            Debug.LogError("[Localization] LocalizationTable 에셋이 없습니다.");
            return;
        }

        int names = 0;
        int descriptions = 0;
        int noKey = 0;

        foreach (ItemDataSO item in ItemDatabaseBuilder.LoadAllItemAssets())
        {
            if (item == null) continue;

            if (string.IsNullOrEmpty(item.LocalizationKey))
            {
                noKey++;
                continue;
            }

            // 한국어 원문이 비어 있으면 행을 만들지 않는다. 번역할 게 없는 빈 행은 시트를 어지럽힐 뿐이다.
            if (Write(table, item.NameKey, item.FallbackName)) names++;
            if (Write(table, item.DescriptionKey, item.RawDescription)) descriptions++;
        }

        AssetDatabase.SaveAssets();

        string tail = noKey > 0
            ? $" 키가 없어 건너뛴 것 {noKey}개 — Item Creator에서 먼저 채우세요."
            : string.Empty;

        Debug.Log($"[Localization] 아이템 이름 {names}개, 설명 {descriptions}개를 표에 등록했습니다.{tail}", table);
    }

    private static bool Write(LocalizationTableSO table, string key, string value)
        => !string.IsNullOrEmpty(value)
           && LocalizationEditorUtil.Write(table, key, LanguageType.Korean, value);

    [MenuItem(MenuRoot + "언어/한국어")]
    private static void SetKorean() => SetLanguage(LanguageType.Korean);

    [MenuItem(MenuRoot + "언어/English")]
    private static void SetEnglish() => SetLanguage(LanguageType.English);

    /// <summary>
    /// 에디터에서 언어를 바꿔 확인한다.
    /// 에디트 모드에서는 컴포넌트가 이벤트를 구독하고 있지 않을 수 있어 직접 한 번 더 훑는다.
    /// </summary>
    private static void SetLanguage(LanguageType language)
    {
        Localization.SetLanguage(language);

        foreach (LocalizedText localized in FindAll())
            localized.Refresh();

        Debug.Log($"[Localization] 언어를 {language}로 바꿨습니다.");
    }

    private static LocalizedText[] FindAll()
        => Object.FindObjectsByType<LocalizedText>(FindObjectsInactive.Include, FindObjectsSortMode.None);
}
