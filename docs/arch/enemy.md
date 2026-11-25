# 적

[아키텍처 목차](../ARCHITECTURE.md)로 돌아가기. 구현 현황은 [STATUS.md](../STATUS.md)에 있습니다.

## 적

플레이어와 **같은 부품**을 쓴다. `Agent` 컴포지션, `Health`, `TargetBase`, `HitBox`,
캐스터, `Status<T>` — 적만 다른 길을 쓰면 같은 지형·상태이상·방어력에 서로 다르게 반응한다.

| 컴포넌트 | 하는 일 |
|---|---|
| `Enemy : Agent` | 데이터 주입과 죽음·드랍 |
| `EnemyMoveController` | Rigidbody2D 이동과 바라보기. **어디로 갈지는 안 정한다** |
| `EnemyAIController` | 상태 전환과 대상 선택 |
| `EnemyManager` | 스폰 |

### 머리와 다리를 나눈다

`EnemyAIController`는 **방향만 넘기고**, 실제 이동은 `EnemyMoveController`가 한다.
AI가 Rigidbody를 직접 만지면 둔화·기절·넉백이 걸릴 때마다 AI 쪽에도 같은 조건이 생긴다.
"못 움직이는 상태"의 판단은 이동 컴포넌트 한 곳에만 있다.

### 상태는 클래스다

`EnemyState` 파생이 상태 하나씩 맡는다(Idle / Chase / Attack). **MonoBehaviour가 아니다** —
상태가 컴포넌트면 적 하나에 컴포넌트가 상태 수만큼 붙는다.

지금은 셋뿐이라 `if`로도 되지만, 원거리 적과 보스가 들어오면 반드시 갈아엎게 된다.
상태를 나누는 비용은 지금이 가장 싸다.

**전환 경계마다 여유(hysteresis)를 둔다.** 쫓기 시작하는 거리(`DetectRange`)보다
놓아주는 거리(`LoseRange`)가 넓고, 사정권에 드는 거리보다 벗어나는 거리가 넓다.
같은 값이면 경계에서 상태가 매 프레임 뒤집히며 떨린다. (하체 회전과 같은 문제다)

### 공격은 선딜을 거친다

쿨타임만 두고 즉시 때리면 반응할 구간이 없어서 "붙으면 무조건 맞는" 적이 된다.
`AttackWindup` 동안 대상을 바라보며 기다리고, 그 시점의 자리에서 판정을 낸다 —
그 사이에 빠져나갔으면 안 맞는다. **회피가 성립해야 한다.**

판정은 `CasterBase`가 한다. 적이 직접 데미지를 계산하면 방어력·치명타·상태이상이
플레이어 무기와 다르게 걸린다.

### 거리는 프리팹에, 능력치는 데이터에

탐지·사정권은 `EnemyAIController`(프리팹)에, 체력·공격력·이동속도는 `UnitDataSO`에 있다.
앞은 몸집과 무기 길이가 정하는 값이라 비주얼과 같이 움직여야 하고,
뒤는 밸런스로 조정되는 값이라 아트를 건드리지 않고 바뀌어야 한다.

### 풀링하지 않는다

`Agent`와 `PoolableMono`가 둘 다 MonoBehaviour 직계라 `Enemy`가 양쪽을 상속할 수 없다.
`Instantiate`/`Destroy`로 가고, `Agent.OnDestroy`가 컴포넌트 `Dispose()`를 부르므로 상태가 새지 않는다.
스폰이 잦아지는 시점에 다시 본다. ([MILESTONE.md](../MILESTONE.md))

### 적도 메카다

기본 적은 플레이어와 **같은 조립 경로**를 쓴다 — `EquipmentController` + `MechaPartController` +
`MechaAssembler` + 무장. 적만 다른 길로 만들면 모듈·탄종·과열·방어력이 한쪽에만 적용된다.

```
UnitDataSO.LoadoutTable         슬롯별 후보 (코어 / 좌팔 / 우팔 / 추진체)
      │ Roll()  ← GameRandom.World
      ▼
MechaPartController.ApplyEntries
      ├──▶ EquipmentController    슬롯에 끼운다
      │        ├──▶ MechaAssembler   코어 프리팹 생성, 소켓 마련
      │        └──▶ HandController   소켓에 무장 생성
      └──▶ Inventory              적재품(탄약)
```

**구성은 매번 굴린다.** 같은 종류라도 다른 조합으로 나온다. 손으로 만든 로드아웃 여러 개를
두고 고르는 방식도 있지만, 그러면 파츠가 늘어날 때마다 조합을 사람이 적어줘야 한다.

**`MechaLoadoutSO`를 런타임에 만들지 않는다.** ScriptableObject는 참조가 끊겨도 바로
사라지지 않아서, 적이 스폰될 때마다 하나씩 만들면 조용히 쌓인다. 굴린 결과는 재사용 리스트에
담아 `ApplyEntries`로 넘긴다.

**메카가 아닌 적도 있다.** `LoadoutTable`을 비워두면 조립하지 않는다 — 아래 [단일 기체](#단일-기체).

### 단일 기체

조립되지 않는 적(터렛·드론·부표·보스). **1기체 1무기** — 팔(`HandController`)을 쓰지 않고 `UnitWeapon` 하나를 총구 소켓에 단다.

**플레이어 무기와 섞지 않는다.** 같은 것은 탄(`Projectile`, `ProjectileLaunchData`)뿐이다. 플레이어 무기는 개량·과열·탄종·내구도가 핵심이고,
적 무기는 "언제 쏘는지 읽히는가"가 핵심이다. 플레이어 무기를 꽂으면 탄 무한·내구도 예외가 플레이어 코드에 섞이고,
적만 쓸 무기가 아이템 DB(루팅)에 들어간다. (처음엔 그렇게 했다가 2026-11-24에 걷어냈다.)

| 빠졌던 것 | 어떻게 |
|---|---|
| 무기 | `UnitWeapon`(소켓에) + `UnitWeaponSO`(**아이템 아님** — Id·루팅·인벤토리 없음). SO에는 **한 발**(투사체·위력·탄속·사거리·관통·폭발·맞히는 대상·반응 시간)만 |
| 공격 방식 | **SO 파생 타입**이 정한다(`CreatePattern()` → 기체마다 진행 상태 하나). `UnitBurstWeaponSO` 연발 → 쉼(단발·기관포·산탄이 수치 차이) / `UnitPrecisionWeaponSO` 조준선 → 고정 → 한 발, 또는 `_shots` > 1이면 **고정된 채 조준선을 따라 긋는 연발**(드론 — 연발 동안 조준선이 남고 쉼에 꺼진다). 새 방식 = 파생 하나 |
| 총구 | `UnitWeapon._muzzle` 하나, 또는 `_muzzles`(좌우 발사관) — 한 발마다 번갈아 |
| 박자 | AI는 "대상 쪽을 보고 있고 맞힐 수 있나"만 넘긴다(`UnitWeapon.Tick`). 단계(`UnitWeaponPhase`)는 이벤트로 나가 연출이 예고를 그린다. **쉼은 시각으로 잰다** — 사정권 밖에서도 흘러야 경계를 들락거려 쉼 없이 쏘지 못한다 |
| 정밀 사격의 회피 | 고정(`Locked`) 동안 포탑이 돌지 않고(`EnemyAIController.IsAimLocked`) 사정권을 벗어나도 상태를 놓지 않는다 — 예고한 한 발은 제 시간에 나간다. 죽으면 `Stop(cancel: true)`로 안 나간다 |
| 맞히는 대상 | `UnitWeaponSO._hitTargets`(지상/공중/둘 다). 탄 판정에 걸리고(`ProjectileLaunchData.targetTypes` → `CasterBase.OverrideTargetTypes`, 풀에서 나올 때 되돌림) AI도 못 맞힐 대상엔 안 쏜다(`CanHitTarget`). 범위 표시도 이걸 본다 |
| 거리 | 사격 거리 = **무기 사거리**(AI `_fireRange` 무시). 탐지 거리는 사거리보다 좁아지지 않는다 |
| 다리 | `EnemyMoveController._locomotion` — 걷기(구렁 가장자리에서 멈춤) / 비행(넘음) / 제자리(몸은 안 움직이고 바라보기만 돈다) |
| 포탑 | `_visualRoot`를 포탑 머리로 두고 `_turnSpeed`를 느리게. 무기는 머리의 소켓에 단다 |
| 피할 틈 | `EnemyAIController._aimFollowsFacing` — 대상이 아니라 **포탑이 보는 쪽으로** 쏜다. 보는 쪽과 대상 사이가 `_fireAngleTolerance` 안일 때만 방아쇠를 당긴다. 메카는 끈다(몸이 덜 돌았어도 대상 쪽으로) |
| 잔해 | `PartBreakFeedback._bodyRoot` — 그 아래 스프라이트가 잔해가 된다. 그림자·실드 원은 뺀다 |

판정 분류(지상/공중)는 다리와 따로 `TargetBase._defaultTargetType`이 정한다. 비행 다리를 달고 지상 판정일 수도 있다.

**공격 범위 표시** — `UnitRangeIndicator`(기체 `Root/RangeIndicator`). 지상 = 바닥 실선 원 + 옅은 채움, 공중 = 활공 높이(`_airLift`, 플레이어 `_glideHeight`와 맞출 것)만큼 띄운 점선 원. 색도 분류별. 공격 상태면 진해진다. 메시는 시작 때 한 번 만든다.

테스트 터렛 — 제자리 다리, 머리 60°/s, 체력 600. 무기는 `08.SO/UnitWeapon/`.

| 프리팹 | 무기 | 대상 |
|---|---|---|
| `Enemy_Turret_Test` | 기관포(연발 5, 쉼 1.4초) | 지상 |
| `Enemy_Turret_Sniper_Test` | 정밀(노림 1.2 → 고정 0.45 → 35 피해) | 둘 다 |
| `Enemy_Turret_Flak_Test` | 산탄 5발 × 2(대공) | 공중 |
| `Enemy_Turret_Cannon` | 대포 — 정밀(노림 1.0 → 고정 0.4 → 40 피해, 사거리 11) | 둘 다 |
| `Enemy_Turret_Missile` | 미사일 — 좌우 발사관 번갈아 4연발 → 쉼 3.5초, [유도 미사일](#미사일--맞는-탄-2026-11-26) | 둘 다 |

대포·미사일 포탑은 사용자 아트(`Unit_Turret_1.png` 시트, 정면 위). 포탑 머리(`Head`) 아래 몸·포신·발사관 스프라이트, 총구는 포신 끝.

### 떠 있는 적 — 드론·부표 (2026-11-25)

| | 사격 드론 | 자폭 드론 | 부표 |
|---|---|---|---|
| 프리팹 | `Enemy_Drone_Gun` | `Enemy_Drone_Bomb` | `Enemy_Buoy` |
| 다리 · 고도 | 비행 · 0.8 | 비행 · 0.7 | 제자리 · 0.35 |
| 판정 · 편 | 공중 · 적 | 공중 · 적 | 공중 · **중립** |
| 공격 | 조준 연발(`UnitWeapon_Precision_DroneGun`, 지상+공중, 사거리 6.5) — **편대 → 산개 → 조준선 → 6연발 → 자리 옮김**(아래 [편대](#편대--드론-2026-11-26)) | 몸이 무기 — 0.9 안이면 0.6초 멈춰 깜빡이고 폭발(반경 1.6, 60, 플레이어·중립) | 누구든 0.75 안 → 즉시 폭발(반경 2, 80, **전부**) |
| 죽으면 | 추락 → 잔해(회로 기판 1 + 고철 1) | 추락 → 땅에서 폭발 → 잔해 | 그 자리에서 폭발(0.12초 뒤 — 연쇄가 한 프레임에 안 몰리게) |
| 무리 | 3~5 | 2~3 | 배치(맵) |

**고도는 이동이 갖고(`EnemyMoveController._altitude`), 띄워 그리는 건 `UnitHoverFeedback`** — 플레이어 활공(`PlayerMoveController.Altitude` / `GlideFeedback`)과 같은 나눔. 띄우는 대상에 히트박스·총구가 같이 있다(보이는 곳에서 맞고 쏜다). 그림자는 제자리.

**폭발은 `UnitDetonator` 하나.** 자식에 폭발 캐스터(`CircleCaster` + `DamageCastEffector`)를 두고, 터질 때 떼어 내 고정 위력으로 쏜다(`DelayedCast`) — 몸은 부서지는 프레임 끝에 사라지므로 붙어 있으면 늦게 터질 수 없다. AI는 이 캐스터를 근접 공격으로 집지 않는다.

**적의 탄은 적을 맞히지 않는다** — `UnitWeaponSO._hitFactions`(기본 플레이어·중립)가 탄에 실린다(`ProjectileLaunchData.hitFactions` → `CasterBase.OverrideHitFactions`). 떼로 도는 드론이 서로 쏘지 않게. 중립은 맞히므로 **드론의 탄이 부표를 터뜨린다** — 유인 플레이가 된다.

**간격 유지** — `_separationRadius` 안의 같은 다리 동료를 서로 민다. 길찾기가 아니다.

### 편대 — 드론 (2026-11-26)

`EnemySquad` — MonoBehaviour가 아닌 무리 하나. `EnemyManager.Spawn`이 무리를 찍을 때 프리팹이 편대를 쓰면(`EnemyAIController._formationSpacing` > 0) 묶는다. 씬에 직접 놓은 것은 편대가 없다.

| | 평시(Idle) | 목표를 보면 |
|---|---|---|
| 대장 = 살아 있는 첫 멤버 | 집(스폰 자리) 반경 `_patrolRadius` 안을 느리게(`_patrolSpeed`) 순찰, 지점에서 잠깐 쉼 | **경보**(`Alert`) — 쫓는 멤버가 매 틱 갱신, 1.5초 끊기면 풀림 |
| 나머지 | 대장 뒤 V자 자리(대장이 보는 쪽 기준). 대장 속도 + 거리 비례로 따라잡는다 | 경보 중이면 놓치는 거리 안에서 같이 쫓는다 |

자리 번호는 **살아 있는 순서** — 대장이 죽으면 다음이 대장, 빈자리는 당겨진다.

**공격 중 이동 `_attackMove`** — 서서(`Stand`) / 돌기(`Strafe`, 옛 사격 드론) / **옮기기(`Reposition`)**. 옮기기는 대상 둘레 `사거리 × _strafeDistance` 위의 각도 하나를 자리로 잡는다. 처음 각도 = 지금 있는 쪽 + 편대 자리 번호 × `_scatterStep`(산개). 자리에 닿아야 방아쇠를 당기고, 노리고 쏘는 동안은 선다(조준선이 흔들리면 못 피한다). **쉼이 시작되면** 각도를 35~80° 굴려(`GameRandom.Combat`) 다음 자리로 간다.

**자리 예약** — `AttackSpotRegistry`(대상별 표). 자리를 잡을 때 같은 대상을 노리는 다른 적(편대가 달라도)의 자리와 `_spotSpacing`(2.2) 이상 떨어진 각도를 원하는 쪽에서 가까운 것부터 찾는다. 공격 상태를 나가면 내놓는다. 이동 중 겹침은 간격 유지(`_separationRadius` 1.8)가 맡는다.

**탄 거리와 사거리는 따로** — `UnitWeaponSO._travelRange`(0이면 사거리). 사거리 끝에서 탄이 뚝 끊기지 않게 드론은 9 / 14.

### 미사일 — 맞는 탄 (2026-11-26)

`Missile_0`은 `Projectile` 그대로다. 두 가지가 붙었다 — 판정은 [combat-core](combat-core.md#agent-없이-맞는-것)에.

- **유도** — `Projectile._turnRate`(초당 110°), 0.2초 곧게 난 뒤 꺾는다. 대상은 쏜 쪽이 `ProjectileLaunchData.homingTarget`으로 실어 보낸다(`UnitWeapon` ← `IAimTargetProvider` ← AI의 대상). 꺾는 힘이 한정이라 옆으로 빠지면 피한다
- **맞는 몸** — 총에 맞으면(체력 6) 그 자리에서 터지고, 그 폭발은 **편을 가리지 않는다**. 쏜 포탑 근처에서 떨어뜨리면 포탑이 맞는다
- 직격 캐스터에는 피해 이펙터가 없다 — 닿으면 터지기만 하고 피해는 폭발이 준다(이중 피해 방지)

### 방아쇠는 AI가 민다

`HandController`가 만들고 붙이는 일을 하고, 파생이 **누가 당기는가**만 다르게 한다.

| | 당기는 쪽 |
|---|---|
| `PlayerHandController` | 입력 (좌클릭 = 왼팔, 우클릭 = 오른팔) |
| `EnemyHandController` | `EnemyAttackState`가 매 틱 `Tick(firing, dt)` |

**"쏴라"와 "방아쇠를 당겨라"는 다르다.** 연사는 잡고 있으면 되지만 **단발·점사는 눌렀다 떼야**
다음 발이 나가고, 차지는 떼는 순간 나간다. 잡고만 있으면 단발 무기는 한 발 쏘고 영영 멈춘다.
무기가 `HandActionBase.HoldsTrigger` / `TriggerHoldTime`으로 알려주고,
`EnemyHandController`가 그에 맞춰 누르고 뗀다.

**발사 속도는 AI가 재지 않는다.** 무기의 쿨타임과 과열이 정한다 — 같은 무기가 플레이어 손에서와
적 손에서 다른 속도로 나가면 안 된다.

`EnemyHandController`는 **스스로 Update를 돌지 않는다.** "쏘기로 정한 쪽"과 "방아쇠를 당기는 쪽"이
다른 타이밍에 돌면 한 프레임씩 어긋나고, 어디서 끊겼는지 밖에서 볼 수 없다.

### 겨누는 방향은 인터페이스로 묻는다

무기가 `PlayerAimController`를 직접 찾으면 **적은 무기를 쓸 수 없다.** 플레이어는 커서로,
적은 AI가 고른 대상으로 정하지만 무기 입장에서는 같은 질문이다 — `IAimDirectionProvider`.

겨누는 쪽이 아예 없으면(씬에 놓아둔 더미 무기) **무기가 보고 있는 쪽**(`transform.up`)으로 나간다.
팔에 달린 것은 아트 공간에 살아서 +Y가 앞이다.

### 무기와 탄의 구경이 맞는지 표에서 걸러낸다

구경이 안 맞는 무기를 달면 **조용히 한 발도 못 쏜다.** 조립도 되고 에러도 안 나고
적이 가만히 서서 방아쇠만 당긴다. `MechaLoadoutTableSO.HasAmmoFor`가 그걸 판정하고,
`OnValidate`가 표를 짤 때 경고한다.

