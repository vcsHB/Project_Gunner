# Project_Gunner

2D 탑다운 샌드박스 슈터. Unity 6000.3.19f1 / URP 2D / Input System / Physics2D / DOTween.

## 문서

| 문서 | 내용 | 언제 읽나 |
|---|---|---|
| [docs/MILESTONE.md](docs/MILESTONE.md) | 무엇을 언제 만들지 | 다음 할 일을 고를 때 |
| [docs/STATUS.md](docs/STATUS.md) | 시스템별 개발 현황 | 작업 시작 전 |
| [docs/BACKLOG.md](docs/BACKLOG.md) | 미뤄둔 작업과 그 이유 | 뭘 할지 고를 때 |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | 시스템별 구조와 설계 근거 | 기존 시스템을 건드릴 때 |
| [docs/CONVENTIONS.md](docs/CONVENTIONS.md) | 네이밍, 코드 스타일, 반복 패턴 | 코드를 쓸 때 |
| [docs/RULES.md](docs/RULES.md) | 어기면 나중에 비싸지는 제약 | 항상 |
| [docs/INCIDENTS.md](docs/INCIDENTS.md) | 실제로 겪은 함정과 결론 | 비슷한 걸 만들 때 |
| [docs/PIVOT.md](docs/PIVOT.md) | **메카 전환** — 전환 후 구조와 작업 순서 | 전환이 끝날 때까지 항상 |

새 시스템을 만들면 STATUS와 ARCHITECTURE를, 함정에 빠졌다 나오면 INCIDENTS를 갱신할 것.

**지금 코드는 전환 전 상태입니다.** 총을 든 사람 기준으로 짜여 있고, 메카 구조는 아직 들어가지 않았습니다. 무엇을 건드리든 [PIVOT.md](docs/PIVOT.md)를 먼저 보세요 — 손대려는 시스템이 폐기 대상일 수 있습니다.

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
dotnet build Assembly-CSharp.csproj -v:q -nologo          # 런타임
dotnet build Assembly-CSharp-Editor.csproj -v:q -nologo   # 에디터
```

**주의** — `.csproj`는 유니티가 생성하며 **새로 만든 파일은 유니티가 한 번 갱신하기 전까지 포함되지 않습니다.** 파일을 추가한 직후 CLI 빌드가 통과해도 그 파일은 검사되지 않은 것입니다. 자세한 내용과 우회 방법은 [INCIDENTS.md](docs/INCIDENTS.md#csproj가-오래되어-새-파일이-빌드에-빠진다).

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
