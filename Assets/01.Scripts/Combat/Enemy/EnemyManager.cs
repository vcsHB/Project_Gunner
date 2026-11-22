using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적을 찍어낸다. <b>스폰만 한다</b> — 어떤 적이 무엇을 하는지는 적 자신이 안다.
///
/// 플레이어 주위의 <b>고리</b>에 놓는다. 너무 가까우면 눈앞에 튀어나오고,
/// 너무 멀면 영영 안 만난다. 화면 밖인지까지 보는 것은 맵이 생긴 뒤다(M2) —
/// 지금은 벽이 없어서 "화면 밖"이 곧 "아무것도 없는 곳"이다.
///
/// <b>풀링하지 않는다.</b> <see cref="Agent"/>와 <see cref="PoolableMono"/>가 둘 다
/// MonoBehaviour 직계라 <see cref="Enemy"/>가 양쪽을 상속할 수 없다. 스폰이 잦아지는
/// 시점(M6)에 다시 본다. (docs/MILESTONE.md)
/// </summary>
public class EnemyManager : MonoBehaviour
{
    [Tooltip("찍어낼 적 종류. 매번 이 중 하나를 고른다. 비어 있으면 아무것도 안 나온다.")]
    [SerializeField] private List<UnitDataSO> _units = new();

    [Header("스폰")]
    [Tooltip("동시에 살아있을 수 있는 최대 수.")]
    [SerializeField, Min(0)] private int _maxAlive = 6;

    [Tooltip("스폰 간격(초).")]
    [SerializeField, Min(0.1f)] private float _interval = 3f;

    [Tooltip("시작하고 이만큼 지난 뒤부터 나온다. 들어가자마자 맞지 않게 한다.")]
    [SerializeField, Min(0f)] private float _startDelay = 2f;

    [Header("위치")]
    [Tooltip("플레이어로부터 최소 거리. 눈앞에 튀어나오지 않게 한다.")]
    [SerializeField, Min(1f)] private float _minDistance = 14f;

    [Tooltip("플레이어로부터 최대 거리.")]
    [SerializeField, Min(1f)] private float _maxDistance = 22f;

    [Tooltip("끄면 스폰을 멈춘다. 디버깅용.")]
    [SerializeField] private bool _spawnEnabled = true;

    private readonly List<Enemy> _alive = new();

    private Transform _player;
    private float _timer;

    /// <summary>지금 살아있는 적 수. 웨이브·클리어 판정이 읽는다.</summary>
    public int AliveCount
    {
        get
        {
            Prune();

            return _alive.Count;
        }
    }

    private void Start()
    {
        _timer = _startDelay;

        Player player = FindAnyObjectByType<Player>();
        _player = player != null ? player.transform : null;

        if (_player == null)
            Debug.LogWarning("[EnemyManager] 씬에서 Player를 찾지 못했습니다. 스폰 기준이 원점이 됩니다.", this);
    }

    private void Update()
    {
        if (!_spawnEnabled) return;

        _timer -= Time.deltaTime;
        if (_timer > 0f) return;

        _timer = _interval;

        // 가득 찼으면 이번 차례를 거른다. 타이머는 돌려둔다 — 하나 죽으면 바로 채워진다.
        if (AliveCount >= _maxAlive) return;

        Spawn();
    }

    /// <summary>한 마리 찍어낸다. 밖에서도 부를 수 있다(디버그 버튼, 웨이브 스크립트).</summary>
    public Enemy Spawn()
    {
        UnitDataSO data = PickUnit();
        if (data == null) return null;

        if (data.Prefab == null)
        {
            Debug.LogError($"[EnemyManager] {data.EditorName}에 프리팹이 없습니다.", data);
            return null;
        }

        Agent spawned = Instantiate(data.Prefab, PickPosition(), Quaternion.identity);

        // Agent.Awake가 Instantiate 안에서 이미 끝났다. 그 뒤라야 AgentStatus가 서 있다.
        if (spawned is not Enemy enemy)
        {
            Debug.LogError($"[EnemyManager] {data.EditorName}의 프리팹이 Enemy가 아닙니다.", data);
            Destroy(spawned.gameObject);
            return null;
        }

        enemy.Setup(data);
        _alive.Add(enemy);

        return enemy;
    }

    /// <summary>
    /// <see cref="GameRandom.World"/>를 쓴다. 스폰은 월드를 만드는 판정이라
    /// 전투 판정(치명타·산탄)과 소스를 섞으면 호출 순서가 깨져 재현이 안 된다.
    /// </summary>
    private UnitDataSO PickUnit()
    {
        if (_units.Count == 0) return null;

        // 비어 있는 칸이 섞여 있어도 터지지 않게 한 번 더 본다.
        for (int attempt = 0; attempt < _units.Count; attempt++)
        {
            UnitDataSO picked = _units[GameRandom.World.Range(0, _units.Count)];
            if (picked != null) return picked;
        }

        return null;
    }

    private Vector2 PickPosition()
    {
        Vector2 center = _player != null ? (Vector2)_player.position : Vector2.zero;

        float angle = GameRandom.World.Range(0f, Mathf.PI * 2f);
        float distance = GameRandom.World.Range(_minDistance, Mathf.Max(_minDistance, _maxDistance));

        return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
    }

    /// <summary>
    /// 죽은 적을 목록에서 뺀다. <see cref="Enemy"/>는 죽으면 Destroy되므로
    /// 유니티 null이 된다 — 일반 null 비교로는 안 걸리고, 여기서는 == null이 맞다.
    /// </summary>
    /// <summary>새 판. 살아 있는 적을 전부 없애고 시작 지연부터 다시 센다.</summary>
    public void ClearAll()
    {
        for (int i = 0; i < _alive.Count; i++)
        {
            if (_alive[i] != null)
                Destroy(_alive[i].gameObject);
        }

        _alive.Clear();
        _timer = _startDelay;
    }

    private void Prune()
    {
        for (int i = _alive.Count - 1; i >= 0; i--)
        {
            if (_alive[i] == null)
                _alive.RemoveAt(i);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_maxDistance < _minDistance)
            _maxDistance = _minDistance;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = _player != null ? _player.position : transform.position;

        Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.5f);
        Gizmos.DrawWireSphere(center, _minDistance);
        Gizmos.DrawWireSphere(center, _maxDistance);
    }
#endif
}
