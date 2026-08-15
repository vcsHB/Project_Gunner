using UnityEngine;

/// <summary>
/// 바닥에 떨어진 아이템. 닿으면 인벤토리로 들어간다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class WorldItem : PoolableMono
{
    [SerializeField] private SpriteRenderer _renderer;

    [Tooltip("떨군 직후 바로 다시 줍지 않도록 하는 시간.")]
    [SerializeField] private float _pickupDelay = 0.5f;

    [Tooltip("끄면 지나가도 줍지 않는다. 상호작용 키로만 줍게 할 때 쓴다.")]
    [SerializeField] private bool _autoPickup = true;

    public ItemStack Stack { get; private set; }

    private float _spawnTime;

    public bool CanPickup => _autoPickup && !Stack.IsEmpty && Time.time >= _spawnTime + _pickupDelay;

    private void Reset() => _renderer = GetComponentInChildren<SpriteRenderer>();

    public void Setup(ItemStack stack)
    {
        Stack = stack;
        RefreshVisual();
    }

    public override void OnSpawn()
    {
        _spawnTime = Time.time;
    }

    public override void OnDespawn()
    {
        Stack = ItemStack.Empty;

        if (_renderer != null)
            _renderer.sprite = null;
    }

    /// <summary>
    /// 넣을 수 있는 만큼 넣는다. 전부 들어가면 오브젝트가 반납된다.
    /// 일부만 들어가면 남은 수량으로 바닥에 그대로 남는다.
    /// </summary>
    public bool TryPickup(Inventory inventory)
    {
        if (inventory == null || Stack.IsEmpty) return false;

        if (Stack.HasInstance)
        {
            if (!inventory.AddStack(Stack)) return false;

            Stack = ItemStack.Empty;
            Release();
            return true;
        }

        int remain = inventory.Add(Stack.itemId, Stack.count);
        if (remain == Stack.count) return false; // 한 개도 못 넣었다

        if (remain <= 0)
        {
            Stack = ItemStack.Empty;
            Release();
            return true;
        }

        Stack = new ItemStack(Stack.itemId, remain);
        RefreshVisual();
        return true;
    }

    private void RefreshVisual()
    {
        if (_renderer == null) return;

        ItemDataSO data = Stack.Resolve();

        // UI 아이콘이 아니라 월드용을 쓴다. 지정이 없으면 WorldSprite가 아이콘으로 되돌려준다.
        _renderer.sprite = data != null ? data.WorldSprite : null;
        _renderer.enabled = _renderer.sprite != null;
    }
}
