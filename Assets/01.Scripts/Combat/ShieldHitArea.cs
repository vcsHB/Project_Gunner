using UnityEngine;

/// <summary>
/// 실드 원을 <b>판정</b>으로 쓴다. 실드가 남아 있는 동안만 콜라이더가 켜진다 —
/// 실드가 있으면 원에 먼저 맞고, 깨지면 꺼져서 안쪽의 파츠 히트박스가 드러난다.
///
/// 실드 그림(<c>Shied</c>) 오브젝트에 붙인다. 원 크기는 자식 SpriteRenderer에서 잰다(<see cref="_radius"/>가 0일 때).
/// 콜라이더는 <b>트리거</b>다 — 캐스터는 트리거도 찾지만(<c>useTriggers</c>), 물리로 밀어내는 몸이 되면 안 된다.
///
/// 이 원에 맞은 타격은 본체(<see cref="EquipSlotType.Core"/>)로 간다. 어차피 실드가 받아내므로 부위는 의미가 없다.
/// </summary>
[RequireComponent(typeof(CircleCollider2D))]
[RequireComponent(typeof(HitBox))]
public class ShieldHitArea : MonoBehaviour
{
    [Tooltip("월드 반지름. 0이면 자식 SpriteRenderer 크기로 잰다.")]
    [SerializeField, Min(0f)] private float _radius;

    [Tooltip("실드 그림. 실드가 0이면 콜라이더와 같이 꺼진다. 비우면 자식 'Visual'.")]
    [SerializeField] private GameObject _visual;

    private CircleCollider2D _collider;
    private ShieldStatus _shield;

    private void Awake()
    {
        _collider = GetComponent<CircleCollider2D>();
        _collider.isTrigger = true;

        float radius = _radius;
        if (radius <= 0f)
        {
            SpriteRenderer sprite = GetComponentInChildren<SpriteRenderer>(true);
            // 렌더러 bounds는 꺼진 오브젝트에서 0이다. 스프라이트 자체 크기에 스케일을 곱한다.
            if (sprite != null && sprite.sprite != null)
                radius = sprite.sprite.bounds.extents.x * Mathf.Abs(sprite.transform.lossyScale.x);
        }

        // 콜라이더 반지름은 로컬이다. 오브젝트가 스케일돼 있으면 나눠야 월드 반지름이 맞는다.
        float scale = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));
        if (radius > 0f)
            _collider.radius = radius / scale;

        _collider.enabled = false;

        if (_visual == null)
        {
            Transform visual = transform.Find("Visual");
            if (visual != null) _visual = visual.gameObject;
        }

        // 실드가 없는 유닛(메카가 아닌 적, 코어에 실드가 없음)은 그림도 없어야 한다. Start에서 실제 값으로 맞춘다.
        if (_visual != null) _visual.SetActive(false);
    }

    private void Start()
    {
        Agent owner = GetComponentInParent<Agent>();
        _shield = owner != null ? owner.GetCompo<ShieldStatus>() : null;

        if (_shield == null) return;

        _shield.OnShieldChangedEvent += HandleShieldChanged;
        HandleShieldChanged(_shield.Current, _shield.Max);
    }

    private void OnDestroy()
    {
        if (_shield != null)
            _shield.OnShieldChangedEvent -= HandleShieldChanged;
    }

    private void HandleShieldChanged(float current, float max)
    {
        bool up = max > 0f && current > 0f;

        _collider.enabled = up;

        if (_visual != null && _visual.activeSelf != up)
            _visual.SetActive(up);
    }
}
