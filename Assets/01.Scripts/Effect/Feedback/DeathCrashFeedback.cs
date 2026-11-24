using UnityEngine;

/// <summary>
/// 죽음의 두 장면 — <b>추락하는 동안 연기</b>(<see cref="IDeathCrash"/>)와 <b>부서질 때 폭발</b>(<see cref="Health.OnWreckedEvent"/>).
/// 유닛 루트에 붙인다. 적·플레이어 공통.
///
/// 땅에서 죽으면 연기 없이 바로 폭발한다. 떠 있다 죽으면 연기를 끌며 날아가다 땅에 닿을 때 폭발한다.
///
/// 파티클 에셋이 아직 없어 <b>코드로 만든 임시 파티클</b>을 쓴다(사각 입자 — 픽셀 아트에 어울린다).
/// 아트가 나오면 <see cref="_impactEffect"/>에 풀 이펙트를 넣고, 연기는 <see cref="_smoke"/>에 프리팹의 파티클을 끼우면 된다.
/// </summary>
public class DeathCrashFeedback : MonoBehaviour
{
    [Tooltip("연기가 나오는 곳. 활공으로 뜨는 몸체 쪽(GlideFeedback이 띄우는 대상)에 둔다 — 그림자에서 나오면 안 된다.")]
    [SerializeField] private Transform _emitAt;

    [Tooltip("스프라이트용 머티리얼(Sprite-Unlit-Default). 임시 파티클이 쓴다.")]
    [SerializeField] private Material _material;

    [SerializeField] private string _sortingLayer = "Effect_Front";

    [Header("연기 (추락 중)")]
    [Tooltip("비우면 코드로 만든다.")]
    [SerializeField] private ParticleSystem _smoke;
    [SerializeField] private Color _smokeColor = new(0.25f, 0.25f, 0.27f, 0.85f);
    [SerializeField, Min(0f)] private float _smokeRate = 40f;

    [Header("폭발 (부서질 때)")]
    [Tooltip("풀 이펙트. 비우면 코드로 만든 임시 폭발.")]
    [SerializeField] private PoolType _impactEffect = PoolType.None;
    [SerializeField] private SoundDataSO _impactSound;
    [SerializeField] private Color _fireColor = new(1f, 0.6f, 0.15f, 1f);
    [SerializeField] private Color _emberColor = new(0.9f, 0.2f, 0.1f, 1f);
    [SerializeField, Range(4, 120)] private int _burstCount = 45;

    private Health _health;
    private IDeathCrash _crash;
    private ParticleSystem _burst;

    private void Start()
    {
        Agent owner = GetComponentInParent<Agent>();
        if (owner == null) return;

        if (_emitAt == null) _emitAt = transform;

        _health = owner.GetCompo<Health>();
        _crash = owner.GetCompo<IDeathCrash>();

        if (_smoke == null) _smoke = BuildSmoke();
        _smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (_impactEffect == PoolType.None) _burst = BuildBurst();

        if (_crash != null)
            _crash.OnCrashStartedEvent += HandleCrashStarted;

        if (_health != null)
        {
            _health.OnWreckedEvent += HandleWrecked;
            _health.OnRevivedEvent += HandleRevived;
        }
    }

    private void OnDestroy()
    {
        if (_crash != null)
            _crash.OnCrashStartedEvent -= HandleCrashStarted;

        if (_health != null)
        {
            _health.OnWreckedEvent -= HandleWrecked;
            _health.OnRevivedEvent -= HandleRevived;
        }
    }

    private void HandleCrashStarted() => _smoke.Play(true);

    private void HandleWrecked()
    {
        // 떨어지면서 끌던 연기는 남은 입자가 흩어지게 두고 새로 뿜지 않는다.
        _smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        Vector2 at = transform.position;
        SoundManager.Play(_impactSound, at);

        if (_impactEffect != PoolType.None)
        {
            EffectPlayer.Play(_impactEffect, at, Vector2.up);
            return;
        }

        if (_burst == null) return;

        // 몸이 부서지면(적) 이 오브젝트도 사라진다. 폭발은 떼어서 스스로 끝나게 둔다.
        _burst.transform.SetParent(null, true);
        _burst.transform.position = at;
        _burst.Emit(_burstCount);
        Destroy(_burst.gameObject, 2f);
        _burst = BuildBurst();
    }

    private void HandleRevived()
    {
        _smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    #region 임시 파티클

    private ParticleSystem BuildSmoke()
    {
        ParticleSystem ps = Create("CrashSmoke", _emitAt);

        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.26f);
        main.startColor = _smokeColor;
        main.maxParticles = 200;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = _smokeRate;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.15f;

        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.8f, 1f, 2.2f));

        ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = Fade(Color.white);

        return ps;
    }

    private ParticleSystem BuildBurst()
    {
        ParticleSystem ps = Create("CrashBurst", transform);

        ParticleSystem.MainModule main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
        main.startColor = new ParticleSystem.MinMaxGradient(_fireColor, _emberColor);
        main.maxParticles = 150;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.2f;

        ParticleSystem.LimitVelocityOverLifetimeModule drag = ps.limitVelocityOverLifetime;
        drag.enabled = true;
        drag.drag = 3f;

        ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = Fade(new Color(0.3f, 0.3f, 0.3f, 1f));

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return ps;
    }

    private ParticleSystem Create(string objectName, Transform parent)
    {
        GameObject go = new(objectName);
        go.transform.SetParent(parent, false);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f;
        main.playOnAwake = false;

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = _material;
        renderer.sortingLayerName = _sortingLayer;

        return ps;
    }

    /// <summary>시작 색에 곱해진다. 끝으로 갈수록 <paramref name="end"/>만큼 어두워지며 사라진다.</summary>
    private static ParticleSystem.MinMaxGradient Fade(Color end)
    {
        Gradient gradient = new();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(end, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });

        return new ParticleSystem.MinMaxGradient(gradient);
    }

    #endregion
}
