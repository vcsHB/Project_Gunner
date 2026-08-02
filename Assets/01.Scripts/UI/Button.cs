using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class Button : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Button Setting")]
    [SerializeField] private float _hoverScale = 1.04f;
    [SerializeField] private float _hoverScaleUpDuration = 0.5f;
    public UnityEvent OnClick;


    private void Awake()
    {

    }

    public void OnPointerClick(PointerEventData eventData)
    {

    }

    public void OnPointerEnter(PointerEventData eventData)
    {

    }

    public void OnPointerExit(PointerEventData eventData)
    {

    }
}