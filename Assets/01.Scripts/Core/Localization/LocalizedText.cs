using TMPro;
using UnityEngine;

/// <summary>
/// 화면에 박히는 고정 문구를 언어에 따라 갈아끼운다. TMP 텍스트에 붙여 쓴다.
///
/// 원문(<see cref="SourceText"/>)을 함께 들고 있는 이유는 두 가지다.
/// 표에 키가 없을 때 화면에 키 문자열이 뜨는 대신 원문이 뜨고,
/// 에디터에서 "이 텍스트를 표에 등록"할 때 그 값이 그대로 한국어 칸으로 들어간다.
///
/// 문구가 런타임에 바뀌는 텍스트(잔탄, 남은 시간)에는 붙이지 않는다.
/// 그런 것은 코드가 매번 <see cref="Localization.Get"/>으로 만들어 넣는다.
/// </summary>
[DisallowMultipleComponent]
public class LocalizedText : MonoBehaviour
{
    [Tooltip("표에서 찾을 키. 비우면 원문이 그대로 표시된다.")]
    [SerializeField] private string _key;

    [Tooltip("표에 키가 없을 때 쓸 원문. 한국어 기준이다.")]
    [TextArea]
    [SerializeField] private string _sourceText;

    [Tooltip("비우면 같은 오브젝트의 TMP를 찾는다.")]
    [SerializeField] private TMP_Text _target;

    public string Key => _key;
    public string SourceText => _sourceText;

    public TMP_Text Target
    {
        get
        {
            if (_target == null)
                _target = GetComponent<TMP_Text>();

            return _target;
        }
    }

    // 컴포넌트를 붙이는 순간의 텍스트가 곧 원문이다. 따로 옮겨 적게 하면 반드시 빠뜨린다.
    private void Reset()
    {
        _target = GetComponent<TMP_Text>();

        if (_target != null && string.IsNullOrEmpty(_sourceText))
            _sourceText = _target.text;
    }

    private void OnEnable()
    {
        Localization.OnChangedEvent += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        Localization.OnChangedEvent -= Refresh;
    }

    public void Refresh()
    {
        TMP_Text target = Target;
        if (target == null) return;

        string value = Localization.Get(_key, _sourceText);

        // 같은 값을 다시 넣으면 TMP가 메시를 다시 만든다. 에디터에서는 씬까지 더러워진다.
        if (target.text != value)
            target.text = value;
    }

    /// <summary>에디터 툴이 키를 채워 넣을 때 쓴다.</summary>
    public void SetKey(string key)
    {
        _key = key;
        Refresh();
    }

#if UNITY_EDITOR
    // 인스펙터에서 키를 바꾸면 씬에서 바로 보이게 한다.
    private void OnValidate()
    {
        if (_target == null)
            _target = GetComponent<TMP_Text>();

        Refresh();
    }
#endif
}
