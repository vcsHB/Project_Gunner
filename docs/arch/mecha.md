# 기체 — 파츠와 자원

[아키텍처 목차](../ARCHITECTURE.md)로 돌아가기. 구현 현황은 [STATUS.md](../STATUS.md)에 있습니다.

## 기체 파츠

기체는 **코어 / 좌팔 / 우팔 / 추진체** 네 슬롯입니다(`EquipSlotType`). 더 세분된 것(적재 용량, 전력량)은 슬롯을 늘리지 않고 **파츠의 하위 모듈로 내려갑니다.**

팔에 들어가는 것은 `PlayerWeaponDataSO`(또는 도구)이고, 코어·추진체는 `PartDataSO` 파생입니다. 무기를 `PartDataSO`로 묶지 않은 이유는 **스탯 체계가 다르기 때문**입니다 — 무기는 `WeaponStatus`를 따로 갖습니다.

### 장착이 스탯이 되는 지점

`EquipmentController`는 "어느 슬롯에 무엇이 끼워져 있는가"만 압니다. 그게 무슨 의미인지는 구독자가 정하고, **`MechaPartController`가 그 유일한 구독자**입니다.

```
EquipmentController.OnEquipChangedEvent
        │
        ▼
MechaPartController  ← PartDataSO.Deltas 를 AgentStatus modifier 로
```

**origin은 슬롯마다 다른 객체입니다**(`SlotOrigin`). `PartDataSO` 자신을 origin으로 쓰면 같은 파츠를 두 슬롯에 달았을 때 한쪽을 빼는 순간 양쪽이 같이 걷힙니다 — origin은 참조 동일성으로 비교되기 때문입니다.

교체든 해제든 **먼저 걷어내고 다시 겁니다.** 교체에서 제거를 빼먹으면 조용히 중첩됩니다.

**`Status<int>`에는 Percent를 걸 수 없습니다.** modifier 값이 int라서 0.2가 0으로 잘립니다(`StatusCombiner.CombineInt`). `MechaPartController`가 경고를 내고 건너뜁니다 — 조용히 틀리는 것보다 낫습니다.

**비주얼은 여기서 만들지 않습니다.** 스탯과 프리팹 생성을 한 클래스가 들면, 프리팹이 없는 파츠에서 스탯까지 같이 빠집니다. 아트가 안 나온 파츠로도 밸런스를 볼 수 있어야 합니다.

### 진입할 때 기체를 채우는 것

핫바가 없으므로 **`EquipmentController`가 비어 있으면 무장이 아예 생기지 않습니다.** `MechaLoadoutApplier`가 `MechaLoadoutSO`(구성표)를 읽어 채웁니다.

`AfterInitialize`가 아니라 **`Start`에서** 적용합니다. 컴포넌트 초기화 순서는 보장되지 않아서, `AfterInitialize`에서 장착하면 아직 구독하지 않은 컴포넌트가 장착 이벤트를 통째로 놓칩니다. (`MechaPartController`는 그래도 안전하도록 구독 직후 현재 상태를 한 번 훑습니다.)

**여기가 "새 판 시작" 지점입니다.** 월드 상태를 되돌릴 유일한 자리라 `ItemInstanceRegistry.Clear()`와 `GameRandom.Reseed()`를 같이 부릅니다. 안 부르면 플레이 모드를 들락거릴 때마다 이전 판의 인스턴스가 쌓입니다 — 정적이라 플레이 모드를 나가도 남습니다.

무기처럼 `RequiresInstance`인 아이템은 `ItemInstanceRegistry.CreateStack`으로 만듭니다. 그냥 `ItemStack`을 만들면 모딩·내구도가 붙을 자리가 없습니다.

## 기체 자원 — 전력과 연료

`AgentStatus`가 **최대값**을, `VitalStatus`가 **현재값**을 갖습니다. `Health`와 같은 구조입니다 — 스탯은 "얼마까지 찰 수 있나"고, 컴포넌트는 "지금 얼마인가"입니다.

체력은 여기 들어가지 않습니다. 체력만 방어력·저항 계산을 타기 때문입니다.

**둘의 성격이 다릅니다.**

| | 전력 | 연료 |
|---|---|---|
| 쓰는 것 | 발사, 실드 | 부스터, 이동 |
| 회복 | 시간당 (제너레이터) | **안 됨.** 보급 아이템만 |
| 바닥나면 | 임계치까지 차야 다시 쓸 수 있다 | 이동 페널티 + 지속 손상 |

**연료가 시간만으로 줄지 않는 이유** — 자동 감소를 두면 가만히 서 있어도 손해라 "아껴 쓴다"는 선택이 사라집니다. 반대로 전력이 안 차면 무장이 일회용이 됩니다. 그래서 하나는 차고 하나는 안 찹니다.

**무엇이 쓰는지는 `VitalStatus`가 정하지 않습니다.** `TryConsume(VitalType, amount)`를 부르는 쪽이 정합니다. 여기서 부스터를 알게 되면 발사·실드가 생길 때마다 이 파일을 고쳐야 합니다.

부스터는 **입력(`_sprintHeld`)과 실제 상태(`_sprintActive`)를 나눠** 둡니다. 연료가 바닥나면 누르고 있어도 켜지지 않습니다. 제자리에서는 소모하지 않습니다 — 서서 Shift를 누르고 있다고 연료가 닳으면 이상합니다.

전력은 바닥나면 **일정 비율까지 차야 다시 쓸 수 있습니다**(`_powerRecoverThreshold`). 안 그러면 한 방울씩 차며 딸꾹질합니다. **연료에는 이 잠금이 없습니다** — 연료가 0인 것은 "못 쓰는 상태"가 아니라 "없는 상태"라서, 보급하면 바로 써야 합니다.

연료가 0이면 **이동속도 페널티와 지속 손상이 둘 다** 들어갑니다. 페널티는 modifier라 켜고 끄는 순간에만 손대고, 손상은 `Health.ApplyDirectDamage`로 방어력을 무시합니다 — 장갑을 둘렀다고 연료가 덜 떨어지지는 않습니다. 제너레이터도 연료를 먹으므로 전력 회복도 같이 느려집니다.


## 실드

`ShieldStatus`가 현재값을, `AgentStatus`가 최대값·회복 대기·회복 속도(`MaxShield` / `ShieldRegenDelay` / `ShieldRegenRate`)를 듭니다. 전력·연료와 같은 구조입니다. **코어가 `Deltas`로 실드를 줍니다** — 기본 `MaxShield`가 0이라 코어가 없거나 컴포넌트가 없으면 실드 없는 유닛입니다.

```
Health.ApplyDamage
  └─ ShieldStatus.TryAbsorb(원래 피해)   ← 방어력 계산 전
        실드 > 0 → 실드만 깎고 끝 (넘친 피해는 버린다)
        실드 = 0 → 타이머만 초기화, 체력으로 진행
```

- **실드가 조금이라도 남아 있으면 그 타격은 거기서 끝납니다.** 넘친 피해가 체력으로 넘어가지 않습니다 (2026-11-15 결정).
- **실드는 방어력·저항을 타지 않습니다.** 장갑은 기체 껍데기 수치입니다.
- **피격 이벤트(`OnDamagedEvent`)는 실드에 막혀도 쏩니다.** 맞았다는 사실(흔들림·피격 표시)은 같습니다. 체력 변화(`OnHealthChangedEvent`)는 안 옵니다.
- `ApplyDirectDamage`(연료 고갈)는 실드를 거치지 않습니다.
- 맞을 때마다(실드가 0이어도) 대기 타이머가 처음부터. 대기가 끝나면 **전력을 쓰며** 찹니다(`_powerPerShield`). 전력이 모자라거나 잠기면 회복만 멈추고 타이머는 그대로 둡니다.
- 최대값이 바뀌면 비율을 유지합니다. 최대 0 → 양수(출격 시 코어 장착)는 가득 채웁니다.
- `Health.ResetHealth()`가 실드도 채웁니다. 풀 재사용·리스폰이 따로 부를 필요 없습니다.

**전력 회복과 엮입니다.** `VitalStatus`는 전력을 쓴 뒤 `_powerRegenDelay`(1초) 동안 안 찹니다. 실드가 차는 동안은 전력이 계속 줄기만 하므로, 실드 총량 × `_powerPerShield`가 최대 전력보다 크면 회복 도중 전력이 잠겨 딸꾹질합니다.

HUD는 `PartPlayerStatus._shieldGauge`(비우면 자식 `GaugeShield`를 찾음)입니다. **체력 게이지와 같은 자리에 겹쳐 있어서, 실드가 남아 있으면 실드만, 0이면(깨짐·실드 없는 기체) 체력만 보입니다.** 숨겨진 쪽도 값은 계속 갱신합니다.

**실드 원이 판정입니다** (2026-11-17). 플레이어의 `Shied`에 `CircleCollider2D`(트리거) + `HitBox` + `ShieldHitArea`. 실드가 남아 있는 동안만 콜라이더가 켜져서, 실드가 있으면 원(반지름 2)에 먼저 맞고 깨지면 안쪽 파츠 히트박스가 드러납니다. 트리거인 이유 — 캐스터는 트리거도 찾지만(`useTriggers`), 밀어내는 몸이 되면 안 됩니다. 실드가 있는 동안은 **맞는 범위가 커집니다** — 의도된 것입니다.

## 부위파괴

**코어 체력은 `Health` 그대로입니다.** 코어가 0이면 죽습니다(코어 파괴 = 사망). 팔·추진체만 `MechaPartHealth`가 따로 듭니다.

```
Health.ApplyDamage
  ├─ ShieldStatus.TryAbsorb          실드 > 0 → 끝
  ├─ 방어력·저항 계산
  ├─ MechaPartHealth.TryAbsorb(hitPart, 최종 피해)
  │     팔·추진체이고 살아 있으면 → 그 부위만 깎고 끝 (넘친 피해는 버린다)
  └─ 본체(코어) 체력
```

### 맞은 부위는 붙은 자리가 정한다

`DamageData.hitPart`는 `HitBox.Part`에서 옵니다. **`HitBox.Part`는 가장 가까운 상위 `MechaSocket`의 슬롯**입니다 — 같은 무기 프리팹이 왼팔에도 오른팔에도 붙기 때문에 프리팹에 적을 수 없습니다. 소켓 아래가 아니면(코어 자체, 실드 원, 메카가 아닌 유닛) `Core`입니다.

- 한 번의 훑기에 여러 부위가 걸리면 **먼저 만난 것**이 남습니다(`CasterBase.ResolveHits` — 배율이 같으면 앞의 것). 투사체의 `CircleCast` 결과는 거리순이라 앞에 있는 부위가 맞습니다
- **팔 히트박스는 팔이 만들어진 뒤에 생깁니다.** 조립기(`MechaAssembler`)가 콜라이더를 모은 다음이라, `HandController.Create`가 `TargetBase.RefreshColliders()`를 다시 부릅니다. 안 부르면 팔 콜라이더가 프리팹 레이어(Default)를 단 채로 남아 **아무것도 안 맞고 에러도 안 납니다**
- 무기 프리팹의 히트박스는 `Visual`의 `BoxCollider2D` + `HitBox`입니다 (`Part_Weapon_Scatter`, `PlayerWeapon_Scatter`, `PlayerWeapon_IronSpear`)

### 부서지면

| 부위 | 효과 |
|---|---|
| 팔 | 그 부위의 히트박스가 꺼진다(뒤의 코어가 대신 맞는다). 무기는 `HandActionBase.SetDisabled(true)` — 쏘지 않고, 빈 격발 소리도 없고, 차지·점사는 **쏘지 않고** 버린다(`Interrupt`) |
| 추진체 | 내려앉고(빠르면 미끄러진다), 활공·부스터 불가, 이동속도 -40%(`PlayerMoveController._brokenBoosterSpeed`). 이동은 `OnPartDestroyedEvent`를 듣는다 |
| 코어 | 사망 (`Health.OnDeadEvent`) |

막는 곳은 **쓰는 쪽**(`PlayerRangedWeapon.TryFire`)입니다. 누르는 쪽은 입력과 AI 둘이라, 거기서 막으면 한쪽을 빠뜨립니다.

### 부위 체력의 출처

`EquipableItemDataSO._partHealth`. 무기는 지금 전부 150입니다. **0이면 부서지지 않는 부위**라 맞은 피해가 코어로 갑니다. 코어는 이 값을 쓰지 않습니다(`AgentStatus.Health`).

**부위 체력은 아이템 내구도(`ItemInstance`)와 별개입니다.** 판마다, 그리고 장착이 바뀔 때마다 가득 찬 채로 시작합니다 — 같은 값이면 판이 끝나도 손상이 남아 매 판이 수리부터 시작합니다.

> 추진체는 아직 맞지 않습니다. 추진체 SO·비주얼 프리팹이 없어서 히트박스가 없습니다. 로직은 리플렉션으로 확인했습니다.

## 필드 수리 (R)

`MechaRepair` + `RepairKitDataSO`. R을 누르면 적재함 앞칸부터 키트를 하나 찾아 **쓰고**, `Duration`(4초)에 걸쳐 코어와 **각** 부위를 `Amount`(60)만큼 채웁니다.

- **맞으면 끊깁니다.** 실드에 막혀도 끊깁니다. 키트는 이미 썼으므로 돌아오지 않습니다 — 전투 중에 쓰는 물건이 아닙니다
- 부서진 부위는 최대치의 `_restoreRatio`(30%)까지 차면 되살아납니다(히트박스·무기 복구, 추진체 감속 해제)
- 고칠 것이 없거나 키트가 없으면 시작하지 않고 `OnRepairRejectedEvent`(이유 포함)를 쏩니다. 안내음·UI용
- 게이지는 목표값만 받습니다. 차오르는 것은 로직이고 UI는 끊긴 줄 모릅니다
- Id 대역 10000~10999 (`RepairKitDataSO`). 폐기된 소모품 대역 4000은 재사용하지 않습니다
- 테스트 로드아웃(`Loadout_Test`)에 3개 실었습니다
