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
    [field: SerializeField] public Sprite IconSprite { get; private set; }

    [Tooltip("한 칸에 겹칠 수 있는 최대 개수. 무기처럼 개별 상태를 갖는 것은 1로 둔다.")]
    [SerializeField, Min(1)] private int _maxStackCount = 1;

    public string itemName; // TODO...for long future. Localization.
    [TextArea] public string itemDescription;

    public int MaxStackCount => _maxStackCount;
    public bool IsStackable => _maxStackCount > 1;

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
