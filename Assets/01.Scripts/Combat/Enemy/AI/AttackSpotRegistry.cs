using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 대상 둘레의 공격 자리(각도) 예약. 자리를 옮기며 쏘는 적(<see cref="EnemyAttackMove.Reposition"/> — 사격 드론)이
/// 같은 대상을 노리는 <b>모든</b> 적과 자리가 겹치지 않게 한다. 편대가 달라도 같은 대상이면 서로 피한다.
///
/// 판정에 쓰이지만 난수가 없고 순서대로 정해진다. 싱글턴 상태지만 플레이어별 상태가 아니라 "대상별" 표다(RULES — 멀티).
/// </summary>
public static class AttackSpotRegistry
{
    private struct Spot
    {
        public TargetBase target;
        public float angle;
    }

    private static readonly Dictionary<EnemyAIController, Spot> s_spots = new();

    // 원하는 각도에서 좌우로 넓혀 갈 때 한 칸(도)과 칸 수.
    private const float Step = 10f;
    private const int Tries = 18;

    /// <summary>
    /// <paramref name="target"/> 둘레 반경 <paramref name="radius"/>에서 자리를 잡는다. <paramref name="desired"/>(도)부터 좌우로 넓혀 가며
    /// 다른 적이 잡은 자리와 <paramref name="minSpacing"/>(유닛) 이상 떨어진 첫 각도. 다 막혔으면 원하는 각도 그대로.
    /// </summary>
    public static float Claim(EnemyAIController who, TargetBase target, float desired, float radius, float minSpacing)
    {
        float gap = radius > 0.01f ? 2f * Mathf.Asin(Mathf.Clamp01(minSpacing / (2f * radius))) * Mathf.Rad2Deg : 0f;
        float chosen = desired;

        if (gap > 0f && target != null)
        {
            for (int i = 0; i <= Tries; i++)
            {
                // 0, +10, -10, +20, -20 ... 원하는 쪽에서 가까운 것부터.
                float offset = (i + 1) / 2 * Step * (i % 2 == 1 ? 1f : -1f);
                float candidate = desired + offset;

                if (IsFree(who, target, candidate, gap))
                {
                    chosen = candidate;
                    break;
                }
            }
        }

        s_spots[who] = new Spot { target = target, angle = chosen };
        return chosen;
    }

    /// <summary>공격을 그만뒀다. 잡은 자리를 내놓는다.</summary>
    public static void Release(EnemyAIController who) => s_spots.Remove(who);

    private static bool IsFree(EnemyAIController who, TargetBase target, float angle, float gap)
    {
        foreach (KeyValuePair<EnemyAIController, Spot> pair in s_spots)
        {
            if (pair.Key == who || pair.Key == null || pair.Key.IsDead) continue;
            if (pair.Value.target != target) continue;
            if (Mathf.Abs(Mathf.DeltaAngle(pair.Value.angle, angle)) < gap) return false;
        }

        return true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay() => s_spots.Clear();
}
