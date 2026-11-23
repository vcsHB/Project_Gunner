using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 단일 기체 무기의 <b>공격 범위</b>를 바닥에 그린다. 무엇을 쏘는지(지상/공중)를 모양으로 가른다.
///
/// | 맞히는 대상 | 그림 |
/// |---|---|
/// | 지상 | 바닥에 <b>실선</b> 원 + 옅은 채움 |
/// | 공중 | 활공 높이만큼 위로 띄운 <b>점선</b> 원 |
/// | 둘 다 | 둘 다 |
///
/// 공중 원을 띄우는 이유 — 활공 중인 기체는 몸이 화면 위로 뜬다(<c>PlayerMoveController._glideHeight</c>).
/// 원을 같은 높이로 그려야 "떠 있는 내 몸이 이 안에 있나"를 눈으로 맞출 수 있다. 색도 다르게 둔다(색만으로 가르지 않는다).
///
/// 반경은 <see cref="UnitWeapon.Range"/> 하나만 본다 — 그린 범위와 쏘는 거리가 갈라지면 안 된다.
/// 대상이 사정권 안이면(AI가 공격 상태) 진해진다.
///
/// 메시는 시작할 때 한 번 만들고, 진하기가 바뀔 때만 색을 다시 넣는다.
/// </summary>
public class UnitRangeIndicator : MonoBehaviour
{
    [Tooltip("비우면 이 기체의 UnitWeapon.")]
    [SerializeField] private UnitWeapon _weapon;

    [Tooltip("스프라이트용 머티리얼(Sprite-Unlit-Default). 정점 색을 쓴다.")]
    [SerializeField] private Material _material;

    [SerializeField] private string _sortingLayer = "Effect_Back";
    [SerializeField] private int _sortingOrder;

    [Header("지상")]
    [SerializeField] private Color _groundColor = new(1f, 0.45f, 0.2f, 1f);
    [SerializeField, Range(0f, 1f)] private float _groundFillAlpha = 0.035f;

    [Header("공중")]
    [SerializeField] private Color _airColor = new(0.35f, 0.8f, 1f, 1f);

    [Tooltip("공중 원을 띄우는 높이. 플레이어의 활공 고도와 맞출 것.")]
    [SerializeField, Min(0f)] private float _airLift = 0.6f;

    [SerializeField, Range(4, 128)] private int _dashCount = 36;

    [Tooltip("점선 한 칸에서 선이 차지하는 비율.")]
    [SerializeField, Range(0.1f, 0.9f)] private float _dashFill = 0.55f;

    [Header("공통")]
    [SerializeField, Min(0.005f)] private float _lineWidth = 0.05f;
    [SerializeField, Range(16, 256)] private int _segments = 96;

    [Tooltip("대상이 사정권 밖일 때 선의 진하기.")]
    [SerializeField, Range(0f, 1f)] private float _idleAlpha = 0.35f;

    [Tooltip("대상이 사정권 안일 때(공격 중) 선의 진하기.")]
    [SerializeField, Range(0f, 1f)] private float _alertAlpha = 0.85f;

    private readonly List<Piece> _pieces = new();
    private EnemyAIController _ai;
    private Health _health;
    private bool _alert;
    private bool _built;

    private class Piece
    {
        public Mesh mesh;
        public MeshRenderer renderer;
        public Color color;
        public bool isFill;
    }

    private void Start()
    {
        Agent owner = GetComponentInParent<Agent>();

        if (_weapon == null && owner != null) _weapon = owner.GetCompo<UnitWeapon>();
        if (owner != null)
        {
            _ai = owner.GetCompo<EnemyAIController>();
            _health = owner.GetCompo<Health>();
        }

        if (_weapon == null || _weapon.Range <= 0f) return;

        Build();

        if (_health != null) _health.OnDeadEvent += HandleDead;
    }

    private void OnDestroy()
    {
        if (_health != null) _health.OnDeadEvent -= HandleDead;

        foreach (Piece piece in _pieces)
        {
            if (piece.mesh != null) Destroy(piece.mesh);
        }
    }

    private void HandleDead() => SetVisible(false);

    private void Update()
    {
        if (!_built || _ai == null) return;

        bool alert = _ai.CurrentState == EnemyStateType.Attack;
        if (alert == _alert) return;

        _alert = alert;
        ApplyColors();
    }

    private void Build()
    {
        float radius = _weapon.Range;
        TargetType targets = _weapon.HitTargets;

        if ((targets & TargetType.Ground) != 0)
        {
            AddPiece("GroundFill", BuildDisc(radius, Vector2.zero), _groundColor, true);
            AddPiece("GroundRing", BuildRing(radius, Vector2.zero, 1, 1f), _groundColor, false);
        }

        if ((targets & TargetType.Air) != 0)
            AddPiece("AirRing", BuildRing(radius, new Vector2(0f, _airLift), _dashCount, _dashFill), _airColor, false);

        _built = true;
        ApplyColors();
    }

    private void AddPiece(string pieceName, Mesh mesh, Color color, bool isFill)
    {
        GameObject go = new(pieceName);
        go.transform.SetParent(transform, false);

        go.AddComponent<MeshFilter>().sharedMesh = mesh;

        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = _material;
        renderer.sortingLayerName = _sortingLayer;
        renderer.sortingOrder = _sortingOrder + (isFill ? 0 : 1);

        _pieces.Add(new Piece { mesh = mesh, renderer = renderer, color = color, isFill = isFill });
    }

    private void ApplyColors()
    {
        float lineAlpha = _alert ? _alertAlpha : _idleAlpha;

        foreach (Piece piece in _pieces)
        {
            Color c = piece.color;
            c.a *= piece.isFill ? _groundFillAlpha * (_alert ? 1.5f : 1f) : lineAlpha;

            Color[] colors = new Color[piece.mesh.vertexCount];
            for (int i = 0; i < colors.Length; i++) colors[i] = c;

            piece.mesh.colors = colors;
        }
    }

    private void SetVisible(bool visible)
    {
        foreach (Piece piece in _pieces)
        {
            if (piece.renderer != null) piece.renderer.enabled = visible;
        }
    }

    /// <summary>
    /// 원 띠. <paramref name="dashes"/>가 1이면 실선, 그보다 크면 그만큼 끊긴 점선.
    /// </summary>
    private Mesh BuildRing(float radius, Vector2 center, int dashes, float fill)
    {
        List<Vector3> vertices = new();
        List<int> triangles = new();

        float inner = radius - _lineWidth * 0.5f;
        float outer = radius + _lineWidth * 0.5f;

        int stepsPerDash = Mathf.Max(1, _segments / dashes);
        float dashArc = Mathf.PI * 2f / dashes;

        for (int d = 0; d < dashes; d++)
        {
            float start = d * dashArc;
            float length = dashes == 1 ? dashArc : dashArc * fill;
            int baseIndex = vertices.Count;

            for (int s = 0; s <= stepsPerDash; s++)
            {
                float a = start + length * s / stepsPerDash;
                Vector2 dir = new(Mathf.Cos(a), Mathf.Sin(a));

                vertices.Add(center + dir * inner);
                vertices.Add(center + dir * outer);
            }

            for (int s = 0; s < stepsPerDash; s++)
            {
                int i = baseIndex + s * 2;
                triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 3);
                triangles.Add(i); triangles.Add(i + 3); triangles.Add(i + 2);
            }
        }

        return MakeMesh("RangeRing", vertices, triangles);
    }

    private Mesh BuildDisc(float radius, Vector2 center)
    {
        List<Vector3> vertices = new() { center };
        List<int> triangles = new();

        for (int s = 0; s <= _segments; s++)
        {
            float a = Mathf.PI * 2f * s / _segments;
            vertices.Add(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
        }

        for (int s = 1; s <= _segments; s++)
        {
            triangles.Add(0); triangles.Add(s + 1); triangles.Add(s);
        }

        return MakeMesh("RangeDisc", vertices, triangles);
    }

    private static Mesh MakeMesh(string meshName, List<Vector3> vertices, List<int> triangles)
    {
        Mesh mesh = new() { name = meshName };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();

        return mesh;
    }
}
