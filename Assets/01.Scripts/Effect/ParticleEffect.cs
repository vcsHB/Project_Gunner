using UnityEngine;

/// <summary>
/// <see cref="ParticleSystem"/>으로 만든 이펙트. <see cref="EffectPlayer"/>가 꺼내 위치·방향을 잡은 뒤 재생한다.
/// 루트는 빈 Transform이어도 되고, 파티클 시스템은 자식에 여럿 있어도 된다.
///
/// <b>방향</b> — 아트가 바라보는 축을 <see cref="_artForwardAngle"/>에 적는다. 이 프로젝트의 이펙트 아트는 <b>+Y가 발산 방향</b>(90)이다.
/// <see cref="EffectPlayer"/>에는 "이펙트가 뿜어져야 할 월드 방향"만 넘기면 보정은 여기서 한 번 한다.
///
/// <b>반납</b> — 방출이 끝난 뒤(버스트만 있으면 마지막 버스트 시각, 계속 뿜는 시스템이 있으면 Duration 또는 <see cref="Stop"/>)
/// 살아있는 파티클이 0이 되면 스스로 풀로 돌아간다. 아트의 Duration(기본 5초)을 짧게 맞추지 않아도 버스트형은 바로 돌아간다.
/// 루프 이펙트는 끝나지 않으므로 핸들로 <see cref="Stop"/>을 불러야 한다.
///
/// 정렬(Sorting Layer / Order)은 각 <see cref="ParticleSystemRenderer"/>에서 지정한다 — 기본값이면 스프라이트 뒤로 숨는다.
/// </summary>
public class ParticleEffect : EffectBase
{
    [Tooltip("아트가 발산하는 방향(도). +Y로 만들었으면 90, +X면 0.")]
    [SerializeField] private float _artForwardAngle = 90f;

    private ParticleSystem[] _systems;
    private bool _loops;

    // 이 시각까지는 파티클이 0개여도 아직 방출 중일 수 있다.
    private float _emitUntil;

    private Transform _follow;
    private bool _playing;
    private bool _stopped;
    private float _elapsed;
    private int _framesPlayed;

    /// <summary>꺼낼 때마다 바뀐다. 핸들이 "내가 꺼낸 그 회차인지"를 확인하는 데 쓴다.</summary>
    public int SpawnVersion { get; private set; }

    private void Awake()
    {
        _systems = GetComponentsInChildren<ParticleSystem>(true);

        for (int i = 0; i < _systems.Length; i++)
        {
            ParticleSystem system = _systems[i];
            ParticleSystem.MainModule main = system.main;

            // 재생 시점은 EffectPlayer가 정한다. 켜지는 순간 옛 위치에서 터지면 안 된다.
            main.playOnAwake = false;
            // 반납은 여기서 한다. 시스템이 스스로 꺼지거나 파괴되면 풀이 꼬인다.
            main.stopAction = ParticleSystemStopAction.None;

            if (main.loop)
            {
                _loops = true;
                continue;
            }

            _emitUntil = Mathf.Max(_emitUntil, main.startDelay.constantMax + EmissionEnd(system, main));
        }
    }

    /// <summary>계속 뿜는 시스템은 Duration 끝까지, 버스트만 있으면 마지막 버스트까지.</summary>
    private static float EmissionEnd(ParticleSystem system, ParticleSystem.MainModule main)
    {
        ParticleSystem.EmissionModule emission = system.emission;
        if (!emission.enabled) return 0f;

        if (emission.rateOverTime.constantMax > 0f || emission.rateOverDistance.constantMax > 0f)
            return main.duration;

        float last = 0f;
        for (int i = 0; i < emission.burstCount; i++)
        {
            ParticleSystem.Burst burst = emission.GetBurst(i);
            float end = burst.time + burst.repeatInterval * Mathf.Max(0, burst.cycleCount - 1);
            last = Mathf.Max(last, end);
        }

        return Mathf.Min(last, main.duration);
    }

    public override void OnSpawn()
    {
        base.OnSpawn();

        SpawnVersion++;
        ResetState();
        StopAll(ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    public override void OnDespawn()
    {
        base.OnDespawn();

        ResetState();
        StopAll(ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void ResetState()
    {
        _follow = null;
        _playing = false;
        _stopped = false;
        _elapsed = 0f;
        _framesPlayed = 0;
    }

    /// <summary>
    /// 위치가 잡힌 뒤 <see cref="EffectPlayer"/>가 부른다.
    /// <paramref name="follow"/>는 부모로 붙이지 않고 매 프레임 위치만 맞춘다 —
    /// 대상이 풀로 돌아가거나 파괴돼도 이펙트가 끌려가지 않는다.
    /// </summary>
    public void Begin(float angle, Transform follow)
    {
        SetAngle(angle);

        _follow = follow;
        _playing = true;

        for (int i = 0; i < _systems.Length; i++)
            _systems[i].Play(false);
    }

    /// <summary>발산 방향(월드, 도). 아트 축 보정은 여기서 한다.</summary>
    public void SetAngle(float angle)
    {
        transform.rotation = Quaternion.Euler(0f, 0f, angle - _artForwardAngle);
    }

    /// <summary>방출을 멈춘다. 남은 파티클은 끝까지 살고, 다 사라지면 반납된다.</summary>
    public void Stop()
    {
        _follow = null;
        _stopped = true;
        StopAll(ParticleSystemStopBehavior.StopEmitting);
    }

    private void StopAll(ParticleSystemStopBehavior behavior)
    {
        if (_systems == null) return;

        for (int i = 0; i < _systems.Length; i++)
            _systems[i].Stop(false, behavior);
    }

    protected override void Update()
    {
        if (!_playing) return;

        // 루프는 끝이 없으니 수명 안전장치를 걸지 않는다. Stop을 빠뜨리면 남는다 — 핸들 소유자가 책임진다.
        if (!_loops)
        {
            base.Update();
            if (IsInPool) return;
        }

        _elapsed += Time.deltaTime;

        // 재생을 건 프레임에는 아직 파티클 시뮬레이션이 안 돌았을 수 있다. 0개를 "끝"으로 오해하지 않게 한 번 넘긴다.
        if (++_framesPlayed < 2) return;

        bool emitting = _loops ? !_stopped : !_stopped && _elapsed < _emitUntil;
        if (emitting) return;

        if (CountParticles() == 0)
            Release();
    }

    private int CountParticles()
    {
        int count = 0;
        for (int i = 0; i < _systems.Length; i++)
            count += _systems[i].particleCount;

        return count;
    }

    private void LateUpdate()
    {
        if (_follow == null) return;

        // 대상이 꺼지면(풀 반납·파괴) 그 자리에 남겨 두고 방출을 멈춘다.
        if (!_follow.gameObject.activeInHierarchy)
        {
            Stop();
            return;
        }

        transform.position = _follow.position;
    }
}
