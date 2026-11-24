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

    // 버린 사람. 그 사람이 한 번 멀어지기 전까지는 그 사람에게 안 주워진다 — 발밑에 버리면 0.5초 뒤 다시 주워졌다.
    private Transform _ignoredPicker;
    private float _ignoreRadius;

    public bool CanPickup => _autoPickup && !Stack.IsEmpty && Time.time >= _spawnTime + _pickupDelay;

    public bool CanPickupBy(Transform picker) => CanPickup && (_ignoredPicker == null || picker != _ignoredPicker);

    /// <summary>
    /// <paramref name="picker"/>가 <paramref name="radius"/> 밖으로 나갔다 올 때까지 줍지 않게 한다. 버릴 때 부른다.
    /// 다른 사람(멀티)은 바로 주울 수 있다.
    /// </summary>
    public void IgnorePickupBy(Transform picker, float radius)
    {
        _ignoredPicker = picker;
        _ignoreRadius = radius;
    }

    private void Update()
    {
        if (_ignoredPicker == null) return;

        float distance = Vector2.Distance(_ignoredPicker.position, transform.position);
        if (distance > _ignoreRadius || !_ignoredPicker.gameObject.activeInHierarchy)
            _ignoredPicker = null;
    }

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
        _ignoredPicker = null;

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
