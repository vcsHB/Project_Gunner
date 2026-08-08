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

    private void Awake()
    {
        // Set Default
        SetAimPointEnable(_isVisualEnable);
    }


    public void SetAimPointPosition(Vector2 position)
    {
        transform.position = position;
    }
    public void SetAimPointEnable(bool value)
    {
        _isVisualEnable = value;
        _aimPointVisualGroup.SetActive(value);
    }

    public void SetAimPointState(PlayerAimPointStateType type)
    {
        if (_currentState == type) return;

        _currentState = type;
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
        }
        _aimPointRenderer.sprite = aimPointVisualData.icon;
        _aimPointVisualTrm.position = aimPointVisualData.visualOffset;
    }
}