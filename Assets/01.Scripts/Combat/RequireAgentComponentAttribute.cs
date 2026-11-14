using System;

/// <summary>
/// 이 컴포넌트가 같은 Agent 안에서 무엇을 필요로 하는지 선언한다.
///
/// <b>유니티의 RequireComponent를 쓰지 않는 이유</b> — 그건 <b>같은 GameObject</b>만 본다.
/// Agent는 자식까지 훑어서 컴포넌트를 모으므로(<see cref="Agent"/>), 역할별로 자식에 나눠 두면
/// RequireComponent가 루트에 중복으로 다시 붙여버린다.
///
/// 그래서 강제하지 않고 <b>검증만</b> 한다. Agent 인스펙터가 계층 전체에서 찾아보고
/// 빠진 것을 짚어준다. 없다고 컴파일이나 실행이 막히지는 않는다 —
/// 빠졌을 때 조용히 아무것도 안 하는 것보다, 어디가 비었는지 보이는 것이 목적이다.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class RequireAgentComponentAttribute : Attribute
{
    public Type ComponentType { get; }

    /// <summary>없어도 동작은 하지만 기능이 꺼지는 경우. 에러 대신 경고로 표시한다.</summary>
    public bool Optional { get; }

    public RequireAgentComponentAttribute(Type componentType, bool optional = false)
    {
        ComponentType = componentType;
        Optional = optional;
    }
}
