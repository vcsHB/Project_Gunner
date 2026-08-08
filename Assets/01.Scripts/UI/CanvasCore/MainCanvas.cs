using UnityEngine;

public class MainCanvas : MonoBehaviour
{
    public SubCanvas[] subCanvases;

    private void Awake()
    {
        for (int i = 0; i < subCanvases.Length; i++)
        {
            subCanvases[i].Initialize();
        }
        for (int i = 0; i < subCanvases.Length; i++)
        {
            subCanvases[i].LateInitialize();
        }
    }
}