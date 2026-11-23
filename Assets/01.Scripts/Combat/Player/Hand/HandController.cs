using System;
using UnityEngine;

/// <summary>
/// 팔에 달린 것을 <b>만들고 붙이는</b> 쪽. 플레이어와 적이 같이 쓴다.
///
/// 무엇이 달릴지는 <see cref="EquipmentController"/>가 정하고, 붙을 자리는
/// <see cref="MechaAssembler"/>가 세운 코어의 소켓이 정한다. 여기는 그 둘을 잇는다.
///
/// <b>누가 방아쇠를 당기는지는 여기서 정하지 않는다.</b> 플레이어는 입력이
/// (<see cref="PlayerHandController"/>), 적은 AI가 당긴다(<see cref="EnemyHandController"/>).
/// 그 차이만 파생 클래스에 둔다 — 만들고 붙이는 일은 양쪽이 완전히 같다.
///
/// <b>컨트롤러는 하나다.</b> 팔마다 컴포넌트를 붙이면 Agent가 타입당 하나만 등록하므로
/// (<see cref="Agent"/>) 한쪽이 통째로 조회되지 않는다. 손을 안에서 배열로 들고 간다.
///
/// 개체 상태는 ItemInstance에 있으므로 오브젝트를 부수고 다시 만들어도 유지된다.
///
/// <b>고정 무장</b> — 자리(<c>socket</c>)와 무기(<c>fixedWeapon</c>)를 프리팹에 직접 적으면 장비·조립 없이 그 자리에 무기를 단다.
/// 조립되지 않는 단일 기체(터렛·드론·보스)가 쓴다. <b>무기 오브젝트는 메카 손의 것과 같다</b> — 과열·투사체·발사 연출이 그대로라
/// 같은 총이 터렛에 달려도 같은 수치로 나간다. 적재함이 없으므로 탄은 무한이다(<see cref="WeaponAmmoController.SetInfinite"/>).
/// </summary>
[RequireAgentComponent(typeof(EquipmentController), optional: true)]
[RequireAgentComponent(typeof(MechaAssembler), optional: true)]
public abstract class HandController : MonoBehaviour, IAgentComponent
{
    [Serializable]
    private struct ArmMount
    {
        [Tooltip("이 팔의 슬롯. 고정 무장이면 좌우 뒤집기에만 쓴다(그려둔 팔과 다르면 뒤집힌다).")]
        public EquipSlotType slot;

        [Tooltip("고정 무장의 자리. 비우면 장비 → 조립된 코어의 소켓 경로를 쓴다.")]
        public Transform socket;

        [Tooltip("고정 무장. 있으면 장비와 무관하게 이것을 단다. 탄은 무한.")]
        public PlayerWeaponDataSO fixedWeapon;

        [Tooltip("고정 무장의 탄종. 탄종 보정(StatDelta)이 걸린다. 비우면 무기 기본 탄.")]
        public AmmoDataSO fixedAmmo;

        public bool IsFixed => fixedWeapon != null;
    }

    [Tooltip("이 기체가 가진 팔 슬롯. 붙을 자리는 코어 소켓이 정한다.")]
    [SerializeField] private ArmMount[] _mounts =
    {
        new() { slot = EquipSlotType.ArmLeft },
        new() { slot = EquipSlotType.ArmRight },
    };

    /// <summary>(슬롯, 새로 달린 것). 떼어내면 두 번째가 null.</summary>
    public event Action<EquipSlotType, HandActionBase> OnHandChangedEvent;

    private EquipmentController _equipment;
    private MechaAssembler _assembler;

    // _mounts와 같은 순서다. 슬롯으로 찾는 비용을 피하려고 인덱스를 맞춰 둔다.
    private HandActionBase[] _hands;
    private uint[] _itemIds;
    private uint[] _instanceIds;

    protected Agent Owner { get; private set; }

    /// <summary>이 기체가 가진 팔 슬롯 수. 양팔을 훑는 쪽이 쓴다.</summary>
    public int MountCount => _mounts.Length;

    public EquipSlotType SlotAt(int index) => _mounts[index].slot;

    public HandActionBase HandAt(int index) => _hands != null ? _hands[index] : null;

    public HandActionBase Get(EquipSlotType slot)
    {
        int index = IndexOf(slot);

        return index >= 0 ? _hands[index] : null;
    }

    public HandActionBase Left => Get(EquipSlotType.ArmLeft);
    public HandActionBase Right => Get(EquipSlotType.ArmRight);

    public virtual void Initialize(Agent owner)
    {
        Owner = owner;

        _hands = new HandActionBase[_mounts.Length];
        _itemIds = new uint[_mounts.Length];
        _instanceIds = new uint[_mounts.Length];
    }

    public virtual void AfterInitialize()
    {
        _equipment = Owner.GetCompo<EquipmentController>();
        _assembler = Owner.GetCompo<MechaAssembler>();

        if (_equipment != null)
            _equipment.OnEquipChangedEvent += HandleEquipChanged;

        // 코어가 바뀌면 소켓도 바뀐다. 달려 있던 무기를 새 자리로 옮겨야 한다.
        if (_assembler != null)
            _assembler.OnAssembledEvent += HandleAssembled;

        // 구독보다 먼저 장착된 것이 있으면 이벤트를 놓친다. 현재 상태를 한 번 훑는다.
        RefreshAll();
    }

    public virtual void Dispose()
    {
        if (_equipment != null)
        {
            _equipment.OnEquipChangedEvent -= HandleEquipChanged;
            _equipment = null;
        }

        if (_assembler != null)
        {
            _assembler.OnAssembledEvent -= HandleAssembled;
            _assembler = null;
        }

        for (int i = 0; i < _mounts.Length; i++)
            DestroyHand(i);
    }

    protected int IndexOf(EquipSlotType slot)
    {
        for (int i = 0; i < _mounts.Length; i++)
        {
            if (_mounts[i].slot == slot) return i;
        }

        return -1;
    }

    #region 손 만들기

    private void RefreshAll()
    {
        for (int i = 0; i < _mounts.Length; i++)
        {
            if (_mounts[i].IsFixed)
            {
                Refresh(i, new ItemStack(_mounts[i].fixedWeapon.Id, 1));
                continue;
            }

            if (_equipment != null)
                Refresh(i, _equipment.GetStack(_mounts[i].slot));
        }
    }

    private void HandleEquipChanged(EquipSlotType slot, ItemStack stack)
    {
        for (int i = 0; i < _mounts.Length; i++)
        {
            // 고정 무장은 장비를 따르지 않는다.
            if (_mounts[i].slot != slot || _mounts[i].IsFixed) continue;

            Refresh(i, stack);
            return;
        }
    }

    private void Refresh(int index, ItemStack stack)
    {
        HandActionBase prefab = ResolvePrefab(stack);

        if (prefab == null)
        {
            DestroyHand(index);
            return;
        }

        // 같은 개체를 다시 받은 것이면 그대로 둔다. 매번 부수면 열·차지 같은 게 날아간다.
        if (_hands[index] != null
            && _itemIds[index] == stack.itemId
            && _instanceIds[index] == stack.instanceId)
        {
            return;
        }

        DestroyHand(index);
        Create(index, prefab, stack);
    }

    /// <summary>이 아이템을 달면 무엇이 만들어지는가. 달 수 없으면 null.</summary>
    private static HandActionBase ResolvePrefab(ItemStack stack)
    {
        ItemDataSO data = stack.Resolve();
        if (data == null) return null;

        return data is IHandItemData handItem ? handItem.HandPrefab : null;
    }

    /// <summary>이 팔이 달릴 자리. 조립된 코어의 소켓만 쓴다.</summary>
    private Transform ResolveRoot(int index)
    {
        if (_mounts[index].socket != null) return _mounts[index].socket;
        if (_assembler == null) return null;

        MechaPartView core = _assembler.Core;

        return core != null ? core.GetSocket(_mounts[index].slot) : null;
    }

    /// <summary>코어가 바뀌어 소켓이 옮겨갔다. 달려 있는 것을 새 자리로 옮긴다.</summary>
    private void HandleAssembled()
    {
        for (int i = 0; i < _hands.Length; i++)
        {
            if (_hands[i] == null) continue;

            if (_mounts[i].socket != null) continue;

            Transform root = ResolveRoot(i);
            if (root == null || _hands[i].transform.parent == root) continue;

            _hands[i].transform.SetParent(root, false);
            _hands[i].transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        // 코어가 늦게 조립되면 그때까지는 붙일 자리가 없어서 만들어지지 않았다.
        // 자리가 생긴 지금 다시 훑는다.
        RefreshAll();
    }

    private void Create(int index, HandActionBase prefab, ItemStack stack)
    {
        Transform root = ResolveRoot(index);
        if (root == null)
        {
            // 코어보다 먼저 무장이 장착되는 순서가 흔하다. 조립이 끝나면 다시 시도하므로
            // 여기서는 조용히 넘어간다 — 매번 에러를 내면 정상 순서에서도 로그가 쌓인다.
            return;
        }

        HandActionBase hand = Instantiate(prefab, root);
        hand.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

        hand.Initialize(Owner, _mounts[index].slot, stack);

        if (_mounts[index].IsFixed)
            SetupFixed(hand, _mounts[index].fixedAmmo);

        hand.OnEquipped();

        _hands[index] = hand;
        _itemIds[index] = stack.itemId;
        _instanceIds[index] = stack.instanceId;

        // 팔에도 히트박스가 있다(부위파괴). 조립기가 콜라이더를 모은 뒤에 생기므로 다시 모으게 해야 한다 —
        // 안 그러면 팔 콜라이더가 프리팹 레이어를 단 채로 남아 아무것도 안 맞는다.
        Owner.GetCompo<TargetBase>()?.RefreshColliders();

        OnHandChangedEvent?.Invoke(_mounts[index].slot, hand);
    }

    /// <summary>고정 무장은 적재함이 없다. 탄을 무한으로 하고, 탄종이 지정됐으면 그 보정을 건다.</summary>
    private static void SetupFixed(HandActionBase hand, AmmoDataSO ammo)
    {
        if (hand is not PlayerRangedWeapon ranged || ranged.Ammo == null) return;

        ranged.Ammo.SetInfinite(true);

        if (ammo != null && ranged.Instance != null)
            ranged.Instance.SetLoadedAmmo(ammo.Id);
    }

    private void DestroyHand(int index)
    {
        _itemIds[index] = 0;
        _instanceIds[index] = 0;

        HandActionBase hand = _hands[index];
        if (hand == null) return;

        _hands[index] = null;

        hand.OnUnequipped();

        // 개체 상태는 건드리지 않는다. 오브젝트가 사라져도 남아야 한다.
        Destroy(hand.gameObject);

        OnHandChangedEvent?.Invoke(_mounts[index].slot, null);
    }

    #endregion

    /// <summary>눌린 채로 남은 것을 모두 놓아준다. 기절·사망·UI 차단에서 쓴다.</summary>
    protected void ReleaseAll()
    {
        if (_hands == null) return;

        for (int i = 0; i < _hands.Length; i++)
        {
            if (_hands[i] != null)
                _hands[i].OnUseReleased();
        }
    }
}
