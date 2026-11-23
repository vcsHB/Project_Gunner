using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 부위가 부서지면 <b>떨어져 나간다.</b> 유닛 루트(<see cref="MechaPartHealth"/>와 같은 오브젝트)에 붙인다. 적·플레이어 공통.
///
/// 부서진 부위의 스프라이트를 어둡게 칠한 <b>복제본</b>(<see cref="PartDebris"/>)으로 날려 보내고, 원래 스프라이트는 숨긴다.
/// 원본을 떼어내지 않는 이유 — 팔 오브젝트에는 무기 로직·히트박스가 붙어 있고, 수리로 되살아나면 그 자리에 다시 보여야 한다.
/// 되살아나면(<see cref="MechaPartHealth.OnPartRestoredEvent"/>) 숨긴 스프라이트를 다시 켠다.
///
/// 화면 흔들림은 여기가 아니다 — 내 화면만 흔들려야 하므로 카메라 쪽(<see cref="PartBreakCameraShake"/>)이 듣는다.
/// </summary>
public class PartBreakFeedback : MonoBehaviour
{
    [Header("Debris")]
    [Tooltip("떨어져 나간 조각에 곱하는 색. 어둡게.")]
    [SerializeField] private Color _debrisTint = new(0.35f, 0.35f, 0.38f, 1f);
    [Tooltip("튕겨 나가는 속도 범위(유닛/초).")]
    [SerializeField] private Vector2 _speedRange = new(3f, 6f);
    [Tooltip("회전 속도 범위(도/초). 방향은 무작위.")]
    [SerializeField] private Vector2 _spinRange = new(180f, 540f);
    [SerializeField, Min(0.1f)] private float _lifetime = 1.4f;

    [Header("Wreck (사망)")]
    [Tooltip("기체가 부서지면(코어 0) 몸 전체가 이 색의 잔해로 남는다. 조종사는 비상탈출했다.")]
    [SerializeField] private Color _wreckTint = new(0.22f, 0.22f, 0.25f, 1f);
    [SerializeField, Min(0.1f)] private float _wreckLifetime = 3.5f;

    [Tooltip("조립되지 않는 단일 기체(터렛·드론)의 몸. 이 아래 스프라이트가 잔해가 된다. " +
             "비우면 유닛 루트 전체(그림자는 뺀다). 메카는 코어가 쓰이므로 비워둔다.")]
    [SerializeField] private Transform _bodyRoot;

    [Header("Effect / Sound")]
    [SerializeField] private PoolType _breakEffect = PoolType.None;
    [SerializeField] private SoundDataSO _breakSound;
    [SerializeField] private PoolType _wreckEffect = PoolType.None;
    [SerializeField] private SoundDataSO _wreckSound;

    private MechaPartHealth _parts;
    private MechaAssembler _assembler;
    private Health _health;

    private readonly Dictionary<EquipSlotType, List<SpriteRenderer>> _hidden = new();

    private void Start()
    {
        Agent owner = GetComponentInParent<Agent>();
        if (owner == null) return;

        _parts = owner.GetCompo<MechaPartHealth>();
        _assembler = owner.GetCompo<MechaAssembler>();
        _health = owner.GetCompo<Health>();

        if (_health != null)
            _health.OnDeadEvent += HandleDead;

        if (_parts == null) return;

        _parts.OnPartDestroyedEvent += HandleDestroyed;
        _parts.OnPartRestoredEvent += HandleRestored;
    }

    private void OnDestroy()
    {
        if (_health != null)
            _health.OnDeadEvent -= HandleDead;

        if (_parts == null) return;

        _parts.OnPartDestroyedEvent -= HandleDestroyed;
        _parts.OnPartRestoredEvent -= HandleRestored;
    }

    private void HandleDestroyed(EquipSlotType slot)
    {
        Transform mount = _assembler != null ? _assembler.GetMountPoint(slot) : null;
        if (mount == null) return;

        Vector2 center = transform.position;
        Vector2 partCenter = mount.position;

        // 몸 중심에서 부위 쪽으로 튄다. 겹쳐 있으면 무작위.
        Vector2 outward = partCenter - center;
        if (outward.sqrMagnitude < 0.0001f) outward = Random.insideUnitCircle;
        outward.Normalize();

        if (!_hidden.TryGetValue(slot, out List<SpriteRenderer> hidden))
        {
            hidden = new List<SpriteRenderer>();
            _hidden.Add(slot, hidden);
        }

        hidden.Clear();

        // 한 부위의 조각은 같은 방향·회전으로 날아가야 한 덩어리로 보인다.
        Vector2 velocity = Rotate(outward, Random.Range(-30f, 30f)) * Random.Range(_speedRange.x, _speedRange.y);
        float spin = Random.Range(_spinRange.x, _spinRange.y) * (Random.value < 0.5f ? -1f : 1f);

        GameObject debris = new($"Debris_{slot}");
        debris.transform.SetPositionAndRotation(mount.position, mount.rotation);

        foreach (SpriteRenderer source in mount.GetComponentsInChildren<SpriteRenderer>(false))
        {
            if (source.sprite == null || !source.enabled) continue;

            CopyInto(debris.transform, source, _debrisTint);

            source.enabled = false;
            hidden.Add(source);
        }

        debris.AddComponent<PartDebris>().Launch(velocity, spin, _lifetime);

        EffectPlayer.Play(_breakEffect, partCenter, outward);
        SoundManager.Play(_breakSound, partCenter);
    }

    /// <summary>
    /// 기체 파괴. 몸 전체(코어와 거기 붙은 모든 부위)를 어두운 잔해로 바꿔 그 자리에 남긴다 — 거의 안 움직이고 천천히 돈다.
    /// 원본은 숨긴다. 새 판에서 장비가 갈리며 원본은 어차피 새로 만들어진다.
    /// </summary>
    private void HandleDead()
    {
        MechaPartView core = _assembler != null ? _assembler.Core : null;

        // 메카는 코어와 거기 붙은 것 전부, 단일 기체는 지정한 몸(없으면 유닛 전체).
        Transform root = core != null ? core.transform : (_bodyRoot != null ? _bodyRoot : transform);

        GameObject wreck = new("Wreck");
        wreck.transform.SetPositionAndRotation(root.position, root.rotation);

        foreach (SpriteRenderer source in root.GetComponentsInChildren<SpriteRenderer>(false))
        {
            if (source.sprite == null || !source.enabled) continue;

            // 그림자·실드 원은 몸이 아니다. 유닛 전체를 훑을 때 섞여 들어온다.
            if (source.GetComponentInParent<Shadow>() != null) continue;
            if (source.GetComponentInParent<ShieldHitArea>() != null) continue;

            CopyInto(wreck.transform, source, _wreckTint);
            source.enabled = false;
        }

        Vector2 drift = Random.insideUnitCircle * 0.6f;
        float spin = Random.Range(-40f, 40f);
        wreck.AddComponent<PartDebris>().Launch(drift, spin, _wreckLifetime);

        EffectPlayer.Play(_wreckEffect, root.position, Vector2.up);
        SoundManager.Play(_wreckSound, root.position);
    }

    private void HandleRestored(EquipSlotType slot)
    {
        if (!_hidden.TryGetValue(slot, out List<SpriteRenderer> hidden)) return;

        for (int i = 0; i < hidden.Count; i++)
        {
            if (hidden[i] != null)
                hidden[i].enabled = true;
        }

        hidden.Clear();
    }

    private static void CopyInto(Transform parent, SpriteRenderer source, Color tint)
    {
        GameObject piece = new(source.name);
        piece.transform.SetParent(parent, false);
        piece.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        piece.transform.localScale = Divide(source.transform.lossyScale, parent.lossyScale);

        SpriteRenderer copy = piece.AddComponent<SpriteRenderer>();
        copy.sprite = source.sprite;
        copy.flipX = source.flipX;
        copy.flipY = source.flipY;
        copy.sharedMaterial = source.sharedMaterial;
        copy.sortingLayerID = source.sortingLayerID;
        copy.sortingOrder = source.sortingOrder;
        copy.color = source.color * tint;
    }

    private static Vector3 Divide(Vector3 a, Vector3 b)
        => new(a.x / Safe(b.x), a.y / Safe(b.y), a.z / Safe(b.z));

    private static float Safe(float v) => Mathf.Abs(v) < 0.0001f ? 1f : v;

    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        float c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }
}
