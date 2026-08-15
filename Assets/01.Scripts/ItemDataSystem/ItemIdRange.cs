using System;
using UnityEngine;

/// <summary>
/// Id 대역 하나. "이 타입의 아이템은 이 번호대를 쓴다"를 정의한다.
///
/// Id를 만든 순서대로 1, 2, 3으로 주면 <b>번호만 보고는 그게 무엇인지 알 수 없다.</b>
/// 대역을 나눠두면 3xxx는 탄약이라는 게 로그·세이브 파일·네트워크 패킷에서 바로 읽힌다.
///
/// 대역은 <b>SO 파생 타입</b>으로 나눈다. ItemCategoryType은 표시 전용이라 언제든 바뀌는데,
/// 한 번 부여된 Id는 바뀌지 않으므로 그걸 키로 쓰면 대역 밖에 남는 아이템이 생긴다.
///
/// 타입이 아직 없는 대역도 미리 예약해 둘 수 있다. 그때는 typeName을 비우고 label만 적는다.
/// </summary>
[Serializable]
public struct ItemIdRange
{
    [Tooltip("ItemDataSO 파생 타입의 클래스 이름. 하위 타입은 자기 설정이 없으면 이 대역을 물려받는다. " +
             "비워두면 예약 대역이 되어 아무것도 배정되지 않는다.")]
    public string typeName;

    [Tooltip("인스펙터에서 알아보기 위한 이름. 동작에는 영향이 없다.")]
    public string label;

    public uint start;

    [Tooltip("이 값을 포함한다.")]
    public uint end;

    [Tooltip("다음에 부여할 Id. 재사용을 막기 위한 커서다. 직접 고치면 Id가 겹칠 수 있다.")]
    public uint nextId;

    public ItemIdRange(string typeName, string label, uint start, uint end)
    {
        this.typeName = typeName;
        this.label = label;
        this.start = start;
        this.end = end;
        nextId = start;
    }

    public bool IsValid => start > 0 && end >= start;

    /// <summary>타입이 지정되지 않은, 자리만 잡아둔 대역인지.</summary>
    public bool IsReservation => string.IsNullOrEmpty(typeName);

    public bool Contains(uint id) => id >= start && id <= end;

    public uint Capacity => IsValid ? end - start + 1 : 0;

    /// <summary>커서가 대역을 넘어갔는지. 넘어가면 더 이상 배정할 수 없다.</summary>
    public bool IsFull => IsValid && Cursor > end;

    /// <summary>nextId가 0이거나 대역 앞쪽이면 시작값으로 본다.</summary>
    public uint Cursor => nextId < start ? start : nextId;

    public string DisplayName => string.IsNullOrEmpty(label) ? typeName : label;
}
