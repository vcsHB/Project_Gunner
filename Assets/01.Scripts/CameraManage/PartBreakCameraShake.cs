using UnityEngine;

/// <summary>
/// 내 기체의 부위가 부서지면 화면을 크게 흔든다.
///
/// <see cref="MechaPartHealth.OnPartDestroyedEvent"/>를 듣는다 — 부위 체력이 카메라를 부르지 않는다.
/// 발사·피격·착륙 흔들림과 같은 자리(CameraManager)에 산다. 적의 부위파괴는 흔들지 않는다(내 화면이다).
/// </summary>
public class PartBreakCameraShake : MonoBehaviour
{
    [SerializeField, Min(0f)] private float _power = 0.6f;
    [SerializeField, Min(0.01f)] private float _duration = 0.45f;

    private MechaPartHealth _parts;
    private CameraShakeEffector _shake;

    // HUD와 같은 방식. Agent의 컴포넌트 초기화가 Awake에서 끝나므로 Start에서 붙는다.
    private void Start()
    {
        if (_parts == null)
            Bind(FindAnyObjectByType<Player>());
    }

    public void Bind(Player player)
    {
        Unbind();

        if (player == null) return;

        _shake = CameraManager.Instance != null
            ? CameraManager.Instance.GetEffector<CameraShakeEffector>()
            : null;

        if (_shake == null) return;

        _parts = player.GetCompo<MechaPartHealth>();

        if (_parts != null)
            _parts.OnPartDestroyedEvent += HandleDestroyed;
    }

    public void Unbind()
    {
        if (_parts != null)
        {
            _parts.OnPartDestroyedEvent -= HandleDestroyed;
            _parts = null;
        }

        _shake = null;
    }

    private void OnDestroy() => Unbind();

    private void HandleDestroyed(EquipSlotType slot)
    {
        if (_shake != null)
            _shake.Shake(_power, _duration);
    }
}
