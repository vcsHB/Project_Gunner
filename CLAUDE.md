# Project_Gunner

2D 탑다운 샌드박스 슈터. Unity 6000.3.19f1 / URP 2D / Input System / Physics2D / DOTween.

## 문서

| 문서 | 내용 | 언제 읽나 |
|---|---|---|
| [docs/MILESTONE.md](docs/MILESTONE.md) | 무엇을 언제 만들지 | 다음 할 일을 고를 때 |
| [docs/STATUS.md](docs/STATUS.md) | 시스템별 개발 현황 | 작업 시작 전 — **건드릴 시스템의 표만** (`## ` 제목으로 찾기) |
| [docs/BACKLOG.md](docs/BACKLOG.md) | 미뤄둔 작업과 그 이유 | 뭘 할지 고를 때 |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | 목차 + 파일 사이 연결. 본문은 `docs/arch/*.md` | 기존 시스템을 건드릴 때 — **목차를 보고 해당 파일만** |
| [docs/CONVENTIONS.md](docs/CONVENTIONS.md) | 네이밍, 코드 스타일, 반복 패턴 | 코드를 쓸 때 |
| [docs/RULES.md](docs/RULES.md) | 어기면 나중에 비싸지는 제약 | 아래 "핵심 규칙"에 걸리는 작업일 때 해당 절만 |
| [docs/INCIDENTS.md](docs/INCIDENTS.md) | 실제로 겪은 함정과 결론 | 비슷한 걸 만들 때 |
| [docs/PIVOT.md](docs/PIVOT.md) | 메카 전환 기록 — 용어, 남은 2항목 | 용어가 헷갈리거나 전환 이력이 필요할 때 |

새 시스템을 만들면 STATUS와 ARCHITECTURE(해당 `arch/` 파일 + 목차 표)를, 함정에 빠졌다 나오면 INCIDENTS를 갱신할 것.

### 핵심 규칙

[RULES.md](docs/RULES.md)의 요약입니다. 이 중 하나에 걸리는 작업이면 RULES의 해당 절을 읽으세요.

- **식별자** — Id·enum 값은 재사용 금지. 아이템 Id는 SO 타입별 대역에서. `WeaponStatType`·`VitalType`은 0부터 빈틈없이, 뒤에만 추가. `PoolType.cs`는 자동 생성이라 직접 편집 금지.
- **상태 되돌리기** — 풀링 대상은 `OnDespawn`에서 런타임 상태 전부 리셋. 건 modifier는 `Dispose`에서 `RemoveModifier(this)`.
- **멀티플레이** — `Random.value` 직접 호출 금지(시드 기반). 싱글턴에 플레이어별 상태 금지. 이펙트·사운드·카메라는 이벤트 구독으로.
- **데이터** — `ItemCategoryType`으로 사용처 판정 금지(표시 전용). 개체 상태 있는 아이템은 `AddStack`. modifier origin에 박싱된 enum/struct 금지.
- **조립** — 소켓은 프리팹 Transform으로. +X 보정은 `PartView` 루트에서 한 번. 스탯 적용 경로는 하나. 루트 Animator로 파츠 경로 애니메이트 금지.
- **물리** — 캐스터에 LayerMask 직접 금지(`TargetType` + `TargetLayerTableSO`). 레이어 변경은 충돌 매트릭스에도 걸린다.

**메카 전환은 끝났고(M0), 적도 살아 움직입니다(M1).** 코드는 메카 기준입니다 — 핫바·재장전·소모품·정조준은 없습니다. 다음 목표는 [MILESTONE.md](docs/MILESTONE.md)의 **M2 — 한 판이 끝난다**(실드·부위파괴·활공/착륙·맵·죽음·새 판)입니다.

## 기획 방향

전투와 제작·건설이 함께 도는 샌드박스입니다. 최종적으로 **멀티플레이**를 목표로 합니다.

**전투** — 조립식 **메카 유닛**을 조종합니다. 기체는 코어 / 좌무기 / 우무기 / 추진체 네 파츠로 구성되고, 파츠마다 모듈이 더 붙습니다. 좌클릭이 좌무기, 우클릭이 우무기입니다.

무장은 **진입할 때 들고 간 것만** 씁니다. 교체는 안전지대에서만 가능합니다. 재장전은 없고 **과열**이 연사를 제한합니다.

**모딩**이 핵심입니다 — 모듈 장착과 수치 개량. 용어는 [PIVOT.md](docs/PIVOT.md#용어)에 정의되어 있습니다.

**샌드박스** — 절차적 맵 생성, 블럭 파괴와 설치. 파괴는 **무기가 아닌 도구**로 합니다. 도구는 팔 슬롯을 무기와 공유하지만, **판정은 처음부터 분리**해서 설계할 것.

**성장** — 필드에서 파밍해 들고 나오는 **탈출형 루프**. 적재함, 숙련도, 테크트리. 탐험 요소와 보스.

## 컴파일 확인

유니티 에디터가 열려 있으면 저장 시 자동 컴파일됩니다. 콘솔로 확인하세요.

CLI로 확인하려면:

```bash
dotnet build Assembly-CSharp.csproj -v:q -nologo -clp:ErrorsOnly          # 런타임
dotnet build Assembly-CSharp-Editor.csproj -v:q -nologo -clp:ErrorsOnly   # 에디터
```

경고까지 봐야 할 때만 `-clp:ErrorsOnly`를 뺍니다.

**주의** — `.csproj`는 유니티가 생성하며 **새로 만든 파일은 유니티가 한 번 갱신하기 전까지 포함되지 않습니다.** 파일을 추가한 직후 CLI 빌드가 통과해도 그 파일은 검사되지 않은 것입니다. 자세한 내용과 우회 방법은 [INCIDENTS.md](docs/INCIDENTS.md#csproj가-오래되어-새-파일이-빌드에-빠진다).

## 토큰 절약

### 큰 파일은 읽지 않는다

`.claude/settings.json`이 아래 경로의 Read를 막습니다. Grep·Glob도 같이 막히니 우회 방법을 쓰세요.

| 막힌 것 | 대신 |
|---|---|
| `*.unity`, `*.prefab` | 내용은 MCP (`manage_prefabs action=get_hierarchy`, 컴포넌트 리소스). GUID 참조 찾기는 Bash `grep -rl <guid> Assets --include=*.unity --include=*.prefab` |
| `*.csproj` | 새 파일이 빌드에 들어갔는지는 Bash `grep -c <파일명> Assembly-CSharp.csproj` |
| `Library/`, `Temp/`, `Logs/`, `Assets/11.Fonts/` | 읽을 일 없음 |

SO `.asset`은 막지 않았습니다. 데이터 값을 볼 때는 읽어도 됩니다.

### MCP(MCP for Unity) 사용 규칙

> 2026-11-15에 `CoderGamester/mcp-unity` → `CoplayDev/unity-mcp`로 바꿨습니다.
> 바꾼 이유: 프리팹·SO를 못 고치고, 오브젝트 참조를 못 넣고, 컴포넌트 하나만 읽을 수 없어서
> 매번 일회성 에디터 스크립트를 만들어야 했습니다. ([INCIDENTS.md](docs/INCIDENTS.md))

**읽기는 좁게.** 컴포넌트 하나는 `mcpforunity://scene/gameobject/{id}/component/{name}` 리소스로 읽습니다.
GameObject 전체 덤프는 한 번에 수천 토큰이라 구조만 볼 때도 쓰지 않습니다.

**참조를 넣을 수 있습니다.** `{"guid": "..."}` / `{"path": "Assets/..."}` / `{"instanceID": 123}`.
스프라이트 서브에셋은 `{"guid": "...", "spriteName": "..."}`. **이제 배선 때문에 에디터 스크립트를 쓰지 마세요.**

| 하려는 일 | 도구 |
|---|---|
| 프리팹 수정 (에디터에 안 열고) | `manage_prefabs` `action=modify_contents` + `component_properties` |
| 씬 오브젝트의 컴포넌트 | `manage_components` (`add` / `remove` / `set_property`) |
| SO 에셋 값 | `manage_scriptable_object` — **YAML 직접 편집 금지** |
| 한 번만 돌릴 에디터 코드 | `execute_code` — 파일을 만들지 않는다 |
| 콘솔 | `read_console` (`error`, 스택트레이스는 필요할 때만) |
| 플레이 모드 | `manage_editor` |
| 여러 수정 | `batch_execute` |

**일회성 에디터 스크립트를 만들지 마세요.** `execute_code`가 메모리에서 컴파일합니다 —
파일 생성 → Refresh → 재컴파일 2회 → 실행 → 삭제의 왕복이 전부 없어집니다.

**플레이 모드 검증** — 에디터가 비활성이면 플레이 모드가 멈춥니다. 값이 두 번 연속 똑같으면
로직을 의심하기 전에 시간이 흐르는지 확인하세요. ([INCIDENTS.md](docs/INCIDENTS.md))

**컴파일 확인** — 에디터가 열려 있으면 MCP로, 닫혀 있으면 위 `dotnet build ... -clp:ErrorsOnly`.

원인을 모르는 버그를 추적할 때는 좁은 조회를 여러 번 하는 것보다 한 번 넓게 보는 게 쌀 수 있습니다.

## 폴더

```
Assets/
├── 00.Scenes/
├── 01.Scripts/
│   ├── CameraManage/
│   ├── Combat/
│   │   ├── Ammo/            구경(enum) + 탄종(SO)
│   │   ├── Caster/          탐색 + 이펙터 분배
│   │   ├── ConditionSystem/ 상태이상
│   │   ├── Enemy/
│   │   ├── Player/          입력, 이동, 조준, Weapon/
│   │   ├── Projectile/      투사체
│   │   └── Unit/
│   ├── Core/                Singleton, ObjectPool, Attributes, Random
│   ├── Effect/  ItemDataSystem/  Map/  Sound/
│   └── UI/                  CanvasCore/, Inventory/
├── 02.Sprites/  03.Prefabs/  04.Animations/  05.Materials/
├── 08.SO/                   Items/, Pool/
├── Resources/               전역 조회가 필요한 SO만
└── 99.Settings/             InputSetting(Controls.inputactions)
```

숫자 접두사 순서를 유지하세요. 새 대분류가 생기면 번호를 이어서 붙입니다.
