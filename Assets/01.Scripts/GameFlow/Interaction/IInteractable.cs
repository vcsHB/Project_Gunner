using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 다가가서 상호작용 키(E)로 쓰는 것. 잔해 적재함이 처음이고, 상자·구조물·탈출 지점이 같은 길을 쓴다.
///
/// 찾는 쪽(<see cref="PlayerInteractor"/>)이 물리 질의 없이 <see cref="InteractableRegistry"/>를 훑는다 —
/// 수가 적고, 상호작용 범위가 콜라이더 모양과 상관없어야 해서다.
/// </summary>
public interface IInteractable
{
    /// <summary>거리를 재는 기준점.</summary>
    Vector2 InteractPosition { get; }

    /// <summary>키 옆에 붙는 동사. "탐색", "열기".</summary>
    string InteractLabel { get; }

    bool CanInteract(Agent who);

    void Interact(Agent who);
}

/// <summary>살아 있는 상호작용 대상 목록. 대상이 켜질 때 넣고 꺼질 때 뺀다.</summary>
public static class InteractableRegistry
{
    private static readonly List<IInteractable> s_all = new();

    public static IReadOnlyList<IInteractable> All => s_all;

    public static void Register(IInteractable target)
    {
        if (target != null && !s_all.Contains(target)) s_all.Add(target);
    }

    public static void Unregister(IInteractable target) => s_all.Remove(target);

    /// <summary><paramref name="radius"/> 안에서 가장 가까운 것. 없으면 null.</summary>
    public static IInteractable FindNearest(Agent who, Vector2 from, float radius)
    {
        IInteractable best = null;
        float bestSqr = radius * radius;

        for (int i = 0; i < s_all.Count; i++)
        {
            IInteractable target = s_all[i];
            if (!target.CanInteract(who)) continue;

            float sqr = (target.InteractPosition - from).sqrMagnitude;
            if (sqr > bestSqr) continue;

            bestSqr = sqr;
            best = target;
        }

        return best;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay() => s_all.Clear();
}
