using UnityEngine;

/// <summary>
/// 불붙은 폭발물(<see cref="UnitDetonator"/>)의 예고. 몸이 붉게 깜빡이고, 터질 때가 다가올수록 빨라진다. 유닛 루트에 붙인다.
/// 예고가 보여야 피한다 — 퓨즈가 있는 자폭 드론에만 의미가 있다(부표는 퓨즈 0).
/// </summary>
public class DetonatorFeedback : MonoBehaviour
{
    [SerializeField] private Color _flashColor = new(1f, 0.25f, 0.15f, 1f);
    [SerializeField, Min(0.1f)] private float _startRate = 4f;
    [SerializeField, Min(0.1f)] private float _endRate = 16f;
    [SerializeField] private SoundDataSO _fuseSound;

    private UnitDetonator _detonator;
    private SpriteRenderer[] _renderers;
    private Color[] _baseColors;
    private float _duration;
    private float _elapsed = -1f;

    private void Start()
    {
        Agent owner = GetComponentInParent<Agent>();
        _detonator = owner != null ? owner.GetCompo<UnitDetonator>() : null;
        if (_detonator != null) _detonator.OnFuseStartedEvent += HandleFuse;
    }

    private void OnDestroy()
    {
        if (_detonator != null) _detonator.OnFuseStartedEvent -= HandleFuse;
    }

    private void HandleFuse(float duration)
    {
        _duration = duration;
        _elapsed = 0f;

        _renderers = GetComponentsInChildren<SpriteRenderer>();
        _baseColors = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++) _baseColors[i] = _renderers[i].color;

        SoundManager.Play(_fuseSound, transform.position);
    }

    private void Update()
    {
        if (_elapsed < 0f || _renderers == null) return;

        _elapsed += Time.deltaTime;
        float t = _duration > 0f ? Mathf.Clamp01(_elapsed / _duration) : 1f;
        float rate = Mathf.Lerp(_startRate, _endRate, t);
        bool on = Mathf.Repeat(_elapsed * rate, 1f) < 0.5f;

        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;
            _renderers[i].color = on ? _baseColors[i] * _flashColor : _baseColors[i];
        }
    }
}
