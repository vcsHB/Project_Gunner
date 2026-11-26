using System.Collections.Generic;

/// <summary>
/// <b>판 밖 상태</b> — 판이 끝나도, 씬이 바뀌어도 남는 플레이어 한 명의 것. 조종권(<see cref="PlayerPilot.Profile"/>)이 든다.
///
/// | | 무엇 |
/// |---|---|
/// | <see cref="Storage"/> | 창고. 들고 나온 것·자원(재료 아이템)이 쌓인다. 자원은 따로 세지 않는다 — 재료 아이템이 곧 자원이다 |
/// | <see cref="Hangar"/> | 세워 둔 기체들(격납고). 조립해 두고 나중에 탄다 |
/// | <see cref="Sortie"/> | 씬을 건너는 동안의 탄 기체. 떠나는 씬에서 적고 도착한 씬에서 다시 세운다(씬 분리 단계) |
///
/// 손상·개량은 아이템 인스턴스에 있다. 이 안의 스택이 가리키는 인스턴스는 새 판에 지우지 않는다(<see cref="CollectInstanceIds"/>).
/// 저장은 M7 — 지금은 게임을 끄면 사라진다. 모양은 그대로 저장할 수 있게 둔다(스택 + 인스턴스).
/// 플레이어마다 하나다. 싱글턴에 두지 않는다(RULES).
/// </summary>
public class PlayerProfile
{
    /// <summary>창고 칸 수. 확장은 나중에 정한다(2026-11-27).</summary>
    public const int StorageCapacity = 60;

    public Inventory Storage { get; } = new(StorageCapacity);

    public List<MechaRecord> Hangar { get; } = new();

    /// <summary>씬을 건너는 중인 탄 기체. 없으면 null.</summary>
    public MechaRecord Sortie { get; set; }

    /// <summary>이 프로필이 붙잡고 있는 아이템 인스턴스. 새 판에 레지스트리를 비울 때 남긴다.</summary>
    public void CollectInstanceIds(ICollection<uint> into)
    {
        for (int i = 0; i < Storage.Capacity; i++)
        {
            ItemStack stack = Storage[i];
            if (!stack.IsEmpty && stack.instanceId != 0) into.Add(stack.instanceId);
        }

        for (int i = 0; i < Hangar.Count; i++)
            Hangar[i]?.CollectInstanceIds(into);

        Sortie?.CollectInstanceIds(into);
    }
}
