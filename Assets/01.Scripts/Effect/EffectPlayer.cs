using UnityEngine;

/// <summary>
/// 이펙트 재생 진입점. <b>위치·방향·데이터만 받는다</b> — 누가 쐈는지는 모른다.
/// 멀티에서 이벤트를 받은 쪽이 각자 로컬에서 그대로 부를 수 있게 하려는 것이다.
///
/// 전투 로직은 이걸 부르지 않는다. 이벤트를 듣는 피드백 컴포넌트가 부른다 (RULES — 시뮬레이션과 표현 분리).
/// </summary>
public static class EffectPlayer
{
    /// <param name="type">이펙트 프리팹의 풀. <see cref="PoolType.None"/>이면 아무것도 하지 않는다 — SO에서 비워두면 "이펙트 없음".</param>
    /// <param name="angle">도(degree). 이펙트의 +X가 이 방향을 본다.</param>
    /// <param name="follow">총구 화염처럼 붙어 다녀야 할 때. 부모로 붙이지 않는다.</param>
    public static EffectHandle Play(PoolType type, Vector2 position, float angle = 0f,
        Transform follow = null, bool followRotation = false)
    {
        if (type == PoolType.None) return default;

        PoolableMono item = ObjectPool.Get(type, position, Quaternion.Euler(0f, 0f, angle));
        if (item == null) return default;

        if (item is not ParticleEffect effect)
        {
            // EffectBase만 있는 옛 이펙트(수명으로 반납)는 그대로 둔다. 핸들은 줄 수 없다.
            if (item is not EffectBase)
                Debug.LogError($"[EffectPlayer] {type}은 이펙트가 아닙니다. ({item.GetType().Name})", item);

            return default;
        }

        effect.Begin(follow, followRotation);
        return new EffectHandle(effect);
    }
}

/// <summary>
/// 재생 중인 이펙트를 가리킨다. 루프 이펙트를 멈출 때 쓴다.
/// 이펙트가 풀로 돌아가 다른 곳에서 다시 꺼내졌으면 무효가 된다 — 남의 이펙트를 끄지 않는다.
/// </summary>
public readonly struct EffectHandle
{
    private readonly ParticleEffect _effect;
    private readonly int _version;

    public EffectHandle(ParticleEffect effect)
    {
        _effect = effect;
        _version = effect != null ? effect.SpawnVersion : 0;
    }

    public bool IsValid => _effect != null && !_effect.IsInPool && _effect.SpawnVersion == _version;

    public void Stop()
    {
        if (IsValid) _effect.Stop();
    }
}
