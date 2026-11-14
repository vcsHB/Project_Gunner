using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 파츠의 런타임 표현. 프리팹 루트에 붙는다.
///
/// 들고 있는 것은 <b>소켓과 비주얼</b>뿐이다. 스탯은 <see cref="PartDataSO"/>가 갖고,
/// 그 반영은 <see cref="MechaPartController"/>가 한다 — 프리팹이 없는 파츠도
/// 스탯은 적용되어야 아트 없이 밸런스를 볼 수 있다.
///
/// <b>회전 보정은 하지 않는다.</b> 기체 루트가 한 번만 한다(<see cref="FacingType"/>).
/// 여기서도 하면 두 번 걸려 180° 틀어진다. 표준을 안 따르는 리소스는
/// 이 프리팹 <b>안에서</b> 스프라이트 자식을 돌려 가둘 것.
/// </summary>
public class MechaPartView : MonoBehaviour
{
    [Tooltip("비워두면 자식에서 자동으로 모은다. 순서를 고정하고 싶을 때만 채운다.")]
    [SerializeField] private MechaSocket[] _sockets;

    [Tooltip("상체와 따로 도는 부분. 코어만 쓴다. 비우면 파츠 전체가 한 덩어리로 돈다.")]
    [SerializeField] private Transform _waistRoot;

    private Dictionary<EquipSlotType, Transform> _lookup;

    /// <summary>이 파츠를 만들어낸 데이터. 조립기가 채운다.</summary>
    public PartDataSO Data { get; private set; }

    /// <summary>
    /// 하체. 회전이 상체보다 늦게 따라올 부분이다. 없으면 null.
    ///
    /// 프리팹이 들고 있는 이유 — 어느 오브젝트가 다리인지는 <b>그 파츠만 안다.</b>
    /// 씬이나 컨트롤러가 들고 있으면 코어를 바꿀 때마다 다시 꽂아야 한다.
    /// </summary>
    public Transform WaistRoot => _waistRoot;

    private void Awake() => Build();

    public void Bind(PartDataSO data) => Data = data;

    private void Build()
    {
        if (_lookup != null) return;

        _lookup = new Dictionary<EquipSlotType, Transform>();

        MechaSocket[] sockets = _sockets != null && _sockets.Length > 0
            ? _sockets
            : GetComponentsInChildren<MechaSocket>(true);

        for (int i = 0; i < sockets.Length; i++)
        {
            MechaSocket socket = sockets[i];
            if (socket == null || socket.Slot == EquipSlotType.None) continue;

            // 같은 슬롯이 둘이면 뒤엣것이 조용히 버려진다. 실행해 보기 전엔 안 드러난다.
            if (_lookup.ContainsKey(socket.Slot))
            {
                Debug.LogWarning($"[MechaPartView] {name}에 {socket.Slot} 소켓이 둘 이상입니다.", socket);
                continue;
            }

            _lookup.Add(socket.Slot, socket.transform);
        }
    }

    /// <summary>이 슬롯이 달릴 자리. 없으면 null.</summary>
    public Transform GetSocket(EquipSlotType slot)
    {
        Build();

        return _lookup.TryGetValue(slot, out Transform socket) ? socket : null;
    }

    public bool HasSocket(EquipSlotType slot) => GetSocket(slot) != null;
}
