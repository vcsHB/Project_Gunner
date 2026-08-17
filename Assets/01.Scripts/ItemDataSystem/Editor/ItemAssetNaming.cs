using System;
using System.Text;
using UnityEditor;

/// <summary>
/// 아이템 에셋 이름 규칙: Item_{종류}_{이름}
/// 예) PlayerWeaponDataSO + "Rusty Rifle" -> Item_PlayerWeapon_RustyRifle
/// </summary>
public static class ItemAssetNaming
{
    public const string Prefix = "Item";
    public const char Separator = '_';

    /// <summary>로컬라이즈 키의 맨 앞. 시트에서 아이템 행을 한 덩어리로 모아준다.</summary>
    public const string LocalizationPrefix = "item";

    /// <summary>SO 타입명에서 종류 부분을 뽑는다. PlayerWeaponDataSO -> PlayerWeapon</summary>
    public static string GetKindName(Type type)
    {
        string name = type.Name;

        if (name.EndsWith("DataSO", StringComparison.Ordinal))
            return name.Substring(0, name.Length - "DataSO".Length);

        if (name.EndsWith("SO", StringComparison.Ordinal))
            return name.Substring(0, name.Length - "SO".Length);

        return name;
    }

    public static string BuildAssetName(Type type, string itemName)
        => $"{Prefix}{Separator}{GetKindName(type)}{Separator}{Sanitize(itemName)}";

    /// <summary>
    /// 이 에셋이 가져야 할 이름. 표시 이름이 비어있으면 현재 파일명에서 규칙 부분을 걷어내고 쓴다.
    /// </summary>
    public static string BuildAssetName(ItemDataSO item)
        => BuildAssetName(item.GetType(), GetNameSource(item));

    /// <summary>
    /// 로컬라이즈 키. item.{종류}.{식별자} — 전부 소문자다.
    ///
    /// 만들 때 한 번 채워 넣고 그 뒤로는 <b>따라 바뀌지 않는다.</b>
    /// 에셋 이름에서 매번 계산하면 이름을 바꾸는 순간 시트 행이 고아가 된다.
    /// </summary>
    public static string BuildLocalizationKey(Type type, string editorName)
        => $"{LocalizationPrefix}.{GetKindName(type).ToLowerInvariant()}.{Sanitize(editorName).ToLowerInvariant()}";

    public static string BuildLocalizationKey(ItemDataSO item)
        => BuildLocalizationKey(item.GetType(), GetNameSource(item));

    /// <summary>이름을 만들 재료. 식별자가 비어 있으면 에셋 이름에서 규칙 부분을 걷어내고 쓴다.</summary>
    private static string GetNameSource(ItemDataSO item)
        => string.IsNullOrEmpty(item.RawEditorName) ? StripPrefix(item) : item.RawEditorName;

    /// <summary>파일명에서 공백과 경로에 못 쓰는 문자를 걷어낸다.</summary>
    public static string Sanitize(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;

        StringBuilder builder = new();
        foreach (char c in raw)
        {
            if (char.IsLetterOrDigit(c))
                builder.Append(c);
        }

        return builder.ToString();
    }

    public static bool IsValidName(string raw) => !string.IsNullOrEmpty(Sanitize(raw));

    private static string StripPrefix(ItemDataSO item)
    {
        string expected = $"{Prefix}{Separator}{GetKindName(item.GetType())}{Separator}";

        return item.name.StartsWith(expected, StringComparison.Ordinal)
            ? item.name.Substring(expected.Length)
            : item.name;
    }

    /// <summary>이름 규칙에 이미 맞는지.</summary>
    public static bool IsConventional(ItemDataSO item)
        => item.name == BuildAssetName(item);

    public static string GetFolder(string rootFolder, Type type)
        => $"{rootFolder}/{GetKindName(type)}";

    public static string GetAssetPath(string rootFolder, Type type, string itemName)
        => $"{GetFolder(rootFolder, type)}/{BuildAssetName(type, itemName)}.asset";

    /// <summary>Assets/A/B/C 형태의 폴더를 없으면 만든다.</summary>
    public static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;

        string[] parts = folder.Split('/');
        string current = parts[0]; // "Assets"

        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";

            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }
    }
}
