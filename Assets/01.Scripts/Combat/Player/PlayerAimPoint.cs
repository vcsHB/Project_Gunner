using UnityEngine;


public enum PlayerAimPointStateType
{
    None = 0,
    Check,
    Break,
    Use,
    Ping,

    MeleeAim = 50,
    GunAim_Default = 51,
    GunAim_Hg,
    GunAim_Ar,
    GunAim_Sr,
    GunAim_Sg,

    Scrope_Reddot = 150,
    Scrope_Hologram,
    Scrope_Optical1,


}

/// <summary>
/// 월드에 그려지는 조준점. 커서를 따라가고, 든 무기에 맞는 모양으로 바뀐다.
///
/// 조준 로직은 갖지 않는다. <see cref="PlayerAimController"/>가 계산해 둔 값을 읽어 오기만 한다.
/// 표현이 시뮬레이션을 끌고 가면 나중에 서버 권한 구조로 옮길 수 없다.
/// </summary>
[DefaultExecutionOrder(AimExecutionOrder.AimPoint)]
public class PlayerAimPoint : MonoBehaviour
{
    [System.Serializable]
    private struct AimPointVisualData
    {
        [Header("Default")]
        public AimPointVisual None;
        public AimPointVisual Check;
        public AimPointVisual Break;
        public AimPointVisual Use;
        public AimPointVisual Ping;

        [Header("OnWeapon")]
        public AimPointVisual Melee;
        [Header("OnWeapon_Gun_Default")]
        public AimPointVisual GunDefault;
        public AimPointVisual GunHg;
        public AimPointVisual GunAr;
        public AimPointVisual GunSr;
        public AimPointVisual GunSg;
        [Header("OnWeapon_Gun_ScopeAim")]
        public AimPointVisual Scope_Reddot;
        public AimPointVisual Scope_Hologram;
        public AimPointVisual Scope_Optical1;

    }
    [System.Serializable]
    private class AimPointVisual
    {
        public Sprite icon;
        public Vector2 visualOffset;
    }
    [SerializeField] private AimPointVisualData _aimPointVisualData;
    [SerializeField] private GameObject _aimPointVisualGroup;
    [SerializeField] private Transform _aimPointVisualTrm;
    [SerializeField] private SpriteRenderer _aimPointRenderer;
    [SerializeField] private PlayerAimPointStateType _defaultStateType;
    [SerializeField] private PlayerAimPointStateType _currentState;
    private bool _isVisualEnable = true;

    private PlayerAimController _aimController;
    private PlayerHandController _weaponController;

    private void Awake()
    {
        // Set Default
        SetAimPointEnable(_isVisualEnable);

        // _currentState는 인스펙터에 저장되어 있다. 같은 값이면 SetAimPointState가 빠져나가므로
        // 스프라이트가 한 번도 적용되지 않은 채로 시작할 수 있다. 여기서 강제로 그린다.
        _currentState = _defaultStateType;
        ApplyVisual(_defaultStateType);
    }

    // 플레이어의 컴포넌트 초기화가 Awake에서 끝나므로 Start에서 붙는다. HUD와 같은 방식이다.
    private void Start()
    {
        if (_aimController == null)
            Bind(FindAnyObjectByType<Player>());
    }

    public void Bind(Player player)
    {
        Unbind();

        if (player == null)
        {
            Debug.LogWarning("[AimPoint] 씬에서 Player를 찾지 못했습니다.", this);
            return;
        }

        _aimController = player.GetCompo<PlayerAimController>();
        _weaponController = player.GetCompo<PlayerHandController>();

        if (_weaponController != null)
        {
            _weaponController.OnHandChangedEvent += HandleWeaponChanged;
            HandleWeaponChanged(_weaponController.Current);
        }

        UIInputBlocker.OnBlockedChangedEvent += HandleInputBlocked;
        HandleInputBlocked(UIInputBlocker.IsBlocked);
    }

    public void Unbind()
    {
        if (_weaponController != null)
        {
            _weaponController.OnHandChangedEvent -= HandleWeaponChanged;
            _weaponController = null;
        }

        UIInputBlocker.OnBlockedChangedEvent -= HandleInputBlocked;
        _aimController = null;
    }

    // 인벤토리를 열면 조준선을 숨긴다. 마우스로 UI를 다루는 중에 조준선이 따라다니면 방해가 된다.
    private void HandleInputBlocked(bool blocked) => SetAimPointEnable(!blocked);

    private void OnDestroy() => Unbind();

    // 조준 계산이 Update에서 끝난 뒤에 따라가야 한 프레임 밀리지 않는다.
    private void LateUpdate()
    {
        if (_aimController == null || !_isVisualEnable) return;

        SetAimPointPosition(_aimController.AimPosition);
    }

    private void HandleWeaponChanged(HandActionBase hand)
        => SetAimPointState(GetStateFor(hand is PlayerWeaponBase weapon ? weapon.Data : null));

    /// <summary>
    /// 무기 분류에 맞는 조준점. 맨손이면 기본값을 쓴다.
    /// 스코프 파츠에 따른 전환은 아직 없다 — 어느 파츠가 어느 조준점인지 데이터가 없다.
    /// </summary>
    private PlayerAimPointStateType GetStateFor(PlayerWeaponDataSO data)
    {
        if (data == null) return _defaultStateType;

        switch (data.Category)
        {
            case PlayerWeaponCategory.Melee: return PlayerAimPointStateType.MeleeAim;
            case PlayerWeaponCategory.HandGun: return PlayerAimPointStateType.GunAim_Hg;
            case PlayerWeaponCategory.AutoRifle: return PlayerAimPointStateType.GunAim_Ar;
            case PlayerWeaponCategory.SniperRifle: return PlayerAimPointStateType.GunAim_Sr;
            case PlayerWeaponCategory.Shotgun: return PlayerAimPointStateType.GunAim_Sg;

            // 유탄·로켓처럼 전용 조준점이 없는 것은 기본 총기 조준점을 쓴다.
            default: return PlayerAimPointStateType.GunAim_Default;
        }
    }

    public void SetAimPointPosition(Vector2 position)
    {
        transform.position = position;
    }
    public void SetAimPointEnable(bool value)
    {
        _isVisualEnable = value;

        if (_aimPointVisualGroup != null)
            _aimPointVisualGroup.SetActive(value);
    }

    public void SetAimPointState(PlayerAimPointStateType type)
    {
        if (_currentState == type) return;

        _currentState = type;
        ApplyVisual(type);
    }

    private void ApplyVisual(PlayerAimPointStateType type)
    {
        AimPointVisual aimPointVisualData = _aimPointVisualData.None;
        switch (type)
        {
            case PlayerAimPointStateType.Check:
                aimPointVisualData = _aimPointVisualData.Check;
                break;
            case PlayerAimPointStateType.Break:
                aimPointVisualData = _aimPointVisualData.Break;
                break;
            case PlayerAimPointStateType.Use:
                aimPointVisualData = _aimPointVisualData.Use;
                break;
            case PlayerAimPointStateType.Ping:
                aimPointVisualData = _aimPointVisualData.Ping;
                break;
            case PlayerAimPointStateType.MeleeAim:
                aimPointVisualData = _aimPointVisualData.Melee;
                break;
            case PlayerAimPointStateType.GunAim_Default:
                aimPointVisualData = _aimPointVisualData.GunDefault;
                break;
            case PlayerAimPointStateType.GunAim_Hg:
                aimPointVisualData = _aimPointVisualData.GunHg;
                break;
            case PlayerAimPointStateType.GunAim_Ar:
                aimPointVisualData = _aimPointVisualData.GunAr;
                break;
            case PlayerAimPointStateType.GunAim_Sr:
                aimPointVisualData = _aimPointVisualData.GunSr;
                break;
            case PlayerAimPointStateType.GunAim_Sg:
                aimPointVisualData = _aimPointVisualData.GunSg;
                break;
            case PlayerAimPointStateType.Scrope_Reddot:
                aimPointVisualData = _aimPointVisualData.Scope_Reddot;
                break;
            case PlayerAimPointStateType.Scrope_Hologram:
                aimPointVisualData = _aimPointVisualData.Scope_Hologram;
                break;
            case PlayerAimPointStateType.Scrope_Optical1:
                aimPointVisualData = _aimPointVisualData.Scope_Optical1;
                break;
        }

        if (aimPointVisualData == null) return;

        if (_aimPointRenderer != null)
            _aimPointRenderer.sprite = aimPointVisualData.icon;

        // 오브젝트 자체가 커서를 따라 움직이므로 오프셋은 로컬이어야 한다.
        // 월드로 넣으면 조준점이 매 프레임 원점 근처로 끌려간다.
        if (_aimPointVisualTrm != null)
            _aimPointVisualTrm.localPosition = aimPointVisualData.visualOffset;
    }
}
