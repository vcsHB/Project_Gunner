using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PVPVE에서 E를 담당. 적 유닛 하나.
///
/// 이동·AI는 별도 <see cref="IAgentComponent"/>가 맡는다. 이 클래스는
/// <b>데이터 주입과 죽음</b>만 책임진다.
///
/// 풀링하지 않고 <see cref="Object.Destroy"/>로 없앤다. <see cref="Agent"/>와
/// <see cref="PoolableMono"/>가 둘 다 MonoBehaviour 직계라 양쪽을 상속할 수 없고,
/// 풀링은 <c>OnDespawn</c>에서 체력·TargetType·구독을 빠짐없이 되돌려야 해서
/// 스폰이 잦아지는 시점에 다시 본다. (docs/MILESTONE.md)
/// </summary>
public class Enemy : Agent
{
    [Header("Enemy")]
    [Tooltip("씬에 직접 놓고 테스트할 때 쓸 데이터. 스포너가 만들면 Setup으로 덮인다.")]
    [SerializeField] private UnitDataSO _data;

    [Tooltip("전리품이 떨어질 위치. 비우면 자기 위치.")]
    [SerializeField] private Transform _dropPoint;

    /// <summary>전리품을 굴릴 때만 잠깐 쓴다. 한 프레임에 여럿 죽어도 순차 실행이라 안전하다.</summary>
    private static readonly List<ItemStack> s_lootBuffer = new();

    public UnitDataSO Data => _data;

    private bool _dataApplied;

    private Vector2 DropPosition => _dropPoint != null ? (Vector2)_dropPoint.position : (Vector2)transform.position;

    /// <summary>
    /// 스포너가 찍어낸 직후 부른다. <see cref="Agent.Awake"/>가 이미 끝난 뒤여야
    /// AgentStatus가 초기화되어 있다.
    /// </summary>
    public void Setup(UnitDataSO data)
    {
        if (data == null) return;

        _data = data;
        ApplyData();
    }

    protected override void Start()
    {
        base.Start();

        // 씬에 직접 놓인 경우엔 Setup을 불러주는 쪽이 없다.
        if (!_dataApplied)
            ApplyData();

        // TargetBase가 없으면 캐스터가 이 유닛을 대상으로 인식하지 못한다.
        // 총알이 그냥 지나가는데 오류는 하나도 안 나므로 여기서 잡아준다.
        if (GetCompo<TargetBase>() == null)
            Debug.LogError($"[Enemy] {name}에 TargetBase가 없습니다. 공격이 통하지 않습니다.", this);
    }

    private void ApplyData()
    {
        _dataApplied = true;

        if (_data == null) return;

        _data.ApplyTo(AgentStatus);

        // 최대 체력이 바뀌었으니 현재 체력을 다시 채운다.
        // Health는 비율을 유지하며 따라오지만, 스폰 시점에는 가득 찬 상태여야 한다.
        if (AgentHealth != null)
            AgentHealth.ResetHealth();
    }

    protected override void HandleDead()
    {
        base.HandleDead();

        DropLoot();

        // Destroy는 프레임 끝에 처리되므로, 지금 돌고 있는 캐스터 루프 안에서 불려도 안전하다.
        // 그 사이 다시 맞는 것은 TargetBase.IsTargetable이 막는다(IsAlive == false).
        Destroy(gameObject);
    }

    private void DropLoot()
    {
        if (_data == null) return;

        s_lootBuffer.Clear();
        _data.RollLoot(s_lootBuffer);

        if (s_lootBuffer.Count == 0) return;

        WorldItemSpawner.SpawnAll(s_lootBuffer, DropPosition);
        s_lootBuffer.Clear();
    }
}
