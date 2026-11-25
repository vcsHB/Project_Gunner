using UnityEngine;

/// <summary>
/// 터지는 탄(미사일)의 폭발 연출. <see cref="Projectile.OnExplodedEvent"/>를 듣는다 — 판정은 탄이, 그리기는 여기서.
/// 탄은 터지는 즉시 풀로 돌아가므로 자식 파티클로는 못 그린다. 풀 이펙트(<see cref="EffectPlayer"/>)로 자리에 남긴다.
/// </summary>
[RequireComponent(typeof(Projectile))]
public class ProjectileExplodeFeedback : MonoBehaviour
{
    [SerializeField] private PoolType _effect = PoolType.None;
    [SerializeField] private SoundDataSO _sound;

    private Projectile _projectile;

    private void Awake() => _projectile = GetComponent<Projectile>();

    // 풀에서 꺼지고 켜질 때마다 붙였다 뗀다. 꺼진 채 이벤트를 들고 있으면 안 된다(RULES — 풀링 대상의 구독).
    private void OnEnable() => _projectile.OnExplodedEvent += HandleExploded;
    private void OnDisable() => _projectile.OnExplodedEvent -= HandleExploded;

    private void HandleExploded(Vector2 position)
    {
        EffectPlayer.Play(_effect, position, Vector2.up);
        SoundManager.Play(_sound, position);
    }
}
