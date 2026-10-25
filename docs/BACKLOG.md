# Backlog

미뤄둔 작업. **왜 미뤘는지**와 **언제 해야 하는지**를 같이 적습니다.
바로 다음에 할 일은 [STATUS.md](STATUS.md) 아래쪽에 있습니다.

메카 전환([PIVOT.md](PIVOT.md))으로 **없어진 기능의 항목은 지웠습니다.** 핫바 숫자키, 정조준 이동속도, 투척무기 마무리, 소모품 효과 정의가 그랬습니다.

---

## 지금 막고 있진 않지만 언젠가 터지는 것

### 개량(`UpgradeSO`)에 안정적인 Id가 없다
모듈은 `ItemDataSO`라 Id가 있는데 개량 경로는 없습니다. `WeaponItemInstance`가 개량을 SO 참조로 들고 있어서 **저장할 방법이 없습니다.**

> **해야 하는 시점** — 저장/불러오기 붙일 때. 선택지는 `ItemDataSO` 파생으로 올리거나(도감에도 뜸) 별도 Id 체계를 두거나.

### `ItemInstanceRegistry.Clear()`를 부르는 곳이 없다
정적이라 플레이 모드를 나가도 이전 판의 인스턴스가 남습니다. 지금은 월드 로드 개념이 없어 증상이 안 보일 뿐입니다.

> **해야 하는 시점** — 새 게임 / 월드 로드 흐름이 생길 때.

### ~~`Random.value` 직접 호출~~ → 해결됨 (2026-08-10)
`IRandomSource` / `GameRandom`이 들어갔습니다. `DamageHandler`, `ConditionCastEffector`, 산탄 각도가 전부 `GameRandom.Combat`을 씁니다.

`WorldItemSpawner`의 흩뿌리기만 `UnityEngine.Random`으로 남겨뒀습니다 — 연출이라 판정 순서에 끼면 오히려 재현이 깨집니다.

> **남은 것** — `GameRandom.Reseed`를 부르는 곳이 없습니다. 새 게임/월드 로드 흐름이 생길 때 붙이세요. 재현은 시드가 같고 **호출 순서가 같을 때만** 보장된다는 점도 기억할 것.

### `Resources` 아래에 아이템 에셋이 통째로 들어가 있다
`Resources`는 참조된 것을 시작할 때 전부 메모리에 올립니다. 아이템이 수백 개가 되고 아이콘까지 물리면 부담이 됩니다.

> **해야 하는 시점** — 아이템이 100개를 넘어가거나 로딩이 눈에 띌 때. Addressables로 옮기면 `ItemDatabaseSO`의 로더만 갈면 됩니다.

### `PlayerInput`이 SO라 로컬 1인 전용
에셋 하나를 공유하므로 플레이어가 둘 이상이면 입력이 섞입니다.

> **해야 하는 시점** — 로컬 협동이나 멀티를 붙일 때.

---

## 입력

### 패드 조준
`Look` 액션이 없어 `PlayerAimController`가 마우스 전용입니다. 패드는 스틱 방향 × `AimRange`로 조준점을 만들면 되므로, `PointerPosition`을 채우는 부분만 갈아끼우면 됩니다.

### 반동 제어를 실력 요소로
지금 반동은 조준점을 밀었다가 **스스로 회복**합니다. 플레이어가 마우스를 내려서 잡는 조작이 아닙니다.

> **바꾸려면** — 상대 조준(커서 잠금 + 마우스 이동량)으로 가야 합니다. 인벤토리가 커서를 쓰므로 `UIInputBlocker`가 열릴 때 커서 모드를 갈아타는 처리가 함께 필요합니다.

### `AimRange` 스탯을 읽는 곳이 없다
조준점을 자르는 데 쓰다가 뺐습니다 — 사거리 밖이라고 조준점을 붙들면 커서와 갈라져서 조작이 어긋난 것처럼 보입니다.

스탯·필드(`WeaponStatType.AimRange`, `PlayerAimController.AimRange`)는 남아 있고 무기 에셋에도 값이 들어 있습니다(Scatter 17, IronSpear 21). **지금은 아무도 읽지 않습니다.**

> **전환에서 정리됩니다** — 좌우 무기가 서로 다른 `AimRange`를 주장하는 문제가 생기므로 **코어 스탯으로 옮기는 쪽**이 유력합니다. 쓸 데가 없으면 빼는 것도 방법입니다.

### `Crawl` / `Interact`
입력은 받고 있는데 동작이 없습니다.
- `Crawl` — 이동 속도 페널티를 어떻게 줄지 미정. 기체에 포복이 남는지부터 정해야 합니다
- `Interact` — 대상 탐색이 없음. 안전지대·격납고 진입이 첫 사용처가 됩니다

---

## 로컬라이제이션

### 구글 스프레드시트 자동 동기화
지금은 표 편집기의 **TSV 복사/붙여넣기**로 손으로 왕복합니다. 실무에서는 이걸로도 꽤 오래 버팁니다 — 자동화는 왕복이 귀찮아진 뒤에 붙이세요.

**결정이 필요한 것**
- 시트 스키마 — `key / ko / en` 열 순서를 `LanguageType`과 맞출지, 헤더 이름으로 찾을지
- 받는 방법 — CSV 내보내기 URL을 런타임에 받을지, 에디터에서 받아 `LocalizationTableSO`에 구워둘지
- 시트를 공개로 둘지 (공개 CSV URL은 인증이 필요 없지만 누구나 봅니다)
- 반대 방향 — 에디터에서 등록한 키를 시트로 밀어 올릴지, 시트가 항상 원본일지

에디터에서 굽는 쪽이 단순합니다. 런타임 다운로드는 실패·지연·오프라인을 다 다뤄야 하는데, 지금 그럴 이유가 없습니다.

### 언어 전환 — UIPanelBase 밖은 아직
`LocalizedText`와 `UIPanelBase` 파생 패널은 언어가 바뀌면 다시 그립니다. 그 밖(월드 텍스트, 인벤토리 셀, 툴팁)은 아직 구독하지 않아 다음 갱신 전까지 이전 언어로 남습니다.

> **해야 하는 시점** — 게임 중 언어 전환을 실제로 지원할 때. 지금은 에디터에서 확인하는 용도라 문제가 안 됩니다.

### 아이템 설명을 보여주는 UI가 없다
`ItemDataSO.Description`은 키까지 갖춰졌는데 **읽는 곳이 없습니다.** `PartItemDetailInfo`와 `CellItemDetailInfo`가 빈 스텁입니다. 상세 패널을 만들 때 `DisplayName`/`Description`을 그대로 물리면 됩니다.

### 아이템 외 문자열
상태이상 이름(`ConditionVisualTableSO.GetDisplayName`), 장비 슬롯 이름(`CellEquipmentSlot._displayName`)은 아직 하드코딩입니다. 아이템과 `LocalizedText`가 붙은 UI 문구만 키를 갖습니다.

### 언어 설정 저장
`Localization.Language`는 정적이라 게임을 껐다 켜면 한국어로 돌아옵니다. 세이브/설정 시스템이 생길 때 같이 넣으세요.

## 인벤토리 / 아이템

### 상세 분할 UI
지금은 우클릭 드래그로 **반개**만 됩니다. `ItemTransfer.Move`가 `count`를 받으므로 개수를 정하는 창만 붙이면 됩니다.

### 컨테이너(상자) 인벤토리
`PartLootingInventory`가 비어 있습니다. `Inventory`를 가진 `ItemContainer` 컴포넌트를 만들고 `UIInventory.Bind`를 부르면 됩니다. **`UIInventory`는 이미 재사용 가능한 상태입니다** (`_bindPlayerInventory`를 끄고 직접 Bind).

### ~~`EquipSlotType`의 무기 3종~~ → 전환에서 해소 (2026-10-25)
`PrimaryWeapon` / `SecondaryWeapon`이 `ArmLeft` / `ArmRight`가 되고, 무장이 **다시 장비 슬롯에서 나옵니다.** 핫바가 없어지므로 "쓰이는 곳이 없는 슬롯" 문제 자체가 사라집니다. ([PIVOT.md](PIVOT.md#슬롯))

### 아이템 버리기 입력
`ItemDropArea`(드래그해서 버리기)는 있는데, "선택 후 키를 눌러 버리기"는 없습니다. 버리기 액션이 필요합니다.

---

## 무기 / 전투

### 투사체가 지형을 뚫는다
벽 충돌 판정이 없습니다. 맵/블럭 시스템 자체가 없어서 지금은 증상이 안 보일 뿐입니다.

`TargetType`은 유닛 분류라 지형에는 대응 값이 없습니다. 지형 전용 LayerMask를 `Projectile`에 둘지, `TargetType`에 지형을 추가할지 정해야 합니다.

> **해야 하는 시점** — `MapGenerator`를 만들 때 같이.

### 근접 무기
`PlayerWeaponBase`를 상속해 `OnUsePressed`에서 캐스터를 직접 `Cast()`하면 됩니다. 탄약을 쓰지 않는 무기는 `PlayerWeaponDataSO.Calibers`를 비워두면 됩니다.

**버튼이 하나뿐입니다.** 좌우 무장이 각자 버튼 하나를 받으므로, 막기나 설치 미리보기 같은 보조 동작을 넣으려면 누르는 시간이나 별도 키로 풀어야 합니다.

### 도구 (채굴 / 설치)
팔 슬롯을 무기와 공유하되 **판정은 분리**합니다. 블럭 시스템이 없어서 아직 시작하지 않았습니다.

> 슬롯을 공유하는 것과 판정을 공유하는 것은 다릅니다. 캐스터 레벨에서 갈라야 합니다.

### 넉백
`Knockback` 스탯과 `ProjectileLaunchData`가 값을 나르지만 **소비하는 이펙터가 없습니다.** `KnockbackCastEffector`가 필요합니다.

### 차지 무기 밸런싱
차지율은 데미지와 탄속에 곱해집니다. 최소 배율은 `PlayerRangedWeapon.MinChargeRatio`(0.25) 상수입니다. 무기별로 달라야 하면 스탯으로 올리세요.

### ItemPreview 프리팹 구조
**전환의 소켓 구조와 같은 문제입니다.** `PartView`의 소켓 + 프리팹 안 Pivot 보정이 서면 미리보기도 그걸 그대로 씁니다 — 미리보기용 배치 규칙을 따로 만들면 실제 조립과 어긋납니다. ([PIVOT.md](PIVOT.md#조립))

---

## UI

### `UIManager` 패널 라우팅
패널을 스택으로 쌓을지, 이름으로 조회할지, 직접 참조할지 미정이라 비워뒀습니다. 지금은 `PartInventory`가 입력을 직접 받아 스스로 토글합니다.

### `Button`
호버 스케일 필드만 있고 핸들러가 비어 있습니다. DOTween이 들어와 있으니 그걸 쓰면 됩니다.

### 전력 / 연료 밸런싱
`VitalStatus`의 기본값은 **동작 확인용 숫자**입니다. 전환 후 수치대를 다시 잡아야 합니다.

정하지 않은 것:
- 전력을 무엇이 쓰는지 (발사, 실드, 부스터 중 어디까지)
- 연료 보급 아이템의 수치대
- 둘 다 바닥났을 때 기체가 어떤 상태가 되는지

### 내구도 수리
0이 되면 사용만 막히고 아이템은 남습니다. **고치는 수단이 없습니다.** `ItemInstance.RepairFull`과 `SetDurability`는 열려 있습니다.

전환 후에는 **안전지대가 유일한 수리 경로**입니다. 필드 수리 아이템은 없습니다.

> **정해야 하는 것** — 파츠별로 내구도가 깎이는지, 기체 체력 하나로 묶는지.

### 탄종 표시 / 탄약 없음 피드백
잔탄은 붙었지만(`PartEquipmentStatus`) 아직 안 보이는 것이 있습니다.

- **어떤 탄을 물고 있는지** — `WeaponAmmoController.OnAmmoChangedEvent`의 첫 인자가 탄종입니다
- **고를 수 있는 탄종 목록** — `CollectAvailableAmmo(List<uint>)`. 전환 후에는 안전지대 로드아웃 화면이 이걸 씁니다
- **탄 떨어짐** — 자동 급탄이라 재고가 0이 되는 순간이 유일한 신호입니다

`PlayerRangedWeapon`은 `OnFiredEvent(반동)` / `OnDryFireEvent`를 쏩니다. 머즐 플래시·소리·화면 흔들림은 전부 이걸 구독해서 처리하세요. **무기가 직접 재생하면 안 됩니다.**

### 코어별 조준점
조준점은 무기가 아니라 **코어**가 정합니다. `PlayerAimPointStateType`의 `GunAim_*` / `Scrope_*` 계열은 폐기되고 코어 타입 계열이 들어옵니다.

`PlayerAimPointStateType.None`이 "기본 조준점"이라는 유효한 값이라 **"지정 안 함"을 그걸로 표현할 수 없다**는 문제는 그대로 남습니다. 코어 데이터에 조준점 필드를 넣을 때 같이 정리해야 합니다.

---

## 기타

- `CameraFollowEffector` — 비어 있음
- `SoundPlayer` — 비어 있음
- `EnemyManager`, 적 AI — 비어 있음
- `MapGenerator` / 블럭 파괴·설치 — 미착수
- 숙련도 / 테크트리 — 미착수
- 저장 / 불러오기 — 미착수
