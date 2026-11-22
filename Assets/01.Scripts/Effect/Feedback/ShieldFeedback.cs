using UnityEngine;

/// <summary>
/// 실드가 <b>깨질 때</b>와 <b>다시 돌아올 때</b>. 실드 프리팹(<c>UnitShield</c>, <see cref="ShieldHitArea"/>와 같은 오브젝트)에 붙인다. 적·플레이어 공통.
///
/// 깨질 때 — 실드 그림은 <see cref="ShieldHitArea"/>가 그 순간 끈다. 그래서 여기서는 그림의 <b>복제본</b>을 띄워
/// 하얗게 번쩍이며 커졌다가 흩어지게 한다. 원본의 켜고 끄기는 건드리지 않는다 — 판정과 그림이 어긋나면 안 된다.
/// 깨지는 순간에는 이미 실드 판정이 꺼져 있다.
///
/// 돌아올 때(0 → 0 초과) — 다시 켜진 원본 그림을 작게 시작해 살짝 넘치며 제 크기로 돌린다.
///
/// 화면 흔들림은 여기가 아니다 — 내 화면만 흔들려야 하므로 카메라 쪽(<see cref="ShieldBreakCameraShake"/>)이 듣는다.
/// 기체가 죽어서 0이 된 것은 깨짐이 아니다(<see cref="ShieldStatus.OnShieldBrokenEvent"/>가 안 온다) — 잔해 위에서 번쩍이지 않는다.
/// </summary>
public class ShieldFeedback : MonoBehaviour
{
    [Tooltip("실드 그림. 비우면 자식 'Visual'.")]
    [SerializeField] private Transform _visual;

    [Header("깨짐")]
    [SerializeField, Min(0.01f)] private float _breakDuration = 0.35f;
    [Tooltip("깨질 때 이 배수까지 커진다.")]
    [SerializeField, Min(1f)] private float _breakScale = 1.45f;
    [Tooltip("번쩍이는 색. 시작 색이고, 끝으로 갈수록 투명해진다.")]
    [SerializeField] private Color _breakFlash = new(1f, 1f, 1f, 1f);
    [SerializeField] private PoolType _breakEffect = PoolType.None;
    [SerializeField] private SoundDataSO _breakSound;

    [Header("복귀")]
    [SerializeField, Min(0.01f)] private float _restoreDuration = 0.3f;
    [Tooltip("돌아올 때 이 배수에서 시작한다.")]
    [SerializeField, Range(0f, 1f)] private float _restoreFromScale = 0.4f;
    [Tooltip("얼마나 넘쳤다 돌아오나. 0이면 넘치지 않는다.")]
    [SerializeField, Min(0f)] private float _restoreOvershoot = 1.6f;
    [SerializeField] private PoolType _restoreEffect = PoolType.None;
    [SerializeField] private SoundDataSO _restoreSound;

    private ShieldStatus _shield;
    private Vector3 _visualScale = Vector3.one;
    private bool _wasUp;
    private bool _initialized;

    // 깨짐 연출 — 복제본 하나만 돈다. 연달아 깨질 일은 없다(복귀를 거쳐야 다시 깨진다).
    private Transform _ghost;
    private SpriteRenderer[] _ghostRenderers;
    private Color[] _ghostBaseColors;
    private float _breakTime = -1f;

    private float _restoreTime = -1f;

    private void Awake()
    {
        if (_visual == null) _visual = transform.Find("Visual");
        if (_visual != null) _visualScale = _visual.localScale;
    }

    private void Start()
    {
        Agent owner = GetComponentInParent<Agent>();
        _shield = owner != null ? owner.GetCompo<ShieldStatus>() : null;
        if (_shield == null) return;

        _shield.OnShieldChangedEvent += HandleChanged;
        _shield.OnShieldBrokenEvent += HandleBroken;

        _wasUp = _shield.HasShield;
        _initialized = true;
    }

    private void OnDestroy()
    {
        if (_shield != null)
        {
            _shield.OnShieldChangedEvent -= HandleChanged;
            _shield.OnShieldBrokenEvent -= HandleBroken;
        }

        if (_ghost != null) Destroy(_ghost.gameObject);
    }

    private void HandleChanged(float current, float max)
    {
        bool up = max > 0f && current > 0f;
        if (_initialized && up && !_wasUp)
            BeginRestore();

        _wasUp = up;
    }

    private void HandleBroken()
    {
        if (_visual == null) return;

        BuildGhost();
        _breakTime = 0f;

        // 복귀가 덜 끝난 채로 깨졌으면 원본 크기를 되돌려 둔다(꺼진 상태라 안 보인다).
        _restoreTime = -1f;
        _visual.localScale = _visualScale;

        EffectPlayer.Play(_breakEffect, transform.position, Vector2.up);
        SoundManager.Play(_breakSound, transform.position);
    }

    private void BeginRestore()
    {
        if (_visual == null) return;

        _restoreTime = 0f;
        _visual.localScale = _visualScale * _restoreFromScale;

        EffectPlayer.Play(_restoreEffect, transform.position, Vector2.up);
        SoundManager.Play(_restoreSound, transform.position);
    }

    private void Update()
    {
        TickBreak(Time.deltaTime);
        TickRestore(Time.deltaTime);
    }

    private void TickBreak(float dt)
    {
        if (_breakTime < 0f || _ghost == null) return;

        _breakTime += dt;
        float t = Mathf.Clamp01(_breakTime / _breakDuration);

        // 빠르게 부풀고 끝에서 느려진다.
        float grow = 1f - (1f - t) * (1f - t);
        _ghost.localScale = _visualScale * Mathf.Lerp(1f, _breakScale, grow);

        for (int i = 0; i < _ghostRenderers.Length; i++)
        {
            if (_ghostRenderers[i] == null) continue;

            // 앞 1/4은 번쩍이는 색에서 원래 색으로, 전체에 걸쳐 투명해진다.
            Color flashed = Color.Lerp(_breakFlash, _ghostBaseColors[i], Mathf.Clamp01(t * 4f));
            flashed.a = _ghostBaseColors[i].a * (1f - t);
            _ghostRenderers[i].color = flashed;
        }

        if (t >= 1f)
        {
            _breakTime = -1f;
            _ghost.gameObject.SetActive(false);
        }
    }

    private void TickRestore(float dt)
    {
        if (_restoreTime < 0f || _visual == null) return;

        _restoreTime += dt;
        float t = Mathf.Clamp01(_restoreTime / _restoreDuration);

        // back ease-out — 살짝 넘쳤다가 제 크기로.
        float s = _restoreOvershoot;
        float u = t - 1f;
        float eased = 1f + (s + 1f) * u * u * u + s * u * u;

        _visual.localScale = _visualScale * Mathf.LerpUnclamped(_restoreFromScale, 1f, eased);

        if (t >= 1f)
        {
            _restoreTime = -1f;
            _visual.localScale = _visualScale;
        }
    }

    /// <summary>
    /// 그림의 복제본. 한 번 만들고 재사용한다. 실드 오브젝트의 자식이라 기체를 따라 움직이고 활공 높이도 같이 탄다.
    /// </summary>
    private void BuildGhost()
    {
        if (_ghost == null)
        {
            _ghost = new GameObject("ShieldBreakGhost").transform;
            _ghost.SetParent(_visual.parent, false);

            SpriteRenderer[] sources = _visual.GetComponentsInChildren<SpriteRenderer>(true);
            _ghostRenderers = new SpriteRenderer[sources.Length];
            _ghostBaseColors = new Color[sources.Length];

            for (int i = 0; i < sources.Length; i++)
            {
                SpriteRenderer src = sources[i];

                Transform piece = src.transform == _visual ? _ghost : new GameObject(src.name).transform;
                if (piece != _ghost)
                {
                    piece.SetParent(_ghost, false);
                    piece.localPosition = _visual.InverseTransformPoint(src.transform.position);
                    piece.localRotation = Quaternion.Inverse(_visual.rotation) * src.transform.rotation;
                    piece.localScale = src.transform.localScale;
                }

                SpriteRenderer copy = piece.gameObject.AddComponent<SpriteRenderer>();
                copy.sprite = src.sprite;
                copy.sharedMaterial = src.sharedMaterial;
                copy.sortingLayerID = src.sortingLayerID;
                copy.sortingOrder = src.sortingOrder + 1;
                copy.flipX = src.flipX;
                copy.flipY = src.flipY;

                _ghostRenderers[i] = copy;
                _ghostBaseColors[i] = src.color;
            }
        }

        _ghost.localPosition = _visual.localPosition;
        _ghost.localRotation = _visual.localRotation;
        _ghost.localScale = _visualScale;
        _ghost.gameObject.SetActive(true);
    }
}
