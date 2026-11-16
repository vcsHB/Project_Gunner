using UnityEngine;

/// <summary>
/// 필드 수리 키트. 적재함에 겹쳐 쌓이는 평범한 아이템이고, R(<see cref="MechaRepair"/>)로 하나씩 쓴다.
///
/// 쓰는 순간 하나가 없어지고, <see cref="Duration"/>에 걸쳐 코어와 모든 부위를 채운다.
/// <b>맞으면 거기서 끊기고 키트는 돌아오지 않는다</b> — 싸우는 중에 쓰는 물건이 아니다.
///
/// Id 대역: 10000~10999 (<see cref="ItemDatabaseSO"/>).
/// </summary>
[CreateAssetMenu(menuName = "SO/Item/RepairKitData")]
public class RepairKitDataSO : ItemDataSO
{
    [Header("Repair")]
    [Tooltip("다 돌았을 때 채우는 양. 코어와 각 부위가 <b>각각</b> 이만큼 찬다.")]
    [SerializeField, Min(0f)] private float _amount = 60f;

    [Tooltip("다 채우는 데 걸리는 시간(초). 그동안 맞으면 끊긴다.")]
    [SerializeField, Min(0.1f)] private float _duration = 4f;

    public float Amount => _amount;
    public float Duration => _duration;

    /// <summary>키트는 개체마다 다른 상태가 없다.</summary>
    public override bool RequiresInstance => false;
}
