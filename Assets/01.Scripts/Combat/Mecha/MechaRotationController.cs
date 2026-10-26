using UnityEngine;

/// <summary>
/// 기체를 조준 방향으로 돌린다. <b>상체가 먼저 돌고 하체가 따라온다.</b>
///
/// 메카의 무게감은 "전체가 한 덩어리로 도는데 부위마다 반응이 다르다"에서 나온다.
/// 상체(코어)는 커서를 빠르게 쫓고, 하체(허리)는 한참 벌어진 뒤에야 느리게 끌려온다.
/// 둘을 같은 속도로 돌리면 그냥 스프라이트 하나가 회전하는 것과 구별되지 않는다.
///
/// <b>발사 방향은 여기서 정하지 않는다.</b> 탄은 <see cref="PlayerAimController.AimDirection"/>으로
/// 나가므로, 상체가 아직 다 안 돌아갔어도 조준한 곳으로 맞는다. 회전은 표현이다 —
/// 표현이 판정을 끌고 가면 "보이는 곳과 맞는 곳이 다른" 문제를 연출로 덮게 된다.
/// </summary>
[DefaultExecutionOrder(AimExecutionOrder.MechaRotation)]
public class MechaRotationController : MonoBehaviour, IAgentComponent
{
    [Tooltip("상체. 무기가 달린 루트가 이 아래에 있어야 총구가 같이 돈다.")]
    [SerializeField] private Transform _coreRoot;

    [Tooltip("하체. 상체를 뒤늦게 따라간다.")]
    [SerializeField] private Transform _waistRoot;

    [Header("상체")]
    [Tooltip("초당 회전 각도. 클수록 커서에 민감하게 붙는다.")]
    [SerializeField, Min(1f)] private float _coreTurnSpeed = 540f;

    [Header("하체")]
    [Tooltip("초당 회전 각도. 상체보다 느려야 따라오는 것으로 보인다.")]
    [SerializeField, Min(1f)] private float _waistTurnSpeed = 160f;

    [Tooltip("상체와 이 각도 이상 벌어져야 하체가 움직이기 시작한다. " +
             "작은 조준 보정에는 다리가 가만히 있어야 무게가 느껴진다.")]
    [SerializeField, Range(0f, 90f)] private float _waistBreakAngle = 30f;

    [Tooltip("한 번 돌기 시작하면 이 각도 안으로 들어올 때까지 멈추지 않는다. " +
             "0으로 두면 경계에서 돌았다 멈췄다 하며 떨린다.")]
    [SerializeField, Range(0f, 30f)] private float _waistSettleAngle = 2f;

    private Player _player;
    private PlayerAimController _aim;

    // 경계에서 떨지 않도록 "지금 돌고 있는 중인가"를 들고 간다.
    // 각도 차이만 보고 매 프레임 판단하면 BreakAngle 근처에서 켜졌다 꺼졌다 한다.
    private bool _waistTurning;

    /// <summary>상체가 향한 각도(도). 조준 방향과 다를 수 있다 — 따라가는 중이면 뒤처진다.</summary>
    public float CoreAngle => _coreRoot != null ? _coreRoot.eulerAngles.z : 0f;

    /// <summary>하체가 향한 각도(도).</summary>
    public float WaistAngle => _waistRoot != null ? _waistRoot.eulerAngles.z : 0f;

    public void Initialize(Agent owner)
    {
        _player = owner as Player;
    }

    public void AfterInitialize()
    {
        _aim = _player != null ? _player.GetCompo<PlayerAimController>() : null;

        if (_aim == null)
            Debug.LogError("[MechaRotation] PlayerAimController가 없습니다.", this);
    }

    public void Dispose()
    {
        _aim = null;
    }

    // 조준 계산이 끝난 뒤에 돌아야 한 프레임 늦지 않는다. (AimExecutionOrder)
    private void LateUpdate()
    {
        if (_aim == null) return;

        float deltaTime = Time.deltaTime;

        float targetAngle = Mathf.Atan2(_aim.AimDirection.y, _aim.AimDirection.x) * Mathf.Rad2Deg;

        TurnCore(targetAngle, deltaTime);
        TurnWaist(deltaTime);
    }

    private void TurnCore(float targetAngle, float deltaTime)
    {
        if (_coreRoot == null) return;

        float current = _coreRoot.eulerAngles.z;
        float next = Mathf.MoveTowardsAngle(current, targetAngle, _coreTurnSpeed * deltaTime);

        _coreRoot.rotation = Quaternion.Euler(0f, 0f, next);
    }

    /// <summary>
    /// 하체는 조준 방향이 아니라 <b>상체를 따라간다.</b>
    /// 조준을 직접 보게 하면 상체가 아직 돌고 있는데 하체가 먼저 도는 구간이 생긴다.
    /// </summary>
    private void TurnWaist(float deltaTime)
    {
        if (_waistRoot == null || _coreRoot == null) return;

        float current = _waistRoot.eulerAngles.z;
        float coreAngle = _coreRoot.eulerAngles.z;

        float gap = Mathf.Abs(Mathf.DeltaAngle(current, coreAngle));

        if (_waistTurning)
        {
            if (gap <= _waistSettleAngle)
            {
                _waistTurning = false;
                return;
            }
        }
        else
        {
            if (gap < _waistBreakAngle) return;

            _waistTurning = true;
        }

        float next = Mathf.MoveTowardsAngle(current, coreAngle, _waistTurnSpeed * deltaTime);
        _waistRoot.rotation = Quaternion.Euler(0f, 0f, next);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // 정착 각도가 출발 각도보다 크면 하체가 영영 멈추지 않는다.
        if (_waistSettleAngle >= _waistBreakAngle)
            _waistSettleAngle = Mathf.Max(0f, _waistBreakAngle - 1f);
    }
#endif
}
