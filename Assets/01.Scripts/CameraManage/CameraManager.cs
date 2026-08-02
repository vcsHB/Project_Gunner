using UnityEngine;

public class CameraManager : Singleton<CameraManager>
{
    [SerializeField] private Camera _mainCamera;

    public Camera MainCamera => _mainCamera;

    private ICameraEffector[] _effectors;

    protected override void Awake()
    {
        base.Awake();

        if (_mainCamera == null)
            _mainCamera = GetComponentInChildren<Camera>();

        _effectors = GetComponentsInChildren<ICameraEffector>(true);
        for (int i = 0; i < _effectors.Length; i++)
            _effectors[i].Intialize();
    }

    public T GetEffector<T>() where T : class, ICameraEffector
    {
        for (int i = 0; i < _effectors.Length; i++)
        {
            if (_effectors[i] is T effector)
                return effector;
        }

        return null;
    }
}
