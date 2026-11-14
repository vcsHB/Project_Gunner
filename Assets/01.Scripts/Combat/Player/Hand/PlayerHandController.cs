using UnityEngine;

/// <summary>
/// 플레이어의 팔. 만들고 붙이는 일은 <see cref="HandController"/>가 하고,
/// 여기는 <b>입력을 슬롯별로 고정 배분</b>한다.
///
/// 좌클릭은 왼팔, 우클릭은 오른팔. 이 배분표는 여기에만 있다.
/// 구독하는 쪽이 각자 "내가 나설 차례인가"를 판단하면 조건이 흩어지고,
/// 결국 둘 다 반응하거나 아무도 반응하지 않는 상태가 생긴다.
///
/// 공급원은 <see cref="EquipmentController"/>다. 핫바는 없다 — 들고 들어간 것만 쓴다.
/// </summary>
public class PlayerHandController : HandController
{
    private Player _player;

    public override void Initialize(Agent owner)
    {
        base.Initialize(owner);

        _player = owner as Player;
    }

    public override void AfterInitialize()
    {
        base.AfterInitialize();

        if (_player != null && _player.Input != null)
        {
            _player.Input.OnPrimaryEvent += HandlePrimary;
            _player.Input.OnSecondaryEvent += HandleSecondary;
        }

        UIInputBlocker.OnBlockedChangedEvent += HandleInputBlocked;
    }

    public override void Dispose()
    {
        if (_player != null && _player.Input != null)
        {
            _player.Input.OnPrimaryEvent -= HandlePrimary;
            _player.Input.OnSecondaryEvent -= HandleSecondary;
        }

        UIInputBlocker.OnBlockedChangedEvent -= HandleInputBlocked;

        base.Dispose();
    }

    #region 입력 배분

    private void HandlePrimary(bool pressed) => Route(EquipSlotType.ArmLeft, pressed);

    private void HandleSecondary(bool pressed) => Route(EquipSlotType.ArmRight, pressed);

    private void Route(EquipSlotType slot, bool pressed)
    {
        HandActionBase hand = Get(slot);
        if (hand == null) return;

        // 인벤토리를 열어둔 채로 누른 것은 무시한다.
        // 떼는 것은 막지 않는다 — 막힌 동안 뗀 것을 무시하면 계속 눌린 상태로 남는다.
        if (pressed && UIInputBlocker.IsBlocked) return;

        if (pressed) hand.OnUsePressed();
        else hand.OnUseReleased();
    }

    /// <summary>
    /// 막히기 시작하면 누르고 있던 것을 놓아준다.
    /// 안 그러면 연사가 인벤토리를 연 채로 계속 돌고, 차지가 걸린 채로 남는다.
    /// </summary>
    private void HandleInputBlocked(bool blocked)
    {
        if (!blocked) return;

        ReleaseAll();
    }

    #endregion
}
