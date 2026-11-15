/// <summary>
/// 재생 중인 소리를 가리킨다. 루프 소리(부스터·활공·과열 경보)를 멈출 때 쓴다 —
/// 소유자의 <c>Dispose</c>/<c>OnDespawn</c>에서 반드시 <see cref="Stop"/>할 것.
/// 소리가 끝나 풀로 돌아가 다른 곳에서 다시 꺼내졌으면 무효가 된다 — 남의 소리를 끄지 않는다.
/// </summary>
public readonly struct SoundHandle
{
    private readonly SoundPlayer _player;
    private readonly int _version;

    public SoundHandle(SoundPlayer player)
    {
        _player = player;
        _version = player != null ? player.SpawnVersion : 0;
    }

    public bool IsValid => _player != null && !_player.IsInPool && _player.SpawnVersion == _version;

    public void Stop()
    {
        if (IsValid) _player.Stop();
    }
}
