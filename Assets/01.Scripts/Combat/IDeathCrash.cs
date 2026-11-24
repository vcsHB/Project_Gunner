using System;

/// <summary>
/// 죽는 순간 <b>바로 부서지지 않고 추락하는</b> 몸. 활공 중인 메카, (나중에) 나는 드론이 구현한다. Agent 컴포넌트로 붙는다.
///
/// 죽음은 두 단계다 — <see cref="Health.OnDeadEvent"/>(조종을 잃음: 입력·AI·조준·판정이 꺼진다)와
/// <see cref="Health.OnWreckedEvent"/>(몸이 부서짐: 폭발·잔해·흔들림·새 판 카운트다운).
/// 땅에 있으면 둘이 같은 프레임에 온다. 떠 있으면 그 사이에 추락이 끼어든다.
///
/// <see cref="Health"/>가 죽을 때 <see cref="TryBeginCrash"/>를 묻는다. true면 구현체가 땅에 닿는 순간 <c>onImpact</c>를 부른다.
/// <b>반드시 한 번 불러야 한다</b> — 안 부르면 잔해도 새 판도 오지 않는다. 리셋(새 판)으로 추락이 끊기면 부르지 않는다(Health가 이미 되살아났다).
/// </summary>
public interface IDeathCrash
{
    /// <summary>추락이 시작됐다. 연기 같은 연출이 듣는다.</summary>
    event Action OnCrashStartedEvent;

    /// <summary>땅에 닿았다(폭발 직전). <see cref="Health.OnWreckedEvent"/>보다 먼저 온다.</summary>
    event Action OnCrashImpactEvent;

    bool IsCrashing { get; }

    /// <summary>떠 있으면 추락을 시작하고 true. 땅에 있으면 false — 그 자리에서 바로 부서진다.</summary>
    bool TryBeginCrash(Action onImpact);
}
