using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 착지 불가 지역(구렁·심연·물 등). <b>활공 중에만 의미가 있다.</b>
///
/// <list type="bullet">
/// <item>이 위에서는 Space로 착륙할 수 없다.</item>
/// <item>이 위에서 <b>강제로</b> 내려앉으면(연료 고갈·추진체 파괴) <b>추락해 죽는다</b> (<see cref="Health.Kill"/>).</item>
/// </list>
///
/// 판정은 물리 레이어가 아니라 <b>콜라이더 모양</b>으로 한다(<see cref="Collider2D.OverlapPoint"/>). 레이어를 쓰면
/// 충돌 매트릭스·캐스터 규칙에 걸리는데, 여기는 "이 점이 영역 안인가"만 알면 된다. 콜라이더는 트리거로 둔다.
///
/// 지상에서 걸어 들어가는 것을 막는 건 여기가 아니다 — 맵의 벽(지상 유닛만 막는 콜라이더)이 할 일이다(M2 맵).
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
