# 한 판 — 죽음 · 새 판 · 안전 슬롯 · 착지 불가 지역

## 죽음 = 비상탈출

**코어 체력이 0이면 죽습니다.** 컨셉은 비상탈출입니다 — 조종사가 기체를 버리고 나가므로 **장착 파츠(코어·양팔·추진체)와 적재함을 전부 잃습니다.** 안전 슬롯 한 칸만 남습니다 (2026-11-22 결정).

**죽음은 두 단계입니다** (2026-11-25). 땅에서 죽으면 둘이 같은 프레임에 옵니다. **활공 중이면 그 사이에 추락이 끼어듭니다.**

```
Health.OnDeadEvent — 조종을 잃음
  ├─ (로직) 무기 정지(PlayerRangedWeapon — Owner.IsDead), 이동 입력 끊김(CanMove), 수리 중단, 실드 0(ShieldStatus), 판정 None
  ├─ (연출) PlayerAimPoint — 조준점 끔
  └─ (UI) PanelInventory — 열려 있으면 닫고, 죽어 있는 동안 안 열림
        │
        │  떠 있으면 IDeathCrash.TryBeginCrash — 연기를 끌며 그때 속도로 미끄러지듯 날아가고 고도를 잃는다(~1.1초)
        ▼
Health.OnWreckedEvent — 몸이 부서짐(땅에 닿음)
  ├─ (로직) RunController — 새 판 타이머 시작(3초) — 폭발부터 센다
  ├─ (로직) Enemy — 전리품·자기 파괴(HandleWrecked)
  ├─ (연출) DeathCrashFeedback — 폭발(임시 파티클) / PartBreakFeedback — 몸 전체를 어두운 잔해(Wreck)로
  ├─ (연출) DeathCameraShake — 강한 흔들림 / CameraFollowEffector — IsWrecked를 읽어 천천히 줌아웃(×1.5)
  └─ (UI) PanelDeathScreen — RunController.OnPlayerDiedEvent로 "기체 파괴 — 비상 탈출" + 카운트다운
```

**추락은 `IDeathCrash`가 맡습니다.** `Health.Die()`가 Agent에서 찾아 묻고, true면 구현체가 땅에 닿을 때 부서짐을 부릅니다. 지금은 `PlayerMoveController`(활공 중 사망) 하나. 나는 적(드론)은 자기 이동에 구현하면 같은 길을 탑니다.
착지 불가 지역 위에서 땅에 닿으면 `OnFellEvent`를 먼저 쏩니다(떨어진 것). 추락 중 새 판이 오면(`ResetMotion`) 끊고 부서짐은 부르지 않습니다.

**연출은 `OnWreckedEvent`를 듣습니다. 조종을 잃는 일(입력·AI·조준)은 `OnDeadEvent`.** 새 사망 연출을 붙일 때 둘을 헷갈리면 활공 중 사망에서 잔해가 공중에 생깁니다.

**잃는 순간은 새 판이 시작될 때입니다.** 죽은 직후에는 기체가 잔해로 보여야 해서 장비를 바로 걷지 않습니다. 대신 그사이 **인벤토리 화면이 열리지 않습니다** — 죽은 뒤에 안전 슬롯으로 옮겨 담는 걸 막습니다.

실드를 거치지 않는 죽음(추락 즉사 `Health.Kill`)도 있어서, `ShieldStatus`는 주인이 죽으면 0으로 떨어집니다. 안 그러면 실드 원이 잔해를 감싼 채 남습니다.

## 잔해 적재함 — `LootContainer` (2026-11-25)

적이 부서지면 잔해(`PartBreakFeedback`가 만든 모양)에 `WreckLoot`가 **적재함**을 얹습니다.

| | |
|---|---|
| 내용 | 달고 있던 **코어의 재료 구성 일부**(재료마다 25~60%) + **고철** 1~3 + 유닛 데이터 전리품 표. 코어가 없는 단일 기체는 유닛 데이터의 재료 구성 |
| 수명 | **비지 않은 동안 남는다**(`PartDebris.MakePersistent`). 비면 더 열 수 없고(`CanInteract`), **보던 사람이 창을 닫은 뒤** `_emptyLinger`(2초) 있다가 잔해째 흐려져 사라진다(`PartDebris.FadeOut`). 다 꺼내는 순간 창을 닫지 않는다 — 무슨 일이 났는지 헷갈린다. 새 판이 `LootContainer.ClearAll()` |
| 열기 | 가까이 가면 `PlayerInteractor`가 가장 가까운 하나를 고르고 `PartInteractPrompt`가 "[E] 탐색". E → `LootContainer.OnOpenRequestedEvent` → `PanelInventory`가 오른쪽에 `SectionLootingInventory`를 붙여 연다. 활공 중에는 고르지 않는다. 멀어지면 닫힌다 |
| 탐색 | 처음 연 사람에게 안의 것이 전부 가려지고(`?`) 앞에서부터 하나씩 드러난다(0.5초 + 등급×0.35초). 진행은 쿨타임 막대를 빌려 보인다. **보는 사람마다** 따로, 상태는 잔해에. 닫았다 열면 이어서 |
| 옮기기 | 드래그, 또는 Shift+클릭(`ItemQuickMove` → `ItemTransfer.QuickMove`) — 반대편 적재함으로. 꽉 차면 사용 칸 표시가 깜빡인다. 가려진 칸은 드래그·툴팁·메뉴·놓기 전부 막힌다 |

UI는 적재함을 직접 부르지 않고 적재함도 UI를 부르지 않습니다 — 여는 요청은 정적 이벤트, 탐색 상태는 적재함이 들고 구역이 읽기만 합니다.

## 새 판 — `RunController`

씬의 `Managers/RunController`. 죽고 `_respawnDelay`(3초) 뒤 `StartNewRun()`.

| 순서 | 하는 일 | 왜 이 순서 |
|---|---|---|
| 1 | 적 전부 제거(`EnemyManager.ClearAll` — 시작 지연부터 다시), 바닥 아이템·날아가던 탄 반납 | |
| 2 | 안전 슬롯의 인스턴스 Id를 챙긴다 | 레지스트리를 지우기 전에 |
| 3 | **적재함 비우기 → 장비 전부 해제** | 코어를 먼저 떼면 적재 칸 수가 줄어 넘친 아이템이 발밑에 떨어진다 |
| 4 | 스폰 지점으로 이동, `ResetHealth`(체력·실드·부위, 되살림 이벤트), 상태이상 제거, `ResetMotion`(착륙·정지·고도 0·modifier 제거·판정 분류) | 장비를 입기 **전에** 되살린다 — 장착이 최대치를 올리면 비율(가득)이 유지된다 |
| 5 | `MechaPartController.StartNewRun(keep)` — 인스턴스 레지스트리(안전 슬롯 제외)·난수 시드 초기화 후 진입 구성을 다시 입힌다 | |
| 6 | 전력·연료 가득 (`VitalStatus.ResetAll`), 카메라 순간이동 | 최대치를 추진체·코어가 정하므로 장착 뒤에 |

- **플레이어 오브젝트는 부수지 않고 재사용합니다.** HUD·카메라·조준점의 구독이 살아 있어서 다시 붙일 필요가 없습니다. 실루엣은 조립 이벤트로 알아서 다시 짓습니다
- **되살림**은 `Health.ResetHealth`가 죽어 있던 경우에만 `OnRevivedEvent`를 쏘고, `Agent`가 이걸 듣고 `IsDead`를 내립니다
- 진입 구성은 지금 기본 로드아웃(`Loadout_Test`)입니다. 격납고가 생기면 `MechaPartController.PendingLoadout`이 대신합니다
- 플레이어별 상태는 `RunController`에 두지 않습니다(RULES). 멀티에서는 플레이어마다 따로 부르면 됩니다

### 인스턴스 레지스트리 — 안전 슬롯은 남긴다

`ItemInstanceRegistry.ClearExcept(keep)`. 안전 슬롯의 아이템이 무기처럼 개체 상태(개량·내구도)를 가지면 레지스트리에서 지우는 순간 그 상태가 날아갑니다. 남긴 인스턴스 뒤에서 다음 번호를 시작합니다 — 1로 되돌리면 번호가 겹칩니다.

## 안전 슬롯

`InventoryController.SafeSlot` — 칸 수 1짜리 `Inventory`. **아무 아이템이나 한 칸 분량(최대 스택)까지** 들어갑니다. 적재함과 같은 타입이라 드래그·스왑이 그대로 됩니다. 칸 수는 적재 스탯과 무관하게 고정입니다.

UI는 인벤토리 화면 장비 칸 옆의 `CellSafeSlot`(`SectionPlayerInventory._safeSlot`)입니다.

## 착지 불가 지역 — `NoLandingZone`

구렁·심연·물 같은 곳. **활공 중에만 의미가 있습니다.**

- 이 위에서는 **Space로 착륙할 수 없습니다** (`OnLandingRejectedEvent`). HUD 활공 표시가 "착륙 불가 지역"으로 바뀝니다
- 이 위에서 **강제로 내려앉으면(연료 고갈·추진체 파괴) 추락해 즉사**합니다 (`OnFellEvent` → `Health.Kill`)
- 판정 기준점은 **그림자 = 실제 위치**입니다. 떠 있는 몸체가 아닙니다

**판정은 물리 레이어가 아니라 콜라이더 모양**(`Collider2D.OverlapPoint`)으로 합니다. 레이어를 쓰면 충돌 매트릭스·캐스터 규칙에 걸리는데, 여기는 "이 점이 영역 안인가"만 알면 됩니다. 콜라이더는 트리거로 강제됩니다.

### 지상에서 — 걸으면 멈추고, 미끄러지면 빠진다 (2026-11-23)

| 어떻게 들어가나 | 결과 |
|---|---|
| 걸어서 | **가장자리에서 멈춘다** (`NoLandingZone.ClampWalk` — 들어가는 속도 성분만 지움, `_edgeMargin` 0.3 앞에서). 적도 같다(지상 판정일 때만) |
| 미끄러져서(고속 착륙의 관성) · 밀려서 | 뜰 수 있으면(연료·추진체·행동 가능) **착륙 잠금을 무시하고 자동으로 뜬다**(`OnAutoGlideEvent`). 못 뜨면 **추락**(`OnFellEvent` → `Health.Kill`) |

**가장자리는 벽 콜라이더가 아닙니다.** 벽이면 미끄러짐도 막혀서 "착륙 위치를 잘못 고르면 빠진다"가 성립하지 않습니다. 걷기만 이동 코드가 막습니다. 축별로 시험해서 상자 모양 영역에서는 가장자리를 따라 걸을 수 있고, 비스듬한 가장자리에서는 조금 일찍 멈춥니다.

지금 테스트 영역은 씬의 `NoLandingZone_Test`(14, 0, 8×8, 붉은 반투명) 하나입니다.
