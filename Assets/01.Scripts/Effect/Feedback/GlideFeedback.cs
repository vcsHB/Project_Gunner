using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 활공의 "떠 있는 느낌". <b>기체(플레이어 루트)에 붙인다.</b>
///
/// 높이는 <see cref="PlayerMoveController.Altitude"/>가 정한다(로직). 여기는 그 값을 읽어 그리기만 한다 —
/// <list type="bullet">
/// <item><b>몸체를 화면 위로 띄운다</b>(<see cref="_liftTargets"/>: 몸체 루트, 실드). 히트박스·총구·실드 원도 같이 뜬다 — 보이는 곳에 맞고, 보이는 곳에서 쏜다.</item>
/// <item><b>그림자는 제자리</b>에 남아 작아지고 흐려진다. 그림자가 실제 위치(이동·벽·줍기)다.</item>
/// <item>몸체가 살짝 커지고, 떠 있는 동안 위아래로 미세하게 흔들린다.</item>
/// <item>떠 있는 동안 정렬을 올려 지상 유닛 위에 그린다(<see cref="SortingGroup"/>, 활공 중에만 켬).</item>
/// <item>이륙·착륙·활공 중 이펙트와 소리(비워도 된다).</item>
/// </list>
/// 카메라 줌아웃은 카메라 쪽(<see cref="CameraFollowEffector"/>)이 같은 고도 값을 읽는다.
///
/// <b>부모가 돌지 않는다고 가정한다.</b> 띄우는 대상의 부모는 기체 루트처럼 회전하지 않는 곳이어야 한다 —
/// 도는 코어 아래를 띄우면 "위"가 기체를 따라 돈다.
/// </summary>
public class GlideFeedback : MonoBehaviour
{
    [Header("Lift")]
    [Tooltip("화면 위로 띄울 것. 몸체 루트와 실드. 그림자는 넣지 않는다.")]
    [SerializeField] private Transform[] _liftTargets;
    [Tooltip("완전히 떴을 때 몸체 크기 배율(띄우는 대상 중 첫 번째만).")]
    [SerializeField, Range(1f, 1.5f)] private float _liftScale = 1.1f;
    [Tooltip("떠 있는 동안 위아래 흔들림 폭(월드).")]
    [SerializeField, Min(0f)] private float _bobAmplitude = 0.04f;
    [SerializeField, Min(0f)] private float _bobFrequency = 1.5f;

    [Header("Shadow")]
    [Tooltip("비우면 자식의 Shadow를 찾는다.")]
    [SerializeField] private Transform _shadow;
    [Tooltip("완전히 떴을 때 그림자 크기 배율.")]
    [SerializeField, Range(0.2f, 1f)] private float _shadowScale = 0.75f;
    [Tooltip("완전히 떴을 때 그림자 불투명도 배율.")]
    [SerializeField, Range(0f, 1f)] private float _shadowAlpha = 0.6f;

    [Header("Sorting")]
    [Tooltip("활공 중 몸체 정렬 순서. 지상 유닛보다 위에 그린다.")]
    [SerializeField] private string _glideSortingLayer = "Unit";
    [SerializeField] private int _glideSortingOrder = 50;

    [Header("Effect / Sound")]
    [SerializeField] private PoolType _takeoffEffect = PoolType.None;
    [SerializeField] private PoolType _landEffect = PoolType.None;
    [Tooltip("활공 중 계속 뿜는 이펙트(하강기류). 발밑(그림자)을 따라간다.")]
    [SerializeField] private PoolType _glideLoopEffect = PoolType.None;
    [SerializeField] private SoundDataSO _takeoffSound;
    [SerializeField] private SoundDataSO _landSound;
    [Tooltip("활공 중 루프 소리.")]
    [SerializeField] private SoundDataSO _glideLoopSound;

    private PlayerMoveController _move;

    private Vector3[] _baseLocal;
    private Vector3 _bodyBaseScale;
    private Vector3 _shadowBaseScale;
    private SpriteRenderer[] _shadowRenderers;
    private float[] _shadowBaseAlpha;
    private SortingGroup _bodySorting;

    private EffectHandle _loopEffect;
    private SoundHandle _loopSound;

    private void Awake()
    {
        _move = GetComponent<PlayerMoveController>();

        if (_liftTargets == null) _liftTargets = new Transform[0];

        _baseLocal = new Vector3[_liftTargets.Length];
        for (int i = 0; i < _liftTargets.Length; i++)
        {
            if (_liftTargets[i] != null)
                _baseLocal[i] = _liftTargets[i].localPosition;
        }

        if (_liftTargets.Length > 0 && _liftTargets[0] != null)
        {
            _bodyBaseScale = _liftTargets[0].localScale;

            // 지상에서는 기존 정렬 그대로 두고, 뜬 동안만 묶어서 위로 올린다.
            _bodySorting = _liftTargets[0].GetComponent<SortingGroup>();
            if (_bodySorting == null)
                _bodySorting = _liftTargets[0].gameObject.AddComponent<SortingGroup>();

            _bodySorting.sortingLayerName = _glideSortingLayer;
            _bodySorting.sortingOrder = _glideSortingOrder;
            _bodySorting.enabled = false;
        }

        if (_shadow == null)
        {
            Shadow shadow = GetComponentInChildren<Shadow>(true);
            if (shadow != null) _shadow = shadow.transform;
        }

        if (_shadow != null)
        {
            _shadowBaseScale = _shadow.localScale;
            _shadowRenderers = _shadow.GetComponentsInChildren<SpriteRenderer>(true);
            _shadowBaseAlpha = new float[_shadowRenderers.Length];
            for (int i = 0; i < _shadowRenderers.Length; i++)
                _shadowBaseAlpha[i] = _shadowRenderers[i].color.a;
        }
    }

    private void OnEnable()
    {
        if (_move != null)
            _move.OnMoveModeChangedEvent += HandleModeChanged;
    }

    private void OnDisable()
    {
        if (_move != null)
            _move.OnMoveModeChangedEvent -= HandleModeChanged;

        StopLoop();
        Apply(0f, 0f);
    }

    private void HandleModeChanged(MechaMoveMode prev, MechaMoveMode next)
    {
        Vector2 feet = transform.position;

        if (next == MechaMoveMode.Gliding)
        {
            EffectPlayer.Play(_takeoffEffect, feet, Vector2.up);
            SoundManager.Play(_takeoffSound, feet);

            StopLoop();
            _loopEffect = EffectPlayer.Play(_glideLoopEffect, feet, Vector2.up, transform);
            _loopSound = SoundManager.Play(_glideLoopSound, feet, transform);
        }
        else
        {
            StopLoop();

            EffectPlayer.Play(_landEffect, feet, Vector2.up);
            SoundManager.Play(_landSound, feet);
        }
    }

    private void StopLoop()
    {
        _loopEffect.Stop();
        _loopEffect = default;

        _loopSound.Stop();
        _loopSound = default;
    }

    private void LateUpdate()
    {
        if (_move == null) return;

        Apply(_move.Altitude, _move.Altitude01);
    }

    private void Apply(float altitude, float altitude01)
    {
        float bob = altitude01 > 0f ? Mathf.Sin(Time.time * _bobFrequency * Mathf.PI * 2f) * _bobAmplitude * altitude01 : 0f;
        float height = altitude + bob;

        for (int i = 0; i < _liftTargets.Length; i++)
        {
            Transform target = _liftTargets[i];
            if (target == null) continue;

            // 부모 공간의 "화면 위". 부모가 돌지 않으면 그냥 +Y다.
            Vector3 up = target.parent != null ? target.parent.InverseTransformDirection(Vector3.up) : Vector3.up;
            target.localPosition = _baseLocal[i] + up * height;
        }

        if (_liftTargets.Length > 0 && _liftTargets[0] != null)
            _liftTargets[0].localScale = _bodyBaseScale * Mathf.Lerp(1f, _liftScale, altitude01);

        if (_bodySorting != null)
            _bodySorting.enabled = altitude01 > 0.05f;

        if (_shadow != null)
        {
            _shadow.localScale = _shadowBaseScale * Mathf.Lerp(1f, _shadowScale, altitude01);

            float alpha = Mathf.Lerp(1f, _shadowAlpha, altitude01);
            for (int i = 0; i < _shadowRenderers.Length; i++)
            {
                Color c = _shadowRenderers[i].color;
                c.a = _shadowBaseAlpha[i] * alpha;
                _shadowRenderers[i].color = c;
            }
        }
    }
}
