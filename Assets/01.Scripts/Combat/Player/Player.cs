using UnityEngine;

public class Player : Agent
{
    [SerializeField] private PlayerInput _input;

    public PlayerInput Input => _input;

    protected override void Awake()
    {
        base.Awake();

        if (_input == null)
            Debug.LogError("[Player] PlayerInput이 지정되지 않았습니다.", this);
    }
}
