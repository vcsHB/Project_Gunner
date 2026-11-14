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
///
/// 각도는 전부 <b>논리 각도</b>(0° = +X)로 들고 간다. 리소스가 위를 보고 그려진 차이는
/// <see cref="FacingType"/>이 transform에 쓸 때만 흡수한다. 내부 상태에 보정을 섞으면
/// 각도를 읽을 때마다 "이게 보정된 값인가"를 따져야 한다.
/// </summary>
[RequireAgentComponent(typeof(PlayerAimController))]
[DefaultExecutionOrder(AimExecutionOrder.MechaRotation)]
public class MechaRotationController : MonoBehaviour, IAgentComponent
{
    [Tooltip("기체 리소스가 그려진 방향. 탑다운 메카는 보통 Up이다.")]
    [SerializeField] private FacingType _facing = FacingType.Up;

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
    private MechaAssembler _assembler;

    // 조립된 코어에서 받아온다. 씬 참조로 박아두면 코어를 바꾸는 순간 옛 몸을 돌린다.
    private Transform _coreRoot;
    private Transform _waistRoot;

    // 경계에서 떨지 않도록 "지금 돌고 있는 중인가"를 들고 간다.
    // 각도 차이만 보고 매 프레임 판단하면 BreakAngle 근처에서 켜졌다 꺼졌다 한다.
    private bool _waistTurning;

    /// <summary>상체가 향한 논리 각도(도). 조준 방향과 다를 수 있다 — 따라가는 중이면 뒤처진다.</summary>
    public float CoreAngle { get; private set; }

    /// <summary>하체가 향한 논리 각도(도).</summary>
    public float WaistAngle { get; private set; }

    public void Initialize(Agent owner)
    {
        _player = owner as Player;
    }

    public void AfterInitialize()
    {
        _aim = _player != null ? _player.GetCompo<PlayerAimController>() : null;

        if (_aim == null)
        {
            Debug.LogError("[MechaRotation] PlayerAimController가 없습니다.", this);
            return;
        }

        _assembler = _player.GetCompo<MechaAssembler>();

        if (_assembler != null)
            _assembler.OnAssembledEvent += HandleAssembled;

        // 시작부터 조준 방향을 보고 있게 한다. 안 그러면 첫 프레임에 기체가 홱 돈다.
        CoreAngle = WaistAngle = ToAngle(_aim.AimDirection);

        HandleAssembled();
    }

    public void Dispose()
    {
        if (_assembler != null)
        {
            _assembler.OnAssembledEvent -= HandleAssembled;
            _assembler = null;
        }

        _aim = null;
        _coreRoot = null;
        _waistRoot = null;
    }

    /// <summary>
    /// 돌릴 대상을 <b>조립된 코어에서 받아온다.</b>
    /// 코어는 장비라서 판 중에도 갈릴 수 있고, 그때마다 옛 몸을 돌리고 있으면 안 된다.
    /// </summary>
    private void HandleAssembled()
    {
        MechaPartView core = _assembler != null ? _assembler.Core : null;

        _coreRoot = core != null ? core.transform : null;
        _waistRoot = core != null ? core.WaistRoot : null;

        Write();
    }

    // 조준 계산이 끝난 뒤에 돌아야 한 프레임 늦지 않는다. (AimExecutionOrder)
    private void LateUpdate()
    {
        if (_aim == null) return;

        float deltaTime = Time.deltaTime;

        TurnCore(ToAngle(_aim.AimDirection), deltaTime);
        TurnWaist(deltaTime);

        Write();
    }

    private static float ToAngle(Vector2 direction)
        => Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

    private void TurnCore(float targetAngle, float deltaTime)
        => CoreAngle = Mathf.MoveTowardsAngle(CoreAngle, targetAngle, _coreTurnSpeed * deltaTime);

    /// <summary>
    /// 하체는 조준 방향이 아니라 <b>상체를 따라간다.</b>
    /// 조준을 직접 보게 하면 상체가 아직 돌고 있는데 하체가 먼저 도는 구간이 생긴다.
    /// </summary>
    private void TurnWaist(float deltaTime)
    {
        float gap = Mathf.Abs(Mathf.DeltaAngle(WaistAngle, CoreAngle));

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

        WaistAngle = Mathf.MoveTowardsAngle(WaistAngle, CoreAngle, _waistTurnSpeed * deltaTime);
    }

    /// <summary>
    /// 리소스 방향 보정은 여기서만 들어간다.
    ///
    /// <b>월드 회전으로 쓴다.</b> 하체를 상체 자식으로 두는 배치가 흔한데(다리가 몸을 따라다녀야 하니까),
    /// 로컬로 쓰면 상체 회전이 하체에 더해져서 두 배로 돌아간다.
    /// 기체 루트 자체는 돌지 않으므로 월드로 써도 잃는 것이 없다.
    /// </summary>
    private void Write()
    {
        if (_coreRoot != null)
            _coreRoot.rotation = _facing.ToRotation(CoreAngle);

        if (_waistRoot != null)
            _waistRoot.rotation = _facing.ToRotation(WaistAngle);
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
