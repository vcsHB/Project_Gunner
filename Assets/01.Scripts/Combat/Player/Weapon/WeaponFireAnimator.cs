using UnityEngine;

/// <summary>
/// 발사 애니메이션. 무기 프리팹 안에 둔다.
///
/// <b>Trigger가 아니라 Play로 직접 재생한다.</b>
/// Trigger + Transition은 전이 시간과 interrupt 설정에 따라 "쏜 순간"과 "애니메이션 시작"이
/// 어긋난다. 연사에서는 전이가 끝나기 전에 다음 발이 나가서 아예 건너뛰기도 한다.
/// <c>Play(hash, layer, 0f)</c>는 매번 0프레임부터 다시 돌아서 한 발에 한 번이 보장된다.
///
/// 무기가 직접 재생하지 않고 <see cref="PlayerRangedWeapon.OnFiredEvent"/>를 구독하는 이유 —
/// 전투 로직이 표현을 직접 돌리면 서버 권한 구조로 옮길 수 없다. (RULES 시뮬레이션과 표현 분리)
/// </summary>
[RequireComponent(typeof(Animator))]
public class WeaponFireAnimator : MonoBehaviour
{
    [SerializeField] private Animator _animator;

    [Tooltip("발사 때 재생할 상태 이름. Animator 컨트롤러의 상태와 같아야 한다.")]
    [SerializeField] private string _fireState = "Fire";

    [Tooltip("발사가 끝나면 돌아갈 상태. 비워두면 Animator 전이에 맡긴다.")]
    [SerializeField] private string _idleState = "Idle";

    [Tooltip("재생할 레이어.")]
    [SerializeField, Min(0)] private int _layer;

    [Tooltip("켜면 클립 길이만큼 지난 뒤 코드가 직접 Idle로 돌린다. " +
             "끄면 Animator의 Exit Time 전이에 맡긴다.")]
    [SerializeField] private bool _returnToIdle = true;

    private PlayerRangedWeapon _weapon;

    private int _fireHash;
    private int _idleHash;
    private float _fireLength;
    private float _returnAt = -1f;

    private void Awake()
    {
        if (_animator == null)
            _animator = GetComponent<Animator>();

        _fireHash = Animator.StringToHash(_fireState);
        _idleHash = string.IsNullOrEmpty(_idleState) ? 0 : Animator.StringToHash(_idleState);

        _fireLength = FindClipLength(_fireState);
    }

    private void OnEnable()
    {
        // 무기 본체는 부모에 있다. 프리팹 구조가 바뀌어도 따라가도록 위로 찾는다.
        _weapon = GetComponentInParent<PlayerRangedWeapon>();

        if (_weapon == null)
        {
            Debug.LogWarning($"[FireAnim] {name}의 부모에 PlayerRangedWeapon이 없습니다.", this);
            return;
        }

        _weapon.OnFiredEvent += HandleFired;
    }

    private void OnDisable()
    {
        if (_weapon == null) return;

        _weapon.OnFiredEvent -= HandleFired;
        _weapon = null;
    }

    private void HandleFired(float recoil)
    {
        if (_animator == null || !_animator.isActiveAndEnabled) return;

        // 0프레임부터 강제로 다시 시작한다. 연사에서 앞 발의 재생이 남아 있으면 안 된다.
        _animator.Play(_fireHash, _layer, 0f);

        _returnAt = _returnToIdle && _idleHash != 0 && _fireLength > 0f
            ? Time.time + _fireLength
            : -1f;
    }

    private void Update()
    {
        if (_returnAt < 0f || Time.time < _returnAt) return;

        _returnAt = -1f;

        if (_animator != null && _animator.isActiveAndEnabled)
            _animator.Play(_idleHash, _layer, 0f);
    }

    /// <summary>
    /// 상태 이름과 같은 이름의 클립을 찾는다. Play 직후 StateInfo를 읽으면
    /// 아직 전환되기 전이라 이전 상태의 길이가 나온다 — 그래서 미리 재둔다.
    /// </summary>
    private float FindClipLength(string stateName)
    {
        if (_animator == null || _animator.runtimeAnimatorController == null) return 0f;

        AnimationClip[] clips = _animator.runtimeAnimatorController.animationClips;

        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null && clips[i].name == stateName) return clips[i].length;
        }

        return 0f;
    }
}
