using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 이 유닛이 아이템을 <b>갖고 줍는다.</b>
///
/// 보관(Inventory 수명·칸 수)과 줍기(주변 탐색)를 한 컴포넌트에 둔 이유는
/// 줍기가 Inventory 말고는 하는 일이 없기 때문이다. 나눠두면 "인벤토리 없는 줍기"라는
/// 성립하지 않는 조합이 만들어질 수 있고, 컴포넌트 목록만 한 줄 길어진다.
///
/// 줍지 않는 유닛은 <see cref="PickupSettings.radius"/>를 0으로 둔다.
///
/// 보관 로직 자체는 전부 <see cref="Inventory"/>에 있다. 여기는 Agent에 붙여주는 껍데기다.
/// </summary>
public class InventoryController : MonoBehaviour, IAgentComponent
{
    /// <summary>
    /// 줍기 설정. 중첩 클래스로 둬서 인스펙터에서 한 블록으로 접힌다.
    /// </summary>
    [Serializable]
    public class PickupSettings
    {
        [Tooltip("줍는 반경. 0이면 줍지 않는다.")]
        [Min(0f)] public float radius = 1.2f;

        [Tooltip("WorldItem이 올라가 있는 레이어.")]
        public LayerMask itemLayer;

        [Tooltip("확인 주기(초). 매 프레임 돌 필요는 없다.")]
        [Min(0f)] public float interval = 0.1f;
    }

    private const int MaxDetectCount = 16;

    [Tooltip("AgentStatus가 없을 때 쓸 칸 수.")]
    [SerializeField, Min(1)] private int _fallbackCapacity = 30;

    [SerializeField] private PickupSettings _pickup = new();

    public Inventory Inventory { get; private set; }

    /// <summary>줍는 반경. 버린 아이템이 "이 반경을 한 번 벗어나기 전까지는 안 주워진다"의 기준.</summary>
    public float PickupRadius => _pickup != null ? _pickup.radius : 0f;

    /// <summary>
    /// 안전 슬롯. <b>죽어도 남는 한 칸.</b> 아무 아이템이나 한 칸 분량(최대 스택)까지 넣을 수 있다.
    /// 적재함(<see cref="Inventory"/>)과 같은 <see cref="global::Inventory"/>라 드래그·스왑이 그대로 된다. 칸 수는 적재 스탯과 무관하게 고정이다.
    /// </summary>
    public Inventory SafeSlot { get; private set; }

    public const int SafeSlotCount = 1;
    public Agent Owner { get; private set; }

    private AgentStatus _status;
    private readonly List<ItemStack> _overflowBuffer = new();

    // 물리 질의는 논알로케이션으로. 0.1초마다 돌지만 버퍼를 매번 만들 이유가 없다.
    private readonly Collider2D[] _buffer = new Collider2D[MaxDetectCount];
    private ContactFilter2D _filter;
    private float _nextCheckTime;

    public void Initialize(Agent owner)
    {
        Owner = owner;

        _filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = _pickup.itemLayer,
            useTriggers = true,
        };
    }

    public void AfterInitialize()
    {
        _status = Owner.GetCompo<AgentStatus>();

        int capacity = _status != null ? _status.InventorySize.TotalValue : _fallbackCapacity;

        Inventory = new Inventory(ClampCapacity(capacity));
        SafeSlot = new Inventory(SafeSlotCount);

        if (_status != null)
            _status.InventorySize.OnChangedEvent += HandleCapacityChanged;
    }

    public void Dispose()
    {
        if (_status != null)
            _status.InventorySize.OnChangedEvent -= HandleCapacityChanged;
    }

    /// <summary>
    /// 적재 칸이 0 이하로 내려가면 인벤토리가 아예 없는 것이 된다.
    /// 적재 모듈을 떼어도 최소 한 칸은 남긴다.
    /// </summary>
    private static int ClampCapacity(int capacity) => Mathf.Max(1, capacity);

    /// <summary>
    /// 죽었다. 적재함을 비운다 — 기체를 버리고 탈출했으니 실린 것은 전부 잃는다. 안전 슬롯은 건드리지 않는다.
    /// 떨구지 않는다(그냥 사라진다). 잃은 것을 그 자리에서 다시 줍게 할지는 탈출 루프(M5)에서 정한다.
    /// </summary>
    public void LoseCargo()
    {
        Inventory?.Clear();
    }

    private void HandleCapacityChanged(int newCapacity)
    {
        _overflowBuffer.Clear();
        Inventory.Resize(ClampCapacity(newCapacity), _overflowBuffer);

        if (_overflowBuffer.Count == 0) return;

        // 칸이 줄어서 자리를 잃은 아이템은 발밑에 떨군다. 조용히 사라지면 안 된다.
        for (int i = 0; i < _overflowBuffer.Count; i++)
            WorldItemSpawner.Spawn(_overflowBuffer[i], transform.position);
    }

    #region 줍기

    /// <summary>
    /// 아이템 쪽 트리거로 하지 않는 이유:
    /// - Rigidbody2D가 잠들면 OnTriggerStay2D가 오지 않아서, 가만히 서 있으면 안 주워진다
    /// - 줍기 반경은 히트박스 모양과 무관해야 한다
    /// - 줍기용 콜라이더를 따로 달 필요가 없어진다
    /// </summary>
    private void Update()
    {
        if (Inventory == null || _pickup.radius <= 0f) return;
        if (Owner == null || Owner.IsDead) return;
        if (Time.time < _nextCheckTime) return;

        _nextCheckTime = Time.time + _pickup.interval;
        Scan();
    }

    private void Scan()
    {
        int count = Physics2D.OverlapCircle(transform.position, _pickup.radius, _filter, _buffer);

        for (int i = 0; i < count; i++)
        {
            if (_buffer[i] == null) continue;

            WorldItem item = _buffer[i].GetComponentInParent<WorldItem>();
            if (item == null || !item.CanPickupBy(transform)) continue;

            item.TryPickup(Inventory);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_pickup == null || _pickup.radius <= 0f) return;

        Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, _pickup.radius);
    }
#endif

    #endregion
}
