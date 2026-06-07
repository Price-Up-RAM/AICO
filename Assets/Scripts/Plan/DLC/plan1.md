# DLC Addressables 전환 계획 1

## 목표

Local 캐릭터/의상/아이콘을 Addressables에서 전면 제외한다. `character_database.json`에 남아 있는 `isLocal`은 "Addressables 다운로드/조회 대상이 아님"을 나타내는 메타데이터로만 사용한다.

Local 캐릭터 프리팹은 `CharManager.charList`에 Inspector로 직접 등록한다. Addressables는 DLC 전용으로만 사용한다.

게임 시작 직후 0초 단계에서 `isLocal == false`인 DLC 항목 중 이미 다운로드되어 로드 가능한 프리팹만 `charList`에 먼저 추가한다. 이 단계는 다운로드를 시작하지 않고, Config의 DLC 주소를 기준으로 캐시 로드만 시도한다. 그 후 Local `charList`와 캐시 등록된 DLC로 기본/마지막 캐릭터를 초기화한다.

시작 15초 후에는 다운로드가 필요한 대상이 있을 때만 기존 다운로드 흐름을 시작한다.

## 결정된 원칙

- `isLocal == true`
  - 다운로드 시도 없음
  - `GetDownloadSizeAsync` 호출 없음
  - `Addressables.LoadAssetAsync` 호출 없음
  - `CharManager.charList`와 게임 내 GUI/Inspector에 이미 등록된 기존 에셋을 사용
  - DLC 로더 입장에서는 무시 대상

- `isLocal == false`
  - DLC Addressables 대상
  - 시작 0초에 "이미 다운로드된 것만" `charList`에 추가
  - 시작 0초 캐시 등록에서는 다운로드 UI를 띄우지 않음
  - 시작 15초 후 다운로드가 필요한 대상이 있으면 기존 다운로드 흐름 사용
  - 사용자가 선택했을 때 필요하면 다운로드 UI를 띄우고 다운로드 후 `charList`에 추가

- `CharManager.charList`
  - Local 캐릭터의 단일 진실
  - 사용자가 Inspector에서 직접 관리
  - Local 자동 등록 로직은 만들지 않음

- Local GUI/아이콘/의상 데이터
  - Config의 Local 항목을 런타임 로딩 재료로 쓰지 않음
  - 게임 내 GUI/Inspector 등록 데이터를 사용
  - 따라서 Local `spriteAddress`, `prefabAddress`는 DLC 로더에서 해석하지 않음

- Addressables
  - DLC 카탈로그/다운로드/로드에만 사용
  - `Default Local Group`에 있던 기본 캐릭터/아이콘/`2d_general` 등록은 제거 대상

## 현재 파악

관련 중심 파일:

- `Scripts/ChangeCharManager.cs`
- `Scripts/AddressableManager.cs`
- `Scripts/ChangeCharCardController.cs`
- `Scripts/ChangeCharListSlotController.cs`
- `Scripts/CharManager.cs`

현재 `character_database.json`에는 `spriteAddress`, `prefabAddress`, `animatorControllerAddress`, `toggleClothesAddress`, `changeClothesAddress`, `isLocal`이 있다.

현재 문제는 `isLocal == true`인 항목도 실제로는 `AddressableManager.LoadIfExist`, `Addressables.LoadAssetAsync("2d_general")` 같은 경로에 의존한다는 점이다. Local Addressables 그룹을 제거하면 Local 아이콘, Local 캐릭터 선택, `2d_general` 로드가 깨질 수 있다.

## 새 동작 흐름

### 앱 시작 직후: 0초 캐시 등록

1. `CharManager.Awake()`에서 기존처럼 `SettingManager`를 로드한다.
2. `character_database.json`은 읽어서 `_characterDatabaseData`에 보관한다.
3. JSON의 모든 clothes 중 `isLocal == false`만 순회한다.
4. `prefabAddress`가 비어 있거나 `"2d_general"`이면 일반 프리팹 추가 대상에서는 제외한다.
5. 각 DLC prefab address에 대해 `AddressableManager.LoadIfExist<GameObject>()`를 호출한다.
6. 결과가 null이면 미다운로드 또는 로드 실패로 보고 skip한다.
7. 결과가 있으면 `charList`에 없을 때만 추가한다.
8. 이 작업이 끝난 뒤 `InitCharacter()`를 실행한다.
9. 기본/마지막 캐릭터는 Local `charList`와 이미 다운로드되어 등록된 DLC 안에서 찾아 생성한다.

이 단계는 "다운로드 시작"이 아니다. Addressables 그룹 설정을 수동으로 읽거나 Local 그룹을 검사하지 않고, Config에 적힌 DLC 주소를 `LoadIfExist`로 캐시 로드만 시도한다.

### 게임 시작 15초 후

1. 다운로드가 필요한 대상이 있는지 확인한다.
2. 필요한 대상이 없다면 아무 것도 하지 않는다.
3. 필요한 대상이 있으면 기존 다운로드 흐름을 사용한다.
4. 다운로드/로드 성공 시 `charList`에 없을 때만 추가한다.

여기서 "다운로드가 필요한 대상"은 명시적으로 요구된 대상이다. 예를 들면 마지막 캐릭터 복원이 미다운로드 DLC를 가리키거나, 사용자가 DLC 캐릭터를 선택한 경우다. 모든 DLC를 자동으로 전부 다운로드하는 프리패치로 보지는 않는다.

### 사용자가 DLC 캐릭터를 선택할 때

1. 슬롯 컨트롤러가 `isLocal`을 본다.
2. Local이면 Addressables를 거치지 않고 기존 게임 내 GUI/`CharManager.charList` 흐름으로 변경한다.
3. DLC이면 `AddressableManager.LoadWithDownloadable<GameObject>()`를 사용한다.
4. 다운로드/로드 성공 시 `CharManager.ChangeCharacterFromDLC(prefab)`를 호출한다.
5. `ChangeCharacterFromDLC`는 기존처럼 `charList`에 없으면 추가 후 교체한다.

## 구현 방향

### 1. AddressableManager는 DLC 전용으로 축소

`AddressableManager`는 더 이상 Local fallback 로더가 아니다.

유지할 기능:

- `GetPendingSize`
- `LoadIfExist`
- `LoadWithDownloadable`
- `LoadWithDownloadableAsync`
- 다운로드 캐시 초기화

정리할 의미:

- `LoadIfExist`는 DLC preview 또는 "이미 다운로드된 DLC만 로드"에만 사용한다.
- Local 항목에 대해서는 호출하지 않는다.
- `LoadLocal`은 제거하거나 미사용 상태로 둔다.

### 2. CharManager 시작 흐름 변경

현재 `LoadCharacterListFromJSON()`은 시작 중 Remote 주소를 모아 사이즈를 조회하고, 이미 다운로드된 DLC prefab을 즉시 `charList`에 추가한다.

변경 후:

- `LoadCharacterListFromJSON()`은 JSON 파싱과 `_characterDatabaseData` 저장만 담당한다.
- `Awake()`에서는 JSON 파싱 후 즉시 `RegisterAlreadyDownloadedDlcPrefabsAsync()`를 실행한다.
- 이 즉시 등록은 `LoadIfExist`만 사용하고 다운로드를 시작하지 않는다.
- 즉시 등록이 끝난 뒤 `InitCharacter()`를 호출한다.
- 15초 후에는 필요한 대상이 있을 때만 기존 다운로드 흐름을 시작한다.

예상 메서드:

```csharp
private async Task RegisterAlreadyDownloadedDlcPrefabsAsync()
{
    // isLocal == false 프리팹만 LoadIfExist로 확인 후 charList 추가
}

private IEnumerator StartNeededDlcDownloadDelayed(float delaySeconds)
{
    yield return new WaitForSeconds(delaySeconds);
    // 필요한 대상이 있으면 기존 LoadWithDownloadable 흐름 시작
}
```

### 3. Local 캐릭터 변경 방식

Local 선택 시 `prefabAddress`를 Addressables 주소로 보지 않는다. 더 나아가 DLC 로더는 Local config 값을 해석하지 않는다.

원칙:

- Local 프리팹은 `CharManager.charList`에 미리 등록한다.
- Local GUI/버튼/아이콘/의상 데이터도 게임 내에 직접 등록한다.
- Config에 `isLocal == true` 항목이 남아 있어도 DLC 등록/다운로드 로직은 이를 건드리지 않는다.
- 만약 Local 의상 캐릭터가 별도 prefab인 경우도 기술적으로는 `charList`/GUI 등록으로 해결한다.

따라서 구현에서 필요한 것은 "Local을 찾는 복잡한 resolver"가 아니라, DLC 로딩 루프가 Local을 절대 건드리지 않게 하는 것이다.

### 4. Local 아이콘 표시 방식

Local `spriteAddress`도 다운로드 대상이 아니다. 따라서 슬롯 UI에서 Local 아이콘을 Addressables로 읽으면 안 된다.

Local 아이콘은 게임 내 GUI/Inspector 등록 데이터를 사용한다. Config의 Local `spriteAddress`는 DLC 로더가 해석하지 않는다.

DLC 아이콘은 기존처럼 `AddressableManager.LoadIfExist<Sprite>()`를 사용한다. 미다운로드 상태면 fallback을 보여준다.

### 5. 2d_general 처리

`2d_general`은 가장 조심해야 한다.

결정:

- 공용 `2d_general` 프리팹은 Local이다.
- 공용 프리팹도 `charList`에 Inspector로 반드시 등록한다.
- `"2d_general"`을 Addressables로 직접 로드하지 않는다.

변경 대상:

- `CharManager.InitCharacterFromCharCode()`
- `CharManager.ChangeCharacter2DGeneral()`

두 메서드 모두 `Addressables.LoadAssetAsync<GameObject>("2d_general")`를 제거하고, `charList`에서 `"2d_general"`에 해당하는 프리팹을 찾아 사용한다.

2D 캐릭터별 animator/sprite는 `isLocal`에 따라 다르게 처리한다.

- Local 2D: Addressables 호출 없음. 가능하면 prefab/기존 참조에서 사용하거나 fallback.
- DLC 2D: 선택 시 animator는 `LoadWithDownloadableAsync<RuntimeAnimatorController>()`로 다운로드 포함 로드.
- DLC 2D preview sprite는 `LoadIfExist<Sprite>()`로 이미 다운로드된 경우에만 표시.

### 6. 슬롯 컨트롤러 변경

`ChangeCharCardController`

- `UpdateClothesUI()`
  - Local이면 Addressables 호출 없이 local sprite resolve
  - DLC이면 `LoadIfExist<Sprite>()`

- `ChangeChar()`
  - Local 일반 프리팹이면 `CharManager.ChangeLocalCharacter(currentClothes)`
  - DLC 일반 프리팹이면 기존 다운로드 포함 로드
  - `2d_general`이면 `CharManager.ChangeCharacter2DGeneral(currentClothes)` 호출하되 내부에서 Local 공용 프리팹 사용

`ChangeCharListSlotController`

- 같은 분기 적용
- 리스트는 기본 의상 index 0 기준으로 처리

### 7. ChangeCharManager 변경

`ChangeCharManager`는 UI 데이터베이스 로드와 슬롯 생성에 집중한다.

정리 대상:

- 개발용 `TestDLC()`는 제거하거나 명시적 테스트 플래그 뒤로 숨긴다.
- `CheckCatalogUpdates()`는 DLC 카탈로그용으로 유지 가능하다.
- 단, Local 항목 때문에 Catalog 업데이트/다운로드 체크가 돌면 안 된다.

### 8. Addressables 설정 정리

Unity Editor에서 정리할 것:

- `Default Local Group`의 기본 캐릭터/아이콘/`2d_general` 제거
- DLC 그룹만 유지
- Remote BuildPath/LoadPath 확인
- Remote Catalog 유지
- Addressables build 재생성

코드상 Local Addressables 호출이 완전히 사라진 뒤에 제거해야 한다.

## 구현 순서

1. `CharManager.LoadCharacterListFromJSON()`을 JSON 저장 전용으로 축소한다.
2. `CharManager`에 0초 즉시 DLC 캐시 등록 메서드를 추가한다.
3. `2d_general`의 Addressables 직접 로드를 제거하고 `charList` 검색으로 교체한다.
4. `ChangeCharCardController`, `ChangeCharListSlotController`에서 `isLocal` 분기를 추가한다.
5. `Inject2DGeneralComponentsAsync()`에서 Local 항목은 Addressables를 호출하지 않도록 분기한다.
6. 15초 후 필요한 대상만 기존 다운로드 흐름에 태우는 지연 다운로드 시작점을 둔다.
7. `AddressableManager.LoadLocal` 등 Local용 의미를 제거/미사용화한다.
8. Unity Editor에서 Local Addressables 등록을 제거한다.

## 검증 체크리스트

- 앱 시작 직후 인터넷/DLC 상태와 무관하게 Local 기본 캐릭터가 뜨는지
- 시작 직후 `isLocal == true` 항목에 대해 `GetDownloadSizeAsync`가 호출되지 않는지
- 시작 직후 DLC 다운로드 UI가 뜨지 않는지
- 시작 0초 캐시 등록에서 이미 다운로드된 DLC만 `charList`에 추가되는지
- 미다운로드 DLC는 0초 캐시 등록에서 skip되는지
- 마지막 캐릭터가 이미 다운로드된 DLC이면 0초 캐시 등록 후 정상 복원되는지
- 시작 15초 후 필요한 대상이 있을 때만 기존 다운로드 흐름이 시작되는지
- Local 캐릭터 선택 시 Addressables 로그 없이 교체되는지
- DLC 캐릭터 선택 시에만 다운로드 UI가 뜨는지
- 다운로드 성공 후 DLC prefab이 `charList`에 추가되고 교체되는지
- `2d_general` 공용 프리팹이 Addressables 없이 `charList`에서 찾아지는지
- Local 아이콘이 `CharAttributes.charSprite` 또는 fallback으로 표시되는지
- Default Local Group에서 Local 에셋을 제거한 뒤에도 Local 캐릭터가 정상 동작하는지

## GRILL ME

현재 남은 GRILL ME는 없다. 기존 두 질문은 0초 캐시 등록 + 15초 필요 다운로드 흐름으로 합쳐서 해결한다.

정리된 구현 결정:

- 0초: 이미 다운로드된 DLC만 `LoadIfExist`로 확인해 `charList`에 추가
- 0초: 다운로드 UI 없음, 다운로드 시작 없음
- 0초: Local `isLocal == true`는 완전히 무시
- 0초 캐시 등록 후 `InitCharacter()` 실행
- 15초: 다운로드가 필요한 명시 대상이 있으면 기존 `LoadWithDownloadable` 흐름 시작
- 15초: 모든 DLC를 자동으로 전부 다운로드하지 않음
- 중복 추가 방지를 위해 모든 DLC 추가 경로에서 `charList.Contains(prefab)` 확인

주의할 표현:

- "Addressable 세팅을 전혀 보지 않는다"는 말은 Unity Addressables 설정 파일을 수동 분석하지 않는다는 의미로 둔다.
- 이미 다운로드된 DLC인지 확인하려면 런타임 Addressables API와 카탈로그/캐시 조회는 필요하다.
- 이 조회는 다운로드 시작과 다르다.

## 결론

새 계획의 핵심은 더 단순하다.

- Local은 `CharManager.charList`에 직접 등록된 기존 에셋만 사용
- `isLocal`은 Addressables 제외 메타데이터
- DLC는 시작 0초에 이미 다운로드된 것만 조용히 `charList`에 추가
- 미다운로드 DLC는 15초 후 필요한 대상이 있을 때 또는 사용자가 선택할 때만 다운로드
- `2d_general` 공용 프리팹은 Local `charList` 필수 등록

이 방향이면 Local Addressables를 제거해도 기본 캐릭터는 시작 즉시 살아 있고, DLC는 Addressables 다운로드 흐름에만 남는다.
