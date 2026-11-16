using UnityEngine;

/// <summary>
/// 고속 착륙 미끄러짐의 바닥 긁힘. <b>기체(플레이어 루트)에 붙인다.</b>
///
/// <see cref="PlayerMoveController.OnSkidStartedEvent"/>에서 이펙트를 <b>한 번</b> 켜고, 미끄러지는 동안 발밑을 따라가며
/// 방향만 매 프레임 갱신한다. 이펙트가 계속 뿜는 형태(Rate over Time, World 시뮬레이션)라 매 프레임 새로 만들 필요가 없다 —
/// 지나온 자리에 파티클이 남아 자국이 된다. 미끄러짐이 끝나면 방출을 멈추고, 남은 파티클이 사라지면 풀로 돌아간다.
///
/// 방향은 진행 방향의 반대(뒤로 튄다). 아트는 "아래로 긁을 때 +Y로 튄다" 기준이다.
/// 카메라 흔들림은 여기가 아니다 — <see cref="SkidCameraShake"/>.
/// </summary>
public class SkidFeedback : MonoBehaviour
{
    [SerializeField] private PoolType _scratchEffect = PoolType.None;
    [Tooltip("미끄러지는 동안 도는 루프 소리. 비워도 된다.")]
    [SerializeField] private SoundDataSO _scratchSound;

    [Tooltip("이펙트를 붙일 발밑. 비우면 이 오브젝트.")]
    [SerializeField] private Transform _feet;

    private PlayerMoveController _move;
    private EffectHandle _effect;
    private SoundHandle _sound;

    private void Awake()
    {
        _move = GetComponent<PlayerMoveController>();

        if (_feet == null)
            _feet = transform;
    }

    private void OnEnable()
    {
        if (_move != null)
            _move.OnSkidStartedEvent += HandleSkidStarted;
    }

    private void OnDisable()
    {
        if (_move != null)
            _move.OnSkidStartedEvent -= HandleSkidStarted;

        StopAll();
    }

    private void HandleSkidStarted(float speedRatio)
    {
        StopAll();

        Vector2 back = -_move.Velocity;

        _effect = EffectPlayer.Play(_scratchEffect, _feet.position, back.sqrMagnitude > Mathf.Epsilon ? back : -(Vector2)_feet.up, _feet);
        _sound = SoundManager.Play(_scratchSound, _feet.position, _feet);
    }

    private void LateUpdate()
    {
        if (!_effect.IsValid && !_sound.IsValid) return;

        if (_move == null || !_move.IsSkidding)
        {
            StopAll();
            return;
        }

        _effect.SetDirection(-_move.Velocity);
    }

    private void StopAll()
    {
        _effect.Stop();
        _effect = default;

        _sound.Stop();
        _sound = default;
    }
}
