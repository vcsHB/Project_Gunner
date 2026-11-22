# 피드백 — 이펙트·사운드 재생

전투 로직은 이벤트만 쏘고, 이펙트·소리는 그 이벤트를 듣는 쪽이 재생합니다 ([RULES](../RULES.md#멀티플레이-대비) — 시뮬레이션과 표현 분리).
계획과 남은 단계는 [MILESTONE M3](../MILESTONE.md#m3--손맛)에 있습니다.

## 진입점은 둘

```
EffectPlayer.Play(PoolType, position, direction 또는 angle, follow) → EffectHandle
SoundManager.Play(SoundDataSO, position, follow)                   → SoundHandle
```

`direction`은 **이펙트가 뿜어져야 할 월드 방향**입니다. 아트가 어느 축으로 만들어졌는지는 호출하는 쪽이 몰라도 됩니다.

**둘 다 위치·방향·데이터만 받습니다. 누가 냈는지는 모릅니다.** 멀티에서 이벤트를 받은 쪽이 각자 로컬에서 같은 함수를 부르게 하려는 것입니다.

**무엇을 재생할지는 데이터가 정합니다.** 무기·탄·코어 SO가 이펙트(`PoolType`)와 소리(`SoundDataSO`)를 각각 들고 있습니다. 둘을 하나로 묶는 큐(Cue) SO는 **아직 만들지 않습니다** — 같은 조합이 반복되면 그때 올립니다.

`PoolType.None` / `null` 데이터는 "없음"입니다. 호출하는 쪽에서 검사하지 않아도 됩니다.

## 핸들

루프 이펙트·소리(부스터, 활공, 과열 경보)는 끝을 알아야 합니다. `Play`가 돌려주는 핸들로 `Stop()`합니다.

- **소유자의 `Dispose`/`OnDespawn`에서 반드시 `Stop()`.** 빠뜨리면 루프가 남습니다
- 핸들은 `(대상, SpawnVersion)` 쌍입니다. 대상이 풀로 돌아가 **다른 곳에서 다시 꺼내졌으면 무효**가 되어 남의 것을 끄지 않습니다
- 걸러진 재생(상한·간격·거리)은 무효 핸들을 줍니다. 무효 핸들의 `Stop()`은 아무것도 하지 않습니다

## 이펙트 — `ParticleEffect`

| 항목 | 내용 |
|---|---|
| 구조 | 루트는 빈 Transform이어도 된다. 자식 `ParticleSystem`을 전부 모아 한꺼번에 재생·정지한다 |
| 방향 | **이펙트 아트는 +Y가 발산 방향**(`_artForwardAngle` = 90). 투사체·파츠(+X)와 다르다 — 보정은 `ParticleEffect.SetAngle`에서 한 번 |
| 반납 | 방출이 끝난 뒤 살아있는 파티클이 0이면 스스로 `Release()`. 방출 끝 = 버스트만 있으면 **마지막 버스트 시각**, Rate over Time이 있으면 Duration 또는 `Stop()`. 아트의 Duration(기본 5초)을 줄이지 않아도 버스트형은 바로 돌아간다 |
| 첫 프레임 | 재생을 건 프레임엔 시뮬레이션이 아직 안 돌아 0개일 수 있다. 2프레임째부터 센다 |
| 루프 | 끝이 없으니 `EffectBase._lifetime` 안전장치를 걸지 않는다. 핸들로 `Stop()` → 방출만 멈추고 남은 파티클이 사라지면 반납 |
| 루프가 아닌 것 | `_lifetime`이 상한(기본 3초, 긁힘은 8초). 설정 실수로 안 끝나도 풀이 마르지 않는다 |
| 재생 시점 | `Awake`에서 모든 시스템의 `playOnAwake`를 끄고 `stopAction`을 None으로. **위치를 잡은 뒤** `EffectPlayer`가 `Begin`한다 |
| 따라가기 | `follow`를 **부모로 붙이지 않는다.** `LateUpdate`에서 위치만 맞춘다(회전은 안 따라간다). 대상이 꺼지면 그 자리에서 방출을 멈춘다 |
| 정렬 | `ParticleSystemRenderer`의 Sorting Layer를 프리팹에서 지정. 기본값이면 스프라이트 뒤로 숨는다 |
| 등록 | 이펙트 프리팹 하나 = `PoolType` 하나. Pool Manager 창의 `Effect` 그룹에 넣고 Generate Enum, 프리셋에 추가 |

### 등록된 이펙트

| PoolType | 쓰는 곳 | 방향 |
|---|---|---|
| `FX_DefaultHit` | `HitFeedback` 몸체 | 공격이 온 쪽(공격 방향의 반대) |
| `FX_ShieldHit` | `HitFeedback` 실드 | 실드 외곽의 바깥 법선 |
| `FX_GunFire_1` | `WeaponFeedback` 발사 | 탄이 나가는 방향. 총구를 따라간다 |
| `FX_GroundScratch` | `SkidFeedback` | 진행 방향의 반대. 발밑을 따라가며 계속 뿜는다 |

## 사운드 — `SoundPlayer` 풀

**풀은 `SoundPlayer` 하나뿐입니다.** 소리마다 프리팹을 만들지 않고, 클립은 재생할 때 넣습니다. `SoundPlayer`를 직접 꺼내지 말고 `SoundManager.Play`를 쓰세요 — 동시 재생 수를 거기서 셉니다.

`SoundManager`는 데이터별로 **지금 몇 개가 울리는지와 마지막 재생 시각**만 듭니다. 플레이어별 상태가 없어서 싱글턴이어도 RULES에 걸리지 않습니다. 씬의 `Managers/SoundManager`에 있습니다.

| 거르기 | 동작 |
|---|---|
| 최소 간격 `_minInterval` | 같은 데이터를 이 간격 안에 다시 틀면 버린다. 산탄이 한 프레임에 여러 발 맞을 때의 겹침 |
| 동시 재생 상한 `_maxConcurrent` | 넘치면 가장 오래된 것을 끊고 튼다(`_stealOldest`). 끄면 새 것을 버린다 |
| 들리는 거리 `_audibleRange` | 귀에서 이 거리 밖이면 **재생하지 않는다.** 루프는 거르지 않는다 — 다가오면 들려야 한다 |

### 들리는 거리는 소리의 크기다

화면 밖 소리도 들립니다. 대신 거리에 따라 줄어듭니다 (`AudioRolloffMode.Linear`).

- `_fullVolumeRange`까지는 최대 볼륨, `_audibleRange`에서 0
- **큰 소리(폭발·대구경)는 `_audibleRange`를 넓게, 작은 소리(빈 탄창)는 좁게** 잡습니다. 화면 밖 위협을 소리로 알 수 있는지는 이 값으로 정해집니다
- 도플러는 끕니다. 탑다운에서 의미가 없고 빠른 투사체에서 피치가 튑니다

### 귀는 기체에 있다 — `AudioListenerFollow`

카메라는 조준점 쪽으로 치우쳐 있어서 거기서 들으면 좌우가 어긋납니다. 귀는 **내 기체 위치**를 따라갑니다.

- **기체의 자식으로 두지 않습니다.** 기체가 돌면 귀도 돌아 좌우가 뒤집힙니다. 회전은 항상 고정입니다
- 기체 위 `-Z`로 `_height`(4)만큼 띄웁니다. 같은 평면이면 바로 옆의 소리(내 총구)가 한쪽 귀로 쏠립니다. 그래서 `_fullVolumeRange` 기본값(5)이 이보다 큽니다
- 메인 카메라의 `AudioListener`는 꺼져 있습니다. 둘이면 유니티가 경고합니다
- 멀티에서는 로컬 플레이어를 `SetTarget`으로 넣어 줍니다

### 믹서 — 아직 없음

`SoundDataSO._mixerGroup` → 비어 있으면 `SoundManager._defaultGroup` → 그것도 비면 믹서를 거치지 않습니다. 믹서 에셋(`Master / BGM / SFX / UI`)은 아직 만들지 않았습니다 ([STATUS](../STATUS.md#그-외)).

## 연출의 난수

피치 흔들기·클립 고르기는 `UnityEngine.Random`을 씁니다. 판정이 아니라서 시드 기반일 필요가 없고, `GameRandom`에 섞으면 호출 순서가 달라져 판정 재현이 깨집니다 ([RULES](../RULES.md#멀티플레이-대비) 예외).

## 피드백 컴포넌트

**그 이벤트를 쏘는 것과 같은 프리팹에 붙입니다.** 적도 같은 경로를 쓰므로 적·플레이어 구분이 없습니다.

의존 방향: **피드백 컴포넌트 → 이벤트를 쏘는 쪽**, **피드백 컴포넌트 → `EffectPlayer`/`SoundManager`**. 무기·`Health`·이동은 피드백을 모릅니다.

| 컴포넌트 | 어디에 | 듣는 이벤트 | 하는 일 |
|---|---|---|---|
| `WeaponFeedback` | 무기 프리팹(`PlayerRangedWeapon`과 같은 오브젝트) | `OnFiredEvent`, `OnDryFireEvent` | 총구 화염(총구를 따라감), 발사음, 빈 격발음 |
| `HitFeedback` | 유닛 루트(`Health`와 같은 오브젝트) | `Health.OnDamagedEvent` | 몸체/실드 피격 이펙트·소리 |
| `SkidFeedback` | 플레이어 루트(`PlayerMoveController`와 같은 오브젝트) | `OnSkidStartedEvent` + 매 프레임 `IsSkidding` | 바닥 긁힘. 한 번 켜서 발밑을 따라가고, 끝나면 `Stop` |
| `GlideFeedback` | 플레이어 루트 | `OnMoveModeChangedEvent` + 매 프레임 `Altitude` | 몸체 띄우기·그림자·정렬, 이륙·착륙·활공 루프 이펙트·소리(에셋 칸은 비어 있음). [input](input.md#고도--몸체를-띄운다) |
| `ShieldFeedback` | 실드 프리팹(`UnitShield`, `ShieldHitArea`와 같은 오브젝트) | `OnShieldBrokenEvent` / `OnShieldChangedEvent`(0 → 0 초과) | 깨질 때 — 그림은 `ShieldHitArea`가 즉시 끄므로 **복제본**(재사용)을 번쩍이며 키우고 흩어지게. 돌아올 때 — 원본을 작게 시작해 넘쳤다 돌아오게. 죽어서 0이 된 건 깨짐이 아니다(이벤트 없음). 화면 흔들림은 `ShieldBreakCameraShake`(플레이어만, 부위파괴보다 약하게). 이펙트·소리 칸은 비어 있음 |
| `PartBreakFeedback` | 유닛 루트(`MechaPartHealth`와 같은 오브젝트) | `OnPartDestroyedEvent` / `OnPartRestoredEvent` | 부서진 부위의 스프라이트를 어둡게 복제해 날려 보내고(`PartDebris`, 풀링 안 함) 원본은 숨긴다. 복구되면 다시 켠다. 화면 흔들림은 카메라 쪽 `PartBreakCameraShake`(플레이어만) |

**피격은 맞는 쪽에서 냅니다.** 투사체 쪽(`ProjectileFeedback`)이 아닌 이유 — 실드에 맞았는지 몸체에 맞았는지는 맞는 쪽만 압니다. 근접·폭발도 같은 이벤트로 옵니다. 투사체 자체의 연출(사거리 끝 소멸, 폭발)은 나중에 `ProjectileFeedback`으로.

**실드 피격 위치** — 판정은 몸체 콜라이더 그대로입니다. 맞은 지점에서 공격이 온 쪽으로 되짚어 **실드 원과 만나는 점**으로 옮겨서 터뜨립니다. 반지름은 `HitFeedback._shieldRadius`, 0이면 `_shieldCenter`(플레이어의 `Shield`) 아래 SpriteRenderer 크기로 잽니다. 실드 그림이 없는 유닛(`Enemy_Test`)은 맞은 지점에서 그대로 터집니다.

> **실드 원이 판정입니다** (2026-11-17, [mecha](mecha.md#실드)). 실드가 있으면 원에 먼저 맞으므로 맞은 지점이 이미 외곽 근처입니다. 위 되짚기는 원이 없는 경로(근접 캐스터가 안쪽에서 걸린 경우 등)의 보정으로 남습니다.

**빠뜨리면 조용히 안 나옵니다.** 무기 프리팹은 Part Forge가 만들지 않으므로(코어·추진체만) 손으로 붙여야 합니다. 지금 붙은 곳: `Part_Weapon_Scatter`, `PlayerWeapon_Scatter`, `PlayerWeapon_IronSpear`, `Enemy_Test`, `TestEnemy`, 씬의 `Player`.

카메라 흔들림은 예외로 카메라 쪽에 남습니다. 내 화면만 흔들려야 하므로 플레이어만 구독하는 게 맞습니다.

## 테스트 에셋

재생 구조 검증용입니다. 실제 소리가 들어오면 지웁니다. (테스트 이펙트 `FX_Test_Burst`는 지웠습니다 — `PoolType` 4번은 재사용하지 않습니다.)

- `06.Audio/SFX/SFX_Test_Beep.wav`, `08.SO/Sound/Sound_Test_Beep`
