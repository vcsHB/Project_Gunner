using UnityEngine;

/// <summary>
/// 풀링되는 <see cref="AudioSource"/> 하나. <b>풀은 하나만 쓴다</b> — 소리마다 프리팹을 만들지 않고 클립은 재생할 때 넣는다.
/// 직접 꺼내지 말고 <see cref="SoundManager.Play"/>를 쓴다 (동시 재생 상한을 거기서 센다).
///
/// 루프가 아니면 끝나면 스스로 반납된다. 루프는 <see cref="SoundHandle.Stop"/>으로 끝낸다.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class SoundPlayer : PoolableMono
{
    [SerializeField] private AudioSource _audioSource;

    private Transform _follow;
    private bool _started;

    public SoundDataSO Data { get; private set; }

    /// <summary>꺼낼 때마다 바뀐다. 핸들이 "내가 꺼낸 그 회차인지"를 확인하는 데 쓴다.</summary>
    public int SpawnVersion { get; private set; }

    public float StartTime { get; private set; }

    private void Awake()
    {
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();

        _audioSource.playOnAwake = false;
        _audioSource.dopplerLevel = 0f;
        _audioSource.rolloffMode = AudioRolloffMode.Linear;
    }

    public override void OnSpawn()
    {
        SpawnVersion++;
        _started = false;
    }

    public override void OnDespawn()
    {
        _audioSource.Stop();
        _audioSource.clip = null;
        _follow = null;
        _started = false;

        if (Data != null)
            SoundManager.NotifyReleased(this);

        Data = null;
    }

    internal void Begin(SoundDataSO data, AudioClip clip, Vector2 position, Transform follow,
        UnityEngine.Audio.AudioMixerGroup group)
    {
        Data = data;
        _follow = follow;
        StartTime = Time.time;

        transform.position = new Vector3(position.x, position.y, 0f);

        AudioSource source = _audioSource;
        source.clip = clip;
        source.volume = data.Volume;
        source.pitch = data.PickPitch();
        source.loop = data.Loop;
        source.outputAudioMixerGroup = group;
        source.spatialBlend = data.SpatialBlend;
        source.minDistance = data.FullVolumeRange;
        source.maxDistance = data.AudibleRange;
        source.Play();

        _started = true;
    }

    public void Stop()
    {
        if (!IsInPool) Release();
    }

    private void LateUpdate()
    {
        if (!_started) return;

        if (_follow != null)
        {
            // 대상이 꺼지면 그 자리에서 마저 울린다. 루프는 주인이 사라졌으니 끝낸다.
            if (!_follow.gameObject.activeInHierarchy)
            {
                _follow = null;
                if (Data != null && Data.Loop)
                {
                    Release();
                    return;
                }
            }
            else
            {
                Vector3 p = _follow.position;
                transform.position = new Vector3(p.x, p.y, 0f);
            }
        }

        if (!_audioSource.loop && !_audioSource.isPlaying)
            Release();
    }
}
