using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 게임에 들어가는 모든 데이터의 공통 베이스. 무기·블럭·자원·유닛 전부 여기서 파생된다.
/// 식별은 Id로만 한다. 세이브/인벤토리/네트워크에 나가는 키가 이것이다.
/// </summary>
public abstract class ItemDataSO : ScriptableObject
{
    /// <summary>로컬라이즈 키에 붙는 접미사. 시트의 행 이름이 된다.</summary>
    public const string NameKeySuffix = ".name";
    public const string DescriptionKeySuffix = ".desc";

    // Id는 ItemDatabase가 부여하고 한 번 부여되면 재사용하지 않는다.
    // 0은 "아직 등록 안 됨"을 뜻한다. 직접 수정하지 말 것.
    [SerializeField] private uint _id;

    [field: SerializeField] public ItemGradeType ItemGrade { get; private set; }
    [field: SerializeField] public ItemCategoryType ItemCategory { get; private set; }

    [Tooltip("인벤토리·핫바 등 UI에 그려지는 아이콘. 칸에 맞춰 그리므로 여백이 있어도 된다.")]
    [field: SerializeField] public Sprite IconSprite { get; private set; }

    [Tooltip("바닥에 떨어졌을 때 월드에 그려지는 스프라이트. " +
             "비워두면 IconSprite를 쓴다. UI 아이콘과 픽셀 밀도·피벗이 다를 때만 채운다.")]
    [SerializeField] private Sprite _worldSprite;

    [Tooltip("한 칸에 겹칠 수 있는 최대 개수. 무기처럼 개별 상태를 갖는 것은 1로 둔다.")]
    [SerializeField, Min(1)] private int _maxStackCount = 1;

    [Tooltip("최대 내구도. 0이면 닳지 않는 아이템이다. 0보다 크면 개체 상태가 필요하다.")]
    [SerializeField, Min(0)] private int _maxDurability;

    [Header("Naming")]
    [Tooltip("에디터에서 이 아이템을 알아보기 위한 식별자. 에셋 이름 규칙의 재료다. " +
             "화면에는 나오지 않으므로 영문으로 짧게 쓴다.")]
    [FormerlySerializedAs("itemName")]
    [SerializeField] private string _editorName;

    [Tooltip("화면에 표시될 이름. 로컬라이즈 시트가 로드되면 그쪽이 우선한다.")]
    [SerializeField] private string _displayName;

    [Tooltip("화면에 표시될 설명. 로컬라이즈 시트가 로드되면 그쪽이 우선한다.")]
    [TextArea]
    [FormerlySerializedAs("itemDescription")]
    [SerializeField] private string _description;

    [Tooltip("로컬라이즈 시트의 행을 찾는 키. .name/.desc 접미사는 코드가 붙인다. " +
             "Item Creator가 채워주고, 한 번 정해지면 에셋 이름을 바꿔도 따라 바뀌지 않는다.")]
    [SerializeField] private string _localizationKey;

    public int MaxStackCount => _maxStackCount;
    public bool IsStackable => _maxStackCount > 1;

    public int MaxDurability => _maxDurability;
    public bool HasDurability => _maxDurability > 0;

    /// <summary>
    /// 개체마다 다른 상태를 갖는 아이템인지. true면 인벤토리에서 한 칸을 혼자 쓴다.
    ///
    /// 내구도가 있으면 개체마다 남은 값이 달라야 하므로 자동으로 true가 된다.
    /// 파생 타입이 다른 이유로 true를 원하면 이 위에 얹어서 오버라이드한다.
    /// </summary>
    public virtual bool RequiresInstance => HasDurability;

    /// <summary>
    /// 월드에 놓였을 때 쓸 스프라이트. 따로 지정하지 않았으면 UI 아이콘을 그대로 쓴다.
    ///
    /// 아이콘 하나로 충분한 아이템이 대부분이라 필수로 두지 않는다.
    /// 다만 <b>월드 표시는 반드시 이 프로퍼티를 통해야 한다.</b>
    /// IconSprite를 직접 읽는 곳이 생기면 전용 스프라이트를 지정해도 그 경로만 조용히 안 바뀐다.
    /// </summary>
    public Sprite WorldSprite => _worldSprite != null ? _worldSprite : IconSprite;

    /// <summary>월드용 스프라이트를 따로 갖고 있는지. 에디터 검증용.</summary>
    public bool HasDedicatedWorldSprite => _worldSprite != null;

    /// <summary>개체 상태가 필요한 아이템이 자기 타입에 맞는 인스턴스를 만든다.</summary>
    public virtual ItemInstance CreateRuntimeInstance() => new PlainItemInstance();

    public uint Id => _id;
    public bool IsRegistered => _id != 0;

    #region Naming

    /// <summary>인스펙터에 적힌 식별자 그대로. 비어있을 수 있다. 에셋 이름 규칙이 쓴다.</summary>
    public string RawEditorName => _editorName;

    /// <summary>에디터 식별자. 비어있으면 에셋 이름을 쓴다. 로그에 찍는 이름이다.</summary>
    public string EditorName => string.IsNullOrEmpty(_editorName) ? name : _editorName;

    public string LocalizationKey => _localizationKey;

    public string NameKey
        => string.IsNullOrEmpty(_localizationKey) ? string.Empty : _localizationKey + NameKeySuffix;

    public string DescriptionKey
        => string.IsNullOrEmpty(_localizationKey) ? string.Empty : _localizationKey + DescriptionKeySuffix;

    /// <summary>
    /// 화면에 쓸 이름. 시트가 로드되어 있으면 그쪽이 우선한다.
    ///
    /// 시트가 없거나 키가 비었으면 기본 표시명 → 에디터 식별자 → 에셋 이름 순으로 내려간다.
    /// 어느 단계에서도 빈 문자열이 나오지 않아야 UI에 이름 없는 칸이 생기지 않는다.
    /// </summary>
    public string DisplayName => Localization.Get(NameKey, FallbackName);

    public string Description => Localization.Get(DescriptionKey, _description);

    /// <summary>
    /// 표가 없을 때 보일 이름. 표에 등록할 한국어 값이기도 하다.
    ///
    /// 등록할 때 <see cref="DisplayName"/>을 쓰면 안 된다 — 그건 이미 표를 거친 값이라,
    /// 영어로 보고 있는 중이면 영어가 한국어 칸에 들어간다.
    /// </summary>
    public string FallbackName => string.IsNullOrEmpty(_displayName) ? EditorName : _displayName;

    /// <summary>인스펙터에 적힌 설명 그대로. 로컬라이즈를 타지 않는다.</summary>
    public string RawDescription => _description;

    #endregion
}
