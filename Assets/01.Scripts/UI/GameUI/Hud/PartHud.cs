using UnityEngine;

public class PartHud : MonoBehaviour
{
    [SerializeField] private PartPlayerStatus _partPlayerStatus;
    [SerializeField] private PartHudHotbar _partHotbar;

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

        if (_partHotbar != null)
            _partHotbar.Bind(player);
    }
}
