# DLC Local/Addressables 전환 구현 후 작업 정리

## 현재 코드 구현 상태

plan1, plan2 기준으로 코드 쪽은 다음 방향으로 반영했다.

- Local 캐릭터/아이콘은 더 이상 Addressables 로딩 대상으로 보지 않는다.
- `isLocal == true`는 다운로드/Addressables 대상이 아니라 Local 메타데이터로 해석한다.
- `isLocal == false`만 DLC Addressables 대상으로 처리한다.
- 게임 시작 직후에는 이미 다운로드되어 있는 DLC만 `LoadIfExist`로 확인해 `charList`에 추가한다.
- 게임 시작 15초 후에는 다운로드가 필요한 DLC가 있을 때만 기존 다운로드 흐름을 시작한다.
- `LoadIfExist`는 "이미 다운로드되어 있나" 판정용으로만 사용한다.
- Local UI 아이콘과 Local 교체 대상 prefab은 `PrefabDataLocal`에서 key로 찾는다.
- `ChangeCharCardController`, `ChangeCharListSlotController`의 Local 아이콘/클릭 흐름은 Addressables를 거치지 않는다.
- `AddressableManager.LoadLocal` 계열 Local 로더는 제거했다.

## 새로 생긴 구조

### `PrefabDataLocal`

파일:

- `Scripts/PrefabDataLocal.cs`

역할:

- config에 적힌 Local key를 실제 Unity 오브젝트와 연결한다.
- sprite key -> `Sprite`
- prefab key -> `GameObject`

Config 해석:

```json
{
  "name": "Basic",
  "text": "Basic",
  "spriteAddress": "arona_sprite",
  "prefabAddress": "arona",
  "isLocal": true
}
```

`isLocal == true`일 때:

- `spriteAddress`는 Addressables 주소가 아니라 `PrefabDataLocal`의 sprite key
- `prefabAddress`는 Addressables 주소가 아니라 `PrefabDataLocal`의 prefab key

`isLocal == false`일 때:

- `spriteAddress`는 기존처럼 DLC Addressables sprite address
- `prefabAddress`는 기존처럼 DLC Addressables prefab address
- 단, `prefabAddress == "2d_general"`은 예외적으로 Local prefab key로 본다.

## 내가 구현한 코드 기준으로 네가 해야 할 일

### 1. Unity에서 새 스크립트 인식

- Unity Editor를 열어서 `Scripts/PrefabDataLocal.cs`가 정상 import 되는지 확인한다.
- `ChangeCharManager` Inspector에 `Local Prefab Data` 필드가 생겼는지 확인한다.
- 내가 중간에 테스트 실행은 중단했으므로, 실제 컴파일/Play 테스트는 네가 진행한다.

### 2. `PrefabDataLocal` 연결

씬 안의 적절한 GameObject에 `PrefabDataLocal` 컴포넌트를 추가한다.

권장:

- `ChangeCharManager`와 같은 GameObject에 붙이거나
- 별도 Manager GameObject에 붙인다.

그 다음:

- `ChangeCharManager.prefabDataLocal` 필드에 해당 `PrefabDataLocal` 컴포넌트를 연결한다.

### 3. Local sprite key 등록

`PrefabDataLocal`의 Sprites 목록에 config의 Local `spriteAddress` 값과 실제 아이콘 Sprite를 연결한다.

예시:

- key: `arona_sprite`
  - icon: Arona 기본 아이콘
- key: `plana_sprite`
  - icon: Plana 기본 아이콘
- key: `arona_2d_sprite`
  - icon: Arona 2D 아이콘

주의:

- config의 문자열과 Inspector key가 정확히 맞아야 한다.
- 대소문자는 코드에서 크게 민감하지 않게 처리했지만, 사람이 보기 쉽게 config와 Inspector 값을 동일하게 맞추는 편이 좋다.
- key가 누락되면 fallback sprite가 표시되고 warning이 뜬다.

### 4. Local prefab key 등록

`PrefabDataLocal`의 Prefabs 목록에 config의 Local `prefabAddress` 값과 실제 prefab을 연결한다.

예시:

- key: `arona`
  - prefab: Arona 기본 prefab
- key: `plana`
  - prefab: Plana 기본 prefab
- key: `arona_2d`
  - prefab: Arona 2D prefab
- key: `2d_general`
  - prefab: 공용 2D General prefab

주의:

- Local 캐릭터 prefab은 기존 방침대로 `CharManager.charList`에도 등록해 둔다.
- `PrefabDataLocal`은 UI/config key와 prefab을 연결하는 명시적 매핑이다.
- `charList`는 기존 캐릭터 생성/복원 흐름에서 계속 중요하다.

## `2d_general` 작업

`2d_general`은 이번 전환에서 가장 헷갈리기 쉬운 예외 규칙이다.

결정된 규칙:

- `2d_general` prefab 자체는 Local이다.
- `2d_general` prefab은 Addressables에서 직접 로드하지 않는다.
- `PrefabDataLocal`에 반드시 key `2d_general`로 등록한다.
- `CharManager.charList`에도 공용 `2d_general` prefab을 등록한다.
- DLC 2D 캐릭터는 `isLocal == false`일 수 있지만, `prefabAddress == "2d_general"`이면 prefab만 Local에서 가져온다.
- DLC 2D의 animator/sprite/toggle/change clothes 등은 기존처럼 Addressables 주소를 사용할 수 있다.

네가 적어준 진행 순서:

1. 코드에서 Local Addressables 호출 제거
2. Unity에서 테스트
3. Default Local Group 정리
4. 다시 테스트

현재 1번은 코드 기준으로 반영된 상태다.

이제 네가 할 일:

- Unity에서 `2d_general` prefab이 `PrefabDataLocal`에 key `2d_general`로 연결되어 있는지 확인한다.
- `CharManager.charList`에 `2d_general` prefab이 들어 있는지 확인한다.
- Local `2d_general` 관련 항목이 Addressables Default Local Group에 남아 있어도, 먼저 코드 동작 테스트를 진행한다.
- 테스트가 안정적이면 Default Local Group에서 `2d_general` Local 등록을 제거한다.
- 제거 후 다시 테스트해서 Addressables 로드 없이 `2d_general`이 살아 있는지 확인한다.

## Config에서 확인할 것

Local 항목:

```json
{
  "spriteAddress": "PrefabDataLocal에 등록한 sprite key",
  "prefabAddress": "PrefabDataLocal에 등록한 prefab key",
  "isLocal": true
}
```

DLC 일반 prefab 항목:

```json
{
  "spriteAddress": "DLC Addressables sprite address",
  "prefabAddress": "DLC Addressables prefab address",
  "isLocal": false
}
```

DLC 2D general 항목:

```json
{
  "spriteAddress": "DLC Addressables sprite address",
  "prefabAddress": "2d_general",
  "animatorControllerAddress": "DLC Addressables animator address",
  "isLocal": false
}
```

## Unity 테스트 체크리스트

### 시작 직후

- Local 기본 캐릭터가 정상 생성되는지 확인한다.
- `isLocal == true` 항목 때문에 다운로드 UI가 뜨지 않는지 확인한다.
- `isLocal == true` 항목 때문에 `GetDownloadSizeAsync`나 Addressables load warning이 뜨지 않는지 확인한다.
- 이미 다운로드된 DLC가 있다면 시작 직후 `charList`에 추가되는지 확인한다.
- 다운로드되지 않은 DLC가 시작 직후 자동 다운로드되지 않는지 확인한다.

### UI

- Local 카드가 config 기반으로 표시되는지 확인한다.
- Local 아이콘이 `PrefabDataLocal` Sprite로 표시되는지 확인한다.
- Local 아이콘 key가 틀렸을 때 fallback이 표시되고 warning만 뜨는지 확인한다.
- DLC 아이콘은 이미 다운로드되어 있을 때만 표시되고, 없으면 fallback으로 남는지 확인한다.

### 캐릭터 변경

- Local 캐릭터 클릭 시 Addressables 다운로드 없이 prefab이 교체되는지 확인한다.
- Local prefab key가 틀렸을 때 다운로드를 시도하지 않고 warning만 뜨는지 확인한다.
- DLC 캐릭터 클릭 시 기존 다운로드 UI/다운로드/로드 흐름이 유지되는지 확인한다.
- 다운로드 성공 후 DLC prefab이 `charList`에 추가되고 교체되는지 확인한다.

### 2D

- Local 2D 항목이 Addressables 없이 동작하는지 확인한다.
- DLC 2D 항목에서 `2d_general` prefab은 Local에서 가져오고 animator는 Addressables에서 가져오는지 확인한다.
- `PrefabDataLocal`에 `2d_general` key가 빠졌을 때 명확한 error가 뜨는지 확인한다.

## Default Local Group 정리 순서

바로 지우기보다 아래 순서가 안전하다.

1. 현재 상태에서 Unity 테스트
2. Local 캐릭터/아이콘/`2d_general`이 Addressables 없이 동작하는지 로그 확인
3. Default Local Group에서 Local 캐릭터 prefab 제거
4. Default Local Group에서 Local 아이콘 sprite 제거
5. Default Local Group에서 `2d_general` 제거
6. Addressables build 재생성
7. 다시 Unity 테스트

정리 후 기대 상태:

- Default Local Group에는 Local 기본 캐릭터 리소스가 없어도 된다.
- DLC 리소스만 Addressables 관리 대상이다.
- Local은 Inspector/Scene/Prefab 참조로만 유지된다.

## 주의사항

- `PrefabDataLocal`에 같은 key를 중복 등록하면 첫 번째 값만 사용하고 warning을 낸다.
- config의 Local key와 `PrefabDataLocal` key가 다르면 UI 표시나 캐릭터 교체가 실패한다.
- Local prefab은 `PrefabDataLocal`뿐 아니라 `CharManager.charList`에도 등록하는 현재 방침을 유지한다.
- `isLocal == true` 항목은 Addressables 주소를 적어도 코드가 Addressables로 보지 않는다.
- `isLocal == false` 항목은 기존 DLC Addressables 주소를 계속 사용한다.
- `prefabAddress == "2d_general"`만 예외적으로 `isLocal == false`여도 prefab resolve는 Local로 탄다.

## 남은 확인 포인트

- Unity Inspector 연결이 빠지지 않았는지
- `character_database.json`의 Local key가 모두 `PrefabDataLocal`에 있는지
- fallback sprite가 `ChangeCharManager`에 연결되어 있는지
- 기존 Addressables catalog update 흐름이 Local 항목 때문에 불필요한 다운로드를 띄우지 않는지
- 15초 후 다운로드 흐름이 네가 원하는 대상에게만 도는지

## 결론

코드 쪽 목적은 다음 상태를 목표로 구현되어 있다.

- Local은 Addressables에서 퇴장
- DLC만 Addressables 사용
- config는 계속 UI 목록의 기준
- `isLocal`은 주소 해석 방식을 바꾸는 메타데이터
- Local sprite/prefab 실참조는 `PrefabDataLocal`에서 관리
- `2d_general` prefab은 Local, DLC 2D asset은 Addressables

이제 남은 핵심은 Unity Inspector 연결, config key 매칭, Default Local Group 정리 후 재테스트다.
