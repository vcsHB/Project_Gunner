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

**메카가 아닌 적도 있다.** `LoadoutTable`을 비워두면 조립하지 않는다 — 짐승·드론처럼
파츠로 만들어지지 않는 적이 그렇다. 그런 적은 근접 캐스터로 때린다.

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

