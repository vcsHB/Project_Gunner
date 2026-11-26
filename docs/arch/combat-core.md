# 전투 기반 — Agent, 판정, 스탯, 데미지, 상태이상, 풀, 편

[아키텍처 목차](../ARCHITECTURE.md)로 돌아가기. 구현 현황은 [STATUS.md](../STATUS.md)에 있습니다.

## Agent 컴포지션

전투 주체는 `Agent`이고, 기능은 상속이 아니라 **컴포넌트**로 붙입니다.

```csharp
public class Health : MonoBehaviour, IAgentComponent
```

`Agent.Awake`가 자식의 `IAgentComponent`를 전부 모아 `Initialize(Agent)` → `AfterInitialize()` 순으로 부르고, `OnDestroy`에서 `Dispose()`를 전파합니다.

**컴포넌트끼리 참조가 필요하면 `AfterInitialize`에서** `owner.GetCompo<T>()`로 잡습니다. `Initialize` 시점엔 아직 다 모이지 않았을 수 있습니다.

`Agent` 없이도 동작해야 하는 컴포넌트(폭발통, 파괴 가능한 엄폐물)는 `Awake`에서 부모에 `Agent`가 없을 때만 스스로 초기화합니다. `Health`, `TargetBase`가 이 패턴입니다.

### 의존은 선언하고 강제하지 않는다

컴포넌트가 무엇을 필요로 하는지는 `[RequireAgentComponent(typeof(T))]`로 적습니다. 없어도 동작은 하지만 기능이 꺼지는 것은 `optional: true`로 구분합니다.

**유니티의 `[RequireComponent]`를 쓰지 않습니다.** 그건 **같은 GameObject만** 봅니다. `Agent`는 자식까지 훑어서 모으므로, 역할별로 자식에 나눠두면 `RequireComponent`가 루트에 중복으로 다시 붙여버립니다.

그래서 강제하지 않고 **검증만** 합니다 — `Agent` 인스펙터가 계층 전체에서 찾아보고 빠진 것을 짚어줍니다.

### 컴포넌트가 많아지는 문제

컴포지션은 조각 수를 늘립니다. 그 자체는 비용이 아니지만 **인스펙터 스크롤과 "무엇이 붙어 있나"는 실제 비용**입니다. 두 가지로 풉니다.

**합칠 수 있는 것은 합칩니다.** 기준은 셋입니다 — ① 항상 같이 붙고 ② 같은 데이터를 공유하고 ③ 하나만 필요한 유닛이 안 생기면 합칩니다. 하나라도 어긋나면 나중에 떼어내게 됩니다. 인스펙터는 `[Serializable]` 중첩 클래스로 블록을 나눠 그대로 유지합니다.

> 합친 예 — `InventoryController`(보관 + 줍기), `MechaPartController`(진입 시 채우기 + 스탯 반영).
> 합치지 않은 예 — `MechaRotationController`는 표현이라 회전만 필요한 유닛이 생깁니다.

**`Agent` 인스펙터가 지도를 그립니다.** 무엇이 어디에 있는지, 의존이 빠지지 않았는지, 같은 타입이 둘 이상 붙지 않았는지(= `TryAdd`에서 조용히 버려지는 것)를 한 화면에 보여줍니다.

## 타게팅과 전투 판정

책임이 네 갈래입니다. 섞지 마세요.

| | 담당 |
|---|---|
| `TargetBase` | **맞기 전.** 타게팅 단위(동일성 기준), 분류, 레이어 동기화, 소실 알림, 조준점 |
| `Health` / `IDamageable` | **맞은 뒤.** HP, 피해 계산, 사망 |
| `HitBox` | 콜라이더 → 소속 대상 직접 참조 + 부위 배율 |
| `CasterBase` | 대상 탐색, 필터링, **중복 제거**, 이펙터 분배 |

### 왜 TargetBase가 따로 있나

한 유닛이 히트박스를 여러 개 갖게 되면 `OverlapCircle`이 같은 적의 머리·몸통·다리를 한 번에 돌려줍니다. 이걸 접어주는 **동일성 기준**이 필요하고, 그게 `TargetBase`입니다. 폭발 한 방에 데미지가 세 번 들어가는 걸 막습니다.

`CasterBase.Cast()`가 콜라이더 목록을 `CastHit` 목록으로 접고, 생존 확인·자기 자신 제외도 여기서 끝냅니다. `ICastEffector` 구현체는 그런 검사를 다시 하지 않습니다.

### TargetType과 레이어

`TargetType`은 `[Flags]`입니다. 유닛은 하나만, 캐스터는 조합해서 씁니다.

물리 레이어 대응은 `TargetLayerTableSO`(`Resources/TargetLayerTable.asset`) 한 곳에만 있습니다. **캐스터가 LayerMask를 직접 들고 있으면 안 됩니다** — "이 캐스터의 TargetType"과 "이 캐스터의 LayerMask"가 따로 놀아서 어긋납니다.

지상↔공중 전환 시 물리 캐스터는 매번 새로 질의하므로 자동으로 맞습니다. 참조를 캐시한 쪽(유도탄, AI 어그로, 락온 UI)만 `OnTargetTypeChangedEvent` / `OnTargetLostEvent`를 구독해 다시 판단하면 됩니다.

### Agent 없이 맞는 것

`TargetBase`와 `Health`는 Agent가 없으면 `Awake`에서 스스로 초기화한다. 미사일(`Missile_0`, 풀)이 첫 사용자다(2026-11-26).

- 몸(`TargetBase` + `Health` + 자식 `HitBox`)을 탄 루트에 둔다. 레이어는 `TargetBase`가 분류(공중)대로 입힌다
- 풀에서 다시 나올 때 `Health.ResetHealth()` + `ResetTargetType()`(`Projectile.Launch`). 사라질 때 `NotifyLost`
- 탄 자신의 직격 캐스터가 첫 프레임 제자리 질의로 **제 몸을 집는다** — `ProjectileCaster.IgnoredTarget`으로 뺀다(편 설정이 "전부"여도)
- 떨어진 미사일의 폭발은 주인을 비우고(`Owner = null`) 편을 "전부"로 덮는다 — 쏜 쪽도 맞는다

**누구 편인지는 여기서 못 가립니다.** 플레이어와 적이 둘 다 지상 유닛이라 같은 레이어입니다. 그건 [편 가르기](#편-가르기-faction)가 따로 봅니다.

## 스탯

`Status<T>`는 기본값 + modifier 목록이고 dirty 캐싱합니다.

**modifier는 origin 객체를 키로** 걸고 뗍니다. 버프를 건 주체가 사라질 때 `RemoveModifier(origin)`으로 정확히 그것만 걷어낼 수 있어야 합니다. origin은 참조 동일성으로 비교하므로 **박싱된 enum 같은 값 타입을 쓰면 안 됩니다.**

계산식:

```
(기본값 + 덧셈 전부) * (1 + 비율 전부)
```

덧셈을 다 적용한 뒤 비율을 곱하므로 **적용 순서가 결과를 바꾸지 않습니다.** 모듈 끼운 순서에 따라 최종 수치가 달라지면 재현 안 되는 밸런싱 버그가 됩니다.

타입별 합산 규칙은 `StatusCombiner`에 있습니다 (float/int 합산, bool OR). 닫힌 제네릭 타입마다 델리게이트를 한 번만 해석해서 박싱이 없습니다.

## 데미지

```
방어력은 깡 차감, 저항은 % 감소
final = max(damage - defense, floor) * (1 - clamp01(resistance))
```

`floor`는 `min(damage, MinDamage)`입니다. 방어력이 아무리 높아도 최소 피해는 들어가되, 원래 데미지가 그보다 작으면 깡 차감이 오히려 데미지를 늘리는 일은 없습니다.

치명타는 `DamageHandler.CalculateDamage`에서 굴립니다. `criticalRate`가 1을 넘으면 치명타 확정 + 초과분이 극대화 확률.

### 피격 정보 — 어디서, 어느 방향으로

`DamageData`는 연출이 쓸 정보도 실어 나릅니다. 채우는 곳은 `DamageCastEffector` 하나입니다.

| 필드 | 뜻 | 누가 정하나 |
|---|---|---|
| `direction` | 공격이 나아가던 방향(공격자 → 대상) | `CasterBase.GetHitDirection`. 투사체는 **비행 방향**, 근접·폭발은 캐스터 중심 → 대상 |
| `hitPoint` / `hasHitPoint` | 대상 콜라이더 표면에서 공격이 온 쪽의 점 | `Collider.ClosestPoint(CasterBase.HitOrigin)`. 투사체의 기준점은 **이번 프레임에 훑기 시작한 자리** |
| `blockedByShield` | 실드가 받아냈다 | `Health`가 피격 이벤트 직전에 |
| `hitPart` | 맞은 부위(팔·추진체·코어) | `HitBox.Part` — 히트박스가 붙은 소켓의 슬롯. 히트박스가 없으면 `Core` ([mecha](mecha.md#부위파괴)) |

투사체의 기준점을 캐스터의 현재 위치로 잡으면 안 됩니다 — 이동이 끝난 뒤라 탄이 이미 대상 중심을 지나쳐 있어서 방향이 뒤집힙니다.

지속 피해(`DamageOverTimeCondition`)는 `hasHitPoint`가 false입니다. 피격 연출은 이걸 보고 건너뜁니다.

`AgentStatus.IsResist`(bool)는 **상태이상 면역**이고, 피해 감소는 `DamageResistance`(float)입니다. 이름이 비슷하니 헷갈리지 마세요.

## 상태이상

`ConditionType` enum + `ConditionFactory`로 생성하고, 강도는 **타입별 상수 × Level**입니다.

같은 타입이 재적용되면 지속시간은 갱신하고, **레벨이 더 높으면** `OnEnd()` → 레벨 교체 → `OnStart()`로 다시 겁니다. modifier 계열(Weakness 등)이 중첩되지 않게 하기 위해서입니다.

구현 형태가 세 가지입니다 — 도트(`DamageOverTimeCondition`), 스탯 modifier(`WeaknessCondition`), 플래그만(`StunCondition`).

## 오브젝트 풀

`PoolListSO`가 전체 카탈로그, `PoolPresetSO`가 씬/세션별로 미리 만들 것입니다. `PoolManager`는 프리셋을 여러 개 받으므로 `Common + Stage1` 식으로 조합합니다.

`PoolItem.Id`가 생성된 `PoolType` enum 값과 1:1이고 재사용되지 않으므로, 이름을 바꾸거나 순서를 바꿔도 저장된 참조가 안 깨집니다. `PoolManager`는 시작할 때 `(PoolType)Id`가 실제 enum에 있는지 이름까지 대조합니다.

**`PoolType.cs`는 자동 생성 파일입니다.** 직접 고치지 말고 `Tools > Pool Manager`의 Generate Enum을 쓰세요.

`Get<T>()`는 그 타입이 풀 하나에만 있을 때만 됩니다. 이펙트(`ParticleEffect`)처럼 여러 풀이 같은 컴포넌트를 쓰면 `Get(PoolType)`으로 꺼냅니다 — 등록 때는 조용하고, `Get<T>()`를 실제로 부르면 에러가 납니다.

`IPoolable`만으로는 `SelfObject`를 구현할 수 없어(인터페이스에 필드가 없음) `PoolableMono` 추상 클래스가 짝으로 있습니다. 풀 대상은 `PoolableMono`를 상속하세요.

## 편 가르기 (Faction)

`TargetType`(지상/공중)과 **축이 다르다.** 그쪽은 "어떻게 생겼나"고 이쪽은 "누구를 때려도 되나"다.
한 enum에 섞으면 "공중에 뜬 플레이어"를 표현할 수 없다.

레이어로는 못 나눈다 — 플레이어와 적이 **둘 다 지상 유닛이라 같은 레이어**다.

- 유닛은 `TargetBase._faction`에 하나만 갖는다
- 캐스터는 `_hitFactions`에 때릴 편을 조합한다 (기본값은 전부)

**같은 편이라고 자동으로 빠지지 않는다.** 이 게임은 PVPVE라 플레이어끼리도 싸운다.
자기 자신만 자동으로 제외되고(`CasterBase.Owner`), 나머지는 캐스터가 명시한다.
적의 근접 공격은 `Player + Neutral`만 켠다 — 전부 켜두면 적끼리 서로 때려 죽는다.

**탄은 쏜 쪽이 편을 정한다** (2026-11-25). 탄 프리팹(`Bullet_0`)은 플레이어와 적이 같이 써서 프리팹 값은 "전부"다. 단일 기체 무기는 `UnitWeaponSO._hitFactions`를 `ProjectileLaunchData.hitFactions`로 실어 보내고 `CasterBase.OverrideHitFactions`가 덮는다(풀에서 다시 나오면 `ResetHitFactions`). 팔에 플레이어 무기를 단 메카 적도 실어 보낸다(2026-11-26) — `PlayerRangedWeapon`이 주인 편이 적이면 "적 제외". 플레이어 손에서는 지정하지 않는다(PVP).

