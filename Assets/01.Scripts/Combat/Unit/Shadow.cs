using UnityEngine;

public class Shadow : MonoBehaviour
{
    [SerializeField] private float _shadowSize = 1f;
    [SerializeField] private Transform _shadowTrm;

    public void SetShadowSize(float newSize)
    {
        _shadowSize = newSize;
        _shadowTrm.localScale = Vector3.one * _shadowSize;
    }
}