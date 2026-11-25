using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 함께 다니는 무리 하나 — 지금은 <b>드론 편대</b>. 스포너가 무리를 찍어낼 때 묶는다(<see cref="EnemyManager.Spawn"/>).
///
/// | | 평시 | 목표를 보면 |
/// |---|---|---|
/// | 대장 | 집(스폰 자리) 근처를 느리게 순찰 | 다 같이 경보 |
/// | 나머지 | 대장 뒤 V자 대열 자리를 따라간다 | 대상 둘레로 <b>산개</b> — 자리마다 각도를 벌린다 |
///
/// 누가 하나라도 대상을 보면 <see cref="Alert"/> — 쫓는 중인 동료가 매 틱 갱신한다. 갱신이 끊기면(전원이 놓침) 대열로 돌아간다.
/// 대장이 죽으면 다음 생존자가 대장이다. 자리 번호는 <b>살아 있는 순서</b>로 매긴다 — 빠진 자리를 메운다.
///
/// MonoBehaviour가 아니다. 무리가 남긴 것은 멤버 참조뿐이고, 마지막 멤버가 사라지면 아무도 붙잡지 않는다.
/// 나중의 <b>소대</b>(엘리트+호위+하급, 대열 모양 데이터)는 이것을 넓혀 쓴다 — 대장·대열 자리·경보 공유가 같다.
/// </summary>
public class EnemySquad
{
    // 경보가 이만큼(초) 갱신되지 않으면 풀린다.
    private const float AlertHold = 1.5f;

    private readonly List<EnemyAIController> _members = new();

    private float _alertUntil = -1f;

    /// <summary>순찰의 중심. 스폰 자리.</summary>
    public Vector2 Home { get; }

    public bool IsAlerted => Time.time < _alertUntil;

    public EnemySquad(IEnumerable<EnemyAIController> members, Vector2 home)
    {
        Home = home;

        foreach (EnemyAIController member in members)
        {
            if (member == null) continue;

            _members.Add(member);
            member.JoinSquad(this);
        }
    }

    /// <summary>지금 대장. 살아 있는 첫 멤버. 전멸이면 null.</summary>
    public EnemyAIController Leader
    {
        get
        {
            for (int i = 0; i < _members.Count; i++)
                if (IsAlive(_members[i])) return _members[i];

            return null;
        }
    }

    public int AliveCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < _members.Count; i++)
                if (IsAlive(_members[i])) count++;

            return count;
        }
    }

    public bool IsLeader(EnemyAIController member) => member != null && Leader == member;

    /// <summary>살아 있는 멤버 중 몇 번째인가(대장 0). 무리에 없거나 죽었으면 -1.</summary>
    public int SlotOf(EnemyAIController member)
    {
        int slot = 0;

        for (int i = 0; i < _members.Count; i++)
        {
            if (!IsAlive(_members[i])) continue;
            if (_members[i] == member) return slot;

            slot++;
        }

        return -1;
    }

    /// <summary>
    /// 대열 자리(월드). 대장 뒤로 V자 — 1·2번이 첫 줄 좌우, 3·4번이 둘째 줄. 대장이 보는 쪽 기준이라 방향을 틀면 대열도 돈다.
    /// </summary>
    public Vector2 FormationSlot(EnemyAIController member, float spacing)
    {
        EnemyAIController leader = Leader;
        if (leader == null || leader == member) return member.transform.position;

        int slot = SlotOf(member);
        if (slot <= 0) return leader.transform.position;

        int rank = (slot + 1) / 2;
        float side = slot % 2 == 1 ? 1f : -1f;

        // 대장 기준 좌표 — +X가 앞. 뒤로 물러나며 옆으로 벌어진다.
        Vector2 local = new(-rank * spacing * 0.8f, side * rank * spacing);

        float angle = leader.Move.FacingAngle * Mathf.Deg2Rad;
        float cos = Mathf.Cos(angle);
        float sin = Mathf.Sin(angle);
        Vector2 world = new(local.x * cos - local.y * sin, local.x * sin + local.y * cos);

        return (Vector2)leader.transform.position + world;
    }

    /// <summary>산개할 때 대상 둘레에서 벌릴 각도(도). 자리 번호가 가운데를 중심으로 좌우로 퍼진다.</summary>
    public float ScatterOffset(EnemyAIController member, float step)
    {
        int slot = SlotOf(member);
        int count = AliveCount;
        if (slot < 0 || count <= 1) return 0f;

        return (slot - (count - 1) * 0.5f) * step;
    }

    /// <summary>대상을 봤다. 쫓는 동안 매 틱 부른다.</summary>
    public void Alert() => _alertUntil = Time.time + AlertHold;

    private static bool IsAlive(EnemyAIController member) => member != null && !member.IsDead;
}
