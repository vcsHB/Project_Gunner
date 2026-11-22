using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 착지 불가 지역(구렁·심연 등).
///
/// <list type="bullet">
/// <item>활공 중 — 이 위에서는 Space로 착륙할 수 없다. <b>강제로</b> 내려앉으면(연료 고갈·추진체 파괴) <b>추락해 죽는다</b> (<see cref="Health.Kill"/>).</item>
/// <item>지상 — <b>걸어서는 가장자리에서 멈춘다</b>(<see cref="ClampWalk"/>). 미끄러짐(관성)은 못 멈춰서 들어간다 →
/// 연료가 있으면 자동으로 뜨고, 없으면 추락한다. 넉백으로 밀려 들어가도 같다.</item>
/// </list>
///
/// 판정은 물리 레이어가 아니라 <b>콜라이더 모양</b>으로 한다(<see cref="Collider2D.OverlapPoint"/>). 레이어를 쓰면
/// 충돌 매트릭스·캐스터 규칙에 걸리는데, 여기는 "이 점이 영역 안인가"만 알면 된다. 콜라이더는 트리거로 둔다.
///
/// 가장자리는 벽 콜라이더가 아니다 — 벽이면 미끄러짐도 막혀서 "착륙 위치를 잘못 고르면 빠진다"가 성립하지 않는다.
/// 걷기만 이동 코드가 막는다(2026-11-23 결정).
/// 판정 기준점은 <b>그림자 = 실제 위치</b>(<see cref="PlayerMoveController"/>의 transform)다. 떠 있는 몸체가 아니다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class NoLandingZone : MonoBehaviour
{
    private static readonly List<NoLandingZone> Active = new();

    private Collider2D[] _areas;

    private void Awake()
    {
        _areas = GetComponents<Collider2D>();

        for (int i = 0; i < _areas.Length; i++)
            _areas[i].isTrigger = true;
    }

    private void OnEnable() => Active.Add(this);

    private void OnDisable() => Active.Remove(this);

    /// <summary>이 점이 어느 착지 불가 지역 안인가.</summary>
    public static bool Contains(Vector2 point)
    {
        for (int i = 0; i < Active.Count; i++)
        {
            if (Active[i].ContainsPoint(point)) return true;
        }

        return false;
    }

    /// <summary>
    /// 걷는 속도에서 <b>영역 안으로 들어가는 성분만</b> 지운다. 가장자리를 따라 미끄러지듯 걷는 건 된다.
    ///
    /// 이미 안에 있으면 건드리지 않는다 — 빠져나가는 걸 막으면 안 된다(그 상태 처리는 호출한 쪽이 한다).
    /// 축별로 시험한다. 상자 모양 영역에서는 정확하고, 비스듬한 가장자리에서는 조금 일찍 멈춘다.
    /// </summary>
    /// <param name="margin">가장자리에서 이만큼 앞에서 멈춘다. 몸 반지름 정도.</param>
    public static Vector2 ClampWalk(Vector2 position, Vector2 velocity, float deltaTime, float margin)
    {
        if (Active.Count == 0 || velocity.sqrMagnitude < 0.0001f) return velocity;
        if (Contains(position)) return velocity;

        if (!Blocks(position, velocity, deltaTime, margin)) return velocity;

        Vector2 x = new(velocity.x, 0f);
        Vector2 y = new(0f, velocity.y);

        bool xOk = Mathf.Abs(velocity.x) > 0.0001f && !Blocks(position, x, deltaTime, margin);
        bool yOk = Mathf.Abs(velocity.y) > 0.0001f && !Blocks(position, y, deltaTime, margin);

        if (xOk && yOk) return Mathf.Abs(velocity.x) >= Mathf.Abs(velocity.y) ? x : y;
        if (xOk) return x;
        if (yOk) return y;

        return Vector2.zero;
    }

    private static bool Blocks(Vector2 position, Vector2 velocity, float deltaTime, float margin)
    {
        Vector2 next = position + velocity * deltaTime + velocity.normalized * margin;
        return Contains(next);
    }

    private bool ContainsPoint(Vector2 point)
    {
        for (int i = 0; i < _areas.Length; i++)
        {
            if (_areas[i] != null && _areas[i].enabled && _areas[i].OverlapPoint(point)) return true;
        }

        return false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.25f);

        foreach (Collider2D area in GetComponents<Collider2D>())
            Gizmos.DrawCube(area.bounds.center, area.bounds.size);
    }
#endif
}
