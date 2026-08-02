using UnityEngine;

public class UIPanelBase : MonoBehaviour, IWindowPanel
{
    public CanvasGroup canvasGroup;

    protected virtual void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    } 

    public void Show()
    {
        
    }

    public void Hide()
    {
        
    }
}