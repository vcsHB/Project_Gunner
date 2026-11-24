using UnityEngine;

/// <summary>
/// 떨어져 나간 부위 조각. 튕겨 나가며 돌다가 느려지고, 끝에 흐려져 사라진다.
/// 부위파괴는 드문 일이라 풀링하지 않는다 — 만들고 부순다. 잦아지면 풀로 옮길 것.
/// </summary>
public class PartDebris : MonoBehaviour
{
    private Vector2 _velocity;
    private float _spin;
    private float _lifetime;
    private float _age;
    private SpriteRenderer[] _renderers;
    private float[] _baseAlpha;

    // 마지막 이 비율 동안 흐려진다.
    private const float FadeRatio = 0.4f;
    // 초당 남는 속도 비율. 바닥에 끌리며 멈추는 느낌.
    private const float Drag = 3f;

    public void Launch(Vector2 velocity, float spin, float lifetime)
    {
        _velocity = velocity;
        _spin = spin;
        _lifetime = lifetime;

        _renderers = GetComponentsInChildren<SpriteRenderer>();
        _baseAlpha = new float[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
            _baseAlpha[i] = _renderers[i].color.a;
    }

    /// <summary>흐려져 사라지지 않게 한다. 적재함이 붙은 잔해 — 비울 때까지 남고, 비우면 적재함이 지운다.</summary>
    public void MakePersistent() => _lifetime = float.PositiveInfinity;

    private void Update()
    {
        float dt = Time.deltaTime;
        _age += dt;

        float damping = Mathf.Exp(-Drag * dt);
        _velocity *= damping;
        _spin *= damping;

        transform.position += (Vector3)(_velocity * dt);
        transform.Rotate(0f, 0f, _spin * dt);

        float fadeStart = _lifetime * (1f - FadeRatio);
        if (_age > fadeStart)
        {
            float alpha = 1f - Mathf.InverseLerp(fadeStart, _lifetime, _age);
            for (int i = 0; i < _renderers.Length; i++)
            {
                Color c = _renderers[i].color;
                c.a = _baseAlpha[i] * alpha;
                _renderers[i].color = c;
            }
        }

        if (_age >= _lifetime)
            Destroy(gameObject);
    }
}
