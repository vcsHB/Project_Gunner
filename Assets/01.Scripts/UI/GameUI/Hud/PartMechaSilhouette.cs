using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD의 기체 실루엣. <b>기체에 실제로 조립된 모습을 그대로 UI로 옮긴다</b> —
/// 코어·추진체·양팔의 스프라이트를 같은 배치로 <see cref="Image"/>로 복제하고, 부위 상태에 따라 색을 입힌다.
///
/// 배치는 살아 있는 기체에서 읽는다. 코어 기준 상대 위치·회전·크기를 쓰므로 기체가 돌거나 떠 있어도(활공 확대)
/// 실루엣은 항상 정면(아트 기준 +Y)을 본다. 왼팔 좌우 반전도 기체에 걸린 그대로 따라온다.
/// 조립이 바뀌거나(<see cref="MechaAssembler.OnAssembledEvent"/>) 팔이 다시 만들어지면 다시 짓는다.
///
/// 색 — 코어는 <see cref="Health"/>, 팔·추진체는 <see cref="MechaPartHealth"/>. 부서진 부위는 <see cref="_destroyedColor"/>.
/// HUD 안에서의 위치·크기는 이 RectTransform과 <see cref="_pixelsPerUnit"/>로 맞춘다.
/// </summary>
public class PartMechaSilhouette : MonoBehaviour
{
    [Tooltip("월드 1유닛을 몇 픽셀로 그릴지. 실루엣 전체 크기다.")]
    [SerializeField, Min(1f)] private float _pixelsPerUnit = 24f;

    [Tooltip("조각에 입힐 머티리얼. UI/SilhouetteSolid — 모양(알파)만 쓰고 단색 + 안쪽 외곽선으로 그린다. 비우면 스프라이트 그대로.")]
    [SerializeField] private Material _pieceMaterial;

    [Tooltip("체력 비율(0~1)에 따른 색. 오른쪽이 가득 찬 상태.")]
    [SerializeField] private Gradient _healthColor = DefaultGradient();

    [SerializeField] private Color _destroyedColor = new(0.15f, 0.15f, 0.15f, 0.6f);

    [Tooltip("부위 체력이 없는(부서지지 않는) 부위의 색.")]
    [SerializeField] private Color _noHealthColor = new(1f, 1f, 1f, 0.5f);

    private struct Piece
    {
        public EquipSlotType slot;
        public Image image;
    }

    private readonly List<Piece> _pieces = new();

    private Player _player;
    private MechaAssembler _assembler;
    private HandController _hands;
    private MechaPartHealth _parts;
    private Health _health;

    private bool _dirty;

    private static Gradient DefaultGradient()
    {
        Gradient g = new();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.25f, 0.2f), 0f),
                new GradientColorKey(new Color(1f, 0.8f, 0.3f), 0.5f),
                new GradientColorKey(new Color(0.55f, 0.9f, 1f), 1f),
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }

    // HUD와 같은 방식. Agent의 컴포넌트 초기화가 Awake에서 끝나므로 Start에서 붙는다.
    private void Start()
    {
        if (_player == null)
            Bind(PlayerPilot.LocalMecha);

        // 갈아타면 다시 붙는다.
        PlayerPilot.OnLocalMechaChangedEvent += Bind;
    }

    public void Bind(Player player)
    {
        Unbind();

        _player = player;
        if (player == null) return;

        _assembler = player.GetCompo<MechaAssembler>();
        _hands = player.GetCompo<HandController>(true);
        _parts = player.GetCompo<MechaPartHealth>();
        _health = player.GetCompo<Health>();

        if (_assembler != null) _assembler.OnAssembledEvent += MarkDirty;
        if (_hands != null) _hands.OnHandChangedEvent += HandleHandChanged;
        if (_parts != null) _parts.OnPartHealthChangedEvent += HandlePartChanged;
        if (_parts != null) _parts.OnPartDestroyedEvent += HandlePartStateChanged;
        if (_parts != null) _parts.OnPartRestoredEvent += HandlePartStateChanged;
        if (_health != null) _health.OnHealthChangedEvent += HandleHealthChanged;

        MarkDirty();
    }

    public void Unbind()
    {
        if (_assembler != null) _assembler.OnAssembledEvent -= MarkDirty;
        if (_hands != null) _hands.OnHandChangedEvent -= HandleHandChanged;
        if (_parts != null) _parts.OnPartHealthChangedEvent -= HandlePartChanged;
        if (_parts != null) _parts.OnPartDestroyedEvent -= HandlePartStateChanged;
        if (_parts != null) _parts.OnPartRestoredEvent -= HandlePartStateChanged;
        if (_health != null) _health.OnHealthChangedEvent -= HandleHealthChanged;

        _assembler = null;
        _hands = null;
        _parts = null;
        _health = null;
        _player = null;
    }

    private void OnDestroy()
    {
        PlayerPilot.OnLocalMechaChangedEvent -= Bind;
        Unbind();
    }

    private void MarkDirty() => _dirty = true;

    private void HandleHandChanged(EquipSlotType slot, HandActionBase hand) => MarkDirty();

    private void HandlePartChanged(EquipSlotType slot, float current, float max) => RefreshColors();

    // 체력 변화 이벤트는 파괴·복구 판정보다 먼저 온다. 파괴·복구 자체도 들어야 색이 맞는다.
    private void HandlePartStateChanged(EquipSlotType slot) => RefreshColors();

    private void HandleHealthChanged(float current, float max) => RefreshColors();

    // 조립·팔 생성이 한 프레임에 여러 번 몰린다. 끝난 뒤 한 번만 짓는다.
    // 파괴 예약된 옛 파츠는 이 프레임까지 살아 있으므로 다음 프레임에 읽어야 섞이지 않는다.
    private void LateUpdate()
    {
        if (!_dirty) return;

        _dirty = false;
        Rebuild();
    }

    #region 짓기

    private void Rebuild()
    {
        Clear();

        MechaPartView core = _assembler != null ? _assembler.Core : null;
        if (core == null) return;

        Transform coreTransform = core.transform;
        Quaternion inverseRotation = Quaternion.Inverse(coreTransform.rotation);
        Vector3 coreScale = coreTransform.lossyScale;

        List<(SpriteRenderer renderer, EquipSlotType slot)> found = new();

        // 코어 아래(소켓에 붙은 추진체·팔 포함)의 보이는 스프라이트 전부.
        foreach (SpriteRenderer renderer in coreTransform.GetComponentsInChildren<SpriteRenderer>(false))
        {
            // 꺼진 렌더러도 넣는다 — 부서져 떨어져 나간 부위는 기체에서 숨겨지지만(PartBreakFeedback) 실루엣에는 어둡게 남아야 한다.
            if (renderer.sprite == null) continue;

            found.Add((renderer, SlotOf(renderer.transform)));
        }

        // 원래 그려지는 순서대로 쌓는다. UI는 형제 순서가 곧 그리기 순서다.
        found.Sort((a, b) =>
        {
            int layer = SortingLayer.GetLayerValueFromID(a.renderer.sortingLayerID)
                .CompareTo(SortingLayer.GetLayerValueFromID(b.renderer.sortingLayerID));
            return layer != 0 ? layer : a.renderer.sortingOrder.CompareTo(b.renderer.sortingOrder);
        });

        for (int i = 0; i < found.Count; i++)
            CreatePiece(found[i].renderer, found[i].slot, coreTransform, inverseRotation, coreScale);

        RefreshColors();
    }

    private void CreatePiece(SpriteRenderer renderer, EquipSlotType slot,
        Transform core, Quaternion inverseRotation, Vector3 coreScale)
    {
        Sprite sprite = renderer.sprite;
        Transform source = renderer.transform;

        GameObject go = new($"Part_{slot}_{renderer.name}", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);

        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        image.preserveAspect = true;
        if (_pieceMaterial != null) image.material = _pieceMaterial;

        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);

        // 코어 공간(정면 = 아트 +Y)으로 옮긴다. 코어의 회전·크기(활공 확대)를 걷어낸다.
        Vector3 local = core.InverseTransformPoint(source.position);
        Quaternion rotation = inverseRotation * source.rotation;
        Vector3 scale = new(
            source.lossyScale.x / SafeDivisor(coreScale.x),
            source.lossyScale.y / SafeDivisor(coreScale.y),
            1f);

        // 스프라이트 반전(flipX/Y)도 크기 부호로 옮긴다.
        if (renderer.flipX) scale.x = -scale.x;
        if (renderer.flipY) scale.y = -scale.y;

        Vector2 unitSize = sprite.rect.size / sprite.pixelsPerUnit;
        rect.sizeDelta = unitSize * _pixelsPerUnit;
        rect.pivot = new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);
        rect.anchoredPosition = (Vector2)local * _pixelsPerUnit;
        rect.localRotation = rotation;
        rect.localScale = scale;

        _pieces.Add(new Piece { slot = slot, image = image });
    }

    private static float SafeDivisor(float value)
        => Mathf.Abs(value) < 0.0001f ? 1f : value;

    /// <summary>가장 가까운 상위 소켓의 슬롯. 소켓 아래가 아니면 코어. <see cref="HitBox.Part"/>와 같은 규칙.</summary>
    private static EquipSlotType SlotOf(Transform t)
    {
        MechaSocket socket = t.GetComponentInParent<MechaSocket>();
        return socket != null && socket.Slot != EquipSlotType.None ? socket.Slot : EquipSlotType.Core;
    }

    private void Clear()
    {
        for (int i = 0; i < _pieces.Count; i++)
        {
            if (_pieces[i].image != null)
                Destroy(_pieces[i].image.gameObject);
        }

        _pieces.Clear();
    }

    #endregion

    #region 색

    private void RefreshColors()
    {
        for (int i = 0; i < _pieces.Count; i++)
        {
            if (_pieces[i].image != null)
                _pieces[i].image.color = ColorOf(_pieces[i].slot);
        }
    }

    private Color ColorOf(EquipSlotType slot)
    {
        if (slot == EquipSlotType.Core)
            return _health != null ? _healthColor.Evaluate(_health.Normalized) : Color.white;

        if (_parts == null) return _noHealthColor;

        if (_parts.IsDestroyed(slot)) return _destroyedColor;

        float max = _parts.GetMax(slot);
        if (max <= 0f) return _noHealthColor;

        return _healthColor.Evaluate(_parts.GetCurrent(slot) / max);
    }

    #endregion
}
