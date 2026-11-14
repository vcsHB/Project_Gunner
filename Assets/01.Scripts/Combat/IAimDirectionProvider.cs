using UnityEngine;

/// <summary>
/// "이 유닛이 어디를 겨누고 있는가". 무기가 탄을 어느 쪽으로 보낼지 물어보는 창구다.
///
/// 무기가 <see cref="PlayerAimController"/>를 직접 찾으면 <b>적은 무기를 쓸 수 없다.</b>
/// 플레이어는 커서로, 적은 AI가 고른 대상으로 정하지만 무기 입장에서는 같은 질문이다.
///
/// 회전(표현)과는 다르다. 기체가 아직 다 돌지 않았어도 탄은 겨눈 곳으로 나간다 —
/// 표현이 판정을 끌고 가면 "보이는 곳과 맞는 곳이 다른" 문제를 연출로 덮게 된다.
/// </summary>
public interface IAimDirectionProvider
{
    /// <summary>겨누는 방향(정규화). 논리 공간이다 — 리소스 방향 보정이 들어가지 않은 값.</summary>
    Vector2 AimDirection { get; }
}
