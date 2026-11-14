using UnityEngine;

/// <summary>
/// 발사할 때마다 총구가 뒤로 밀렸다 돌아온다.
///
/// Animator 클립이 아니라 코드로 하는 이유 — 후퇴량이 <b>연사 중에 누적</b>되어야 한다.
/// 클립은 매번 0프레임부터 다시 돌아서, 빠르게 쏠수록 오히려 덜 움직이는 것처럼 보인다.
/// 여기서는 남은 후퇴량에 더하므로 연사할수록 총구가 더 밀려 있다.
/// (<see cref="WeaponFireAnimator"/>는 반대다 — 한 발에 한 번 보장이 필요한 연출은 그쪽)
///
/// 전투 로직이 아니라 <see cref="PlayerRangedWeapon.OnFiredEvent"/>를 구독한다.
/// 무기가 직접 연출을 돌리면 서버 권한 구조로 옮길 수 없다. (RULES 시뮬레이션과 표현 분리)
/// </summary>
public class WeaponMuzzleRecoil : MonoBehaviour
{
    [Tooltip("뒤로 밀릴 대상. 비우면 자기 자신.")]
    [SerializeField] private Transform _muzzle;

    [Tooltip("후퇴 방향(로컬). 정면이 +X라서 보통 (-1, 0)이다.")]
    [SerializeField] private Vector2 _axis = Vector2.left;

    [Tooltip("한 발당 밀리는 거리.")]
    [SerializeField, Min(0f)] private float _distancePerShot = 0.12f;

    [Tooltip("아무리 연사해도 이보다 더 밀리지는 않는다.")]
    [SerializeField, Min(0f)] private float _maxDistance = 0.3f;

    [Tooltip("초당 돌아오는 거리. 클수록 빨리 제자리로 온다.")]
    [SerializeField, Min(0.01f)] private float _returnSpeed = 1.2f;

    private PlayerRangedWeapon _weapon;
    private PlayerHandController _hands;
    private EquipSlotType _slot = EquipSlotType.None;

    private Vector3 _restPosition;
    private float _offset;

    private void Awake()
    {
        if (_muzzle == null)
            _muzzle = transform;

        _restPosition = _muzzle.localPosition;
    }

    private void OnEnable() => Bind();

    private void OnDisable()
    {
        Unsubscribe();

        if (_hands != null)
        {
            _hands.OnHandChangedEvent -= HandleHandChanged;
            _hands = null;
        }
    }

    #region 구독

    /// <summary>
    /// 무기 프리팹 안에 있으면 부모에서 바로 찾는다.
    ///
    /// 못 찾으면 <b>씬에 손으로 놓아둔 더미</b>로 보고, 이 오브젝트가 들어앉은 소켓의 팔이
    /// 쏠 때 같이 움직이게 한다. 아트를 먼저 맞춰보는 동안 쓰는 길이고,
    /// 더미가 진짜 무기 프리팹이 되면 위쪽 경로로 자연스럽게 넘어간다.
    /// </summary>
    private void Bind()
    {
        _weapon = GetComponentInParent<PlayerRangedWeapon>();

        if (_weapon != null)
        {
            _weapon.OnFiredEvent += HandleFired;
            return;
        }

        Agent owner = GetComponentInParent<Agent>();
        if (owner == null) return;

        _slot = FindSocketSlot();
        if (_slot == EquipSlotType.None) return;

        _hands = owner.GetCompo<PlayerHandController>();
        if (_hands == null) return;

        _hands.OnHandChangedEvent += HandleHandChanged;
        Subscribe(_hands.Get(_slot));
    }

    /// <summary>가장 가까운 상위 소켓. 어느 팔에 얹혀 있는지는 계층이 답한다.</summary>
    private EquipSlotType FindSocketSlot()
    {
        MechaSocket socket = GetComponentInParent<MechaSocket>();

        return socket != null ? socket.Slot : EquipSlotType.None;
    }

    private void HandleHandChanged(EquipSlotType slot, HandActionBase hand)
    {
        if (slot != _slot) return;

        Subscribe(hand);
    }

    private void Subscribe(HandActionBase hand)
    {
        Unsubscribe();

        _weapon = hand as PlayerRangedWeapon;

        if (_weapon != null)
            _weapon.OnFiredEvent += HandleFired;
    }

    private void Unsubscribe()
    {
        if (_weapon == null) return;

        _weapon.OnFiredEvent -= HandleFired;
        _weapon = null;
    }

    #endregion

    /// <summary>
    /// 반동 세기가 셀수록 더 밀린다. 같은 무기라도 모듈·탄종으로 반동이 달라지므로
    /// 고정값을 쓰면 "강한 탄을 넣었는데 똑같아 보이는" 상태가 된다.
    /// </summary>
    private void HandleFired(float recoil)
    {
        float add = _distancePerShot * Mathf.Max(0.25f, recoil);

        _offset = Mathf.Min(_offset + add, _maxDistance);
    }

    private void LateUpdate()
    {
        if (_offset <= 0f) return;

        _offset = Mathf.MoveTowards(_offset, 0f, _returnSpeed * Time.deltaTime);

        // 제자리로 돌아올 때 좌표를 다시 계산하지 않고 기준값에서 더한다.
        // 누적하면 연사할 때마다 조금씩 밀려 영영 안 돌아온다.
        _muzzle.localPosition = _restPosition + (Vector3)(_axis.normalized * _offset);
    }
}
