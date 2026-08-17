using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 프로젝트에 구워두는 문자열 표. Resources에 "LocalizationTable" 이름으로 하나만 둔다.
///
/// 지금은 이게 유일한 출처이고, 나중에 구글 시트에서 받아온 표를
/// <see cref="Localization.SetTable"/>로 덮어씌우면 된다. 그때 이 에셋은
/// 오프라인/시트 실패 시의 기본값으로 남는다.
/// </summary>
[CreateAssetMenu(fileName = Localization.TableResourceName, menuName = "SO/Localization/LocalizationTable")]
public class LocalizationTableSO : ScriptableObject, ILocalizationTable
{
    private static readonly int LanguageCount = Enum.GetValues(typeof(LanguageType)).Length;

    [Serializable]
    public class Entry
    {
        public string key;

        [Tooltip("LanguageType 순서대로. 0번이 한국어다.")]
        [TextArea] public string[] values = new string[LanguageCount];

        public string Get(LanguageType language)
        {
            int index = (int)language;

            return values != null && index >= 0 && index < values.Length ? values[index] : null;
        }
    }

    [SerializeField] private List<Entry> _entries = new();

    // 키가 수백 개가 되면 매번 순회할 수 없다. 처음 조회할 때 한 번만 만든다.
    private Dictionary<string, Entry> _byKey;

    public IReadOnlyList<Entry> Entries => _entries;

    // 에셋을 고치면 캐시가 낡는다. 인스펙터 편집과 도메인 리로드 양쪽에서 버린다.
    private void OnEnable() => _byKey = null;
    private void OnValidate() => _byKey = null;

    public bool TryGet(string key, LanguageType language, out string value)
    {
        value = null;
        if (string.IsNullOrEmpty(key)) return false;

        EnsureMap();

        if (!_byKey.TryGetValue(key, out Entry entry)) return false;

        value = entry.Get(language);
        return !string.IsNullOrEmpty(value);
    }

    public bool Contains(string key) => !string.IsNullOrEmpty(key) && IndexOf(key) >= 0;

    public int IndexOf(string key)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i] != null && _entries[i].key == key)
                return i;
        }

        return -1;
    }

    private void EnsureMap()
    {
        if (_byKey != null) return;

        _byKey = new Dictionary<string, Entry>(_entries.Count);

        for (int i = 0; i < _entries.Count; i++)
        {
            Entry entry = _entries[i];
            if (entry == null || string.IsNullOrEmpty(entry.key)) continue;

            // 같은 키가 두 줄이면 어느 쪽이 나올지 알 수 없다. 첫 줄을 쓰고 알린다.
            if (!_byKey.TryAdd(entry.key, entry))
                Debug.LogError($"[Localization] 키 '{entry.key}'가 중복입니다.", this);
        }
    }
}
