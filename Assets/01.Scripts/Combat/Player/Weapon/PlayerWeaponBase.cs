using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 손에 드는 무기. 주/보조 입력의 의미는 파생 클래스가 정한다.
/// (원거리 무기는 Primary=발사 / Secondary=정조준)
/// </summary>
public abstract class PlayerWeaponBase : HandActionBase
{
    // 인스펙터에서만 채워지는 필드라 "할당된 적 없음" 경고가 뜬다.
#pragma warning disable CS0649
    [Serializable]
    private struct PartMountPoint
    {
        public WeaponPartSlotType slot;
        public Transform point;
    }
#pragma warning restore CS0649

    [Tooltip("파츠 프리팹이 붙을 위치. 지정하지 않은 슬롯은 무기 본체에 붙는다.")]
    [SerializeField] private List<PartMountPoint> _mountPoints = new();

    [Tooltip("리소스가 그려진 방향. 총구가 왼쪽을 향하게 그려졌으면 Left로 둔다. " +
             "스프라이트와 총구 위치를 통째로 미러링해서 +X를 앞으로 맞춘다.")]
    [SerializeField] private WeaponFacingType _spriteFacing = WeaponFacingType.Right;

    public event Action<WeaponFireMode> OnFireModeChangedEvent;

    public PlayerWeaponDataSO Data { get; private set; }

    /// <summary>이 무기 오브젝트의 스탯. 개량과 파츠 modifier가 여기에 붙는다.</summary>
    public WeaponStatus Status { get; private set; }

    /// <summary>개체 상태를 이 오브젝트에 반영해주는 쪽. 상태 자체는 Instance가 갖는다.</summary>
    public WeaponModController Mods { get; private set; }

    /// <summary>파츠·개량의 주인. 인벤토리에 들어가도 유지된다.</summary>
    public WeaponItemInstance Instance { get; private set; }

    public WeaponFireMode CurrentFireMode { get; private set; }

    private readonly List<WeaponFireMode> _fireModeBuffer = new();

    /// <summary>
    /// 스폰 직후 한 번. 장착/해제와는 별개다.
    /// 오버라이드할 때 반드시 base를 먼저 호출할 것.
    /// </summary>
    public override void Initialize(Agent owner, ItemStack stack)
    {
        base.Initialize(owner, stack);

        Data = stack.Resolve<PlayerWeaponDataSO>();
        if (Data == null)
        {
            Debug.LogError($"[Weapon] {name}에 무기 데이터가 없습니다.", this);
            return;
        }

        Status = new WeaponStatus(Data.Stats);

        ApplyFacing();

        CurrentFireMode = Data.DefaultFireMode;

        // 개체 상태가 없으면 임시로 하나 만든다. 없으면 이 무기는 모딩이 아예 불가능해진다.
        Instance = stack.ResolveInstance<WeaponItemInstance>() ?? WeaponItemInstance.CreateDetached(Data);

        Mods = new WeaponModController(this, Status);
        Mods.Bind(Instance);
    }

    protected virtual void OnDestroy()
    {
        // 개체 상태는 건드리지 않는다. 여기서 파츠를 떼면 저장된 모딩이 날아간다.
        Mods?.Unbind();
    }


    /// <summary>
    /// 왼쪽을 보고 그려진 리소스를 통째로 X 미러링해서 +X를 앞으로 맞춘다.
    ///
    /// 발사 방향에서 부호를 뒤집는 식으로 맞추면 안 된다. 그렇게 하면 스프라이트는 그대로
    /// 반대를 보고, 총구는 플레이어 뒤에 남고, 파츠 장착점과 이펙트 방향이 전부 따로 논다.
    /// 리소스와 코드의 규칙 차이는 <b>한 곳에서 한 번만</b> 흡수한다.
    ///
    /// 스케일을 곱하지 않고 부호만 덮어쓰므로 여러 번 불려도 결과가 같다.
    /// </summary>
    private void ApplyFacing()
    {
        float sign = _spriteFacing == WeaponFacingType.Left ? -1f : 1f;

        Vector3 scale = transform.localScale;
        transform.localScale = new Vector3(Mathf.Abs(scale.x) * sign, scale.y, scale.z);
    }

#if UNITY_EDITOR
    // 에디터에서도 프리팹이 실제로 나갈 모습으로 보이게 한다.
    // 인스펙터에서 Left로 바꿔놓고 씬에서는 반대로 보이면 어느 쪽이 맞는지 알 수 없다.
    private void OnValidate() => ApplyFacing();
#endif

    /// <summary>파츠 프리팹이 붙을 위치. 지정된 게 없으면 무기 본체.</summary>
    public Transform GetMountPoint(WeaponPartSlotType slot)
    {
        for (int i = 0; i < _mountPoints.Count; i++)
        {
            if (_mountPoints[i].slot == slot && _mountPoints[i].point != null)
                return _mountPoints[i].point;
        }

        return transform;
    }

    #region Fire Mode

    public void GetAvailableFireModes(List<WeaponFireMode> results) => Mods.GetAvailableFireModes(results);

    public bool SetFireMode(WeaponFireMode mode)
    {
        if (CurrentFireMode == mode) return true;

        Mods.GetAvailableFireModes(_fireModeBuffer);
        if (!_fireModeBuffer.Contains(mode)) return false;

        CurrentFireMode = mode;
        OnFireModeChangedEvent?.Invoke(mode);
        return true;
    }

    /// <summary>다음 모드로 순환한다. 선택지가 하나뿐이면 아무 일도 없다.</summary>
    public void CycleFireMode()
    {
        Mods.GetAvailableFireModes(_fireModeBuffer);
        if (_fireModeBuffer.Count <= 1) return;

        int index = _fireModeBuffer.IndexOf(CurrentFireMode);
        WeaponFireMode next = _fireModeBuffer[(index + 1) % _fireModeBuffer.Count];

        CurrentFireMode = next;
        OnFireModeChangedEvent?.Invoke(next);
    }

    /// <summary>
    /// 파츠를 떼면서 현재 모드가 사라졌을 수 있다. 파츠 변경 후 호출할 것.
    /// </summary>
    public void ValidateFireMode()
    {
        Mods.GetAvailableFireModes(_fireModeBuffer);
        if (_fireModeBuffer.Contains(CurrentFireMode)) return;

        CurrentFireMode = _fireModeBuffer.Count > 0 ? _fireModeBuffer[0] : Data.DefaultFireMode;
        OnFireModeChangedEvent?.Invoke(CurrentFireMode);
    }

    #endregion
}
