using UnityEngine;

public abstract class ItemDataSO : ScriptableObject
{
    [field: SerializeField] public ItemType ItemType { get; private set; }
    [field: SerializeField] public ItemGradeType ItemGrade { get; private set; }
    [field: SerializeField] public ItemCategoryType ItemCategory { get; private set; }
    [field: SerializeField] public Sprite IconSprite { get; private set; }

    public string itemName; // TODO...for long future. Localization.
    [TextArea] public string itemDescription;



    internal void SetId(uint newId)
    {

    }
}