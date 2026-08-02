using UnityEngine;

public class PlayerInput : ScriptableObject
{
    private Controls _controls;

    private void OnEnable()
    {
        _controls = new();
        _controls.Enable();
    }
}