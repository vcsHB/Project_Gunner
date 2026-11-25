using UnityEngine;

/// <summary>
/// 떼어 낸 캐스터를 잠시 뒤 한 번 터뜨리고 지운다. 죽으며 터지는 것(자폭 드론·부표)이 쓴다.
///
/// 떼어 내는 이유 — 몸(<see cref="Enemy"/>)은 부서지는 프레임 끝에 사라진다. 캐스터가 몸에 붙어 있으면 늦게 터질 수 없다.
/// 늦게 터지는 이유 — 부표 연쇄. 같은 프레임에 이어 터지면 콜스택이 연쇄 길이만큼 쌓이고, 눈에는 한 번에 다 터진 것으로 보인다.
/// </summary>
public class DelayedCast : MonoBehaviour
{
    private CasterBase _caster;
    private float _timeLeft;

    /// <summary><paramref name="caster"/>를 부모에서 떼고 <paramref name="delay"/>초 뒤에 터뜨린다.</summary>
    public static void Run(CasterBase caster, float delay)
    {
        if (caster == null) return;

        caster.transform.SetParent(null, true);

        DelayedCast runner = caster.gameObject.AddComponent<DelayedCast>();
        runner._caster = caster;
        runner._timeLeft = delay;

        if (delay <= 0f) runner.Fire();
    }

    private void Update()
    {
        _timeLeft -= Time.deltaTime;
        if (_timeLeft <= 0f) Fire();
    }

    private void Fire()
    {
        if (_caster != null) _caster.Cast();

        _caster = null;
        enabled = false;
        Destroy(gameObject);
    }
}
