/// <summary>
/// "이 아이템을 지금 쓸 수 있나"를 판정하는 한 곳.
///
/// 슬롯 표시(_itemNotUseable), 사용 입력, 무기 발사가 <b>같은 답</b>을 내야 한다.
/// 각자 판단하면 "표시는 멀쩡한데 안 쓰이는" 상태가 반드시 생긴다.
///
/// <b>쿨타임은 여기서 보지 않는다.</b> 쿨타임은 잠깐 기다리면 풀리는 것이고,
/// 여기서 보는 것은 손을 쓰지 않으면 영영 안 풀리는 상태다. 표시 방식도 달라야 한다.
/// </summary>
public static class ItemUsability
{
    /// <summary>지금 쓸 수 없는 상태인지. 빈 칸은 false다(막힌 게 아니라 없는 것).</summary>
    public static bool IsBlocked(ItemStack stack, out string reason)
    {
        reason = string.Empty;
        if (stack.IsEmpty) return false;

        ItemInstance instance = stack.ResolveInstance();
        if (instance != null && instance.IsBroken)
        {
            reason = "내구도가 다 닳았습니다.";
            return true;
        }

        return false;
    }

    public static bool IsBlocked(ItemStack stack) => IsBlocked(stack, out _);
}
