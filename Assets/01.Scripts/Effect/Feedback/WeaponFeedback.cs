using UnityEngine;

/// <summary>
/// 무기의 발사·빈 격발 연출. <b>무기 프리팹에 붙인다</b> — 적도 같은 무기를 쓰므로 적·플레이어 구분이 없다.
/// 무기는 이 컴포넌트를 모른다. 여기가 <see cref="PlayerRangedWeapon"/>의 이벤트를 듣는다 (RULES 시뮬레이션과 표현 분리).
///
/// 총구 화염은 총구를 <b>따라다니지만 자식으로 붙지 않는다</b>. 방향은 탄이 나간 방향(<see cref="PlayerRangedWeapon.FireDirection"/>)이다.
///
/// 카메라 흔들림은 여기가 아니다 — 내 화면만 흔들려야 하므로 카메라 쪽(<see cref="WeaponFireCameraShake"/>)에 있다.
/// </summary>
public class WeaponFeedback : MonoBehaviour
{
    [Header("Fire")]
    [SerializeField] private PoolType _fireEffect = PoolType.None;
    [SerializeField] private SoundDataSO _fireSound;

    [Header("Dry Fire")]
    [SerializeField] private SoundDataSO _dryFireSound;

    private PlayerRangedWeapon _weapon;

    private void OnEnable()
    {
        _weapon = GetComponentInParent<PlayerRangedWeapon>();
        if (_weapon == null)
        {
            Debug.LogWarning($"[WeaponFeedback] {name}의 부모에 PlayerRangedWeapon이 없습니다.", this);
            return;
        }

        _weapon.OnFiredEvent += HandleFired;
        _weapon.OnDryFireEvent += HandleDryFire;
    }

    private void OnDisable()
    {
        if (_weapon == null) return;

        _weapon.OnFiredEvent -= HandleFired;
        _weapon.OnDryFireEvent -= HandleDryFire;
        _weapon = null;
    }

    private void HandleFired(float recoil)
    {
        Transform muzzle = _weapon.Muzzle;

        EffectPlayer.Play(_fireEffect, muzzle.position, _weapon.FireDirection, muzzle);
        SoundManager.Play(_fireSound, muzzle.position);
    }

    private void HandleDryFire()
    {
        SoundManager.Play(_dryFireSound, _weapon.Muzzle.position);
    }
}
