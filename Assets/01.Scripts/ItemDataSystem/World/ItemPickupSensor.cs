using UnityEngine;

/// <summary>
/// 주변의 WorldItem을 주워 담는다. 줍는 쪽에 붙인다.
///
/// 아이템 쪽 트리거로 하지 않는 이유:
/// - Rigidbody2D가 잠들면 OnTriggerStay2D가 오지 않아서, 가만히 서 있으면 안 주워진다
/// - 줍기 반경은 히트박스 모양과 무관해야 한다
/// - 플레이어에 줍기용 콜라이더를 따로 달 필요가 없어진다
/// </summary>
public class ItemPickupSensor : MonoBehaviour, IAgentComponent
{
    private const int MaxDetectCount = 16;

    [SerializeField, Min(0f)] private float _radius = 1.2f;

    [Tooltip("WorldItem이 올라가 있는 레이어.")]
    [SerializeField] private LayerMask _itemLayer;

    [Tooltip("확인 주기(초). 매 프레임 돌 필요는 없다.")]
    [SerializeField, Min(0f)] private float _interval = 0.1f;

    private readonly Collider2D[] _buffer = new Collider2D[MaxDetectCount];
    private ContactFilter2D _filter;

    private Agent _owner;
    private InventoryController _inventory;
    private float _nextCheckTime;

    public float Radius => _radius;

    public void Initialize(Agent owner)
    {
        _owner = owner;

        _filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = _itemLayer,
            useTriggers = true,
        };
    }

    public void AfterInitialize()
    {
        _inventory = _owner.GetCompo<InventoryController>();
    }

    public void Dispose()
    {
    }

    private void Update()
    {
        if (_inventory == null || _owner.IsDead) return;
        if (Time.time < _nextCheckTime) return;

        _nextCheckTime = Time.time + _interval;
        Scan();
    }

    private void Scan()
    {
        int count = Physics2D.OverlapCircle(transform.position, _radius, _filter, _buffer);

        for (int i = 0; i < count; i++)
        {
            if (_buffer[i] == null) continue;

            WorldItem item = _buffer[i].GetComponentInParent<WorldItem>();
            if (item == null || !item.CanPickup) continue;

            item.TryPickup(_inventory.Inventory);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _radius);
    }
#endif
}
