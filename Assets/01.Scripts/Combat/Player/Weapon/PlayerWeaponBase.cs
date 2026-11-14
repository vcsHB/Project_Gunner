using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 팔에 다는 무기. 받은 입력 하나가 무슨 뜻인지는 파생 클래스가 정한다.
/// (원거리 무기는 Use=발사)
/// </summary>
public abstract class PlayerWeaponBase : HandActionBase
{
    // 인스펙터에서만 채워지는 필드라 "할당된 적 없음" 경고가 뜬다.
#pragma warning disable CS0649
    [Serializable]
    private struct ModuleMountPoint
    {
        public ModuleSlotType slot;
        public Transform point;
    }
#pragma warning restore CS0649

    [Tooltip("모듈 프리팹이 붙을 위치. 지정하지 않은 슬롯은 무기 본체에 붙는다.")]
    [SerializeField] private List<ModuleMountPoint> _mountPoints = new();


    public event Action<WeaponFireMode> OnFireModeChangedEvent;

    public PlayerWeaponDataSO Data { get; private set; }

    /// <summary>이 무기 오브젝트의 스탯. 개량과 모듈 modifier가 여기에 붙는다.</summary>
    public WeaponStatus Status { get; private set; }

    /// <summary>개체 상태를 이 오브젝트에 반영해주는 쪽. 상태 자체는 Instance가 갖는다.</summary>
    public ModdingController Mods { get; private set; }

    /// <summary>모듈·개량의 주인. 인벤토리에 들어가도 유지된다.</summary>
    public WeaponItemInstance Instance { get; private set; }

    /// <summary>연사를 제한하는 열. 재장전이 없어진 자리를 메운다. 열은 이 오브젝트와 함께 산다.</summary>
    public WeaponHeatController Heat { get; private set; }

    public WeaponFireMode CurrentFireMode { get; private set; }

    private readonly List<WeaponFireMode> _fireModeBuffer = new();

    /// <summary>
    /// 스폰 직후 한 번. 장착/해제와는 별개다.
    /// 오버라이드할 때 반드시 base를 먼저 호출할 것.
    /// </summary>
    public override void Initialize(Agent owner, EquipSlotType slot, ItemStack stack)
    {
        base.Initialize(owner, slot, stack);

        Data = stack.Resolve<PlayerWeaponDataSO>();
        if (Data == null)
        {
            Debug.LogError($"[Weapon] {name}에 무기 데이터가 없습니다.", this);
            return;
        }

        Status = new WeaponStatus(Data.Stats);

        CurrentFireMode = Data.DefaultFireMode;

        // 개체 상태가 없으면 임시로 하나 만든다. 없으면 이 무기는 모딩이 아예 불가능해진다.
        Instance = stack.ResolveInstance<WeaponItemInstance>() ?? WeaponItemInstance.CreateDetached(Data);

        Mods = new ModdingController(this, Status);
        Mods.Bind(Instance);

        // 모딩이 끝난 뒤에 만든다. 열 수치도 모듈이 건드릴 수 있어야 한다.
        Heat = new WeaponHeatController(this);
    }

    protected virtual void OnDestroy()
    {
        // 개체 상태는 건드리지 않는다. 여기서 모듈을 떼면 저장된 모딩이 날아간다.
        Mods?.Unbind();
        Heat?.Dispose();
    }

    /// <summary>
    /// 열은 쏘지 않는 동안에도 흘러야 하므로 기반에서 돈다.
    /// 오버라이드할 때 반드시 base를 부를 것 — 안 부르면 그 무기만 영영 식지 않는다.
    /// </summary>
    protected virtual void Update()
    {
        Heat?.Tick(Time.deltaTime);
    }


    // 리소스 방향·팔 미러링은 HandActionBase가 한 곳에서 처리한다.

    /// <summary>모듈 프리팹이 붙을 위치. 지정된 게 없으면 무기 본체.</summary>
    public Transform GetMountPoint(ModuleSlotType slot)
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
    /// 모듈을 떼면서 현재 모드가 사라졌을 수 있다. 모듈 변경 후 호출할 것.
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
