using UnityEngine;

/// <summary>
/// 파츠가 달릴 자리. 파츠 프리팹 <b>안에</b> 자식 오브젝트로 둔다.
///
/// 부모가 "슬롯 → Transform" 목록을 들고 있는 방식도 있지만, 그러면 소켓을 하나 옮길 때마다
/// 목록에서 찾아 다시 드래그해야 한다. 자리 쪽이 자기 이름을 들고 있으면 옮기기만 하면 된다.
/// 에디터 도구가 소켓을 찍어내기도 쉽다.
///
/// <b>소켓은 논리 공간에 둔다</b>(+X가 앞). 리소스가 위를 보고 그려진 보정은
/// 기체 루트가 하므로, 소켓 좌표까지 같이 돌아간다 — 아트 위에 찍은 그대로 맞는다.
/// </summary>
[DisallowMultipleComponent]
public class MechaSocket : MonoBehaviour
{
    [Tooltip("이 자리에 달리는 슬롯.")]
    [SerializeField] private EquipSlotType _slot = EquipSlotType.None;

    public EquipSlotType Slot => _slot;

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = _slot == EquipSlotType.None ? Color.red : new Color(0.4f, 0.8f, 1f);
        Gizmos.DrawWireSphere(transform.position, 0.08f);

        // 어느 쪽이 앞인지 보여야 파츠를 붙였을 때의 방향을 가늠할 수 있다.
        Gizmos.DrawLine(transform.position, transform.position + transform.right * 0.2f);
    }
#endif
}
