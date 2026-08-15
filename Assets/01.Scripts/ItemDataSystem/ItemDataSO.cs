using UnityEngine;

/// <summary>
/// 게임에 들어가는 모든 데이터의 공통 베이스. 무기·블럭·자원·유닛 전부 여기서 파생된다.
/// 식별은 Id로만 한다. 세이브/인벤토리/네트워크에 나가는 키가 이것이다.
/// </summary>
public abstract class ItemDataSO : ScriptableObject
{
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

    public string itemName; // TODO...for long future. Localization.
    [TextArea] public string itemDescription;

    public int MaxStackCount => _maxStackCount;
    public bool IsStackable => _maxStackCount > 1;

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

    /// <summary>
    /// 개체마다 다른 상태를 갖는 아이템인지. true면 인벤토리에서 한 칸을 혼자 쓴다.
    /// </summary>
    public virtual bool RequiresInstance => false;

    /// <summary>RequiresInstance가 true인 아이템만 구현한다.</summary>
    public virtual ItemInstance CreateRuntimeInstance() => null;

    public uint Id => _id;
    public bool IsRegistered => _id != 0;

    /// <summary>비어있으면 에셋 이름을 쓴다.</summary>
    public string DisplayName => string.IsNullOrEmpty(itemName) ? name : itemName;
}
