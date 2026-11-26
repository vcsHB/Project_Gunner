using UnityEngine;

/// <summary>
/// 내 기체의 실드가 깨지면 화면을 작게 흔든다. 부위파괴(<see cref="PartBreakCameraShake"/>)보다 약하게 —
/// "이제부터 몸으로 받는다"는 신호다.
///
/// <see cref="ShieldStatus.OnShieldBrokenEvent"/>를 듣는다. 적의 실드는 흔들지 않는다(내 화면이다).
/// </summary>
public class ShieldBreakCameraShake : MonoBehaviour
{
    [SerializeField, Min(0f)] private float _power = 0.3f;
    [SerializeField, Min(0.01f)] private float _duration = 0.25f;

    private ShieldStatus _shield;
    private CameraShakeEffector _shake;

    // HUD와 같은 방식. Agent의 컴포넌트 초기화가 Awake에서 끝나므로 Start에서 붙는다.
    private void Start()
    {
        if (_shield == null)
            Bind(PlayerPilot.LocalMecha);

        // 갈아타면 다시 붙는다.
        PlayerPilot.OnLocalMechaChangedEvent += Bind;
    }

    public void Bind(Player player)
    {
        Unbind();

        if (player == null) return;

        _shake = CameraManager.Instance != null
            ? CameraManager.Instance.GetEffector<CameraShakeEffector>()
            : null;

        if (_shake == null) return;

        _shield = player.GetCompo<ShieldStatus>();

        if (_shield != null)
            _shield.OnShieldBrokenEvent += HandleBroken;
    }

    public void Unbind()
    {
        if (_shield != null)
        {
            _shield.OnShieldBrokenEvent -= HandleBroken;
            _shield = null;
        }

        _shake = null;
    }

    private void OnDestroy()
    {
        PlayerPilot.OnLocalMechaChangedEvent -= Bind;
        Unbind();
    }

    private void HandleBroken()
    {
        if (_shake != null)
            _shake.Shake(_power, _duration);
    }
}
