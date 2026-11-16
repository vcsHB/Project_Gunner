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
            if (sprite != null) radius = sprite.bounds.extents.x;
        }

        // 콜라이더 반지름은 로컬이다. 오브젝트가 스케일돼 있으면 나눠야 월드 반지름이 맞는다.
        float scale = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));
        if (radius > 0f)
            _collider.radius = radius / scale;

        _collider.enabled = false;
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
        _collider.enabled = max > 0f && current > 0f;
    }
}
