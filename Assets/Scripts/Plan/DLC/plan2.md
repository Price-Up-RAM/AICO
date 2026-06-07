# DLC UI 연결 계획 2

## 목표

`character_database.json`의 `isLocal`을 단순 무시하지 않고, Local/DLC 분기 메타정보로 활용한다.

Config는 계속 캐릭터/의상 목록의 기준이 된다. 다만 Local 항목의 `spriteAddress`, `prefabAddress`는 Addressables 주소가 아니라 "로컬 매핑 key"로 해석한다.

실제 Unity 오브젝트 참조는 별도 클래스에서 관리한다.

- Config: key를 가진 데이터 원본
- `PrefabDataLocal`: key -> Sprite / GameObject 매핑
- DLC: 기존 Addressables address 사용

## 핵심 구조

### Config의 의미

`ChangeCharClothesInfo`의 기존 필드를 유지한다.

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

- `spriteAddress`: Local icon key
- `prefabAddress`: Local prefab key
- Addressables 호출 금지

`isLocal == false`일 때:

- `spriteAddress`: Addressables sprite address
- `prefabAddress`: Addressables prefab address 또는 `"2d_general"`
- DLC 다운로드/로드 대상

즉, 같은 필드를 쓰되 `isLocal`에 따라 해석만 달라진다.

## 새 클래스 제안

### PrefabDataLocal

Local key와 Unity 오브젝트 참조를 매칭하는 클래스.

이름은 `ModelDataLocal`의 네이밍을 따른다. 다만 `ModelDataLocal`은 string/URL 같은 순수 데이터만 들고 있는 static class이고, `PrefabDataLocal`은 Sprite/Prefab 같은 Unity 오브젝트 참조를 Inspector에서 받아야 한다. 그래서 구현 형태는 static class보다 `MonoBehaviour` 또는 `ScriptableObject`가 맞다.

1차 구현은 씬에 붙이기 쉬운 `MonoBehaviour`를 권장한다.

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class LocalSpriteEntry
{
    public string key;
    public Sprite icon;
}

[Serializable]
public class LocalPrefabEntry
{
    public string key;
    public GameObject prefab;
}

public class PrefabDataLocal : MonoBehaviour
{
    [SerializeField] private List<LocalSpriteEntry> sprites = new();
    [SerializeField] private List<LocalPrefabEntry> prefabs = new();

    private Dictionary<string, Sprite> spriteMap;
    private Dictionary<string, GameObject> prefabMap;

    public Sprite GetSprite(string key) { ... }
    public GameObject GetPrefab(string key) { ... }
}
```

위치는 `ChangeCharManager`와 같은 GameObject에 붙이거나, 별도 Manager GameObject에 붙인다.

1차 권장:

- `ChangeCharManager`가 `[SerializeField] private PrefabDataLocal prefabDataLocal;`로 참조
- 슬롯 컨트롤러는 직접 `PrefabDataLocal`을 알지 않고 `ChangeCharManager`나 `CharManager` 헬퍼를 통해 접근

## 데이터 흐름

### UI 생성

1. `ChangeCharManager`는 기존처럼 `character_database.json`을 읽는다.
2. `isLocal == true` 항목도 버리지 않는다.
3. 전체 config를 기반으로 슬롯을 생성한다.
4. 슬롯에서 아이콘 표시 시 `isLocal`을 본다.

Local icon:

```csharp
Sprite icon = prefabDataLocal.GetSprite(clothes.spriteAddress);
```

DLC icon:

```csharp
Sprite icon = await AddressableManager.Instance.LoadIfExist<Sprite>(clothes.spriteAddress);
```

### 캐릭터 선택

Local prefab:

```csharp
GameObject prefab = prefabDataLocal.GetPrefab(clothes.prefabAddress);
CharManager.Instance.ChangeCharacterFromGameObject(prefab);
```

DLC prefab:

```csharp
AddressableManager.Instance.LoadWithDownloadable<GameObject>(clothes.prefabAddress, ...);
```

`2d_general`:

- prefab 자체는 Local key로 고정한다.
- config의 `prefabAddress == "2d_general"`이면 `PrefabDataLocal`에서 `"2d_general"` prefab을 찾는다.
- `isLocal == false`인 2D DLC는 animator/sprite만 Addressables로 받는다.

## 기존 구현과의 차이

plan1 구현에서는 Local을 `CharManager.charList`에서 검색하거나, `CharAttributes.charSprite`에서 sprite를 가져오려 했다.

plan2에서는 Local을 명시적인 key-value 데이터 클래스인 `PrefabDataLocal`에서 찾는다.

장점:

- config가 계속 UI 목록의 원본으로 유지된다.
- Local/DLC가 한 캐릭터 카드 안에 자연스럽게 섞인다.
- prefab 이름, charcode, nickname으로 추측 검색할 필요가 없다.
- 아이콘 출처가 명확하다.
- 스크린샷의 fallback smile 자리를 key 매핑만으로 채울 수 있다.

## 필요한 코드 변경

### 1. PrefabDataLocal 추가

새 파일:

- `Scripts/PrefabDataLocal.cs`

기능:

- `GetSprite(string key)`
- `GetPrefab(string key)`
- `ContainsSprite(string key)`
- `ContainsPrefab(string key)`
- 중복 key warning
- 빈 key warning

키 비교는 1차로 정확 일치 사용.

권장:

- key는 config와 Inspector에서 같은 문자열을 사용한다.
- 대소문자 차이로 생기는 실수를 줄이려면 내부 map은 `StringComparer.OrdinalIgnoreCase`를 쓸 수 있다.

### 2. ChangeCharManager에 PrefabDataLocal 연결

필드 추가:

```csharp
[Header("Local Asset Registry")]
[SerializeField] private PrefabDataLocal prefabDataLocal;
```

공개 헬퍼 추가:

```csharp
public Sprite GetLocalSprite(string key)
{
    return prefabDataLocal != null ? prefabDataLocal.GetSprite(key) : null;
}

public GameObject GetLocalPrefab(string key)
{
    return prefabDataLocal != null ? prefabDataLocal.GetPrefab(key) : null;
}
```

### 3. ChangeCharCardController 변경

아이콘:

```csharp
if (currentClothes.isLocal)
{
    Sprite sprite = ChangeCharManager.Instance.GetLocalSprite(currentClothes.spriteAddress);
    characterIcon.sprite = sprite != null ? sprite : fallback;
    return;
}
```

캐릭터 변경:

```csharp
if (currentClothes.isLocal)
{
    GameObject prefab = ChangeCharManager.Instance.GetLocalPrefab(currentClothes.prefabAddress);
    if (prefab == null) { warning; return; }
    CharManager.Instance.ChangeCharacterFromGameObject(prefab);
    return;
}
```

### 4. ChangeCharListSlotController 변경

카드 컨트롤러와 동일한 방식으로 변경한다.

리스트는 첫 번째 clothes만 사용하므로:

- `clothes.spriteAddress` -> `PrefabDataLocal` sprite
- `clothes.prefabAddress` -> `PrefabDataLocal` prefab

### 5. CharManager 변경

현재 `ChangeLocalCharacter(ChangeCharClothesInfo clothes)`는 string 검색 fallback을 가진다.

plan2에서는 두 가지 중 하나를 선택한다.

권장 A:

- Local prefab resolve는 슬롯 컨트롤러에서 끝낸다.
- `CharManager.ChangeCharacterFromGameObject(prefab)`만 호출한다.
- `ChangeLocalCharacter`는 제거하거나 wrapper로만 둔다.

대안 B:

```csharp
public bool ChangeLocalCharacter(ChangeCharClothesInfo clothes)
{
    GameObject prefab = ChangeCharManager.Instance.GetLocalPrefab(clothes.prefabAddress);
    if (prefab == null) return false;
    ChangeCharacterFromGameObject(prefab);
    return true;
}
```

1차 구현은 B가 호출부 수정량이 적다.

### 6. 0초 DLC 캐시 등록 유지

0초 캐시 등록은 계속 `isLocal == false`만 대상으로 한다.

```csharp
if (clothes.isLocal) continue;
```

이 원칙은 유지한다.

`PrefabDataLocal`은 0초 DLC 캐시 등록과 관계없다.

## Inspector 연결 방식

`PrefabDataLocal`에 다음처럼 입력한다.

Sprites:

- key: `arona_sprite`
  - icon: Arona 기본 아이콘 Sprite
- key: `arona_2d_sprite`
  - icon: Arona 2D 아이콘 Sprite
- key: `plana_sprite`
  - icon: Plana 기본 아이콘 Sprite

Prefabs:

- key: `arona`
  - prefab: Arona 기본 prefab
- key: `arona_2d`
  - prefab: Arona 2D prefab
- key: `2d_general`
  - prefab: 공용 2D General prefab
- key: `plana`
  - prefab: Plana 기본 prefab

Config와 `PrefabDataLocal` key가 정확히 같아야 한다.

## 검증 체크리스트

- `isLocal == true` 항목이 UI에 표시되는지
- Local icon이 `PrefabDataLocal` sprite로 표시되는지
- Local icon 누락 시 fallback이 표시되고 warning이 남는지
- Local prefab 클릭 시 `PrefabDataLocal` prefab으로 교체되는지
- Local prefab 누락 시 다운로드 시도 없이 warning만 남는지
- `isLocal == true` 항목에서 `GetDownloadSizeAsync`가 호출되지 않는지
- DLC icon은 기존처럼 미다운로드 시 fallback인지
- DLC 다운로드 후 icon이 갱신되는지
- `2d_general` prefab은 `PrefabDataLocal`에서 가져오는지
- DLC 2D animator는 Addressables로 다운로드되는지

## 남은 설계 쟁점

### 1. `2d_general`은 Local/DLC 의미가 섞인다

일반 규칙은 단순하다.

- `isLocal == true`: `prefabAddress`는 Local prefab key
- `isLocal == false`: `prefabAddress`는 Addressables prefab address

그런데 DLC 2D 항목은 예외다.

```json
{
  "prefabAddress": "2d_general",
  "animatorControllerAddress": "mari_2d_animation",
  "isLocal": false
}
```

이 경우 `isLocal == false`이지만 `prefabAddress == "2d_general"`은 Addressables prefab address가 아니라 `PrefabDataLocal` key로 봐야 한다. 반면 `animatorControllerAddress`와 `spriteAddress`는 DLC Addressables address다.

이건 모순은 아니지만 명확한 특수 규칙이다.

대응:

- `prefabAddress == "2d_general"`이면 prefab은 항상 `PrefabDataLocal.GetPrefab("2d_general")`에서 가져온다.
- `isLocal == false`이면 animator/sprite만 Addressables로 처리한다.
- 이 규칙을 코드 주석과 plan에 명시한다.

### 2. 현재 1차 구현과 plan2 구현 방향이 다르다

현재 코드 1차 구현은 Local sprite/prefab을 `CharManager.charList`와 `CharAttributes`에서 찾는 fallback 구조가 들어가 있다.

plan2는 이걸 `PrefabDataLocal` key 방식으로 바꾸자는 것이다.

즉, 다음 구현 단계에서 기존 1차 Local resolve 코드를 교체해야 한다.

교체 대상:

- `CharManager.GetLocalCharacterSprite`
- `CharManager.ChangeLocalCharacter`
- `ChangeCharCardController`의 Local icon/Local click 분기
- `ChangeCharListSlotController`의 Local icon/Local click 분기

이건 방향 충돌이라기보다는 마이그레이션 지점이다.

### 3. Config key가 UI와 실제 교체 대상을 동시에 책임진다

Local 항목에서:

- `spriteAddress`는 icon key
- `prefabAddress`는 prefab key

이름은 여전히 `Address`지만 실제 의미는 key다. 코드상으로는 문제 없지만, 나중에 사람이 JSON을 볼 때 헷갈릴 수 있다.

대응:

- 당장은 필드명을 유지한다.
- 주석에서 `isLocal == true`일 때는 address가 아니라 key라고 명확히 적는다.
- 장기적으로는 `spriteKey`, `prefabKey`로 JSON 필드명을 바꾸는 2차 마이그레이션을 고려할 수 있다.

## 주의사항

### 1. key 중복

같은 key가 `PrefabDataLocal`에 두 번 들어가면 마지막 값이 이길 수 있다.

대응:

- `PrefabDataLocal` map 생성 시 중복 key warning
- 첫 번째 값을 유지할지, 마지막 값을 덮어쓸지 명확히 정한다.

권장:

- 첫 번째 값을 유지하고 warning 출력

### 2. config key와 PrefabDataLocal key 불일치

가장 많이 날 수 있는 실수다.

대응:

- `ChangeCharManager.LoadDatabase()` 후 Local 항목을 순회하며 `PrefabDataLocal`에 key가 있는지 검증
- 누락된 sprite/prefab key warning 출력

### 3. Local prefab이 PrefabDataLocal에는 있는데 charList에는 없는 경우

현재 원칙상 Local prefab은 `CharManager.charList`에도 등록되어 있어야 한다.

대응:

- 클릭 시 `ChangeCharacterFromGameObject(prefab)`가 못 찾으면 warning
- 선택적으로 시작 시 `PrefabDataLocal` prefab이 charList에 있는지 검증
- 자동 추가는 하지 않는다.

### 4. 2d_general key

`2d_general`은 config와 `PrefabDataLocal`에서 반드시 같은 key를 써야 한다.

대응:

- `2d_general` prefab 누락 시 명확한 error

## 결론

새 plan2 방향:

- Config는 유지한다.
- `isLocal`은 address 해석 방식을 정하는 메타정보다.
- Local key는 config의 `spriteAddress`, `prefabAddress`에 둔다.
- 실제 Sprite/Prefab은 `PrefabDataLocal`에서 key-value로 관리한다.
- DLC는 기존 Addressables address를 그대로 사용한다.

이 방식이 지금 목적에 가장 잘 맞는다. UI 데이터 목록은 config에서 관리하면서, Unity 오브젝트 참조는 Inspector에서 안전하게 연결할 수 있다.
