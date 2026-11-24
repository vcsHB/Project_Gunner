using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 한 판의 끝과 새 판. <b>죽음 → (연출) → 새 판</b>을 이끈다. 씬에 하나(Managers 아래).
///
/// <b>기체가 부서지면 비상탈출한다</b> — 기체(장착 파츠)도 적재함도 전부 잃는다. 안전 슬롯(<see cref="InventoryController.SafeSlot"/>)만 남는다.
/// 잃는 순간은 <b>새 판이 시작될 때</b>다. 죽은 직후에는 기체가 잔해로 남아 보여야 해서(사망 연출) 바로 걷어내지 않는다.
/// 그사이 인벤토리 화면은 열리지 않는다(<see cref="PanelInventory"/>) — 죽은 뒤에 안전 슬롯으로 옮기는 걸 막는다.
///
/// 새 판에서 되돌리는 것 — 적·월드 아이템·날아가던 탄, 아이템 인스턴스(안전 슬롯 제외), 난수 시드,
/// 플레이어 위치·이동·체력·실드·부위·자원·상태이상, 그리고 진입 구성(격납고 → 지금은 기본 로드아웃)을 다시 입힌다.
///
/// 플레이어 오브젝트는 <b>부수지 않고 재사용한다.</b> HUD·카메라·조준점의 구독이 그대로 살아 있어서 다시 붙일 필요가 없다.
/// 플레이어별 상태는 여기 두지 않는다(RULES) — 판의 흐름만 든다. 멀티에서는 플레이어마다 따로 부르면 된다.
/// </summary>
public class RunController : MonoBehaviour
{
    /// <summary>플레이어가 죽었다. 인자는 새 판까지 남은 시간. 사망 화면이 카운트다운에 쓴다.</summary>
    public event Action<float> OnPlayerDiedEvent;

    /// <summary>새 판이 시작됐다(플레이어가 되살아났다).</summary>
    public event Action OnRunStartedEvent;

    [Tooltip("죽고 나서 새 판이 시작되기까지(초). 사망 연출이 도는 시간이다.")]
    [SerializeField, Min(0f)] private float _respawnDelay = 3f;

    [Tooltip("되살아날 자리. 비우면 시작할 때 플레이어가 서 있던 자리.")]
    [SerializeField] private Transform _spawnPoint;

    private Player _player;
    private Health _health;
    private Vector3 _startPosition;

    private float _respawnTimer = -1f;

    public bool IsWaitingRespawn => _respawnTimer >= 0f;
    public float RespawnRemaining => Mathf.Max(0f, _respawnTimer);

    private void Start()
    {
        Bind(FindAnyObjectByType<Player>());
    }

    public void Bind(Player player)
    {
        if (_health != null)
            _health.OnWreckedEvent -= HandlePlayerDead;

        _player = player;
        _health = player != null ? player.GetCompo<Health>() : null;

        if (_player != null)
            _startPosition = _player.transform.position;

        if (_health != null)
            _health.OnWreckedEvent += HandlePlayerDead;
    }

    private void OnDestroy()
    {
        if (_health != null)
            _health.OnWreckedEvent -= HandlePlayerDead;
    }

    // 죽은 순간이 아니라 부서진 순간부터 센다 — 활공 중 죽으면 추락하는 동안은 아직 "죽는 중"이다.
    private void HandlePlayerDead()
    {
        _respawnTimer = _respawnDelay;
        OnPlayerDiedEvent?.Invoke(_respawnDelay);
    }

    private void Update()
    {
        if (_respawnTimer < 0f) return;

        _respawnTimer -= Time.deltaTime;
        if (_respawnTimer > 0f) return;

        _respawnTimer = -1f;
        StartNewRun();
    }

    /// <summary>새 판. 죽음 뒤에 자동으로 불리고, 디버그로 밖에서 불러도 된다.</summary>
    public void StartNewRun()
    {
        if (_player == null) return;

        ClearWorld();
        RespawnPlayer();

        OnRunStartedEvent?.Invoke();
    }

    #region 월드

    private static void ClearWorld()
    {
        EnemyManager enemies = FindAnyObjectByType<EnemyManager>();
        if (enemies != null)
            enemies.ClearAll();

        // 남은 잔해 적재함. 비지 않은 잔해는 판 안에서는 계속 남지만 새 판에는 넘어가지 않는다.
        LootContainer.ClearAll();

        // 바닥의 아이템과 날아가던 탄. 이펙트는 스스로 끝나므로 두고, 소리도 둔다.
        foreach (PoolableMono item in FindObjectsByType<PoolableMono>(FindObjectsSortMode.None))
        {
            if (item.IsInPool) continue;

            if (item is WorldItem || item is Projectile)
                item.Release();
        }
    }

    #endregion

    #region 플레이어

    private void RespawnPlayer()
    {
        InventoryController inventory = _player.GetCompo<InventoryController>();
        EquipmentController equipment = _player.GetCompo<EquipmentController>();
        MechaPartController parts = _player.GetCompo<MechaPartController>();

        // 1. 비상탈출 — 기체와 적재물을 잃는다. 안전 슬롯의 인스턴스만 살려 둔다.
        List<uint> keep = new();
        if (inventory != null && inventory.SafeSlot != null)
        {
            for (int i = 0; i < inventory.SafeSlot.Capacity; i++)
            {
                uint id = inventory.SafeSlot[i].instanceId;
                if (id != 0) keep.Add(id);
            }
        }

        // 적재함을 먼저 비운다. 코어를 먼저 떼면 적재 칸 수가 줄며 넘친 아이템이 발밑에 떨어진다.
        inventory?.LoseCargo();
        equipment?.UnequipAll();

        // 2. 자리와 몸. 되살리기(체력·실드·부위)는 장비를 다시 입기 전에 한다 — 장착이 최대치를 올리면 비율(가득)이 유지된다.
        Vector3 spawn = _spawnPoint != null ? _spawnPoint.position : _startPosition;
        _player.transform.position = spawn;

        Rigidbody2D body = _player.GetComponent<Rigidbody2D>();
        if (body != null) body.position = spawn;

        _health?.ResetHealth();
        _player.GetCompo<AgentConditionEffector>()?.ClearConditions();
        _player.GetCompo<PlayerMoveController>()?.ResetMotion();

        // 3. 새 판 — 월드 상태(인스턴스·시드)를 되돌리고 진입 구성을 다시 입힌다.
        if (parts != null)
            parts.StartNewRun(keep);

        // 연료·전력 최대치는 추진체·코어가 정하므로 장착 뒤에 채운다.
        _player.GetCompo<VitalStatus>()?.ResetAll();

        CameraFollowEffector follow = FindAnyObjectByType<CameraFollowEffector>();
        if (follow != null) follow.SnapToTarget();
    }

    #endregion
}
