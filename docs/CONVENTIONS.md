# 코드 규약

## 네이밍

- **네임스페이스를 쓰지 않습니다** (전역)
- private 필드 `_camelCase`, `[SerializeField] private`
- 인스펙터에 노출할 프로퍼티는 `[field: SerializeField] public T Foo { get; private set; }`
- 이벤트는 `On___Event` (`OnDeadEvent`, `OnCastEvent`, `OnTargetLostEvent`)
- 접미사 — `___Base`(상속용 베이스), `___Type`(enum), `___SO`(ScriptableObject), `___Manager`(싱글턴), `___Controller`(Agent 컴포넌트)
- 주석은 한국어. **무엇이 아니라 왜**를 씁니다

## 용어

**파츠**와 **모듈**을 섞어 쓰지 마세요. 층이 다릅니다.

| 용어 | 무엇 | 슬롯 | 데이터 | 프리팹 |
|---|---|---|---|---|
| **파츠** | 기체를 구성한다 | `PartSlotType` | `PartDataSO` | **필수** |
| **모듈** | 파츠에 얹힌다 | `ModuleSlotType` | `ModuleDataSO` | 선택 |
| **개량** | 수치를 단계별로 손본다 | 없음 | `UpgradeSO` | 없음 |

구분 기준은 **"비주얼이 유닛 실루엣을 바꾸는가"**입니다. 코어·팔·추진체가 파츠고, 스코프·냉각기가 모듈입니다.

"파츠마다 모듈이 더 붙는다"가 한 줄 요약입니다. 자세한 것은 [PIVOT.md](PIVOT.md#용어)에 있습니다.

## 반복 패턴

### 키를 고를 때

| | 방식 | 예 |
|---|---|---|
| 개수가 정해져 있고 코드가 이름으로 지목함 | **enum + 코드 생성** | `PoolType` |
| 계속 늘어나고 코드가 이름을 몰라도 됨 | **uint Id + 레지스트리 SO** | `ItemDataSO` |

어느 쪽이든 **한 번 부여한 값은 재사용하지 않습니다.**

### 인터페이스 + MonoBehaviour 베이스

인터페이스에 상태가 필요해지면 짝이 되는 추상 클래스를 둡니다 (`IPoolable` + `PoolableMono`). 인터페이스만으로 밀어붙이지 마세요.

### Base 클래스 우선

유니티는 컴포넌트라 상속 충돌이 잘 안 생깁니다. **구현체가 사실상 하나면 인터페이스보다 클래스가 낫습니다.** 인터페이스는 구현체가 구조적으로 다를 때만.

### SO는 데이터, 로직은 MonoBehaviour

SO에 런타임 상태를 두지 마세요. 에셋이라 **플레이 모드를 나가도 값이 남습니다.**

공유되는 SO에서 인스턴스별 상태를 만들어야 하면 기본값만 읽어와 런타임 객체를 따로 만듭니다 (`WeaponStatDefaults` → `WeaponStatus`).

### 컨트롤러가 한 곳에서

같은 종류의 처리(스탯 적용, 오브젝트 생성)를 두 경로로 나누지 마세요. 예외 케이스가 생기면 그 경로만 조용히 어긋납니다.

## 유니티 관행

- 핫패스는 `foreach` 대신 인덱스 `for`. 물리 질의는 논알로케이션 (`ContactFilter2D` + 버퍼)
- **구체 타입 `GetComponent`가 인터페이스 조회보다 빠릅니다.** 프레임마다 도는 곳에선 구체 타입으로
- **Unity 오브젝트에 `?.`(null 전파)를 쓰지 마세요.** 파괴된 오브젝트의 가짜 null을 통과시킵니다. `if (x != null)`로 쓰세요
- 기즈모·검증은 `#if UNITY_EDITOR`
- 에디터 스크립트는 `Editor/` 하위 폴더 (별도 어셈블리로 들어감)
- 인스펙터 전용 필드의 CS0649 경고는 `#pragma warning disable CS0649`로 그 블록만 끕니다
- `transform.localPosition`/`localRotation`을 연달아 쓰지 말고 `SetLocalPositionAndRotation`
- 풀링 대상은 `OnSpawn`/`OnDespawn`에서 **상태를 전부 되돌려야** 합니다 (체력, TargetType, 타이머, 이벤트 구독)

## 에디터 툴

데이터가 손으로 관리하기 힘들어지는 지점에 툴을 만듭니다. 지금 있는 것:

- `Tools > Pool Manager` — 풀 등록, `PoolType` 생성
- `Tools > Item Creator` — 아이템 SO 생성, 이름 규칙 일괄 적용
- `ItemDatabase` 인스펙터 — 프로젝트 스캔 & Id 부여, Id 대역 편집, **Id 충돌 해결**, 전체 재부여
- `Tools > Asset Highlight Settings` — 에셋 색칠 팔레트. 프로젝트 창에서 `Ctrl+/`
- `Tools > Item Database > Rebuild` — 프로젝트 스캔 & Id 부여. 인스펙터 버튼과 같은 일을 메뉴로도 부를 수 있게 한 것
- 하이어라키 장식 (`HierarchyRowDecorator`, `HierarchyActiveToggle`) — 계층선, 휠클릭 활성 토글, 컴포넌트 아이콘
- `Tools > Part Forge` — **예정.** 스프라이트에서 파츠 SO + 프리팹 생성, 소켓·Pivot 조정 ([PIVOT.md](PIVOT.md#part-forge-에디터-툴))

`Assets/01.Scripts/Core/EditorTools/Editor/`의 것들은 데이터가 아니라 **작업 편의**를 위한 것이라 게임 코드에 아무것도 남기지 않습니다. 저장이 필요하면 EditorPrefs를 씁니다 — 에셋으로 두면 개인 취향이 git diff로 올라옵니다.

**인스펙터 버튼이 에셋을 고치면 그리는 도중에 실행하지 마세요.** 항목 수가 달라져 레이아웃이 어긋납니다. 버튼은 할 일을 필드에 적어두고, `OnInspectorGUI` 끝에서 한 번에 실행합니다 (`ItemDatabaseSOEditor._pendingAction`).

**툴이 유일한 편집 경로면 인스펙터는 읽기 전용으로 막으세요.** 양쪽에서 편집하면 Id 관리가 꼬입니다 (`PoolListSOEditor`, `ItemDatabaseSOEditor`가 그렇게 되어 있습니다).

에디터 툴에서 값을 쓸 때는 `SerializedProperty`를 쓰세요. Undo와 dirty 처리가 자동으로 됩니다. 런타임 코드에 공개 setter를 만들 필요도 없어집니다.

## 아이템 에셋 이름

`Item_{종류}_{이름}` — 종류는 SO 타입명에서 `DataSO`를 뗀 것.

```
PlayerWeaponDataSO + "Glock7"  →  Item_PlayerWeapon_Glock7
```

`Tools > Item Creator`가 자동으로 붙이고, "이름 규칙 일괄 적용"으로 기존 에셋도 맞춥니다.

**에셋 이름의 재료는 `_editorName`(에디터 식별자)입니다.** 화면에 나가는 이름이 아니므로 영문으로 짧게 씁니다. 표시명과 로컬라이즈 키는 따로입니다 — [ARCHITECTURE.md](ARCHITECTURE.md#이름은-세-겹이다)를 보세요.

**화면에 문자열을 낼 때는 `Localization.Get`을 통합니다.** 아이템은 `DisplayName`/`Description`이 이미 그렇게 되어 있습니다. SO 필드를 직접 읽으면 나중에 시트를 붙여도 그 자리만 안 바뀝니다.
