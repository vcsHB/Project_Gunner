# 개발 현황

최종 갱신: 2026-11-15 (적 메카 + 랜덤 로드아웃). 계획은 [MILESTONE.md](MILESTONE.md).

범례 — ✅ 동작함 / 🟡 뼈대만 / ⬜ 미착수 / 🔄 전환 예정 / ❌ 폐기 예정

**🔄와 ❌는 기획이 바뀌어서 붙은 표시입니다.** 코드는 아직 멀쩡히 동작하지만 메카 구조로 가면서 바뀌거나 사라집니다. 무엇으로 바뀌는지는 [PIVOT.md](PIVOT.md)에 있습니다.

## 코어

| 시스템 | 상태 | 비고 |
|---|---|---|
| Singleton | ✅ | `Singleton<T>` 제네릭 |
| ObjectPool | ✅ | 카탈로그(`PoolListSO`) + 씬별 프리셋(`PoolPresetSO`) |
| Pool Manager 창 | ✅ | `Tools > Pool Manager`. 탭 분류, `PoolType` 코드 생성 |
| LayerAttribute | ✅ | `int`를 레이어 드롭다운으로 |
| 시드 난수 | ✅ | `IRandomSource` / `GameRandom.Combat`·`World` |
| 게임 시간 | ✅ | `GameTime` — `timeScale`·`AudioListener.pause`를 바꾸는 유일한 곳. 멈춤은 요청자 집합(하나라도 남으면 멈춤). 멀티는 `SetPauseAllowed(false)` → 메뉴만 뜨고 시간은 흐름 |
| 하이어라키 유틸 | ✅ | 휠클릭 활성 토글, 계층선, 호버 시 컴포넌트 아이콘 + RaycastTarget |
| 에셋 하이라이트 | ✅ | 프로젝트 창에서 `Ctrl+/`로 색칠. 팔레트는 `Tools > Asset Highlight Settings` |

**풀 사용처** — 투사체가 첫 사용처입니다. 프리팹을 만들어 `Tools > Pool Manager`에 등록해야 실제로 발사됩니다.

## 전투

| 시스템 | 상태 | 비고 |
|---|---|---|
| Agent 컴포지션 | ✅ | `IAgentComponent` + `GetCompo<T>` |
| Status / AgentStatus | ✅ | Additive + Percent modifier |
| 데미지 계산 | ✅ | 방어력 깡 차감 → 저항 % 감소 |
| Health | ✅ | 피격/사망 이벤트, 무적, 풀링용 리셋 |
| 상태이상 | ✅ | Burn / Poison / Weakness / Stun |
| 타게팅 (`TargetBase`, `HitBox`) | ✅ | 레이어 동기화, 소실 알림, 조준점 |
| Caster / CastEffector | ✅ | 중복 제거, 부위 배율, `CastPower`로 위력 주입 |
| Projectile | ✅ | 경로 스윕, 관통, 사거리 소멸, 폭발. **유도**(`_turnRate`, 발사자가 `homingTarget`을 실어 보냄), **맞는 몸**(`_body` — 루트 `TargetBase`+`Health`, 총에 맞으면 그 자리에서 편 가리지 않고 폭발). 2026-11-26 — 직격 위력이 `ResetTrail`에 지워지던 버그 수정(그동안 직격은 발사자 스탯으로 맞았다) |
| ProjectileCaster | ✅ | 직전 위치→현재 위치 CircleCast. 터널링·중복타 방지 |
| `Enemy` | ✅ | 데이터 주입·사망·드랍 |
| `EnemyMoveController` | ✅ | Rigidbody2D 이동 + 진행 방향 바라보기. 다리(`EnemyLocomotion`) 걷기 / 비행 / 제자리. 제자리도 바라보기는 돈다(포탑) |
| `EnemyAIController` | ✅ | 상태 머신 Idle / Chase / Attack. 무장이 있으면 사격, 없으면 선딜 근접. 공격 중 이동 `_attackMove` 서서 / 돌기 / **옮기기**(자리 잡고 노리고 쏘고, 쉬는 동안 다른 자리로) |
| `EnemyManager` | ✅ | 플레이어 주위 고리에 스폰. 최대 수·주기 |
| 편 가르기 (`FactionType`) | ✅ | 캐스터가 때릴 편을 고른다. 적끼리 오사 방지 |
| 적 메카 조립 | ✅ | 플레이어와 같은 경로. `MechaLoadoutTableSO`가 매번 굴린다 |
| `HandController` 분리 | ✅ | 만들고 붙이는 일은 공용, 방아쇠만 플레이어/적이 다르다 |
| 단일 기체 | ✅ | 조립 없는 적. **1기체 1무기** `UnitWeapon` + `UnitWeaponSO`(아이템 아님, 플레이어 무기와 분리). 공격 방식은 파생 SO — 연발(`UnitBurstWeaponSO`: 단발·기관포·산탄) / 정밀(`UnitPrecisionWeaponSO`: 조준선 → 고정 → 한 발 또는 **조준선을 따라 긋는 연발** `_shots`). 총구 여럿이면 번갈아(`UnitWeapon._muzzles`). 맞히는 대상(지상/공중/둘 다)은 무기가 정하고 탄 판정에 걸린다(`ProjectileLaunchData.targetTypes`). 몸을 따라 쏘기(`_aimFollowsFacing` + 허용각), 잔해(`PartBreakFeedback._bodyRoot`) ([arch/enemy](arch/enemy.md#단일-기체)) |
| 터렛 공격 범위 | ✅ | `UnitRangeIndicator` — 지상 = 실선 원 + 옅은 채움, 공중 = 활공 높이만큼 띄운 점선 원(색도 다름). 반경 = 무기 사거리. 공격 중이면 진해짐. 조준선 `UnitWeaponFeedback` |
| 떠 있는 적 | ✅ | `EnemyMoveController._altitude`(로직) + `UnitHoverFeedback`(몸 띄움·흔들림·그림자). 떠 있다 죽으면 추락(`IDeathCrash` — 그때 속도로 미끄러지며 고도를 잃고 땅에서 부서짐). `_crashOnDeath` 끄면 그 자리에서(부표) |
| 드론 | 🟡 | `Enemy_Drone_Gun`(사격 — 2026-11-26 교체: 아트 `Unit_Drone_1`, **편대**로 순찰 → 목표를 보면 산개 → 붉은 조준선 0.6초 + 고정 0.2초 → 조준선 따라 6연발 → 조준선 끄고 1초 쉬는 동안 자리 옮김, 사거리 9·탄 14, 속도 6, 탐지 18, 무리 3~5. 공격 자리는 같은 대상을 노리는 적끼리 2.2 이상 벌려 예약 `AttackSpotRegistry`) / `Enemy_Drone_Bomb`(자폭 — 돌진, 0.9 안이면 0.6초 멈춰 깜빡임 후 폭발 60, 격추되면 추락 지점에서. **아트는 터렛 스프라이트 축소·색칠**). 둘 다 `EnemyManager` 스폰 목록에 |
| 편대 (`EnemySquad`) | ✅ | 무리 스폰 때 프리팹이 편대를 쓰면(`_formationSpacing` > 0) 묶는다. 대장(살아 있는 첫 멤버) 순찰, 나머지 V자 대열. 하나가 보면 경보 공유. 산개 각도는 자리 번호로. 소대의 바탕 |
| 미사일 | ✅ | `Missile_0`(풀) — 유도(초당 110°), 체력 6, 맞히면 폭발 1.1. 총으로 떨어뜨리면 그 자리에서 **모든 편**을 맞히며 터진다. 폭발 연출 `ProjectileExplodeFeedback` + `FX_Explosion_Small`(FX_DefaultHit 복제 확대, 임시) |
| 부표(기뢰) | 🟡 | `Enemy_Buoy` — AI 없음, 제자리 떠 있음. 플레이어·적이 닿거나 맞으면 폭발(반경 2, 80, **모든 편**). 0.12초 뒤 이어 터져 연쇄. 중립 편. **배치는 맵 작업**(스폰 목록에 없음) |
| 폭발 (`UnitDetonator`) | ✅ | 자식 폭발 캐스터를 떼어(`DelayedCast`) 고정 위력으로 터뜨림. 불붙는 조건(반경·편·퓨즈·멈춤)과 죽을 때 터짐. 스스로 터질 땐 `Health.Kill(allowCrash: false)` |
| 무리 스폰 | ✅ | `UnitDataSO._groupSize` — 한 자리 근처(`_groupSpread`)에 여럿. 소대 편성은 별도(MILESTONE) |
| 테스트 터렛 | 🟡 | `Enemy_Turret_Test`(기관포, 지상) / `_Sniper_Test`(정밀, 둘 다) / `_Flak_Test`(산탄, 공중만). 무기 `08.SO/UnitWeapon/`. 아트·이펙트·소리 임시, 셋 다 `Item_Unit_TurretTest`(9001) 데이터 공유 |
| 대포·미사일 포탑 | 🟡 | (2026-11-26, 아트 있음) `Enemy_Turret_Cannon`(정밀 한 발 40, 사거리 11, 둘 다, 9005) / `Enemy_Turret_Missile`(좌우 발사관 번갈아 4연발 → 쉼 3.5초, 유도 미사일, 사거리 15, 둘 다, 9006). 씬에 놓임(더미 `TestTurret_1/2` 자리). 소리 없음 |
| `IAimDirectionProvider` | ✅ | 무기가 겨누는 쪽을 인터페이스로 묻는다 |
| `UnitDataSO` | ✅ | 유닛 데이터 + 전리품 테이블. Id 대역 9000~9999 |
| Shadow | 🟡 | 크기 설정만 |

캐스터는 `CircleCaster`와 `ProjectileCaster` 두 종류입니다.

**투사체는 지형을 뚫습니다.** 맵/블럭 시스템이 없어서 벽 충돌 판정이 아직 없습니다.

## 플레이어

| 시스템 | 상태 | 비고 |
|---|---|---|
| PlayerInput | ✅ | 입력 리더 SO. `PlayerInput.asset` 존재 |
| 조종권 / 기체 분리 | 🟡 | `PlayerPilot`(Managers 아래)이 입력을 들고 기체(`Player`)에 탄다. `Board`/`Disembark`, 표시 쪽은 `LocalMecha`·`OnLocalMechaChangedEvent`. 인벤토리 화면은 아직 갈아타기를 안 따라간다 ([input](arch/input.md#조종권--playerpilot--기체--player-2026-11-27)) |
| 이동 | ✅ | Rigidbody2D, 질주는 Percent modifier, 기절 시 정지. **착륙 / 활공 두 모드** |
| 활공 / 착륙 (Space) | 🟡 | `PlayerMoveController`. 활공 = `TargetType.Air` + 속도↑ + 연료 상시 소모 + **몸체가 0.6 뜬다**(`Altitude`, `GlideFeedback`, 카메라 줌아웃). 고속 착륙 미끄러짐 + `SkidCameraShake`, 착륙 후 1초 잠금. **수치 미조정** |
| 조준 | ✅ | 마우스 절대 조준 + `AimRange` 제한 + 반동 오프셋 |
| 주/보조 입력 | ✅ | 좌=Primary, 우=Secondary. **좌=좌무기 / 우=우무기로 고정 배분됨** |
| 정조준(총 + 우클릭) | ❌ | 기능 폐기. `SetAiming`과 카메라 줌 기계장치는 남김 |
| 소모품 사용(우클릭) | ❌ | 폐기. 수리는 R(필드 수리)로 |
| 투척무기 | ❌ | 한 버튼 구조와 충돌. 되살리려면 눌러-떼기로 재작성 |
| 핫바 (인벤토리 앞 7칸) | ❌ | 무장은 장비 슬롯이 정한다 |
| 무기 들기 | ✅ | 핫바가 아니라 `Equipment[ArmLeft/ArmRight]`를 따라감 |
| 전력 / 연료 | ✅ | `VitalStatus`. 전력은 차고 연료는 안 찬다. 고갈 시 페널티 + 손상 |
| 실드 | ✅ | `ShieldStatus`. 피해를 먼저 받고 그 타격은 실드에서 끝. 20초 대기 후 전력을 쓰며 회복. 코어 Deltas로 수치. 실드 원(`ShieldHitArea`)이 판정 — 실드가 있는 동안만 켜짐. 실드 프리팹 `UnitShield`(플레이어·`Enemy_Test` 공용), 0이면 그림도 꺼짐. 플레이어 1000 / 적 400·700. 깨짐·복귀 연출 `ShieldFeedback`(프리팹) + `ShieldBreakCameraShake` |
| 부위파괴 | 🟡 | `MechaPartHealth`. 팔·추진체 체력(`EquipableItemDataSO._partHealth`), 코어 = `Health`. 맞은 부위는 히트박스가 붙은 소켓으로. 부서지면 어두운 조각으로 떨어져 나가고(`PartBreakFeedback`) 화면 크게 흔들림. 팔 파괴 = 무기 정지, 추진체 파괴 = 활공·부스터 불가 + 40% 감속. 추진체 `Item_Booster_Booster1`(Unit_Booster_1_0, 체력 120) + 히트박스. HUD 실루엣 `PartMechaSilhouette` |
| 필드 수리 (R) | ✅ | `MechaRepair` + `RepairKitDataSO`(Id 10000~). 키트 1개 → 4초간 코어·부위 각 60. 맞으면 끊김(키트는 소모). 부서진 부위는 30%까지 차면 복구 |
| 소모품 사용 | ❌ | `PlayerItemUseController`. 쿨타임 UI는 과열 게이지로 재활용 |
| 포복(Crawl) | ❌ | 폐기. 액션이 `Glide`로 이름이 바뀌었다 |
| 재장전 | ❌ | 제거됨. R 키는 필드 수리로 쓴다 |
| 상호작용(Interact/Use) | 🟡 | 입력만 있고 대상 탐색 없음 |
| 장비 → 스탯 연결 | ✅ | `MechaPartController`. 슬롯별 origin, 교체 시 먼저 걷어냄 |
| 기체 회전 | ✅ | `MechaRotationController`. 상체가 먼저, 하체가 뒤따라옴 |
| 출격 로드아웃 | ✅ | `MechaLoadoutSO`(장착 + 적재) + `MechaPartController`. 새 판 리셋도 여기서 |
| Part Forge 창 | ✅ | `Tools > Mecha > Part Forge`. 생성·설정·로드아웃·Play 한 흐름 |
| 기체 조립 (소켓·프리팹) | ✅ | `MechaSocket` / `MechaPartView` / `MechaAssembler` + Part Forge |
| 발사 애니메이션 | ✅ | `WeaponFireAnimator`. Animator.Play 직접 호출 |

## 아이템 / 인벤토리

| 시스템 | 상태 | 비고 |
|---|---|---|
| ItemDataSO / ItemDatabase | ✅ | `uint Id` 기반 |
| Id 대역 | ✅ | 타입별 번호대(무기 1000~, 탄약 3000~). 대역마다 커서가 따로 |
| Id 충돌 해결 | ✅ | Ctrl+D 복제 감지(임포트 시 경고) + 인스펙터 원클릭 정리 |
| UI 아이콘 / 월드 스프라이트 | ✅ | `IconSprite` / `WorldSprite`(비면 아이콘으로 폴백) |
| 이름 3겹 | ✅ | 에디터 식별자 / 기본 표시명 / 로컬라이즈 키 |
| 로컬라이제이션 | ✅ | `Localization.Get` 창구 + `LocalizationTableSO` + 언어 전환 |
| LocalizedText | ✅ | TMP ⋮ 메뉴 `Localize`. 키 자동 생성, 원문 표 등록, 일괄 처리 |
| 표 편집기 | ✅ | `Tools > Localization > 표 편집기`. 격자 편집, 빠진 칸 필터, TSV 복사/붙여넣기 |
| 동적 문구 | ✅ | `Localization.Format` + `UIPanelBase.RefreshLocalization` |
| 구글 시트 자동 동기화 | ⬜ | 지금은 TSV 복사/붙여넣기로 수동 왕복 |
| Item Creator 창 | ✅ | `Tools > Item Creator`. 타입 스캔, 이름 규칙, 일괄 리네임 |
| Item Database 메뉴 | ✅ | `Tools > Item Database > Rebuild`. 자동화에서 스캔을 부를 수 있다 |
| 개체 상태 (`ItemInstance`) | ✅ | 무기 모듈·개량이 인벤토리에서도 유지됨. **장비는 전부 개체**이고 손상(`HealthRatio`)이 파츠를 따라다님 (2026-11-27) |
| 주둔지 ↔ 필드 (`SceneFlow`) | 🟡 | `GarrisonScene`(출격구) ↔ `IngameScene`(임시 귀환문). 탄 기체는 손상·적재함 그대로 건너간다. 죽음은 아직 필드 새 판 ([run](arch/run.md#씬-건너기--주둔지--필드-2026-11-27)) |
| 판 밖 상태 (`PlayerProfile`) | 🟡 | 창고 60칸·격납고 기록·건너는 기체(`MechaRecord`). 새 판에도 인스턴스가 남음. **UI 없음.** 씬을 건너도 남는다(조종권이 DontDestroyOnLoad) ([run](arch/run.md#판-밖-상태--playerprofile-2026-11-27)) |
| Inventory | ✅ | 칸 기반, 스택 병합, 인스턴스 아이템 단독 칸 |
| EquipmentController | ✅ | 슬롯 관리만. 효과는 `MechaPartController`가 붙임 |
| 장비 슬롯 구성 | ✅ | 코어 / 좌팔 / 우팔 / 추진체 |
| 월드 아이템 (드랍/줍기) | ✅ | `WorldItem` + `WorldItemSpawner`. 풀 등록 필요 |
| 디버그 지급/드랍 | ✅ | `DebugItemSpawner` — 단축키로 지급 |
| 인벤토리 칸 수 | ✅ | `AgentStatus.InventorySize` 스탯. 줄면 넘친 건 바닥에 |
| 내구도 | ✅ | `ItemInstance.Durability`. 무기는 발사마다 감소, 0이면 사용 불가 |
| 사용 가능 판정 | ✅ | `ItemUsability` 한 곳. 슬롯 표시와 실제 동작이 같은 답 |
| 재료 / 고철 | 🟡 | `MaterialDataSO`(7000~). 임시 4종(강철·회로 기판·고분자·연료 전지) + 고철. **아이콘 없음** |
| 재료 구성 / 분해 | ✅ | `ItemDataSO.Composition` + `_disassemblable`. 우클릭 → 분해(`ItemDisassembly`, 넘치면 발밑). 코어 3종·터렛 유닛 데이터에 구성 입력 |
| 저장/불러오기 | ⬜ | **미착수** |

**에셋** — 무기 `Perro`, `Scatter` / 모듈 `ScopeReddot`, `HeavyCoolant`. 추진체 에셋은 아직 없습니다.

**아트** — `Unit_Core.png`에 코어 13종이 들어와 있습니다. 프리팹으로 올린 것은 하나뿐입니다.

**기체 파츠 에셋** — `Item_Core_TestCore`(Id 6000, 체력 +50) 하나. 소켓 3개(양팔·추진체)와 `WaistRoot`를 가진 `CoreTest` 프리팹이 붙어 있습니다. 검증용입니다.

## 무기

| 시스템 | 상태 | 비고 |
|---|---|---|
| WeaponStatus | ✅ | 20종 스탯, 인스턴스별. origin 기반 modifier로 모듈·개량·탄약·열이 전부 얹힌다 |
| 개량 (`UpgradeSO`) | ✅ | 단계별, 누적 아닌 교체 |
| 모듈 (`ModuleDataSO`) | ✅ | 스탯만 바꾸면 프리팹 불필요 |
| 발사 모드 | ✅ | Single / Burst / Auto / Charge 전부 동작 |
| 실제 발사 로직 | ✅ | `PlayerRangedWeapon`. 쿨타임, 산탄, 반동 이벤트 |
| 탄약 급탄 | ✅ | `WeaponAmmoController`. 적재함에서 바로 당겨 쓴다 |
| 과열 | ✅ | `WeaponHeatController`. 소프트(쿨타임 +15%까지) + 하드(락). 게이지 UI는 미착수 |
| 근접 무기 | ⬜ | `PlayerWeaponBase`를 상속해 캐스터를 직접 Cast하면 됩니다 |
| 도구 (블럭 파괴) | ⬜ | 팔 슬롯을 무기와 공유하되 **판정은 분리** |

## 탄약

| 시스템 | 상태 | 비고 |
|---|---|---|
| 구경 (`AmmoCaliberType`) | ✅ | 무기 ↔ 탄약 호환 판정의 유일한 키 |
| 탄종 (`AmmoDataSO`) | ✅ | `WeaponStatDelta`로 성능 차이. 모듈과 같은 방식 |
| 장전 탄종 | ✅ | `WeaponItemInstance`가 들고 간다. 잔탄은 적재함 재고 |
| 자동 급탄 | ✅ | 다 떨어지면 그 무기는 끝. 보급 전까지 못 쏜다 |
| 탄종별 투사체 | ✅ | `AmmoDataSO.ProjectilePool`. 비우면 무기 기본 투사체 |
| 탄약 HUD | ✅ | `ArmStatus_Left` / `_Right` 두 인스턴스. 잔탄 + 열 게이지 |
| 탄종 전환 UI | 🔄 | 안전지대 로드아웃에서 지정 (`CollectAvailableAmmo` 재사용) |

**에셋** — 탄약 SO 4종.

## UI

| 시스템 | 상태 | 비고 |
|---|---|---|
| SlicedFilledImage | ✅ | 9슬라이스 + fillAmount 동시 지원, 전용 인스펙터 |
| SimpleGauge | ✅ | |
| UIPanelBase | ✅ | CanvasGroup 기반 Show/Hide/Toggle. `_closesOnCancel` 창은 열린 순서로 쌓여 ESC가 맨 위부터 닫는다(`TryCloseTop`) |
| 일시정지 | 🟡 | ESC / 패드 Start(`PlayerInput.OnPauseEvent`, `Pause` 액션). 다른 창이 떠 있으면 그 창 먼저. `PanelGamePause` → `GameTime.RequestPause`, 게임플레이 입력 막음. 계속하기 / 게임 종료. **UI 임시** |
| SectionInventoryGrid ↔ Inventory | ✅ | 슬롯 변경 이벤트 구독. 칸은 `CellInventorySlot` 프리팹으로 용량만큼 자동 생성. 배너에 사용 칸 "5 / 30"(남은 칸 3 이하 주황) |
| 인벤토리 배치 | 🟡 | 위 기체 구역(장비 4칸을 실루엣 모양 — 코어 위, 좌·우무기 양옆, 추진체 아래 — + 안전 슬롯), 아래 적재함 격자. 칸 라벨 한글(`ItemText.SlotName`). 게임은 멈추지 않는다. **스타일 임시** |
| 장비 잠금 | ✅ | 필드에서는 장비 칸을 드래그로 빼거나 끼울 수 없다(`EquipmentController.IsLocked`, 기본 true). 칸 오른쪽 위 자물쇠(`UI_Sheet_5_2`, `CellEquipmentSlot._lockMark`). 막는 건 UI 경로(`IItemSlotContainer.CanTake`/`CanAccept`)뿐 — 출격 로드아웃·죽음·툴의 `Equip`/`Unequip`은 통한다. 툴팁에 "장착 중 · 안전지대에서만 교체할 수 있습니다". 격납고(M5)가 `SetLocked(false)` |
| 인벤토리 UX 3·5단계 | 🟡 | 3 루팅 구역 ✅(잔해 루팅) · 5 모듈·개량은 격납고의 파츠 상세 화면(M5) |
| 슬롯 컨테이너 추상화 | ✅ | `IItemSlotContainer` — 인벤토리/장비가 구현 |
| 드래그 이동 | ✅ | 병합·스왑. **Alt 드래그 = 반**(홀수면 원래 칸에 큰 쪽). 왼쪽 버튼만 — 오른쪽은 메뉴. `ItemTransfer` 한 곳에 규칙(뺄 수 있는지 `CanTake`까지) |
| 우클릭 메뉴 | 🟡 | `PopupItemContext` — 커서 옆 작은 메뉴. 목록은 `ItemContextActions`: 사용(수리 키트 — 그 칸의 키트로 `MechaRepair.TryStartRepair(slot)`) · 분할 · 분해 · 버리기. 못 하면 회색 + 이유. 메뉴 밖 클릭·ESC로 닫힘. **자세히 보기(도감) 없음** |
| 분할 창 | ✅ | `PopupItemSplit` — 슬라이더 + 입력칸(서로 맞춤), 취소/확인. 같은 적재함의 첫 빈 칸으로. 처음 값은 반 |
| 아이템 선택 | ✅ | `ItemSelectionController`, 어느 칸이든 클릭 |
| 버리기 | ✅ | **패널 밖에 놓으면 버린다** — `ItemDragController._keepAreas`(각 Section 상자) 밖. 밖으로 나가면 고스트에 "버리기" + 패널 뒤 붉게(`DiscardHint`). 버리는 길은 `ItemDiscard` 하나(드래그·메뉴·`ItemDropArea`). 버린 사람은 줍기 반경을 한 번 벗어나야 다시 줍는다(`WorldItem.IgnorePickupBy`) |
| PartPlayerStatus | ✅ | 체력·실드(겹침, 실드 남으면 실드만 / 0이면 체력만) + 전력 / 연료 게이지 |
| PartMechaSilhouette | 🟡 | 기체 실루엣. 조립된 코어·팔·추진체 스프라이트를 그대로 Image로 옮기고 단색 셰이더(`UI/SilhouetteSolid`, 안쪽 외곽선) + 부위 체력 색(부서지면 어둡게). `PartHud/MechaSilhouette`. **위치·크기(`_pixelsPerUnit`) 미조정** |
| SimpleGauge 서브게이지 | ✅ | `_increaseMode`(Instant / SubFirst) · `_decreaseMode`(Instant / BlinkThenDrain). 체력·실드 = SubFirst + Blink, 연료 = SubFirst + Instant, 나머지(전력·과열) Instant. 처음 값·다시 켜질 때는 연출 없이 맞춘다 |
| PartRepairStatus | 🟡 | 실루엣 위. 키트 수 상시, 수리 중 진행 막대, 완료·중단·키트 없음 문구 후 흐려짐. **배치·스타일 임시** |
| PartGlideStatus | 🟡 | 연료 게이지 옆. 준비 / 활공 중 / 착륙 잠금(남은 시간 막대) / 연료 없음 / 추진체 파손. **배치·스타일 임시** |
| PopupItemInformation | ✅ | 슬롯 호버 시 커서 따라다님(상세는 호버로만 — 클릭 고정 없음). 이름·설명·**수치**(`ItemStatSheet` — 무기는 모듈·개량·탄 반영 최종값, 파츠는 Deltas, 탄·모듈·수리 키트). 적재함의 장비는 들어갈 칸 중 먼저 차 있는 칸의 장착품과 비교해 ▲▼(좋으면 초록). 장비 칸은 "장착 중". 내용 길이에 맞춰 늘어나고 자체 Canvas(UI 레이어 100)로 HUD 위에 |
| UIInputBlocker | ✅ | 패널이 뜨면 조준선·발사 차단 (겹침 대비 카운트) |
| PartEquipmentStatus | ✅ | 팔마다 하나. 잔탄 + 열 게이지(잠기면 남은 시간). **위치·크기는 임시** |
| PopupReload | ❌ | 재장전 폐기 |
| PlayerAimPoint | ✅ | 커서 추적. 조준점·조준거리를 **코어**가 정함 |
| WeaponPartBase | ✅ | `_slot`으로 어느 팔을 볼지 지정. 다른 팔의 변화는 여기서 걸러짐 |
| PartConditionDisplay ↔ 상태이상 | ✅ | 셀 재사용, 남은 시간 갱신 |
| PartHudHotbar ↔ Equipment | ❌ | 핫바 폐기. 무장 HUD 좌/우 2칸으로 대체 |
| MainCanvas / SubCanvas | 🟡 | Initialize/LateInitialize 뼈대만 |
| Button | 🟡 | 호버 스케일 필드만, 핸들러 비어있음 |
| UIManager | ⬜ | 패널 라우팅 미정 |

HUD와 인벤토리는 `Start()`에서 씬의 `Player`를 찾아 스스로 붙습니다. 플레이어 스폰 흐름이 생기면 `Bind()`를 밖에서 불러주면 됩니다.

**연결 필요** — `Inventory` 토글 입력 → 패널 `Toggle()`. `ConditionVisualTableSO` 에셋 생성 후 `PartConditionDisplay`에 지정.

TextMeshPro와 UI 스프라이트 시트가 들어와 있습니다.

## 그 외

| 시스템 | 상태 |
|---|---|
| 활공 중 사망 = 추락 | ✅ (2026-11-25) 조종을 잃고 연기를 끌며 그때 속도로 날아가다 고도 0에서 폭발 → 잔해. `IDeathCrash`(`PlayerMoveController`), 죽음 2단계 `OnDeadEvent` / `OnWreckedEvent`. 연기·폭발은 **임시 코드 파티클**(`DeathCrashFeedback`, 적도 붙음). 나는 적은 드론 때 |
| 잔해 루팅 | ✅ (2026-11-25) `WreckLoot`(적 프리팹 4종 + 드론 2종) → 잔해에 `LootContainer`. 코어 재료 일부 + 고철 + 전리품 표. 비지 않으면 남음. **비면 더 열 수 없고, 창을 닫은 뒤 2초 있다 흐려지며 사라짐**(다 꺼내도 창이 안 닫힌다). E 탐색(`PlayerInteractor`, `PartInteractPrompt`) → 루팅 구역. 하나씩 드러남(보는 사람마다), Shift+클릭 빠른 이동, 꽉 차면 깜빡임 ([arch/run](arch/run.md)) |
| 죽음 / 새 판 | ✅ `RunController`(Managers). 코어 0 = 비상탈출 → (추락) → 부서진 뒤 3초 뒤 새 판. 남은 잔해 적재함도 치움. 기체·적재물 잃음, 안전 슬롯만 남음. 사망 연출(잔해·흔들림·줌아웃·조준점 끔·`PanelDeathScreen` — 제목·잃은 것·"N초 뒤 주둔지로" 세 줄) ([arch/run](arch/run.md)) |
| 안전 슬롯 | 🟡 `InventoryController.SafeSlot`(1칸). UI `CellSafeSlot` — 기체 구역 오른쪽. **드래그는 눈으로 확인 전** |
| 착지 불가 지역 | 🟡 `NoLandingZone`(콜라이더 모양 판정). 위에서 Space 착륙 막힘, 강제 착륙 = 추락 즉사. 지상: 걸어서는 가장자리에서 멈춤(적도), 미끄러져 들어가면 연료 있으면 자동 활공·없으면 추락. 테스트 영역 하나. 배치는 맵 작업 |
| CameraManager | ✅ |
| CameraFollowEffector | ✅ (플레이어↔조준점 중간, 정조준 줌) |
| 발사 / 피격 / 착륙 화면 흔들림 | ✅ (`WeaponFireCameraShake`, `DamageCameraShake`, `SkidCameraShake`) |
| CameraShakeEffector | ✅ (`CameraRig` 아래 `MainCamera`를 흔든다) |
| Map / MapGenerator | ⬜ |
| 이펙트 / 사운드 재생 | ✅ 구조 + 이펙트 4종 (`WeaponFeedback` 총구 화염, `HitFeedback` 몸체·실드 피격, `SkidFeedback` 긁힘). 소리 에셋 없음 ([arch/feedback](arch/feedback.md)) |
| 오디오 믹서 | ⬜ (`Master / BGM / SFX / UI`. `SoundManager._defaultGroup`에 SFX를 넣으면 된다) |
| 블럭 파괴·설치 | ⬜ |
| 숙련도 / 테크트리 | ⬜ |
| 멀티플레이 | ⬜ |

## 지금 바로 걸리는 문제

1. **개량(`UpgradeSO`)에 안정적인 Id가 없습니다.** 저장 시스템을 붙일 때 걸립니다. ([ARCHITECTURE.md](arch/weapon.md#무기-모딩) 참고)
2. ~~`ItemInstanceRegistry.Clear()` 호출처 없음~~ → 해결됨 (`MechaLoadoutApplier`가 진입 시 호출).
3. **`Resources` 아래에 아이템 에셋이 통째로 들어가 있습니다.** 지금은 괜찮지만 아이템이 수백 개가 되면 시작할 때 전부 메모리에 올라갑니다. Addressables로 옮길 시점을 정해둘 것.
4. **투사체가 지형을 뚫습니다.** 벽이 아직 없어서 증상이 안 보일 뿐입니다. 맵을 만들 때 같이 처리해야 합니다.
5. ~~`GameRandom.Reseed` 호출처 없음~~ → 해결됨 (`MechaLoadoutApplier`). 고정 시드 옵션도 있습니다.
6. ~~장비 슬롯에 장착 효과가 없음~~ → 해결됨 (`MechaPartController`).

## 다음 작업 후보

**적이 메카 유닛이 됐습니다.** 랜덤 구성으로 조립되고, 팔에 든 무기로 쏩니다. **사격은 아직 눈으로 검증하지 않았습니다** — 아래 "확인할 것"을 먼저 보세요.

남은 다듬기:

1. **중구경·산탄 탄약 에셋이 없습니다** — `Hunter`(Middle), `Burster`(Buckshot)는 맞는 탄이 없어 적 구성 후보에서 빠집니다. 탄약을 만들면 자동으로 들어옵니다
2. **적 기체가 플레이어와 같은 무기 수치를 씁니다** — 적 전용 수치 조정 수단이 없습니다. `UnitDataSO`의 `_damage`는 근접 캐스터에만 걸립니다
3. **적끼리 유탄 오사** — 근접 캐스터는 편을 가리지만 **투사체는 전부 때립니다.** 아래 참고
4. **적끼리 겹칩니다** — 유닛 Rigidbody2D가 전부 Kinematic이라 밀어내기가 없습니다
5. **코어가 한 종류뿐** — `Unit_Core.png`에 13종이 있지만 프리팹은 하나입니다. Part Forge로 늘리면 구성 변화가 눈에 보입니다

그 다음은 [MILESTONE.md](MILESTONE.md)의 **M2 — 한 판이 끝난다**(맵·지형 충돌·죽음·새 판)입니다.
