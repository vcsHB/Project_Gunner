using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 활공(Space) 상태 표시. 지금 뜰 수 있는지, 왜 못 뜨는지를 한 줄로 보여준다.
///
/// 우선순위 — 추진체 파손 &gt; 활공 중(착지 불가 지역 위면 경고) &gt; 연료 없음 &gt; 착륙 잠금(남은 시간 막대) &gt; 준비됨.
/// 상태는 이동(<see cref="PlayerMoveController"/>)에서 매 프레임 읽는다. 값 몇 개를 보는 것뿐이라 이벤트를 따로 파지 않았다.
/// </summary>
public class PartGlideStatus : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _label;
    [Tooltip("착륙 잠금의 남은 시간. 잠금 중에만 찬다(1 → 0).")]
    [SerializeField] private Image _lockFill;
    [Tooltip("상태 색을 입힐 아이콘·배경. 비워도 된다.")]
    [SerializeField] private Graphic _tint;

    [Header("문구")]
    [SerializeField] private string _readyText = "SPACE 활공";
    [SerializeField] private string _glidingText = "활공 중";
    [SerializeField] private string _lockedText = "착륙";
    [SerializeField] private string _noFuelText = "연료 없음";
    [SerializeField] private string _brokenText = "추진체 파손";
    [SerializeField] private string _noLandingText = "착륙 불가 지역";

    [Header("색")]
    [SerializeField] private Color _readyColor = new(1f, 1f, 1f, 0.7f);
    [SerializeField] private Color _glidingColor = new(0.45f, 0.9f, 1f, 1f);
    [SerializeField] private Color _lockedColor = new(0.7f, 0.7f, 0.7f, 1f);
    [SerializeField] private Color _blockedColor = new(1f, 0.45f, 0.35f, 1f);

    private enum State
    {
        None,
        Ready,
        Gliding,
        Locked,
        NoFuel,
        Broken,
        NoLanding,
    }

    private PlayerMoveController _move;
    private State _state;

    // HUD와 같은 방식. Agent의 컴포넌트 초기화가 Awake에서 끝나므로 Start에서 붙는다.
    private void Start()
    {
        if (_move == null)
            Bind(FindAnyObjectByType<Player>());
    }

    public void Bind(Player player)
    {
        _move = player != null ? player.GetCompo<PlayerMoveController>() : null;
        _state = State.None;
    }

    private void Update()
    {
        if (_move == null) return;

        State next = Evaluate();
        if (next != _state)
        {
            _state = next;
            ApplyState(next);
        }

        if (_lockFill != null)
        {
            _lockFill.fillAmount = next == State.Locked && _move.GlideLockDuration > 0f
                ? _move.GlideLockRemaining / _move.GlideLockDuration
                : 0f;
        }
    }

    private State Evaluate()
    {
        if (_move.IsBoosterBroken) return State.Broken;
        if (_move.IsGliding) return _move.IsOverNoLandingZone ? State.NoLanding : State.Gliding;
        if (_move.IsOutOfFuel) return State.NoFuel;
        if (_move.GlideLockRemaining > 0f) return State.Locked;

        return State.Ready;
    }

    private void ApplyState(State state)
    {
        (string text, Color color) = state switch
        {
            State.Gliding => (_glidingText, _glidingColor),
            State.Locked => (_lockedText, _lockedColor),
            State.NoFuel => (_noFuelText, _blockedColor),
            State.Broken => (_brokenText, _blockedColor),
            State.NoLanding => (_noLandingText, _blockedColor),
            _ => (_readyText, _readyColor),
        };

        if (_label != null)
        {
            _label.text = text;
            _label.color = color;
        }

        if (_tint != null)
            _tint.color = color;
    }
}
