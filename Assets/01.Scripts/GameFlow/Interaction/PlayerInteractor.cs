using System;
using UnityEngine;

/// <summary>
/// 플레이어 주변에서 상호작용할 대상 하나를 고르고, E를 누르면 쓴다. 플레이어 Agent 컴포넌트.
///
/// 고르는 기준은 <b>가장 가까운 하나</b>다. 겹쳐 있을 때 여러 개를 띄우면 무엇을 누를지 헷갈린다.
/// 표시(<see cref="PartInteractPrompt"/>)는 <see cref="OnFocusChangedEvent"/>와 <see cref="Current"/>를 읽기만 한다.
///
/// 죽었거나 기절했거나 <b>활공 중</b>이면 고르지 않는다 — 떠서 지나가며 뒤지는 건 안 된다. 내려서 뒤진다.
/// </summary>
public class PlayerInteractor : MonoBehaviour, IAgentComponent
{
    /// <summary>고른 대상이 바뀌었다. 없어지면 null.</summary>
    public event Action<IInteractable> OnFocusChangedEvent;

    [SerializeField, Min(0.1f)] private float _radius = 1.6f;

    private Player _player;
    private PlayerMoveController _move;

    public IInteractable Current { get; private set; }

    public float Radius => _radius;

    public void Initialize(Agent owner) => _player = owner as Player;

    public void AfterInitialize()
    {
        _move = _player.GetCompo<PlayerMoveController>();

        if (_player.Input != null)
            _player.Input.OnInteractEvent += HandleInteract;
    }

    public void Dispose()
    {
        if (_player != null && _player.Input != null)
            _player.Input.OnInteractEvent -= HandleInteract;
    }

    private void Update()
    {
        IInteractable next = CanSearch()
            ? InteractableRegistry.FindNearest(_player, transform.position, _radius)
            : null;

        if (ReferenceEquals(next, Current)) return;

        Current = next;
        OnFocusChangedEvent?.Invoke(Current);
    }

    private bool CanSearch()
    {
        if (_player == null || _player.IsDead) return false;
        if (_move != null && (!_move.CanMove || _move.IsGliding)) return false;

        return true;
    }

    private void HandleInteract()
    {
        if (Current == null || !Current.CanInteract(_player)) return;

        Current.Interact(_player);
    }

    /// <summary>열어 둔 대상이 아직 손 닿는 거리인가. 루팅 창이 멀어지면 닫을 때 쓴다(조금 여유를 둔다).</summary>
    public bool IsInReach(IInteractable target, float margin = 0.5f)
    {
        if (target == null || _player == null) return false;

        float reach = _radius + margin;
        return (target.InteractPosition - (Vector2)transform.position).sqrMagnitude <= reach * reach;
    }
}
