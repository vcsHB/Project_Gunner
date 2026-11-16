using UnityEngine;

/// <summary>
/// 이펙트 재생 진입점. <b>위치·방향·데이터만 받는다</b> — 누가 쐈는지는 모른다.
/// 멀티에서 이벤트를 받은 쪽이 각자 로컬에서 그대로 부를 수 있게 하려는 것이다.
///
/// 전투 로직은 이걸 부르지 않는다. 이벤트를 듣는 피드백 컴포넌트가 부른다 (RULES — 시뮬레이션과 표현 분리).
/// </summary>
public static class EffectPlayer
{
    /// <param name="type">이펙트 프리팹의 풀. <see cref="PoolType.None"/>이면 아무것도 하지 않는다 — 비워두면 "이펙트 없음".</param>
    /// <param name="direction">이펙트가 <b>뿜어져야 할</b> 월드 방향. 아트 축 보정은 <see cref="ParticleEffect"/>가 한다.</param>
    /// <param name="follow">총구 화염처럼 붙어 다녀야 할 때. 부모로 붙이지 않고 위치만 따라간다.</param>
    public static EffectHandle Play(PoolType type, Vector2 position, Vector2 direction, Transform follow = null)
        => Play(type, position, ToAngle(direction), follow);

    /// <param name="angle">뿜어져야 할 방향(도, +X 기준).</param>
    public static EffectHandle Play(PoolType type, Vector2 position, float angle, Transform follow = null)
    {
        if (type == PoolType.None) return default;

        PoolableMono item = ObjectPool.Get(type, position, Quaternion.identity);
        if (item == null) return default;

        if (item is not ParticleEffect effect)
        {
            // EffectBase만 있는 옛 이펙트(수명으로 반납)는 그대로 둔다. 핸들은 줄 수 없다.
            if (item is not EffectBase)
                Debug.LogError($"[EffectPlayer] {type}은 이펙트가 아닙니다. ({item.GetType().Name})", item);

            return default;
        }

        effect.Begin(angle, follow);
        return new EffectHandle(effect);
    }

    public static float ToAngle(Vector2 direction)
        => direction.sqrMagnitude > Mathf.Epsilon ? Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg : 0f;
}

/// <summary>
/// 재생 중인 이펙트를 가리킨다. 루프·지속 이펙트를 돌리거나 멈출 때 쓴다.
/// 이펙트가 풀로 돌아가 다른 곳에서 다시 꺼내졌으면 무효가 된다 — 남의 이펙트를 건드리지 않는다.
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

    public void SetDirection(Vector2 direction)
    {
        if (IsValid && direction.sqrMagnitude > Mathf.Epsilon)
            _effect.SetAngle(EffectPlayer.ToAngle(direction));
    }
}
