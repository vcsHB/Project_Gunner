using UnityEngine;

/// <summary>
/// <see cref="ParticleSystem"/>으로 만든 이펙트. <see cref="EffectPlayer"/>가 꺼내 위치를 잡은 뒤 재생한다.
///
/// <b>파티클이 전부 사라지면 스스로 풀에 돌아간다</b> (<c>IsAlive(true)</c> — 자식 시스템까지 본다).
/// 루프 이펙트는 끝나지 않으므로 핸들로 <see cref="Stop"/>을 불러야 한다. 부르면 방출만 멈추고 남은 파티클이 사라진 뒤 반납된다.
///
/// 루트에 <see cref="ParticleSystem"/>이 있어야 한다. 이펙트 프리팹도 <b>+X가 정면</b>이다.
/// 정렬(Sorting Layer / Order)은 프리팹의 <see cref="ParticleSystemRenderer"/>에서 지정한다 — 기본값이면 스프라이트 뒤로 숨는다.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class ParticleEffect : EffectBase
{
    private ParticleSystem _root;
    private bool _loops;

    private Transform _follow;
    private bool _followRotation;
    private bool _playing;

    /// <summary>꺼낼 때마다 바뀐다. 핸들이 "내가 꺼낸 그 회차인지"를 확인하는 데 쓴다.</summary>
    public int SpawnVersion { get; private set; }

    private void Awake()
    {
        _root = GetComponent<ParticleSystem>();

        foreach (ParticleSystem system in GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = system.main;

            // 재생 시점은 EffectPlayer가 정한다. 켜지는 순간 옛 위치에서 터지면 안 된다.
            main.playOnAwake = false;

            if (main.loop) _loops = true;
        }
    }

    public override void OnSpawn()
    {
        base.OnSpawn();

        SpawnVersion++;
        _playing = false;
        _root.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    public override void OnDespawn()
    {
        base.OnDespawn();

        _follow = null;
        _playing = false;
        _root.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    /// <summary>
    /// 위치가 잡힌 뒤 <see cref="EffectPlayer"/>가 부른다.
    /// <paramref name="follow"/>는 부모로 붙이지 않고 매 프레임 위치만 맞춘다 —
    /// 대상이 풀로 돌아가거나 파괴돼도 이펙트가 끌려가지 않는다.
    /// </summary>
    public void Begin(Transform follow, bool followRotation)
    {
        _follow = follow;
        _followRotation = followRotation;
        _playing = true;

        _root.Play(true);
    }

    /// <summary>방출을 멈춘다. 남은 파티클은 끝까지 살고, 다 사라지면 반납된다.</summary>
    public void Stop()
    {
        _follow = null;
        _root.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    protected override void Update()
    {
        if (!_playing) return;

        // 루프는 끝이 없으니 수명 안전장치를 걸지 않는다. Stop을 빠뜨리면 남는다 — 핸들 소유자가 책임진다.
        if (!_loops)
            base.Update();

        if (IsInPool) return;

        if (!_root.IsAlive(true))
            Release();
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

        if (_followRotation)
            transform.SetPositionAndRotation(_follow.position, _follow.rotation);
        else
            transform.position = _follow.position;
    }
}
