using System;
using UnityEngine;

/// <summary>
/// 플레이어가 탈 수 있는 기체. 조종하는 쪽은 <see cref="PlayerPilot"/>이다.
///
/// 입력은 기체에 없다 — 탄 조종권의 것을 빌린다(<see cref="Input"/>). 아무도 안 타면 null이고 기체는 서 있는다.
/// 입력을 구독하는 컴포넌트는 <see cref="OnInputChangedEvent"/>로 갈아 끼운다 — 초기화 때 한 번 붙이고 끝내면 갈아탄 뒤에 안 움직인다.
/// </summary>
public class Player : Agent
{
    /// <summary>입력이 바뀌었다(탔다·내렸다). (이전, 지금) — 어느 쪽이든 null일 수 있다.</summary>
    public event Action<PlayerInput, PlayerInput> OnInputChangedEvent;

    /// <summary>탄 조종권. 없으면 null.</summary>
    public PlayerPilot Pilot { get; private set; }

    public PlayerInput Input => Pilot != null ? Pilot.Input : null;

    public bool IsPiloted => Pilot != null;

    /// <summary><see cref="PlayerPilot.Board"/>만 부른다.</summary>
    internal void SetPilot(PlayerPilot pilot)
    {
        if (Pilot == pilot) return;

        PlayerInput previous = Input;
        Pilot = pilot;

        if (previous != Input)
            OnInputChangedEvent?.Invoke(previous, Input);
    }
}
