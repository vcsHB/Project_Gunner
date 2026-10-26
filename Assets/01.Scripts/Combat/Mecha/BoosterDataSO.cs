using UnityEngine;

/// <summary>
/// 추진체. 이동과 연료를 담당한다.
///
/// 이동속도와 연료 최대치는 <see cref="PartDataSO.Deltas"/>(MoveSpeed, 연료 스탯)로 준다.
/// 여기에는 스탯으로 표현되지 않는 가속 특성만 둔다.
/// </summary>
[CreateAssetMenu(menuName = "SO/Item/BoosterData")]
public class BoosterDataSO : PartDataSO
{
    [Header("Boost")]
    [Tooltip("부스터를 켰을 때 이동속도에 곱해지는 값.")]
    [SerializeField, Min(1f)] private float _boostMultiplier = 1.6f;

    [Tooltip("부스터가 초당 소모하는 연료.")]
    [SerializeField, Min(0f)] private float _fuelPerSecond = 5f;

    public float BoostMultiplier => _boostMultiplier;
    public float FuelPerSecond => _fuelPerSecond;
}
