using UnityEngine;

/// <summary>
/// 적의 팔. 만들고 붙이는 일은 <see cref="HandController"/>가 하고,
/// 여기는 <b>AI 대신 방아쇠를 다룬다.</b>
///
/// 입력을 받지 않는 것이 플레이어와의 유일한 차이다. 그래서 적 기체도
/// 플레이어와 똑같이 무기를 달고 똑같은 발사·과열·탄약 경로를 쓴다 —
/// 적만 다른 길로 쏘면 모듈·탄종·과열이 한쪽에만 적용된다.
///
/// <b>"쏴라"와 "방아쇠를 당겨라"는 다르다.</b> 연사는 잡고 있으면 되지만 단발·점사는
/// 눌렀다 떼야 다음 발이 나가고, 차지는 떼는 순간 나간다. 무기마다 다른 이 차이를
/// 여기서 흡수한다 — AI는 "쏠까 말까"만 정한다.
///
/// <b>발사 속도는 재지 않는다.</b> 그건 무기의 쿨타임과 과열이 정한다.
/// 여기서 또 재면 같은 무기가 플레이어 손에서와 적 손에서 다른 속도로 나간다.
///
/// <b>스스로 Update를 돌지 않는다.</b> AI가 자기 틱에서 <see cref="Tick"/>을 민다 —
/// "쏘기로 정한 쪽"과 "방아쇠를 당기는 쪽"이 다른 타이밍에 돌면 한 프레임씩 어긋나고,
/// 어디서 끊겼는지 밖에서 볼 수 없다.
/// </summary>
public class EnemyHandController : HandController
{
    [Tooltip("떼고 나서 다시 누르기까지의 간격(초). 발사 속도가 아니라 '손가락 속도'다 — " +
             "실제 간격은 무기 쿨타임이 정하므로 짧게 둬도 된다.")]
    [SerializeField, Min(0f)] private float _repressDelay = 0.05f;

    // 손마다 따로 돈다. 좌우 무기가 다른 방식일 수 있다(한쪽은 연사, 한쪽은 차지).
    private bool[] _pressed;
    private float[] _timers;

    public override void Initialize(Agent owner)
    {
        base.Initialize(owner);

        _pressed = new bool[MountCount];
        _timers = new float[MountCount];
    }

    /// <summary>무장이 하나라도 달려 있는가. 없으면 AI가 근접 캐스터로 돌아간다.</summary>
    public bool HasAnyHand
    {
        get
        {
            for (int i = 0; i < MountCount; i++)
            {
                if (HandAt(i) != null) return true;
            }

            return false;
        }
    }

    /// <summary>
    /// AI가 매 틱 부른다. <paramref name="firing"/>이 false면 들고 있던 것을 놓는다.
    /// </summary>
    public void Tick(bool firing, float deltaTime)
    {
        if (_pressed == null) return;

        if (!firing)
        {
            ReleaseAllTriggers();
            return;
        }

        for (int i = 0; i < MountCount; i++)
            TickHand(i, deltaTime);
    }

    /// <summary>
    /// 즉시 놓는다. 죽거나 기절했을 때. 놓지 않으면 시체가 계속 쏜다 —
    /// 연사 무기는 트리거를 들고 있는 동안 스스로 발사를 이어간다.
    /// </summary>
    public void StopFiring() => ReleaseAllTriggers();

    private void TickHand(int index, float deltaTime)
    {
        HandActionBase hand = HandAt(index);

        if (hand == null)
        {
            // 무기가 떨어져 나갔다. 눌린 기록만 남으면 다시 달 때 눌린 상태로 시작한다.
            _pressed[index] = false;
            return;
        }

        if (!_pressed[index])
        {
            _timers[index] -= deltaTime;
            if (_timers[index] > 0f) return;

            hand.OnUsePressed();

            _pressed[index] = true;
            _timers[index] = hand.TriggerHoldTime;

            return;
        }

        // 잡고 있어야 하는 무기는 떼지 않는다. 떼면 연사가 발마다 끊긴다.
        if (hand.HoldsTrigger) return;

        _timers[index] -= deltaTime;
        if (_timers[index] > 0f) return;

        hand.OnUseReleased();

        _pressed[index] = false;
        _timers[index] = _repressDelay;
    }

    private void ReleaseAllTriggers()
    {
        if (_pressed == null) return;

        bool anyPressed = false;

        for (int i = 0; i < MountCount; i++)
        {
            anyPressed |= _pressed[i];

            _pressed[i] = false;
            _timers[i] = 0f;
        }

        // 누른 적이 없으면 뗄 것도 없다. 매 프레임 OnUseReleased를 부르면
        // 차지 무기가 0% 차지로 계속 격발된다.
        if (anyPressed)
            ReleaseAll();
    }
}
