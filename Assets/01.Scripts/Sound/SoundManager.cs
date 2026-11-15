using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 효과음 재생 진입점. <b>위치·데이터만 받는다</b> — 누가 냈는지는 모른다 (멀티에서 받은 쪽이 로컬로 튼다).
///
/// 여기가 하는 일은 데이터별로 <b>지금 몇 개 울리는지 세는 것</b>뿐이다 —
/// 동시 재생 상한·최소 간격·들리는 거리 밖 거르기. 플레이어별 상태가 없어서 싱글턴이어도 RULES에 걸리지 않는다.
///
/// 전투 로직은 이걸 부르지 않는다. 이벤트를 듣는 피드백 컴포넌트가 부른다.
/// BGM은 여기가 아니다 — 풀 밖의 전용 AudioSource로 따로 다룬다.
/// </summary>
public class SoundManager : Singleton<SoundManager>
{
    [Tooltip("SoundDataSO에 믹서 그룹이 비어 있을 때 쓰는 그룹. 비우면 믹서를 거치지 않는다.")]
    [SerializeField] private AudioMixerGroup _defaultGroup;

    private readonly Dictionary<SoundDataSO, List<SoundPlayer>> _playing = new();
    private readonly Dictionary<SoundDataSO, float> _lastPlayTime = new();

    /// <summary>
    /// 소리를 튼다. 걸러지면(상한·간격·거리 밖) 무효한 핸들을 준다 — 핸들을 쓰는 쪽은 그대로 Stop해도 안전하다.
    /// </summary>
    /// <param name="follow">움직이는 소리(부스터·활공). 부모로 붙이지 않고 위치만 맞춘다.</param>
    public static SoundHandle Play(SoundDataSO data, Vector2 position, Transform follow = null)
    {
        if (data == null || !data.HasClip) return default;

        SoundManager manager = Instance;
        if (manager == null)
        {
            Debug.LogWarning("[SoundManager] 씬에 SoundManager가 없습니다.");
            return default;
        }

        return manager.PlayInternal(data, position, follow);
    }

    internal static void NotifyReleased(SoundPlayer player)
    {
        if (!HasInstance) return;

        if (Instance._playing.TryGetValue(player.Data, out List<SoundPlayer> list))
            list.Remove(player);
    }

    private SoundHandle PlayInternal(SoundDataSO data, Vector2 position, Transform follow)
    {
        float now = Time.time;

        if (data.MinInterval > 0f
            && _lastPlayTime.TryGetValue(data, out float last)
            && now - last < data.MinInterval)
            return default;

        // 루프는 거르지 않는다. 멀리서 시작해도 다가오면 들려야 하고, 소유자는 핸들로 끝을 관리한다.
        if (!data.Loop && IsOutOfRange(data, position))
            return default;

        if (!_playing.TryGetValue(data, out List<SoundPlayer> list))
        {
            list = new List<SoundPlayer>(Mathf.Max(1, data.MaxConcurrent));
            _playing.Add(data, list);
        }

        if (data.MaxConcurrent > 0 && list.Count >= data.MaxConcurrent)
        {
            if (!data.StealOldest) return default;

            // 리스트는 시작 순서다. 앞이 가장 오래됐다. Stop → OnDespawn → NotifyReleased로 리스트에서 빠진다.
            list[0].Stop();
        }

        SoundPlayer player = ObjectPool.Get<SoundPlayer>();
        if (player == null) return default;

        list.Add(player);
        _lastPlayTime[data] = now;

        player.Begin(data, data.PickClip(), position, follow, data.MixerGroup != null ? data.MixerGroup : _defaultGroup);
        return new SoundHandle(player);
    }

    private static bool IsOutOfRange(SoundDataSO data, Vector2 position)
    {
        if (data.SpatialBlend <= 0f) return false;

        AudioListenerFollow listener = AudioListenerFollow.Current;
        if (listener == null) return false;

        Vector3 delta = (Vector3)position - listener.transform.position;
        return delta.sqrMagnitude > data.AudibleRange * data.AudibleRange;
    }

    protected override void OnDestroy()
    {
        _playing.Clear();
        _lastPlayTime.Clear();

        base.OnDestroy();
    }
}
