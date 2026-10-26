using UnityEngine;

/// <summary>
/// 기체 본체. 체력·장갑·적재 칸 수는 <see cref="PartDataSO.Deltas"/>로 주고,
/// 여기에는 <b>스탯으로 표현되지 않는 것</b>만 둔다.
///
/// 조준점이 무기가 아니라 코어에 붙는 이유는 좌우 무기가 동시에 활성이기 때문이다.
/// 무기별로 두면 어느 쪽 조준점을 띄울지 매 프레임 판단해야 한다.
/// </summary>
[CreateAssetMenu(menuName = "SO/Item/CoreData")]
public class CoreDataSO : PartDataSO
{
    [Header("Aim")]
    [Tooltip("이 코어를 달았을 때의 조준점 모양.")]
    [SerializeField] private PlayerAimPointStateType _aimPoint = PlayerAimPointStateType.None;

    [Tooltip("조준점을 기체에서 얼마나 멀리 둘 수 있는가. 탄이 날아가는 거리(Range)와는 별개다.")]
    [SerializeField, Min(1f)] private float _aimRange = 15f;

    public PlayerAimPointStateType AimPoint => _aimPoint;
    public float AimRange => _aimRange;
}
