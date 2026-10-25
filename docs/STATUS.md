# 개발 현황

최종 갱신: 2026-10-25 (메카 전환 결정). 계획은 [MILESTONE.md](MILESTONE.md).

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
| Projectile | ✅ | 경로 스윕, 관통, 사거리 소멸, 폭발 |
| ProjectileCaster | ✅ | 직전 위치→현재 위치 CircleCast. 터널링·중복타 방지 |
| `UnitDataSO` | ✅ | 유닛 데이터 + 전리품 테이블. Id 대역 9000~9999 |
| `Enemy` | 🟡 | 데이터 주입·사망·드랍까지. 이동·AI 없음 |
| EnemyManager | ⬜ | 빈 스텁 |
| Shadow | 🟡 | 크기 설정만 |

캐스터는 `CircleCaster`와 `ProjectileCaster` 두 종류입니다.

**투사체는 지형을 뚫습니다.** 맵/블럭 시스템이 없어서 벽 충돌 판정이 아직 없습니다.

## 플레이어

| 시스템 | 상태 | 비고 |
|---|---|---|
| PlayerInput | ✅ | 입력 리더 SO. `PlayerInput.asset` 존재 |
| 이동 | ✅ | Rigidbody2D, 질주는 Percent modifier, 기절 시 정지 |
| 조준 | ✅ | 마우스 절대 조준 + `AimRange` 제한 + 반동 오프셋 |
| 주/보조 입력 | 🔄 | 좌=Primary, 우=Secondary. **좌=좌무기 / 우=우무기로 고정 배분됨** |
| 정조준(총 + 우클릭) | ❌ | 기능 폐기. `SetAiming`과 카메라 줌 기계장치는 남김 |
| 소모품 사용(우클릭) | ❌ | 안전지대 수리만 남음 |
| 투척무기 | ❌ | 한 버튼 구조와 충돌. 되살리려면 눌러-떼기로 재작성 |
| 핫바 (인벤토리 앞 7칸) | ❌ | 무장은 장비 슬롯이 정한다 |
| 무기 들기 | 🔄 | 핫바가 아니라 `Equipment[ArmLeft/ArmRight]`를 따라감 |
| 스테미너 / 허기 / 갈증 | 🔄 | `VitalStatus` 기계장치는 유지. **전력 / 연료**로 교체 |
| 소모품 사용 | ❌ | `PlayerItemUseController`. 쿨타임 UI는 과열 게이지로 재활용 |
| 포복(Crawl) | 🟡 | 입력만 받고 이동에 반영 안 됨 |
| 재장전 | ❌ | **과열**이 대체. 탄은 적재함에서 자동 급탄 |
| 상호작용(Interact/Use) | 🟡 | 입력만 있고 대상 탐색 없음 |
| 장비 → 스탯 연결 | ⬜ | **장착해도 아무 효과가 없습니다.** `MechaPartController` 필요 |
| 기체 조립 (소켓·프리팹) | ⬜ | `PartView` / `MechaAssembler` / Part Forge |

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
| 개체 상태 (`ItemInstance`) | ✅ | 무기 파츠·개량이 인벤토리에서도 유지됨 |
| Inventory | ✅ | 칸 기반, 스택 병합, 인스턴스 아이템 단독 칸 |
| EquipmentController | 🔄 | 슬롯 관리만. **구독자가 없어서 장착 효과가 전혀 없습니다** |
| 장비 슬롯 구성 | 🔄 | `EquipSlotType`을 코어/좌팔/우팔/추진체로 재편 |
| 월드 아이템 (드랍/줍기) | ✅ | `WorldItem` + `WorldItemSpawner`. 풀 등록 필요 |
| 디버그 지급/드랍 | ✅ | `DebugItemSpawner` — 단축키로 지급 |
| 인벤토리 칸 수 | ✅ | `AgentStatus.InventorySize` 스탯. 줄면 넘친 건 바닥에 |
| 내구도 | ✅ | `ItemInstance.Durability`. 무기는 발사마다 감소, 0이면 사용 불가 |
| 사용 가능 판정 | ✅ | `ItemUsability` 한 곳. 슬롯 표시와 실제 동작이 같은 답 |
| 저장/불러오기 | ⬜ | **미착수** |

**에셋** — 무기 `Perro`, `Scatter` / 모듈 `ScopeReddot`, `LargeHeavyMagazine`. 기체 파츠(코어·추진체) 에셋은 아직 없습니다.

**아트** — `Unit_Core.png`에 코어 13종이 들어와 있습니다. 파츠 SO와 프리팹은 아직 없습니다.

## 무기

| 시스템 | 상태 | 비고 |
|---|---|---|
| WeaponStatus | 🔄 | 16종 스탯, 인스턴스별. **과열 스탯 4종 추가 예정** |
| 개량 (`WeaponUpgradeSO`) | 🔄 | 단계별, 누적 아닌 교체. `UpgradeSO`로 리네임 |
| 파츠 (`WeaponPartDataSO`) | 🔄 | 스탯만 바꾸면 프리팹 불필요. **`ModuleDataSO`로 리네임** |
| 발사 모드 | ✅ | Single / Burst / Auto / Charge 전부 동작 |
| 실제 발사 로직 | ✅ | `PlayerRangedWeapon`. 쿨타임, 산탄, 반동 이벤트 |
| 탄약 / 재장전 | 🔄 | `WeaponAmmoController`가 탄창에서 **급탄기**가 됨 |
| 과열 | ⬜ | 재장전을 대신하는 제재한. `WeaponHeatController` |
| 근접 무기 | ⬜ | `PlayerWeaponBase`를 상속해 캐스터를 직접 Cast하면 됩니다 |
| 도구 (블럭 파괴) | ⬜ | 팔 슬롯을 무기와 공유하되 **판정은 분리** |

## 탄약

| 시스템 | 상태 | 비고 |
|---|---|---|
| 구경 (`AmmoCaliberType`) | ✅ | 무기 ↔ 탄약 호환 판정의 유일한 키 |
| 탄종 (`AmmoDataSO`) | ✅ | `WeaponStatDelta`로 성능 차이. 모듈과 같은 방식 |
| 탄창 상태 | 🔄 | **장전 탄종만 남고 잔탄은 사라짐.** 적재함 재고가 곧 잔탄 |
| 재장전 | ❌ | 적재함에서 자동 급탄. 다 떨어지면 그 무기는 끝 |
| 탄종별 투사체 | ✅ | `AmmoDataSO.ProjectilePool`. 비우면 무기 기본 투사체 |
| 탄약 HUD | 🔄 | `PartEquipmentStatus`를 좌/우 2개로. `PopupReload`는 폐기 |
| 탄종 전환 UI | 🔄 | 안전지대 로드아웃에서 지정 (`CollectAvailableAmmo` 재사용) |

**에셋** — 탄약 SO 4종.

## UI

| 시스템 | 상태 | 비고 |
|---|---|---|
| SlicedFilledImage | ✅ | 9슬라이스 + fillAmount 동시 지원, 전용 인스펙터 |
| SimpleGauge | ✅ | |
| UIPanelBase | ✅ | CanvasGroup 기반 Show/Hide/Toggle |
| UIInventory ↔ Inventory | ✅ | 슬롯 변경 이벤트 구독 |
| 슬롯 컨테이너 추상화 | ✅ | `IItemSlotContainer` — 인벤토리/장비가 구현 |
| 드래그 이동 | ✅ | 병합·스왑·반개 분할. `ItemTransfer` 한 곳에 규칙 |
| 아이템 선택 | ✅ | `ItemSelectionController`, 어느 칸이든 클릭 |
| 버리기 영역 | 🟡 | `ItemDropArea` — 이벤트만 쏨. 월드 픽업 필요 |
| PartItemDetail / ItemPreview | ⬜ | 파츠 슬롯 배치 방식 미정 |
| PartPlayerStatus | 🔄 | 체력 + 스테미너 / 허기 / 갈증 게이지 → **체력 + 전력 / 연료** |
| PopupItemInformation | ✅ | 슬롯 호버 시 커서 따라다님. 이름·설명·내구도, 화면 밖 보정 |
| UIInputBlocker | ✅ | 패널이 뜨면 조준선·발사 차단 (겹침 대비 카운트) |
| PartEquipmentStatus | 🔄 | 잔탄 표시. **좌/우 2개 인스턴스 + 과열 게이지** |
| PopupReload | ❌ | 재장전 폐기 |
| PlayerAimPoint | 🔄 | 커서 추적. 조준점은 무기가 아니라 **코어**가 정함 |
| WeaponPanelBase | 🔄 | "지금 든 무기" 가정. **어느 손인지 지정하는 필드 필요** |
| ConditionDisplayGroup ↔ 상태이상 | ✅ | 셀 재사용, 남은 시간 갱신 |
| PartHudHotbar ↔ Equipment | ❌ | 핫바 폐기. 무장 HUD 좌/우 2칸으로 대체 |
| MainCanvas / SubCanvas | 🟡 | Initialize/LateInitialize 뼈대만 |
| Button | 🟡 | 호버 스케일 필드만, 핸들러 비어있음 |
| UIManager | ⬜ | 패널 라우팅 미정 |

HUD와 인벤토리는 `Start()`에서 씬의 `Player`를 찾아 스스로 붙습니다. 플레이어 스폰 흐름이 생기면 `Bind()`를 밖에서 불러주면 됩니다.

**연결 필요** — `Inventory` 토글 입력 → 패널 `Toggle()`. `ConditionVisualTableSO` 에셋 생성 후 `ConditionDisplayGroup`에 지정.

TextMeshPro와 UI 스프라이트 시트가 들어와 있습니다.

## 그 외

| 시스템 | 상태 |
|---|---|
| CameraManager / ShakeEffector | ✅ |
| CameraFollowEffector | ✅ (플레이어↔조준점 중간, 정조준 줌) |
| Map / MapGenerator | ⬜ |
| SoundPlayer | ⬜ |
| 블럭 파괴·설치 | ⬜ |
| 숙련도 / 테크트리 | ⬜ |
| 멀티플레이 | ⬜ |

## 지금 바로 걸리는 문제

1. **개량(`WeaponUpgradeSO`)에 안정적인 Id가 없습니다.** 저장 시스템을 붙일 때 걸립니다. ([ARCHITECTURE.md](ARCHITECTURE.md#무기-모딩) 참고)
2. **`ItemInstanceRegistry.Clear()`를 부르는 곳이 없습니다.** 아직 월드 로드 개념이 없어서 문제가 안 드러날 뿐입니다.
3. **`Resources` 아래에 아이템 에셋이 통째로 들어가 있습니다.** 지금은 괜찮지만 아이템이 수백 개가 되면 시작할 때 전부 메모리에 올라갑니다. Addressables로 옮길 시점을 정해둘 것.
4. **투사체가 지형을 뚫습니다.** 벽이 아직 없어서 증상이 안 보일 뿐입니다. 맵을 만들 때 같이 처리해야 합니다.
5. **`GameRandom.Reseed`를 부르는 곳이 없습니다.** 지금은 실행할 때마다 다른 시드로 시작합니다. 새 게임 흐름이 생기면 붙이세요.
6. **장비 슬롯에 무엇을 끼워도 아무 일이 일어나지 않습니다.** `EquipmentController.OnEquipChangedEvent`를 구독해 스탯에 반영하는 쪽이 없습니다. 메카 전환에서 기체 파츠가 의미를 가지려면 1순위입니다.

## 다음 작업 후보

**[PIVOT.md](PIVOT.md#작업-순서)의 전환 작업이 우선입니다.** 전환 대상인 시스템을 지금 다듬으면 그대로 버려집니다.

전환과 무관하게 살아있는 것:

- **적 이동·AI** — `Enemy`가 데이터 주입까지만 되어 있습니다
- **맵 생성** — `MapGenerator`가 빈 스텁입니다. 투사체 지형 충돌도 여기서 같이
- **저장/불러오기** — 개량 Id 문제를 여기서 같이 정리
