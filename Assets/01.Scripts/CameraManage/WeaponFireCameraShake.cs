using UnityEngine;

/// <summary>
/// 무기가 나갈 때마다 화면을 흔든다.
///
/// <b>무기가 직접 흔들지 않는다.</b> 전투 로직이 연출을 돌리기 시작하면 서버 권한 구조로
/// 옮길 수 없다(RULES 시뮬레이션과 표현 분리). 여기는 <see cref="PlayerRangedWeapon.OnFiredEvent"/>를
/// 듣기만 하는 구독자다.
///
/// <b>무기 프리팹이 아니라 카메라 쪽에 붙인다.</b> 프리팹마다 붙이면 새 무기를 만들 때마다
/// 빠뜨릴 수 있고, 빠뜨려도 아무 에러가 안 난다 — 그냥 그 무기만 조용히 안 흔들린다.
/// 좌우 두 팔을 여기서 한꺼번에 본다.
///
/// 흔들림 세기는 <see cref="WeaponStatType.Recoil"/>을 따른다. 고정값을 쓰면 모듈·탄종으로
/// 반동이 달라져도 화면은 똑같이 흔들린다.
/// </summary>
public class WeaponFireCameraShake : MonoBehaviour
{
    [Tooltip("반동 1에 해당하는 흔들림 세기.")]
    [SerializeField, Min(0f)] private float _powerPerRecoil = 0.09f;

    [Tooltip("아무리 반동이 세도 이보다 더 흔들리지는 않는다. 연사 중에 화면을 못 보게 되면 안 된다.")]
    [SerializeField, Min(0f)] private float _maxPower = 0.28f;

    [Tooltip("한 발의 흔들림이 가라앉는 시간. 연사 간격보다 짧아야 발마다 끊겨 보인다.")]
    [SerializeField, Min(0.01f)] private float _duration = 0.08f;

    private PlayerHandController _hands;
    private CameraShakeEffector _shake;

    // 팔 슬롯과 1:1. 무기가 갈릴 때 정확히 그 자리만 바꿔 끼운다.
    private static readonly EquipSlotType[] Slots =
    {
        EquipSlotType.ArmLeft,
        EquipSlotType.ArmRight,
    };

    private readonly PlayerRangedWeapon[] _weapons = new PlayerRangedWeapon[Slots.Length];

    // HUD와 같은 방식. Agent의 컴포넌트 초기화가 Awake에서 끝나므로 Start에서 붙는다.
    private void Start()
    {
        if (_hands == null)
            Bind(FindAnyObjectByType<Player>());
    }

    public void Bind(Player player)
    {
        Unbind();

        if (player == null) return;

        _shake = CameraManager.Instance != null
            ? CameraManager.Instance.GetEffector<CameraShakeEffector>()
            : null;

        if (_shake == null)
        {
            Debug.LogWarning("[FireShake] CameraShakeEffector를 찾지 못했습니다. 흔들림이 없습니다.", this);
            return;
        }

        _hands = player.GetCompo<PlayerHandController>();
        if (_hands == null) return;

        _hands.OnHandChangedEvent += HandleHandChanged;

        for (int i = 0; i < Slots.Length; i++)
            Subscribe(i, _hands.Get(Slots[i]));
    }

    public void Unbind()
    {
        if (_hands != null)
        {
            _hands.OnHandChangedEvent -= HandleHandChanged;
            _hands = null;
        }

        for (int i = 0; i < _weapons.Length; i++)
            Subscribe(i, null);

        _shake = null;
    }

    private void OnDestroy() => Unbind();

    private void HandleHandChanged(EquipSlotType slot, HandActionBase hand)
    {
        int index = IndexOf(slot);
        if (index < 0) return;

        Subscribe(index, hand);
    }

    private static int IndexOf(EquipSlotType slot)
    {
        for (int i = 0; i < Slots.Length; i++)
        {
            if (Slots[i] == slot) return i;
        }

        return -1;
    }

    /// <summary>그 자리의 구독을 통째로 갈아끼운다. 근접·도구는 null이 되어 조용히 빠진다.</summary>
    private void Subscribe(int index, HandActionBase hand)
    {
        if (_weapons[index] != null)
            _weapons[index].OnFiredEvent -= HandleFired;

        _weapons[index] = hand as PlayerRangedWeapon;

        if (_weapons[index] != null)
            _weapons[index].OnFiredEvent += HandleFired;
    }

    /// <summary>
    /// 좌우가 동시에 쏘면 <see cref="CameraShakeEffector"/>가 센 쪽을 남긴다.
    /// 더하면 양손 연사에서 화면이 걷잡을 수 없이 흔들린다.
    /// </summary>
    private void HandleFired(float recoil)
    {
        if (_shake == null) return;

        float power = Mathf.Min(_powerPerRecoil * Mathf.Max(0f, recoil), _maxPower);
        if (power <= 0f) return;

        _shake.Shake(power, _duration);
    }
}
