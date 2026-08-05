using System;
using System.Collections.Generic;
using UnityEngine;


public enum ModifierMode
{
    /// <summary>기본값에 그대로 더한다.</summary>
    Additive = 0,

    /// <summary>비율로 더한다. 0.2면 +20%, -0.1이면 -10%.</summary>
    Percent = 1,
}

public struct ModifierData<T>
{
    public object origin;
    public T value;
    public ModifierMode mode;
}

/// <summary>
/// 제네릭 인자 없이 Status를 담아두기 위한 비제네릭 베이스. (AgentStatus의 타입별 조회용)
/// </summary>
public abstract class StatusBase
{
    public abstract void RemoveModifier(object origin);
    public abstract void ClearModifiers();
}

[System.Serializable]
public class Status<T> : StatusBase
{
    // 닫힌 제네릭 타입(Status<float>, Status<bool> ...)마다 한 번만 해석된다.
    private static readonly Func<T, List<ModifierData<T>>, T> s_combine = StatusCombiner.Resolve<T>();

    public event Action<T> OnChangedEvent;
    [SerializeField] private T _defaultValue;
    private readonly List<ModifierData<T>> _modifiers = new();
    private bool _isValueChanged = true;
    private T _totalValue;

    #region Properties
    public T DefaultValue => _defaultValue;

    public IReadOnlyList<ModifierData<T>> Modifiers => _modifiers;

    public T TotalValue
    {
        get
        {
            if (_isValueChanged)
            {
                _totalValue = s_combine(_defaultValue, _modifiers);
                _isValueChanged = false;
            }

            return _totalValue;
        }
    }

    #endregion

    public Status() { }

    public Status(T defaultValue)
    {
        _defaultValue = defaultValue;
    }

    public void SetDefaultValue(T value)
    {
        _defaultValue = value;
        SetDirty();
    }

    public void AddModifier(object origin, T value, ModifierMode mode = ModifierMode.Additive)
    {
        _modifiers.Add(new ModifierData<T>
        {
            origin = origin,
            value = value,
            mode = mode
        });

        SetDirty();
    }

    public override void RemoveModifier(object origin)
    {
        if (_modifiers.RemoveAll(modifier => ReferenceEquals(modifier.origin, origin)) > 0)
            SetDirty();
    }

    public override void ClearModifiers()
    {
        if (_modifiers.Count == 0) return;

        _modifiers.Clear();
        SetDirty();
    }

    private void SetDirty()
    {
        _isValueChanged = true;

        // ?. 는 구독자가 없으면 인자를 평가하지 않으므로 재계산도 일어나지 않는다.
        OnChangedEvent?.Invoke(TotalValue);
    }
}

/// <summary>
/// Status가 modifier를 합산하는 방식을 타입별로 정의한다.
/// 지원 타입을 늘리려면 Resolve에 분기를 추가할 것.
/// </summary>
internal static class StatusCombiner
{
    public static Func<T, List<ModifierData<T>>, T> Resolve<T>()
    {
        if (typeof(T) == typeof(float))
            return Cast<T, float>(CombineFloat);
        if (typeof(T) == typeof(int))
            return Cast<T, int>(CombineInt);
        if (typeof(T) == typeof(bool))
            return Cast<T, bool>(CombineBool);

        throw new NotSupportedException($"Status<{typeof(T).Name}>의 합산 방식이 정의되지 않았습니다.");
    }

    // T와 TConcrete가 같은 타입일 때만 호출되므로 런타임 캐스팅이 안전하다.
    private static Func<T, List<ModifierData<T>>, T> Cast<T, TConcrete>(
        Func<TConcrete, List<ModifierData<TConcrete>>, TConcrete> combine)
        => (Func<T, List<ModifierData<T>>, T>)(object)combine;

    // (기본값 + 덧셈 전부) * (1 + 비율 전부)
    // 덧셈을 먼저 다 적용한 뒤 비율을 곱하므로, 적용 순서에 따라 결과가 달라지지 않는다.
    private static float CombineFloat(float defaultValue, List<ModifierData<float>> modifiers)
    {
        float total = defaultValue;
        float percent = 0f;

        for (int i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i].mode == ModifierMode.Percent)
                percent += modifiers[i].value;
            else
                total += modifiers[i].value;
        }

        return total * (1f + percent);
    }

    private static int CombineInt(int defaultValue, List<ModifierData<int>> modifiers)
    {
        int total = defaultValue;
        float percent = 0f;

        for (int i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i].mode == ModifierMode.Percent)
                percent += modifiers[i].value;
            else
                total += modifiers[i].value;
        }

        return Mathf.RoundToInt(total * (1f + percent));
    }

    // bool은 하나라도 true면 true. (면역/저항 같은 플래그용)
    private static bool CombineBool(bool defaultValue, List<ModifierData<bool>> modifiers)
    {
        for (int i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i].value)
                return true;
        }

        return defaultValue;
    }
}
