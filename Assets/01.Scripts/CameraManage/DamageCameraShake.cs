using UnityEngine;

/// <summary>
/// 플레이어가 맞으면 화면이 흔들린다.
///
/// 체력 게이지만으로는 <b>맞은 순간</b>을 알 수 없다. 숫자가 줄어든 것은
/// 다시 쳐다봐야 알고, 전투 중에는 쳐다볼 겨를이 없다.
///
/// 발사 흔들림(<see cref="WeaponFireCameraShake"/>)과 같은 자리에 산다.
/// 피해량이 클수록 세게 흔들리되 상한이 있다 — 한 방에 화면을 못 보게 되면 안 된다.
/// </summary>
public class DamageCameraShake : MonoBehaviour
{
    [Tooltip("최대 체력의 몇 %를 잃었을 때 최대 세기가 되는가. 0.25면 25%.")]
    [SerializeField, Range(0.01f, 1f)] private float _fullShakeRatio = 0.25f;

    [SerializeField, Min(0f)] private float _minPower = 0.12f;
    [SerializeField, Min(0f)] private float _maxPower = 0.45f;
    [SerializeField, Min(0.01f)] private float _duration = 0.2f;

    private Health _health;
    private CameraShakeEffector _shake;

    // HUD와 같은 방식. Agent의 컴포넌트 초기화가 Awake에서 끝나므로 Start에서 붙는다.
    private void Start()
    {
        if (_health == null)
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

        _health = player.GetCompo<Health>();

        if (_health != null)
            _health.OnDamagedEvent += HandleDamaged;
    }

    public void Unbind()
    {
        if (_health != null)
        {
            _health.OnDamagedEvent -= HandleDamaged;
            _health = null;
        }

        _shake = null;
    }

    private void OnDestroy()
    {
        PlayerPilot.OnLocalMechaChangedEvent -= Bind;
        Unbind();
    }

    private void HandleDamaged(DamageData damage)
    {
        if (_shake == null || _health == null) return;

        // 최대 체력 대비로 본다. 절대값으로 두면 체력이 늘어날수록 같은 피해가 더 아프게 보인다.
        float max = Mathf.Max(1f, _health.MaxHealth);
        float ratio = Mathf.Clamp01(damage.damage / (max * _fullShakeRatio));

        _shake.Shake(Mathf.Lerp(_minPower, _maxPower, ratio), _duration);
    }
}
