알겠습니다. 단일 player 전제로 다이어트했던 v3.3을 “여러 캐릭터(players[])를 등록해서 일괄 스탑 밈, 일괄 복귀”가 가능한 구조로 되돌리면 됩니다. 아래는 v3.3 스타일을 유지하면서, 멀티 플레이어 기준으로 바로 쓸 수 있는 최종 설계서 형태로 다시 정리한 버전입니다. 불필요한 확장 항목은 넣지 않았습니다.

# AnimationPlayerManager 설계서 (v4.0 - 최종)

## 1. 목표

여러 3D 캐릭터를 Singleton 매니저에서 관리하며, 호출 시 등록된 모든 캐릭터를 랜덤 포즈로 정지시키는 스톱 밈 효과를 구현한다. 반복 호출로 포즈가 계속 바뀌는 연출이 가능하며, 마지막 호출 후 3초간 추가 호출이 없으면 자동으로 각 캐릭터가 AnimatorController 기반 재생으로 복귀한다.

## 2. 핵심 기술

| 기술                      | 용도                       |
| ----------------------- | ------------------------ |
| PlayableGraph           | 캐릭터별 그래프 생성, 재사용, 평가, 파괴 |
| AnimationClipPlayable   | 클립 래핑, 시간 점프, 정지 유지      |
| AnimationPlayableOutput | Animator에 출력 연결          |
| graph.Evaluate(0)       | 특정 시점 포즈 즉시 반영           |
| Coroutine               | 반복 호출 시퀀스                |
| Update 폴링               | 자동 해제 타이머                |

### 설계 원칙

* 그래프는 캐릭터별로 최초 1회 생성 후 재사용한다.
* StopAtRandomMoment는 Meme 모드 여부와 상관없이 호출될 때마다 모든 캐릭터의 포즈를 갱신한다.
* 스톱 밈 모드에서는 clipPlayable speed 0으로 시간 진행을 차단한다. (매 호출마다 재설정)
* 복귀는 graph.Destroy를 기본으로 사용하며, Rebind는 사용하지 않는다.

## 3. 데이터 구조

### 3.1 PlayerRuntime

캐릭터별 런타임 데이터 컨테이너.

| 필드                   | 타입                      | 용도          |
| -------------------- | ----------------------- | ----------- |
| animator             | Animator                | 대상 Animator |
| graph                | PlayableGraph           | 재사용 그래프     |
| output               | AnimationPlayableOutput | 출력 연결       |
| clipPlayable         | AnimationClipPlayable   | 클립 재생용      |
| clipsCache           | List<AnimationClip>     | 필터링된 클립 캐시  |
| controllerInstanceId | int                     | 캐시 무효화 판단용  |
| rootPositionBackup   | Vector3                 | 위치 백업       |
| rootRotationBackup   | Quaternion              | 회전 백업       |
| isGraphCreated       | bool                    | 그래프 생성 여부   |
| isRootBackedUp       | bool                    | 루트 백업 여부    |

### 3.2 Manager 필드

| 필드               | 타입                                    | 용도          |
| ---------------- | ------------------------------------- | ----------- |
| Instance         | static                                | Singleton   |
| players          | List<GameObject>                      | 관리 대상 목록    |
| runtimeMap       | Dictionary<GameObject, PlayerRuntime> | 캐릭터별 런타임 매핑 |
| lastCallTime     | float                                 | 마지막 호출 시각   |
| isMemeModeActive | bool                                  | Meme 모드 여부  |
| memeCoroutine    | Coroutine                             | 반복 코루틴 참조   |
| RELEASE_DELAY    | const float                           | 3.0f        |
| DEFAULT_INTERVAL | const float                           | 0.1f        |
| DEFAULT_COUNT    | const int                             | 100         |

## 4. 메서드 구성

### 4.1 등록 관리

| 메서드              | 입력                      | 역할                                   |
| ---------------- | ----------------------- | ------------------------------------ |
| Init             | void                    | 초기화                                  |
| RegisterPlayer   | GameObject              | 목록 추가, Animator 검증, runtime 생성       |
| RegisterPlayers  | IEnumerable<GameObject> | 다수 등록                                |
| UnregisterPlayer | GameObject              | 해당 캐릭터 그래프 파괴 후 목록 및 runtimeMap에서 제거 |
| ClearPlayers     | void                    | 전체 그래프 파괴 후 players 및 runtimeMap 정리  |

### 4.2 반복 호출 제어

| 메서드               | 입력                        | 역할                   |
| ----------------- | ------------------------- | -------------------- |
| StartStopMeme     | float interval, int count | 반복 시작                |
| StopMemeCoroutine | float interval, int count | 반복 실행 (Coroutine)    |
| StopStopMeme      | void                      | ReleaseAllPlayers 호출 |

### 4.3 핵심 기능

| 메서드                          | 반환   | 역할                                   |
| ---------------------------- | ---- | ------------------------------------ |
| StopAtRandomMoment           | void | 모든 캐릭터 포즈 갱신 진입점                     |
| ValidatePlayer               | bool | 개별 캐릭터 유효성 검증                        |
| EnsureClipsCache             | void | 개별 캐릭터 캐시 없거나 controller 변경 시 갱신     |
| EnsureGraph                  | void | 개별 캐릭터 그래프 없으면 생성, 있으면 재사용           |
| BackupRootTransformIfNeeded  | void | 개별 캐릭터 Meme 진입 시 1회 백업               |
| ApplyRandomPoseAndFreeze     | void | 개별 캐릭터 랜덤 포즈 적용 + speed 0 (매 호출 재설정) |
| RestoreRootTransformIfNeeded | void | 개별 캐릭터 루트 복원                         |

### 4.4 자동 복귀

| 메서드               | 역할                            |
| ----------------- | ----------------------------- |
| Update            | 폴링, 3초 경과 시 ReleaseAllPlayers |
| ReleaseAllPlayers | 코루틴 정리, 전체 그래프 파괴, 상태 리셋      |
| ReleasePlayer     | 개별 그래프 파괴 및 상태 리셋             |

#### ReleaseAllPlayers 리셋 범위

* memeCoroutine 유효 시 중단 및 null 처리
* runtimeMap 전체 순회하며 ReleasePlayer 수행
* isMemeModeActive = false

#### ReleasePlayer 리셋 범위

* graph.Destroy()
* graph, output, clipPlayable 무효화
* isGraphCreated = false
* isRootBackedUp = false

### 4.5 생명주기

| 메서드               | 역할                         |
| ----------------- | -------------------------- |
| Awake             | Singleton 설정               |
| Start             | Init 호출                    |
| OnDestroy         | 매니저 오브젝트 파괴 시 그래프 및 리소스 정리 |
| OnApplicationQuit | 앱 종료 시 그래프 및 리소스 정리        |

## 5. 호출 흐름

### 흐름 1: 반복 연출

```
StartStopMeme(interval, count)
    ├─ 기존 memeCoroutine 중단
    └─ StopMemeCoroutine 시작
            ├─ count회 반복
            │   ├─ StopAtRandomMoment
            │   └─ WaitForSeconds(interval)
            └─ 종료
```

### 흐름 2: 단발 호출

```
StopAtRandomMoment
    ├─ isMemeModeActive = true
    ├─ foreach player in players
    │     ├─ if ValidatePlayer(player) == false → continue
    │     ├─ EnsureClipsCache(player)
    │     ├─ if clipsCache 비어있음 → continue
    │     ├─ EnsureGraph(player) (없으면 생성, 있으면 재사용)
    │     ├─ BackupRootTransformIfNeeded(player)
    │     ├─ ApplyRandomPoseAndFreeze(player)
    │     │     ├─ 랜덤 clip 선택 (clipsCache에서)
    │     │     ├─ 랜덤 time (0 ~ clip.length)
    │     │     ├─ clipPlayable에 clip 설정
    │     │     ├─ SetTime(time)
    │     │     ├─ graph.Evaluate(0)
    │     │     └─ clipPlayable speed = 0 (매 호출 재설정)
    │     └─ RestoreRootTransformIfNeeded(player)
    └─ lastCallTime = Time.time
```

### 흐름 3: 자동 복귀

```
Update
    └─ if isMemeModeActive && Time.time - lastCallTime >= 3.0f
           └─ ReleaseAllPlayers
```

### 흐름 4: 즉시 중단

```
StopStopMeme
    └─ ReleaseAllPlayers
```

## 6. 클립 필터링 규칙

EnsureClipsCache 적용 조건.

| 조건                     | 제외 사유 |
| ---------------------- | ----- |
| clip.length < 0.1f     | 너무 짧음 |
| clip.name에 Face 포함     | 얼굴 전용 |
| clip.name에 Blink 포함    | 눈 깜빡임 |
| clip.name에 Eye 포함      | 눈 관련  |
| clip.name에 Additive 포함 | 레이어용  |

### 캐시 무효화

* controllerInstanceId 변경 시 재구축
* 중복 clip 제거

## 7. 안정성 체크포인트

| 상황                           | 대응                                 |
| ---------------------------- | ---------------------------------- |
| player 미설정, 비활성, Animator 없음 | ValidatePlayer false로 스킵           |
| clipsCache 빈 상태              | 해당 캐릭터 스킵                          |
| 루트모션 위치 튐                    | 백업 및 복원                            |
| 복귀 시 포즈 튐                    | graph.Destroy만 사용                  |
| 중복 StartStopMeme             | 기존 코루틴 중단 후 재시작                    |
| Controller 변경                | controllerInstanceId로 캐시 무효화       |
| 그래프 잔존                       | OnDestroy 및 OnApplicationQuit에서 정리 |

## 8. 외부 인터페이스

| 메서드                                      | 용도                        |
| ---------------------------------------- | ------------------------- |
| RegisterPlayer(GameObject)               | 캐릭터 등록                    |
| RegisterPlayers(IEnumerable<GameObject>) | 캐릭터 일괄 등록                 |
| UnregisterPlayer(GameObject)             | 캐릭터 해제                    |
| ClearPlayers()                           | 전체 해제                     |
| StartStopMeme(interval, count)           | 반복 효과 시작 (기본값: 0.1f, 100) |
| StopAtRandomMoment()                     | 단발 포즈 변경                  |
| StopStopMeme()                           | 즉시 복귀                     |

## 9. 핵심 설계 요약

| 항목    | 결정                               |
| ----- | -------------------------------- |
| 구조    | Singleton + players 목록           |
| 그래프   | 캐릭터별 1회 생성, 재사용                  |
| 포즈 갱신 | 매 호출마다 전체 캐릭터 항상 갱신              |
| 정지 유지 | clipPlayable speed 0 (매 호출 재설정)  |
| 타이머   | Update 폴링                        |
| 복귀    | ReleaseAllPlayers (코루틴 + 그래프 정리) |
| 루트모션  | 백업 및 복원                          |
