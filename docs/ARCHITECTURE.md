# 아키텍처

시스템별 구조와 **왜 그렇게 했는지**. 구현 현황은 [STATUS.md](STATUS.md)를 보세요.

> **이 문서들은 지금 코드를 설명합니다.** 2026-10-25에 메카로 기획이 바뀌었고([PIVOT.md](PIVOT.md)) 일부 절은 전환 대상입니다. 해당 절 머리에 ⚠ 로 표시해 두었습니다.

**본문은 시스템별 파일로 나뉘어 있습니다. 건드리는 시스템의 파일만 읽으세요.** 새 절을 쓸 때도 맞는 파일에 넣고, 아래 표의 "절" 칸을 갱신합니다. 어느 파일에도 맞지 않으면 새 파일을 만들고 표에 한 줄 추가합니다.

| 파일 | 절 | 이럴 때 읽기 |
|---|---|---|
| [arch/combat-core.md](arch/combat-core.md) | Agent 컴포지션 · 타게팅과 전투 판정 · 스탯 · 데미지 · 상태이상 · 오브젝트 풀 · 편 가르기 | 컴포넌트 추가, 피격·데미지, 레이어, 풀링 |
| [arch/items.md](arch/items.md) | 아이템 데이터(Id 대역, 이름 세 겹, 로컬라이제이션, 아이콘, 개체 상태) · 내구도와 아이템 사용 | 아이템 SO, Id, 표시명, 인벤토리 |
| [arch/mecha.md](arch/mecha.md) | 기체 파츠(장착→스탯, 진입 시 채우기) · 기체 자원(전력·연료) | 파츠 장착, 로드아웃, 생존 수치 |
| [arch/weapon.md](arch/weapon.md) | 무기 모딩(개량 vs 모듈) · 탄약(StatDelta, 잔탄) · 과열 | 무기 수치, 모듈, 탄, 열 |
| [arch/projectile.md](arch/projectile.md) | 발사와 투사체(+X 규칙, 리소스 방향, CastPower, 발사 모드, 프리팹 구조) | 발사·투사체, 스프라이트 방향 |
| [arch/enemy.md](arch/enemy.md) | 적(머리/다리, 상태 클래스, 선딜, 적도 메카다, AI 방아쇠, 구경 표) | 적 AI, 적 로드아웃 |
| [arch/input.md](arch/input.md) | 조준 · 카메라 · 팔에 달린 것이 입력을 해석한다 · 입력 차단 · 입력 · 이동(착륙/활공) | 입력, 조준점, 카메라 흔들림 |

## 파일 사이의 연결

한 파일만 읽고 고치면 놓치기 쉬운 곳입니다.

- **스탯 흐름** — 파츠 장착([mecha](arch/mecha.md))과 무기 개량([weapon](arch/weapon.md))은 모두 [combat-core의 스탯](arch/combat-core.md#스탯) modifier로 들어갑니다.
- **무기 = 아이템** — 모듈·개량의 주인은 `WeaponItemInstance`([weapon](arch/weapon.md#모듈개량의-주인은-weaponiteminstance))이고, 이는 [items의 개체 상태](arch/items.md#개체-상태)(`ItemInstance`, `instanceId`) 구조 위에 있습니다. 잔탄은 적재함(인벤토리)에 있습니다.
- **발사 경로** — 입력([input](arch/input.md)) → 팔 → 무기 과열·탄([weapon](arch/weapon.md)) → 투사체([projectile](arch/projectile.md)) → 판정·데미지([combat-core](arch/combat-core.md)).
- **적은 플레이어와 같은 길** — 조립은 [mecha](arch/mecha.md), 방아쇠만 AI가 당깁니다([enemy](arch/enemy.md#방아쇠는-ai가-민다)).
- **편 가르기** — 캐스터가 때릴 편(`_hitFactions`)을 고르는 규칙은 [combat-core](arch/combat-core.md#편-가르기-faction)에 있습니다. 투사체나 적의 판정을 고칠 때 같이 보세요.
