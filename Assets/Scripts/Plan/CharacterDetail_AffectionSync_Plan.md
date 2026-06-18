# CharacterDetail 호감도 ↔ 게이지 양방향 동기화 계획

> 목적: `settings_char.json`에 저장되는 캐릭터별 **호감도 수치**를 `CharacterDetail`의 게이지 바에
> 표시하고, **저장소 → UI 실시간 갱신** + **UI → 저장소 쓰기**의 양방향 동기화를 구성한다.
>
> 상태: 설계 확정 / 구현 착수 전. (작업을 다른 곳에서 이어서 진행하기 위한 문서)

---

## 1. 현재 상태 (조사 결과)

| 항목 | 상태 | 근거 |
|------|------|------|
| 게이지 UI (3단 바 · 값 텍스트 · 라벨) | ✅ 완비 | `CharacterDetail.prefab`: `AffectionContainer/AffectionBarFillYellow,Orange,Red`, `AffectionValueText`, `AffectionLabelText` |
| 표시 로직 `SetAffection(int,string)` | ✅ 구현됨 | `CharacterDetailController.cs:224` |
| 컨트롤러의 affection 참조 | ⚠️ 프리팹상 `{fileID: 0}` 미연결 → 런타임 `AutoBindMissingReferences()`가 이름으로 채움 |
| 현재 표시값 | ❌ `RefreshStats()`에서 `SetAffection(0, …)` **0 고정** | `CharacterDetailController.cs:221` |
| 호감도 데이터 필드 | ❌ **미존재** | `SettingCharManager.CharSetting`은 `char_code`, `char_size`뿐 |
| 호감도 읽기 getter | ❌ 미존재 | `SettingCharManager`에 `GetCharAffection` 없음 |
| 변경 알림 이벤트 | ❌ 미존재 | `SettingCharManager`에 이벤트 없음 |
| 쓰기(변동/저장) 코드 | ❓ **워킹트리에 미반영** | MenuTrigger.cs 등에 호감도 코드 없음, `git status` 변경 없음 (2026-06-18 확인) |

> ⚠️ **확인 필요**: "MenuTrigger에 호감도 변동/저장을 넣었다"고 했으나, 조사 시점 디스크/HEAD 기준
> 으로는 해당 코드가 보이지 않았다. 구현 전 파일 저장 여부 / 실제 파일 위치 / 저장 대상 메서드를
> 먼저 확정할 것.

---

## 2. 확정된 설계 결정

- **단일 소스(Source of Truth)**: `settings_char.json` (`SettingCharManager`).
  - 기존 `char_size` 저장 방식과 동일하게 `CharSetting`에 `affection` 필드를 추가한다.
- **키(key)**: **nickname**.
  - 근거: `CharManager.cs:925` `SaveSettingCharOutfit(nickname, charCode)` → `char_info_dict`는 nickname으로 키잉.
  - CharacterDetail 쪽 대응 값: `currentClothesInfo.charAttr_nickname` (없으면 `currentCharInfo.name` 폴백).
  - ⚠️ 대소문자/표기 일치 주의 (예: UI `"ARONA"` vs settings `"arona"`). 구현 시 키 정규화 규칙 확정.
- **동기화 방향**: 저장소 → UI 실시간 **AND** UI → 저장소 쓰기 (진짜 양방향).
- **쓰기 트리거**: 1차로 MenuTrigger 경유(사용자 작업). UI 자체 편집 컨트롤은 후속.

---

## 3. 구현 계획

### 3-1. 데이터 계층 — `SettingCharManager` 확장
파일: `Assets/Scripts/SettingCharManager.cs`

```csharp
// CharSetting에 필드 추가
public class CharSetting {
    public string char_code;
    public float  char_size;
    public int    affection;   // ★ 추가 (0 ~ maxAffection, 기본 0)
}

// 변경 알림 이벤트 (JarvisTodoStore.Changed 패턴 차용)
public event System.Action<string, int> AffectionChanged;   // (nickname, value)

// 읽기
public int GetCharAffection(string nickname) {
    return char_info_dict.TryGetValue(nickname, out var s) ? s.affection : 0;
}

// 쓰기 (UI/외부 공용 진입점) — SaveSettingCharSize와 동일 패턴 + 이벤트 발화
public void SaveCharAffection(string nickname, int affection) {
    if (!char_info_dict.ContainsKey(nickname))
        char_info_dict[nickname] = new CharSetting();
    int clamped = Mathf.Clamp(affection, 0, 300);   // 300 = maxAffection
    char_info_dict[nickname].affection = clamped;
    SaveToFile();                                    // 디스크 영속
    AffectionChanged?.Invoke(nickname, clamped);     // 저장소 → UI 실시간 트리거
}
```

- `JsonUtility`가 `int`를 자동 직렬화 → 스키마 자동 확장, 기존 파일 호환(필드 없으면 0).
- 메모리↔디스크 정합은 기존 `SaveToFile()` / `LoadSettingChar()`가 보장.
- **델타 헬퍼(선택)**: MenuTrigger가 +/- 로 변동시킨다면
  `AddCharAffection(string nickname, int delta) => SaveCharAffection(nickname, GetCharAffection(nickname) + delta);`

### 3-2. UI 읽기 + 실시간 (저장소 → UI)
파일: `Assets/Prefabs/UI/CharacterDetail/CharacterDetailController.cs`

1. **표시값 교체** — `RefreshStats()`의 `SetAffection(0, …)` 제거 후:
   ```csharp
   string key = GetAffectionKey();
   int affection = SettingCharManager.Instance != null
       ? SettingCharManager.Instance.GetCharAffection(key) : 0;
   SetAffection(affection, GetAffectionLabel(affection));
   ```
2. **키 헬퍼 추가**:
   ```csharp
   private string GetAffectionKey() {
       if (!string.IsNullOrEmpty(currentClothesInfo?.charAttr_nickname))
           return currentClothesInfo.charAttr_nickname;
       return currentCharInfo != null ? currentCharInfo.name : string.Empty;
   }
   ```
3. **실시간 구독** (구독/해제 짝 규칙 준수):
   ```csharp
   private void OnEnable() {
       if (SettingCharManager.Instance != null)
           SettingCharManager.Instance.AffectionChanged += OnAffectionChanged;
   }
   private void OnDisable() {
       if (SettingCharManager.Instance != null)
           SettingCharManager.Instance.AffectionChanged -= OnAffectionChanged;
   }
   private void OnAffectionChanged(string nickname, int value) {
       if (nickname == GetAffectionKey())          // 현재 표시 중인 캐릭터만
           SetAffection(value, GetAffectionLabel(value));
   }
   ```
   > 주의: 현재 `Awake`에서만 이벤트 등록 중. `OnEnable/OnDisable` 추가 시 기존 `Awake`/`OnDestroy`
   > 의 버튼 리스너 등록과 충돌하지 않게 역할 분리할 것.

### 3-3. UI 쓰기 (UI → 저장소)
- 게이지에 직접 쓰는 컨트롤은 현재 프리팹에 없음. 1차 쓰기 경로는 **MenuTrigger**(사용자 작업).
- 어떤 경로든 호출은 한 줄: `SettingCharManager.Instance.SaveCharAffection(key, newValue);`
  → 3-1의 이벤트 발화로 3-2 구독부가 게이지를 자동 갱신(동기화 루프 닫힘).
- (후속/선택) CharacterDetail 내 디버그 +/- 버튼 또는 편집 슬라이더 추가 시에도 동일 호출 사용.

### 3-4. 라벨 (선택)
- 현재 단일 라벨 `defaultAffectionLabel = "친밀"`.
- 바가 3단(0–99 / 100–199 / 200–299)이므로 구간 라벨 권장:
  ```csharp
  private string GetAffectionLabel(int v) =>
      v >= 200 ? "친밀" : v >= 100 ? "친근" : "지인";
  ```
  (라벨 텍스트는 기획에 맞게 조정)

---

## 4. 구현 순서 체크리스트

- [ ] (선결) MenuTrigger 쓰기 코드 저장/위치/대상 메서드 확정
- [ ] `SettingCharManager`: `CharSetting.affection` 필드 추가
- [ ] `SettingCharManager`: `GetCharAffection` / `SaveCharAffection`(+`AddCharAffection`) 추가
- [ ] `SettingCharManager`: `AffectionChanged` 이벤트 추가 및 `SaveCharAffection`에서 발화
- [ ] MenuTrigger(또는 실제 쓰기 위치)를 `SaveCharAffection` 호출로 정리
- [ ] `CharacterDetailController`: `GetAffectionKey()` / `GetAffectionLabel()` 추가
- [ ] `CharacterDetailController.RefreshStats()`: 실값 읽기로 교체
- [ ] `CharacterDetailController`: `OnEnable/OnDisable` 이벤트 구독·해제
- [ ] 키 정규화(대소문자/표기) 규칙 확정 및 적용
- [ ] 검증: 값 변동 → 게이지 즉시 반영 / 앱 재시작 후 `settings_char.json`에서 복원

---

## 5. 핵심 참조 위치

- UI 표시 로직: `Assets/Prefabs/UI/CharacterDetail/CharacterDetailController.cs:224` (`SetAffection`)
- 0 고정 호출 지점: `CharacterDetailController.cs:221` (`RefreshStats`)
- 저장 계층: `Assets/Scripts/SettingCharManager.cs` (`CharSetting`, `SaveSettingCharSize`, `SaveToFile`)
- 키잉 근거: `Assets/Scripts/CharManager.cs:925` (`SaveSettingCharOutfit(nickname, charCode)`)
- 진입 흐름: `UIManager.ShowCharacterDetail` → `CharacterDetailController.Show(charInfo, clothesInfo)` (`UIManager.cs:325`)
- 이벤트 패턴 참고: `Assets/Prefabs/UI/TODOList/Scripts/JarvisTodoStore.cs:27` (`public event Action Changed`)
