using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 소리 하나의 재생 규칙. 클립은 여러 개 넣으면 무작위로 고른다.
///
/// <b>들리는 거리는 소리의 크기로 정한다</b> — <see cref="AudibleRange"/> 밖이면 아예 재생하지 않고,
/// 안이면 거리에 따라 선형으로 줄어든다. 큰 소리(폭발·대구경)는 넓게, 작은 소리(빈 탄창)는 좁게 잡는다.
/// 화면 밖이어도 범위 안이면 들린다.
///
/// 피치·클립 고르기에는 <see cref="Random"/>을 쓴다. 연출이라 판정 재현과 무관하다 (RULES 예외).
/// </summary>
[CreateAssetMenu(fileName = "Sound_", menuName = "SO/Sound/SoundData")]
public class SoundDataSO : ScriptableObject
{
    [SerializeField] private AudioClip[] _clips;

    [Header("Mix")]
    [SerializeField, Range(0f, 1f)] private float _volume = 1f;
    [SerializeField, Range(0.1f, 3f)] private float _pitchMin = 1f;
    [SerializeField, Range(0.1f, 3f)] private float _pitchMax = 1f;
    [Tooltip("비우면 SoundManager의 기본 그룹(SFX).")]
    [SerializeField] private AudioMixerGroup _mixerGroup;
    [SerializeField] private bool _loop;

    [Header("Space")]
    [Tooltip("0이면 위치와 무관하게 들린다(UI). 1이면 위치에서 난다.")]
    [SerializeField, Range(0f, 1f)] private float _spatialBlend = 1f;
    [Tooltip("이 거리까지는 최대 볼륨. 귀가 기체 위로 4 떠 있으므로 그보다 크게.")]
    [SerializeField, Min(0f)] private float _fullVolumeRange = 5f;
    [Tooltip("이 거리에서 0이 된다. 밖이면 재생하지 않는다. 소리의 크기다.")]
    [SerializeField, Min(0.1f)] private float _audibleRange = 25f;

    [Header("Limit")]
    [Tooltip("이 데이터로 동시에 날 수 있는 수. 0이면 제한 없음. 연사 무기는 3~5.")]
    [SerializeField, Min(0)] private int _maxConcurrent = 4;
    [Tooltip("넘쳤을 때 가장 오래된 것을 끊고 새 것을 튼다. 끄면 새 것을 버린다.")]
    [SerializeField] private bool _stealOldest = true;
    [Tooltip("같은 데이터를 다시 틀기까지 최소 간격(초). 한 프레임에 여러 발이 동시에 맞을 때 겹침을 막는다.")]
    [SerializeField, Min(0f)] private float _minInterval = 0.02f;

    public float Volume => _volume;
    public AudioMixerGroup MixerGroup => _mixerGroup;
    public bool Loop => _loop;
    public float SpatialBlend => _spatialBlend;
    public float FullVolumeRange => Mathf.Min(_fullVolumeRange, _audibleRange);
    public float AudibleRange => _audibleRange;
    public int MaxConcurrent => _maxConcurrent;
    public bool StealOldest => _stealOldest;
    public float MinInterval => _minInterval;

    public bool HasClip => _clips != null && _clips.Length > 0;

    public AudioClip PickClip()
    {
        if (!HasClip) return null;

        return _clips.Length == 1 ? _clips[0] : _clips[Random.Range(0, _clips.Length)];
    }

    public float PickPitch() => _pitchMin >= _pitchMax ? _pitchMin : Random.Range(_pitchMin, _pitchMax);
}
