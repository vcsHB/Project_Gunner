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

    [Header("Effect / Sound")]
    [SerializeField] private PoolType _breakEffect = PoolType.None;
    [SerializeField] private SoundDataSO _breakSound;

    private MechaPartHealth _parts;
    private MechaAssembler _assembler;

    private readonly Dictionary<EquipSlotType, List<SpriteRenderer>> _hidden = new();

    private void Start()
    {
        Agent owner = GetComponentInParent<Agent>();
        if (owner == null) return;

        _parts = owner.GetCompo<MechaPartHealth>();
        _assembler = owner.GetCompo<MechaAssembler>();

        if (_parts == null) return;

        _parts.OnPartDestroyedEvent += HandleDestroyed;
        _parts.OnPartRestoredEvent += HandleRestored;
    }

    private void OnDestroy()
    {
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

            CopyInto(debris.transform, source);

            source.enabled = false;
            hidden.Add(source);
        }

        debris.AddComponent<PartDebris>().Launch(velocity, spin, _lifetime);

        EffectPlayer.Play(_breakEffect, partCenter, outward);
        SoundManager.Play(_breakSound, partCenter);
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

    private void CopyInto(Transform parent, SpriteRenderer source)
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
        copy.color = source.color * _debrisTint;
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
