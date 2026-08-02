using UnityEngine;

/// <summary>
/// 씬에 하나만 존재하는 MonoBehaviour 매니저의 베이스.
/// 상속받은 쪽에서 Awake를 오버라이드 한다면 반드시 base.Awake()를 호출할 것.
/// </summary>
public abstract class Singleton<T> : MonoBehaviour where T : Singleton<T>
{
    private static T _instance;

    public static T Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindAnyObjectByType<T>();

            return _instance;
        }
    }

    public static bool HasInstance => _instance != null;

    [SerializeField] private bool _dontDestroyOnLoad = false;

    protected virtual void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this as T;

        if (_dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);
    }

    protected virtual void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }
}
