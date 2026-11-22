using UnityEngine;

/// <summary>
/// 내 기체가 부서지면(사망) 화면을 강하게 흔든다. 카메라가 뒤로 빠지는 건 <see cref="CameraFollowEffector"/>가 사망 상태를 읽어서 한다.
/// <see cref="Health.OnDeadEvent"/>를 듣는다 — 죽음이 카메라를 부르지 않는다. 발사·피격 흔들림과 같은 자리(CameraManager)에 산다.
/// </summary>
public class DeathCameraShake : MonoBehaviour
{
    [SerializeField, Min(0f)] private float _power = 1.2f;
    [SerializeField, Min(0.01f)] private float _duration = 0.9f;

    private Health _health;
    private CameraShakeEffector _shake;

    // HUD와 같은 방식. Agent의 컴포넌트 초기화가 Awake에서 끝나므로 Start에서 붙는다.
    private void Start()
    {
        if (_health == null)
            Bind(FindAnyObjectByType<Player>());
    }

    public void Bind(Player player)
    {
        Unbind();

        if (player == null) return;

        _shake = CameraManager.Instance != null
            ? CameraManager.Instance.GetEffector<CameraShakeEffector>()
            : null;

        _health = player.GetCompo<Health>();

        if (_health != null)
            _health.OnDeadEvent += HandleDead;
    }

    public void Unbind()
    {
        if (_health != null)
        {
            _health.OnDeadEvent -= HandleDead;
            _health = null;
        }

        _shake = null;
    }

    private void OnDestroy() => Unbind();

    private void HandleDead()
    {
        if (_shake != null)
            _shake.Shake(_power, _duration);
    }
}
