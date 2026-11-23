using UnityEngine;

/// <summary>
/// 게임 중 상시 떠 있는 HUD. 플레이어를 찾아 조각들에게 나눠준다.
///
/// 무장 패널이 <b>둘</b>인 이유는 좌우 무장이 동시에 활성이기 때문이다.
/// 각 패널이 어느 팔을 보는지는 패널 자신(<see cref="WeaponPartBase"/>)이 들고 간다 —
/// 여기서 짝지어주면 인스펙터의 슬롯 설정과 배열 순서가 어긋날 수 있다.
/// </summary>
public class PartHud : MonoBehaviour
{
    [SerializeField] private PartPlayerStatus _partPlayerStatus;

    [Tooltip("팔마다 하나씩. 보는 팔은 각 패널의 Slot이 정한다.")]
    [SerializeField] private PartEquipmentStatus[] _armStatus;

    public Player Player { get; private set; }

    // Awake가 아니라 Start에서 붙인다. Agent의 컴포넌트 초기화가 Awake에서 끝나기 때문이다.
    private void Start()
    {
        if (Player == null)
            Bind(FindAnyObjectByType<Player>());
    }

    public void Bind(Player player)
    {
        Player = player;

        if (player == null)
        {
            Debug.LogWarning("[HUD] 씬에서 Player를 찾지 못했습니다.", this);
            return;
        }

        if (_partPlayerStatus != null)
            _partPlayerStatus.Bind(player);

        if (_armStatus == null) return;

        for (int i = 0; i < _armStatus.Length; i++)
        {
            if (_armStatus[i] != null)
                _armStatus[i].Bind(player);
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// 두 패널이 같은 팔을 보고 있으면 한쪽 무기가 HUD에 아예 안 나타난다.
    /// 실행해 보기 전에는 "패널이 둘인데 똑같다"로만 보여서 원인을 찾기 어렵다.
    /// </summary>
    private void OnValidate()
    {
        if (_armStatus == null || _armStatus.Length < 2) return;

        for (int i = 0; i < _armStatus.Length; i++)
        {
            for (int j = i + 1; j < _armStatus.Length; j++)
            {
                if (_armStatus[i] == null || _armStatus[j] == null) continue;

                if (_armStatus[i].EditorSlot == _armStatus[j].EditorSlot)
                    Debug.LogWarning($"[HUD] 무장 패널 두 개가 같은 팔({_armStatus[i].EditorSlot})을 봅니다.", this);
            }
        }
    }
#endif
}
