using UnityEngine;

/// <summary>
/// 팔에 달린 것. 입력 하나를 받아 무엇을 할지 <b>여기가</b> 정한다.
///
/// 좌우 무장이 동시에 활성이라 각 팔은 버튼을 하나만 받는다.
/// 좌클릭이 왼팔, 우클릭이 오른팔이고, 그 하나가 무슨 뜻인지는 달린 것마다 다르다.
///
/// | 달린 것 | Use |
/// |---|---|
/// | 총 | 발사 |
/// | 근접 | 휘두르기 |
/// | 도구 | 채굴 |
///
/// 배분은 <see cref="PlayerHandController"/>가 슬롯별로 고정해서 넘긴다.
/// <b>여기에 "내가 나설 차례인가"를 넣지 마세요.</b> 그 판단이 양쪽에 생기면
/// 둘 다 반응하거나 아무도 반응하지 않는 상태가 만들어진다.
/// </summary>
public abstract class HandActionBase : MonoBehaviour
{
    [Header("리소스")]
    [Tooltip("리소스가 그려진 정면. 유닛 리소스는 Up이 표준이고, 뒤집어 그렸으면 Down. " +
             "회전 보정은 기체 루트가 하므로 여기서는 앞뒤 반전만 한다.")]
    [SerializeField] private FacingType _spriteFacing = FacingType.Up;

    [Tooltip("이 리소스를 어느 팔 기준으로 그렸는가. 반대쪽 팔에 달리면 좌우를 뒤집는다. " +
             "팔 하나만 그려두고 양쪽에 쓸 수 있다.")]
    [SerializeField] private EquipSlotType _authoredArm = EquipSlotType.ArmLeft;

    /// <summary>이것을 달고 있는 주체.</summary>
    public Agent Owner { get; private set; }

    /// <summary>어느 팔에 달렸는지. 좌우를 구분해야 하는 표현이 읽어간다.</summary>
    public EquipSlotType Slot { get; private set; }

    /// <summary>달려 있는 아이템. 개체 상태를 볼 때 쓴다.</summary>
    public ItemStack Stack { get; private set; }

    public bool IsEquipped { get; private set; }

    /// <summary>반대쪽 팔에 달려서 뒤집힌 상태인지. 총구 오프셋을 읽는 쪽이 참고한다.</summary>
    public bool IsArmMirrored { get; private set; }

    /// <summary>스폰 직후 한 번. 오버라이드할 때 반드시 base를 먼저 호출할 것.</summary>
    public virtual void Initialize(Agent owner, EquipSlotType slot, ItemStack stack)
    {
        Owner = owner;
        Slot = slot;
        Stack = stack;

        ApplyFacing();
    }

    /// <summary>
    /// 리소스와 코드의 규칙 차이를 <b>한 곳에서 한 번만</b> 흡수한다. 축이 둘이고 이유가 다르다.
    ///
    /// <b>팔에 달리는 것은 아트 공간에서 산다 — +Y가 앞, ±X가 좌우다.</b>
    /// 논리 정면은 +X지만, 그 차이는 기체 루트가 이미 돌려놨다(<see cref="MechaRotationController"/>).
    /// 여기서 또 +X를 앞으로 치면 보정이 두 번 걸린다.
    ///
    /// | 축 | 왜 |
    /// |---|---|
    /// | X | 그려둔 팔과 반대쪽에 달렸는가 |
    /// | Y | 리소스가 앞뒤 어느 쪽을 보고 그려졌는가 |
    ///
    /// 스케일을 곱하지 않고 부호만 덮어쓰므로 여러 번 불려도 결과가 같다.
    /// 총구·모듈 장착점이 자식이라 스프라이트와 같이 따라온다.
    /// </summary>
    protected void ApplyFacing()
    {
        IsArmMirrored = _authoredArm != Slot && IsArm(Slot) && IsArm(_authoredArm);

        float x = IsArmMirrored ? -1f : 1f;
        float y = _spriteFacing == FacingType.Down ? -1f : 1f;

        Vector3 scale = transform.localScale;

        transform.localScale = new Vector3(
            Mathf.Abs(scale.x) * x,
            Mathf.Abs(scale.y) * y,
            scale.z);
    }

    private static bool IsArm(EquipSlotType slot)
        => slot == EquipSlotType.ArmLeft || slot == EquipSlotType.ArmRight;

#if UNITY_EDITOR
    // 에디터에서도 프리팹이 실제로 나갈 모습으로 보이게 한다.
    // 팔 미러링은 달아봐야 정해지므로 여기서는 리소스 방향만 반영된다.
    private void OnValidate() => ApplyFacing();
#endif

    /// <summary>팔에 달렸을 때.</summary>
    public virtual void OnEquipped() => IsEquipped = true;

    /// <summary>
    /// 떼어낼 때. 눌린 채로 넘어가는 것을 여기서 되돌린다.
    /// 안 그러면 다음에 달았을 때 눌린 상태로 시작한다.
    /// </summary>
    public virtual void OnUnequipped()
    {
        IsEquipped = false;

        OnUseReleased();
    }

    public virtual void OnUsePressed() { }
    public virtual void OnUseReleased() { }

    /// <summary>
    /// 쓸 수 없는 상태(팔이 부서짐). 누르는 쪽(입력·AI)은 그대로 두고 <b>쓰는 쪽에서</b> 막는다 —
    /// 누르는 경로가 둘이라 양쪽에서 막으면 한쪽을 빠뜨린다.
    /// </summary>
    public bool IsDisabled { get; private set; }

    public void SetDisabled(bool value)
    {
        if (IsDisabled == value) return;

        IsDisabled = value;

        if (value)
            Interrupt();
    }

    /// <summary>쓰던 것을 <b>결과 없이</b> 끊는다. 차지 무기가 떼는 순간 쏘면 안 되므로 파생이 덮어쓴다.</summary>
    protected virtual void Interrupt() => OnUseReleased();

    /// <summary>
    /// 방아쇠를 <b>잡고 있어야</b> 계속 쓰이는가. 연사 무기가 그렇다.
    ///
    /// AI는 손가락이 없어서 이걸 물어봐야 한다. 잡고 있으면 되는 것을 눌렀다 떼면 발사가 끊기고,
    /// 눌렀다 떼야 하는 것을 잡고만 있으면 <b>한 발만 나가고 영영 멈춘다.</b>
    /// 단발 무기를 든 적이 가만히 서 있던 이유가 이것이었다.
    /// </summary>
    public virtual bool HoldsTrigger => true;

    /// <summary>떼기 전에 눌러두어야 하는 시간(초). 차지 무기가 쓴다.</summary>
    public virtual float TriggerHoldTime => 0f;
}
