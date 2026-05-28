# 📄 악세서리 시스템 설계 문서 (Accessory System Spec)

## 1. 시스템 개요
캐릭터의 특정 뼈대(Bone) 또는 슬롯(Slot)을 이름으로 찾아 악세서리를 동적으로 장착 및 해제하며, 해당 악세서리의 세팅 값을 JSON 형태로 저장하고 불러오는 시스템. 대상 슬롯이 없을 경우 우선순위에 따라 하위 타겟으로 대체하는 Fallback 구조를 가집니다.

---

## 2. 데이터 모델: `AccessoryData.cs`

### A. JSON 저장용 데이터 구조
**역할:** JSON으로 저장되고 읽혀질 순수한 데이터 컨테이너.
*   **타입:** `[System.Serializable]` 클래스
*   **프로퍼티:**
    *   `string accessoryName` : 악세서리 식별 이름 (예: "arona_chipao")
    *   `string target1`, `string target2`, `string target3` : 장착될 대상의 이름 (본 또는 슬롯). `target1`을 먼저 찾고, 없으면 `target2`, 그래도 없으면 `target3`으로 Fallback 탐색.
    *   `Vector3 localPosition` : 장착 시 적용될 단일 로컬 위치
    *   `Vector3 localRotation` : 장착 시 적용될 단일 로컬 회전값

### B. 악세서리 프리팹 딕셔너리 (Static Class)
**역할:** 유니티 인스펙터(UI)에서 악세서리 이름과 프리팹(`GameObject`)을 직접 연결(Mapping)하고, 이를 `static`하게 유지하여 어디서든 값을 Get/Set 할 수 있도록 제공하는 정적 데이터 클래스.
*   에디터 노출을 위해 별도의 관리자 스크립트나 클래스 구조를 활용해 인스펙터에서 설정된 값들을 전역 정적 변수로 캐싱합니다.

---

## 3. 핵심 동작: `AccessoryManager` (MonoBehaviour)

### A. JSON 입출력 (Save/Load)
*   **저장 위치:** `Application.persistentDataPath`
*   **파일 이름:** `accessory_data.json`
*   **역할:** `AccessoryManager` 내부에서 JSON 직렬화/역직렬화를 전담하여 파일 로드 및 저장을 처리합니다.

### B. 주요 메서드: `Equip`
```csharp
public void Equip(GameObject target, string accessoryName, string targetName = null, Vector3? localPosition = null, Vector3? localRotation = null)
```

**동작 상세 로직:**
1.  **변칙 파라미터 적용:**
    *   `targetName`, `localPosition`, `localRotation` 파라미터가 `null`이 아닐 경우 전달된 파라미터 값을 최우선으로 사용.
    *   `null`일 경우 `AccessoryManager`가 로드한 `accessory_data.json` 데이터에서 해당 악세서리 이름의 기본 세팅을 찾아 사용.
2.  **타겟(Slot/Bone) Fallback 탐색:**
    *   파라미터로 받은 타겟이나 JSON의 `target1` -> `target2` -> `target3` 순서대로 대상을 검색하여 가장 먼저 찾아지는 부위를 최종 부모로 결정.
3.  **기존 장착 여부 확인 및 해제 (Unequip 대체):**
    *   `bool HasEquippedAccessory(Transform slot)` (또는 유사한 체크 로직) 메서드를 활용하여, 결정된 대상(Slot) 하위에 이미 장착된 악세서리가 있는지 확인.
    *   이미 장착된 다른 악세서리가 있다면 기존 악세서리를 파괴(해제).
    *   별도의 `Unequip` 함수는 만들지 않으며, `accessoryName`에 해당하는 프리팹이 등록되어 있지 않거나(빈 문자열 전달 등), 새로 달 프리팹이 없다면 기존 악세서리를 벗기는 것만으로 동작 종료.
4.  **장착 실행:**
    *   최종 결정된 위치(`localPosition`)와 회전(`localRotation`) 값을 적용하여 Instantiate 후 부모에 부착.

---

## 4. 작업 진행 시나리오 (Workflow)

1.  **데이터 세팅 (에디터):**
    *   `AccessoryData.cs`와 연계된 인스펙터 UI를 통해 악세서리 이름과 해당하는 프리팹을 정적으로 매핑 등록.
2.  **런타임 로드:**
    *   `AccessoryManager`가 `persistentDataPath`의 `accessory_data.json`을 읽어 악세서리들의 오프셋 및 타겟 데이터를 메모리에 적재.
3.  **장착 및 해제:**
    *   `Equip()` 호출 시 대상 부위를 Fallback 탐색하고, 해당 부위의 기존 장착물을 확인 후 밀어냄.
    *   새 악세서리가 유효하면 새로 장착, 없으면 해제 상태 유지.