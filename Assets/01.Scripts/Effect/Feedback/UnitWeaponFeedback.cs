using UnityEngine;

/// <summary>
/// 단일 기체 무기(<see cref="UnitWeapon"/>)의 연출. 총구 섬광·발사음, 그리고 <b>정밀 사격의 조준선</b>.
///
/// 조준선은 판정이 아니다. <see cref="UnitWeapon.OnPhaseChangedEvent"/>를 듣고 그리기만 한다.
///
/// | 단계 | 조준선 |
/// |---|---|
/// | 노림 | 가늘게, 점점 진해진다. 포탑을 따라 돈다 |
/// | 고정 | 굵게 깜빡인다. 더는 돌지 않는다 — 지금 빠져야 한다 |
/// | 연발 (정밀 연발 — 드론) | 고정 색 그대로 남는다. 탄이 선 위로 나간다 |
/// | 그 밖 | 끈다 |
///
/// 선은 총구에서 사거리 끝까지 그린다. 벽에서 끊기지 않는다(벽이 아직 없다, M2).
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class UnitWeaponFeedback : MonoBehaviour
{
    [Tooltip("비우면 부모에서 찾는다.")]
    [SerializeField] private UnitWeapon _weapon;

    [Header("발사")]
    [SerializeField] private PoolType _muzzleEffect = PoolType.None;
    [SerializeField] private SoundDataSO _fireSound;

    [Header("조준선")]
    [SerializeField] private Color _chargeColor = new(1f, 0.25f, 0.2f, 0.55f);
    [SerializeField] private Color _lockColor = new(1f, 0.15f, 0.1f, 1f);
    [SerializeField, Min(0.001f)] private float _chargeWidth = 0.05f;
    [SerializeField, Min(0.001f)] private float _lockWidth = 0.12f;

    [Tooltip("고정 중 초당 깜빡임 횟수.")]
    [SerializeField, Min(0f)] private float _lockBlinkRate = 14f;

    [SerializeField] private SoundDataSO _chargeSound;
    [SerializeField] private SoundDataSO _lockSound;

    private LineRenderer _line;
    private UnitWeaponPhase _phase;
    private float _phaseTime;
    private float _phaseDuration;

    private void Awake()
    {
        if (_weapon == null) _weapon = GetComponentInParent<UnitWeapon>();

        _line = GetComponent<LineRenderer>();
        _line.useWorldSpace = true;
        _line.positionCount = 2;
        _line.enabled = false;
    }

    private void OnEnable()
    {
        if (_weapon == null) return;

        _weapon.OnPhaseChangedEvent += HandlePhase;
        _weapon.OnFiredEvent += HandleFired;
    }

    private void OnDisable()
    {
        if (_weapon != null)
        {
            _weapon.OnPhaseChangedEvent -= HandlePhase;
            _weapon.OnFiredEvent -= HandleFired;
        }

        _line.enabled = false;
    }

    private void HandlePhase(UnitWeaponPhase phase, float duration)
    {
        _phase = phase;
        _phaseTime = 0f;
        _phaseDuration = duration;

        // 고정된 채 쏘는 연발(정밀 연발)이면 다 쏠 때까지 선을 남긴다. 연발 무기(포탑 기관포)의 연발은 선이 없다.
        bool aiming = phase == UnitWeaponPhase.Charging || phase == UnitWeaponPhase.Locked
                      || (phase == UnitWeaponPhase.Firing && _weapon.IsAimLocked);
        _line.enabled = aiming;

        Vector2 at = _weapon.Muzzle.position;
        if (phase == UnitWeaponPhase.Charging) SoundManager.Play(_chargeSound, at);
        if (phase == UnitWeaponPhase.Locked) SoundManager.Play(_lockSound, at);

        if (aiming) UpdateLine();
    }

    private void HandleFired(Vector2 origin, Vector2 direction)
    {
        EffectPlayer.Play(_muzzleEffect, origin, direction);
        SoundManager.Play(_fireSound, origin);
    }

    private void LateUpdate()
    {
        if (!_line.enabled) return;

        _phaseTime += Time.deltaTime;
        UpdateLine();
    }

    private void UpdateLine()
    {
        Vector2 origin = _weapon.Muzzle.position;
        Vector2 end = origin + _weapon.AimDirection * _weapon.Range;

        _line.SetPosition(0, origin);
        _line.SetPosition(1, end);

        Color color;
        float width;

        if (_phase == UnitWeaponPhase.Firing)
        {
            color = _lockColor;
            width = _chargeWidth;
        }
        else if (_phase == UnitWeaponPhase.Locked)
        {
            bool on = _lockBlinkRate <= 0f || Mathf.Repeat(_phaseTime * _lockBlinkRate, 1f) < 0.6f;
            color = _lockColor;
            color.a *= on ? 1f : 0.25f;
            width = _lockWidth;
        }
        else
        {
            float t = _phaseDuration > 0f ? Mathf.Clamp01(_phaseTime / _phaseDuration) : 1f;
            color = _chargeColor;
            color.a *= Mathf.Lerp(0.2f, 1f, t);
            width = _chargeWidth;
        }

        _line.startColor = color;
        _line.endColor = new Color(color.r, color.g, color.b, color.a * 0.35f);
        _line.startWidth = width;
        _line.endWidth = width;
    }
}
