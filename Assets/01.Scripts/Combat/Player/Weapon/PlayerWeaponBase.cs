using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class PlayerWeaponBase : MonoBehaviour
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

    public event Action<WeaponFireMode> OnFireModeChangedEvent;

    public PlayerWeaponDataSO Data { get; private set; }

    /// <summary>이 무기 오브젝트의 스탯. 개량과 파츠 modifier가 여기에 붙는다.</summary>
    public WeaponStatus Status { get; private set; }

    /// <summary>개체 상태를 이 오브젝트에 반영해주는 쪽. 상태 자체는 Instance가 갖는다.</summary>
    public WeaponModController Mods { get; private set; }

    /// <summary>이 무기를 들고 있는 주체.</summary>
    public Agent Owner { get; private set; }

    /// <summary>파츠·개량의 주인. 인벤토리에 들어가도 유지된다.</summary>
    public WeaponItemInstance Instance { get; private set; }

    public bool IsEquipped { get; private set; }
    public WeaponFireMode CurrentFireMode { get; private set; }

    private readonly List<WeaponFireMode> _fireModeBuffer = new();

    /// <summary>
    /// 스폰 직후 한 번. 장착/해제와는 별개다.
    /// 오버라이드할 때 반드시 base를 먼저 호출할 것.
    /// </summary>
    public virtual void Initialize(PlayerWeaponDataSO data, Agent owner, WeaponItemInstance instance = null)
    {
        Data = data;
        Owner = owner;
        Status = new WeaponStatus(data.Stats);

        CurrentFireMode = data.DefaultFireMode;

        // 개체 상태가 없으면 임시로 하나 만든다. 없으면 이 무기는 모딩이 아예 불가능해진다.
        Instance = instance ?? WeaponItemInstance.CreateDetached(data);

        Mods = new WeaponModController(this, Status);
        Mods.Bind(Instance);
    }

    protected virtual void OnDestroy()
    {
        // 개체 상태는 건드리지 않는다. 여기서 파츠를 떼면 저장된 모딩이 날아간다.
        Mods?.Unbind();
    }

    /// <summary>슬롯에 장착되어 손에 들렸을 때.</summary>
    public virtual void OnEquipped()
    {
        IsEquipped = true;
    }

    /// <summary>슬롯에서 내려놓았을 때. 걸어둔 modifier나 예약된 상태를 여기서 되돌린다.</summary>
    public virtual void OnUnequipped()
    {
        IsEquipped = false;
        OnAttackReleased();
    }

    /// <summary>공격 입력이 눌렸을 때. 실제 발사는 파생 클래스가 구현한다.</summary>
    public virtual void OnAttackPressed()
    {
    }

    /// <summary>공격 입력이 떼졌을 때. 연사 중단, 차지 해제 등.</summary>
    public virtual void OnAttackReleased()
    {
    }

    /// <summary>재장전 입력(짧게 누름). 탄약을 쓰지 않는 무기는 무시한다.</summary>
    public virtual void OnReloadRequested()
    {
    }

    /// <summary>재장전 입력을 길게 눌렀을 때. 쓸 수 있는 탄종을 넘긴다.</summary>
    public virtual void OnAmmoCycleRequested(int direction)
    {
    }

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
