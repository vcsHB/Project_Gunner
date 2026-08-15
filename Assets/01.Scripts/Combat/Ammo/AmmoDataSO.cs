using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 탄약 한 종류. 인벤토리에 겹쳐서 쌓이는 평범한 아이템이다.
///
/// 성능 차이를 <see cref="WeaponStatDelta"/>로 표현하는 이유는 파츠·개량과 같은 길을 쓰기 위해서다.
/// 스탯 적용 경로가 둘로 갈리면 "파츠는 반영되는데 탄약은 안 되는" 버그가 반드시 난다.
/// (적용은 WeaponModController 한 곳에서만 한다)
///
/// 스탯으로 표현되지 않는 것 — 화상 부여, 폭발 연출, 예광 색 — 은 투사체 프리팹이 담당한다.
/// 그런 탄만 <see cref="ProjectilePool"/>을 채우면 되고, 순수 스탯 탄(철갑탄 등)은 비워두면 된다.
/// </summary>
[CreateAssetMenu(menuName = "SO/Item/AmmoData")]
public class AmmoDataSO : ItemDataSO
{
    [Header("Ammo Settings")]
    [Tooltip("이 탄이 들어가는 구경. 무기의 허용 구경 목록과 대조한다.")]
    [SerializeField] private AmmoCaliberType _caliber = AmmoCaliberType.None;

    [Tooltip("한 번 격발에 소모하는 탄약 개수. 보통 1이고, 한 방에 여러 발을 먹는 무기만 올린다.")]
    [SerializeField, Min(1)] private int _costPerShot = 1;

    [Header("Stats")]
    [Tooltip("이 탄을 물렸을 때 무기 스탯에 붙는 변화. 파츠와 완전히 같은 방식이다.")]
    [SerializeField] private List<WeaponStatDelta> _deltas = new();

    [Header("Projectile")]
    [Tooltip("이 탄이 만드는 투사체. None이면 무기의 기본 투사체를 쓴다. " +
             "상태이상·폭발·예광처럼 스탯으로 표현 안 되는 것은 여기 프리팹이 담당한다.")]
    [SerializeField] private PoolType _projectilePool = PoolType.None;

    public AmmoCaliberType Caliber => _caliber;
    public int CostPerShot => _costPerShot;
    public IReadOnlyList<WeaponStatDelta> Deltas => _deltas;
    public PoolType ProjectilePool => _projectilePool;

    /// <summary>탄약은 개체마다 다른 상태가 없다. 수백 발에 객체를 붙일 이유가 없다.</summary>
    public override bool RequiresInstance => false;

    /// <summary>Id로 찾는다. 등록되지 않았거나 탄약이 아닌 Id면 null.</summary>
    public static AmmoDataSO Find(uint itemId)
    {
        if (itemId == 0 || ItemDatabaseSO.Instance == null) return null;

        return ItemDatabaseSO.Instance.TryGet(itemId, out ItemDataSO data) ? data as AmmoDataSO : null;
    }

    public bool FitsIn(PlayerWeaponDataSO weapon)
        => weapon != null && weapon.AcceptsCaliber(_caliber);

    /// <summary>UI 툴팁에 그대로 쓴다. 넣을 수 있으면 빈 문자열.</summary>
    public string GetIncompatibleReason(PlayerWeaponDataSO weapon)
    {
        if (weapon == null) return "무기가 없습니다.";
        if (_caliber == AmmoCaliberType.None) return "구경이 지정되지 않은 탄약입니다.";
        if (!weapon.UsesAmmo) return $"{weapon.DisplayName}은 탄약을 쓰지 않습니다.";
        if (!weapon.AcceptsCaliber(_caliber)) return $"{weapon.DisplayName}에 맞지 않는 구경입니다.";

        return string.Empty;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // 탄약이 한 칸에 하나만 들어가면 인벤토리가 순식간에 찬다. 거의 항상 실수다.
        if (MaxStackCount <= 1)
            Debug.LogWarning($"[Ammo] {name}: MaxStackCount가 1입니다. 탄약은 겹치도록 올려주세요.", this);
    }
#endif
}
