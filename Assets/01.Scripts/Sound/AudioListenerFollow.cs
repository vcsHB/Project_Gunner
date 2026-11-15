using UnityEngine;

/// <summary>
/// 소리를 듣는 귀. <b>카메라가 아니라 내 기체 위치</b>를 따라간다 —
/// 카메라는 조준점 쪽으로 치우치므로 거기서 들으면 좌우가 어긋난다.
///
/// 기체의 자식으로 두지 않는다. 기체는 돌기 때문에 귀도 같이 돌면 좌우가 뒤집힌다. 회전은 항상 고정이다.
/// 기체 바로 위(-Z)로 조금 띄운다 — 같은 평면이면 바로 옆의 소리(내 총구)가 한쪽 귀로 쏠린다.
///
/// 씬의 다른 <see cref="AudioListener"/>(메인 카메라)는 꺼야 한다. 둘이면 유니티가 경고한다.
/// </summary>
[RequireComponent(typeof(AudioListener))]
public class AudioListenerFollow : MonoBehaviour
{
    public static AudioListenerFollow Current { get; private set; }

    [Tooltip("비우면 시작할 때 씬의 Player를 찾는다. 멀티에서는 로컬 플레이어를 넣어준다.")]
    [SerializeField] private Transform _target;
    [Tooltip("기체 위로 띄우는 높이. 클수록 가까운 소리의 좌우 쏠림이 줄고, 너무 크면 전부 가운데로 들린다.")]
    [SerializeField, Min(0f)] private float _height = 4f;

    private void Awake()
    {
        Current = this;
        transform.rotation = Quaternion.identity;
    }

    private void Start()
    {
        if (_target != null) return;

        Player player = FindAnyObjectByType<Player>();
        if (player != null)
            SetTarget(player.transform);
    }

    public void SetTarget(Transform target)
    {
        _target = target;
        LateUpdate();
    }

    private void LateUpdate()
    {
        if (_target == null) return;

        Vector3 p = _target.position;
        transform.SetPositionAndRotation(new Vector3(p.x, p.y, -_height), Quaternion.identity);
    }

    private void OnDestroy()
    {
        if (Current == this) Current = null;
    }
}
