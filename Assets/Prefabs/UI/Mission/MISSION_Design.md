# Mission UI 설계 (계획 문서)

> ⚠️ **아키텍처 변경 (2026-06-27)**: JSON 전면 폐지. 미션 정의+진행 상태를 `MissionInfo` 한 클래스로 통합하고,
> `MissionList`(싱글톤 MonoBehaviour)가 `List<MissionInfo>`를 **코드로 1줄씩** 보유·관리한다(ChangeCharManager 방식).
> 진행도·재화(인벤토리)는 **메모리 전용**(저장 없음). 아래 문서의 `MissionCatalog`/`MissionManager`/`MissionRepository`/
> `InventoryRepository`/`MissionDef`/`MissionProgress`/`missions.json`·`inventory.json` 언급은 **구버전 설계**이며,
> 현재 구현은 `MissionInfo.cs` + `MissionList.cs`로 대체되었다. 골드 지갑은 이후
> `CurrencyManager`(Prefabs/Assist/ItemSystem)로 단일화되어 이 문서의 `InventoryManager` 언급도 구버전이다.

> 작성: 2026-06-23 · 미션(업적) 패널 신규 작업. 구현은 이 문서를 기준으로 자가검수한다.
> 레퍼런스 레이아웃: 첨부 이미지(좌측 탭 분류 + 우측 미션 카드 리스트, 카드 안에
> 설명 / 진행 게이지 / 현황·목표 / 보상 + 성공 시 도장).
> 방법론 레퍼런스: `Assets/Prefabs/UI/Skills`(CLAUDE.md), `Assets/Prefabs/UI/Jukebox`(JUKEBOX_Design.md),
> 좌/우 분할 레이아웃 레퍼런스: `Assets/Prefabs/UI/Alarm`(리스트=좌, 디테일=우).

---

## 0. 한 줄 요약

미션(업적)을 **카테고리 탭(좌)** 으로 나누고, 각 탭의 **미션 카드 리스트(우)** 를 보여준다.
카드마다 `설명 + 진행 게이지바 + 현황/목표 + 보상(gold/item1~3)` 을 표시하고,
**달성 시 도장(stamp)을 사운드 + DOTween으로 찍어준다.**

미션 **정의(제목·목표·보상)는 코드/카탈로그에 박아 두어 읽기 전용**으로 배포하고,
**진행 상태(카운트·달성여부·보상수령)만** JSON으로 저장한다(1차는 평문 JSON, HMAC은 develop 보류).
미션 정의·카테고리 분류는 별도 **[MISSION_Catalog.md](MISSION_Catalog.md)** 로 분리한다.
보상으로 받는 **gold/item1~3은 `inventory.json`** 으로 별도 CRUD한다(§6.2).

---

## 1. 폴더 / 파일 구조  (Skills/Jukebox 방법론 그대로)

```
Assets/Prefabs/UI/Mission/
  MISSION_Design.md                         <- 이 문서 (설계)
  MISSION_Catalog.md                        <- 미션 정의 50개 + 카테고리 분류 (편집·확정은 여기서)
  WORKLOG.md                                <- (구현 시작 후 작성: 현재 가능/남은 것/How to use)
  MissionView/
    Editor/
      MissionViewPrefabBuilder.cs           <- Tools/Mission/Build Mission Prefabs (사용자가 실행)
    Prefabs/
      MissionView.prefab                    <- 메인 패널 (정적 베이크, 컨트롤러 MissionView)
      MissionCardRow.prefab                 <- 미션 카드 1장 템플릿 (컨트롤러 MissionCardRow)
      MissionTab.prefab                     <- 좌측 카테고리 탭 버튼 템플릿 (컨트롤러 MissionTabButton)
    Scripts/
      MissionView.cs                        <- 메인 컨트롤러 (이중 모드: Build / BindExisting)
      MissionCardRow.cs                     <- 카드 행 컨트롤러 (Setup으로 데이터+콜백 주입)
      MissionTabButton.cs                   <- 탭 버튼 컨트롤러
      MissionData.cs                        <- MissionDef / MissionReward / MissionProgress / InventoryData / enum
      MissionCatalog.cs                     <- 미션 "정의" 목록(읽기 전용). 카테고리·제목·목표·보상.
      MissionManager.cs                     <- 런타임 상태(진행도) + 이벤트 + 진행 API + Repository 연결
      MissionRepository.cs                  <- 진행 상태 JSON 저장/로드 (1차 평문, develop HMAC)
      InventoryManager.cs                   <- gold/item1~3 CRUD 싱글톤 + 이벤트 (§6.2)
      InventoryRepository.cs                <- inventory.json 저장/로드 (AlarmRepository 복제)
    Sprites/                                <- (선택) 도장 이미지, 보상 아이콘. 없으면 빌트인/색상 대체.
    Sounds/                                 <- (선택) 도장 찍는 효과음. 사용자가 배치.
```

규칙(기존 폴더와 동일):
- 프리팹은 `Prefabs/`, 스크립트는 `Scripts/`, 에디터 베이크는 `Editor/` 하위.
- `.meta`는 직접 만들지 않는다. Unity 임포트 시 생성.
- TMP / UGUI 전용, 레거시 `UnityEngine.UI.Text` 금지. 다크 테마 팔레트는 `SkillView.cs` 상단 색상 상수를 그대로 차용.

---

## 2. 화면 레이아웃 (레퍼런스 이미지 반영)

```
┌──────────────────────────────────────────────────────────────┐
│ 미션                                                       ×  │  Header
├──────────┬───────────────────────────────────────────────────┤
│  [탭]    │  ┌─────────────────────────────────────────────┐  │
│ ┌──────┐ │  │ 미션 설명 텍스트 ............................  │ 🏅 │  │  Card (MissionCardRow)
│ │ 대화 │ │  │ ▓▓▓▓▓▓▓▓░░░░░░░░░░  3 / 5      [보상아이콘]  │  │  │
│ └──────┘ │  └─────────────────────────────────────────────┘  │
│ ┌──────┐ │  ┌─────────────────────────────────────────────┐  │
│ │ 탐험 │ │  │ 미션 설명 ..................................  │  │  │
│ └──────┘ │  │ ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓  1 / 1     [DONE/도장]  │  │  │
│ ┌──────┐ │  └─────────────────────────────────────────────┘  │
│ │ 특수 │ │  ┌─────────────────────────────────────────────┐  │
│ └──────┘ │  │ ...                                          │  │  │
│          │  └─────────────────────────────────────────────┘  │
│          │            (세로 스크롤 ScrollRect)                │
└──────────┴───────────────────────────────────────────────────┘
   Left(탭)                     Right(카드 리스트)
```

- **Header**: 타이틀 "미션" + 닫기(×). (Alarm/Skill 헤더와 동형)
- **Left (Tabs)**: 카테고리 탭 세로 나열(현재 5개: 첫걸음/대화/교감/생활/도전).
  `MissionTab.prefab` Instantiate. 선택 탭은 강조색, 탭마다 달성/전체 카운트 뱃지(예: `3/8`).
  탭이 더 늘면 좌측도 `ScrollRect`로 감쌀 수 있게 설계(현재 5개는 고정 나열로 충분).
- **Right (Cards)**: 선택된 카테고리의 미션을 `ScrollRect` 안에 세로 리스트로.
  카드 = `MissionCardRow.prefab` Instantiate. 카드 1장 구성:
  1. **설명 텍스트** (좌상, 멀티라인 가능. 주언어 설정에 맞는 ko/en/ja 표시)
  2. **진행 게이지바** (`Image.type = Filled`, fillAmount = 현재 단계 `Progress01`)
  3. **현황/목표 라벨** (`"23 / 30"`. Tiered는 현재 단계 목표, Increment는 다음 레벨 목표)
  4. **단계 표시**(Tiered/Increment): 현재 몇 단계인지 점/뱃지(예: `2/3`, Increment은 `Lv.N`)
  5. **보상 영역** (우측): 현재 단계 보상의 gold/item1~3 칩. **여러 개면 대표 1개만** 보이고
     클릭 시 §2.1 보상 서랍으로 전체 표시
  6. **도장 오버레이** (`stampImage`): 달성+수령 시 보이며 DOTween 팝 연출

> 리스트 채우기 패턴은 두 가지 중 택1 — (A) Alarm식 수동 좌표배치 + 풀링,
> (B) `VerticalLayoutGroup` + `ContentSizeFitter`(Skills TagArea식). 카드 높이가
> 가변(설명 길이)이면 **B**가 단순하다. 1차 구현은 **B** 권장.

### 2.1 보상 서랍 (Reward Drawer)

보상이 여러 종류(예: `g300 + i2×1`)인 카드는, 우측 보상 칩을 **대표 1개**만 접어 두고
칩을 클릭하면 **좌측으로 서랍처럼 슬라이드되며** 전체 보상을 펼쳐 보여준다.

- 닫힘: 대표 보상 1개 칩(또는 "보상 N종" 칩).
- 열림: 칩 클릭 → `DOTween`으로 좌측 패널 폭(또는 위치) 0→목표로 슬라이드(`DOSizeDelta`/`DOAnchorPosX`,
  `Ease.OutCubic`), gold/item1~3 칩을 가로로 나열. 다시 클릭/바깥 클릭 시 역방향으로 닫힘.
- 한 번에 하나만 열리도록 `MissionView`가 현재 열린 카드를 추적(다른 카드 열면 이전 것 닫기).
- 보상이 1종뿐이면 서랍 없이 그 칩만 표시(클릭 무동작).
- 구현 위치: `MissionCardRow`가 자체 서랍 RectTransform을 갖고 토글, 열림 상호배제는 `MissionView`가 중재.

---

## 3. 데이터 모델 (`MissionData.cs`)

```csharp
// 탭 = 카테고리. 미션 분류 결과 5개 (MISSION_Catalog.md). 수집(Collection) 폐지.
public enum MissionCategory {
    Onboarding,    // 첫걸음 — 처음 한 번 해보는 것들
    Conversation,  // 대화 — 대화/감정/선택지/특수 발화
    Affection,     // 교감 — 머리 쓰다듬기/표정/친밀도/캐릭터·액세서리
    Productivity,  // 생활 — 알람/타이머/포모도로/할일/캘린더/주크박스
    Challenge,     // 도전 — 누적·마일스톤·메타(골드, 미션 달성, 카테고리 전체 달성)
}

// 진행 구조(열거형). Flag/Counter 폐지 — 목표는 전부 int.
public enum MissionType {
    OneTime,   // 일회용: 단일 단계. 목표=정수, 1번 수령. tiers=[{target, reward}] 1개
    Increment, // 증가형(무한 반복): 레벨 N(1-based) 목표 = incrementA*N + incrementB. 보상은 매 레벨 동일
    Tiered,    // 열거형: 정해진 단계 배열. 단계마다 목표·보상이 다름
}

[Serializable] public class MissionReward {
    public int gold;
    public int item1;
    public int item2;
    public int item3;
    public bool IsEmpty => gold==0 && item1==0 && item2==0 && item3==0;
    public int RewardKinds => (gold!=0?1:0)+(item1!=0?1:0)+(item2!=0?1:0)+(item3!=0?1:0);
}

// 다국어 제목 (ko/en/ja). 앱 주언어 설정에 따라 노출.
[Serializable] public class LocalizedText {
    public string ko;
    public string en;
    public string ja;
    public string Get(string lang) => lang=="en"?en : lang=="ja"?ja : ko;
}

// 단계(Tiered/OneTime 공용): 목표 누적치 + 그 단계 보상.
[Serializable] public class MissionTier {
    public int target;            // 이 단계 달성에 필요한 누적치
    public MissionReward reward;
}

// "정의": 빌드에 박혀 출하되는 읽기 전용 스펙 (MissionCatalog가 보유)
[Serializable] public class MissionDef {
    public string id;             // 6글자 식별자 (2영문+4숫자, 예 "CV0002"). 저장 매칭 키, 변경 금지
    public string name;           // 옛 식별자(메타데이터, 가독성용). 매칭엔 미사용
    public MissionCategory category;
    public LocalizedText title;   // ko/en/ja
    public MissionType type;

    // OneTime/Tiered: tiers 사용 (OneTime은 1개). Increment: incrementA/B + incrementReward 사용.
    public List<MissionTier> tiers;
    public int incrementA;        // Increment 전용: 레벨당 증가량 a (머리쓰다듬기 10, 인연도 2)
    public int incrementB;        // Increment 전용: 상수항 b (머리쓰다듬기 0, 인연도 1 → 2N+1)
    public MissionReward incrementReward; // Increment 전용: 레벨 1회 보상
}

// "진행 상태": JSON에 저장되는 부분만 (정의는 저장하지 않음)
[Serializable] public class MissionProgress {
    public string id;             // = MissionDef.id (6글자)
    public int currentCount;      // 누적 진행치
    public int claimedTiers;      // 수령 완료한 단계 수. OneTime:0/1, Tiered:0..N, Increment:무한 증가(=레벨)
}

[Serializable] public class MissionSaveData {
    public List<MissionProgress> progresses = new();
    // public string sig;         // (develop) HMAC 서명. 1차 평문 JSON에선 미사용 (§6.1)
}

// 인벤토리(gold/item1~3) — 미션 보상 적립 대상. inventory.json. (§6.2)
[Serializable] public class InventoryData {
    public int gold;
    public int item1;
    public int item2;
    public int item3;
}
```

파생 상태(저장 안 함, 런타임 계산) — 단계 기반:
- **다음 단계 목표** `NextTarget`:
  - OneTime/Tiered: `tiers[claimedTiers].target` (claimedTiers가 tiers 수와 같으면 전부 완료).
  - Increment: `incrementA * (claimedTiers + 1) + incrementB` (항상 다음 레벨 존재. 예 인연도 N=레벨 → 2N+1).
- **현재 단계 진행도** `Progress01` = `Clamp01((currentCount - prevTarget) / (NextTarget - prevTarget))`.
- **Claimable** = 미수령 단계가 달성됨 = `currentCount >= NextTarget && (Increment || claimedTiers < tiers.Count)`.
- **AllDone** = OneTime/Tiered에서 `claimedTiers >= tiers.Count` (Increment은 영원히 false).
- 카드의 "현황/목표" 라벨 = `currentCount / NextTarget` (Tiered는 현재 단계 표기, 예 `23 / 30`).

---

## 4. 필요한 스크립트 (역할)

| 파일 | 역할 | 비고 |
|------|------|------|
| `MissionData.cs` | enum / DTO / 보상·진행·인벤토리 구조 | 위 §3 |
| `MissionCatalog.cs` | **미션 정의 목록**(읽기 전용). `MissionDef[] All`, `GetByCategory`, `GetById` | 정의는 코드에 하드코딩(또는 추후 ScriptableObject). 출하 후 외부 수정 불가. 내용은 MISSION_Catalog.md |
| `MissionManager.cs` | 런타임 상태 보유. 진행 API + 이벤트 + Repository 연결. 싱글톤(기존 `*Manager` 패턴) | 핵심 API ↓ |
| `MissionRepository.cs` | `MissionSaveData` JSON load/save (1차 평문, `AlarmRepository` 복제) | `persistentDataPath/missions.json` |
| `InventoryManager.cs` | gold/item1~3 CRUD 싱글톤 + `InventoryChanged` 이벤트. `AddReward` 등 | §6.2 |
| `InventoryRepository.cs` | `InventoryData` JSON load/save (`AlarmRepository` 복제) | `persistentDataPath/inventory.json` |
| `MissionView.cs` | 메인 컨트롤러. 이중 모드(`Build`/`BindExisting`), 탭/카드 갱신, 도장 연출, 보상 서랍 열림 상호배제 중재 | `SkillView.cs` 골격 차용 |
| `MissionCardRow.cs` | 카드 1장. `Setup(def, progress, onClaim)`, 단계별 게이지·보상칩·도장 렌더 + 보상 서랍 토글(§2.1) | `AlarmListItemView` 류 |
| `MissionTabButton.cs` | 탭 1개. `Setup(category, label, selected, count, onClick)` | `AlarmWeekdayButtonView` 류 |
| `MissionViewPrefabBuilder.cs` | `Tools/Mission/Build Mission Prefabs` 메뉴. 보조→메인 순 베이크 | `SkillViewPrefabBuilder` 복제 |

### MissionManager 공개 API (외부 시스템이 진행도를 올리는 통로)

```csharp
public static MissionManager Instance { get; }
public event Action MissionsChanged;          // View가 구독 → Refresh

// 외부(대화/설정/입력 시스템)에서 호출하는 진행 보고
public void Report(string missionId, int delta = 1);   // Counter 증가
public void ReportFlag(string missionId);              // Flag 달성(=Report with clamp to target)

// 조회
public IReadOnlyList<MissionDef> GetDefs(MissionCategory category);
public MissionProgress GetProgress(string id);
public bool IsCompleted(string id);

// 보상 수령(도장) — View의 카드 클릭에서 호출.
// Claimable(달성한 미수령 단계 존재)일 때만 true. 내부 처리:
//   1) 현재 단계 보상 결정 (Tiered/OneTime: tiers[claimedTiers].reward, Increment: incrementReward)
//   2) InventoryManager.Instance.AddReward(reward)
//   3) claimedTiers++ → missions.json 저장
// Tiered는 단계가 남아 있으면 다음 단계로, Increment은 무한 반복(레벨 증가).
public bool ClaimReward(string id);
```

> **연동 지점(이번 작업 범위 밖, 추후)**: "기쁨 대화" → 대화 감정 분류기에서 `Report("talk_joy")`,
> "캐릭터 변경" → 캐릭터 변경 핸들러에서 `ReportFlag("change_character")` 등.
> 이번 작업에서는 호출 훅만 문서화하고, View는 **샘플 데이터로 동작 검증**한다.

### 도장(stamp) 연출 — `MissionCardRow` / `MissionView`

- 트리거: `ClaimReward` 성공 → 해당 카드의 `stampImage` 활성화 후 DOTween:
  - `transform.localScale = 2f → 1f` (`DOScale`, `Ease.OutBack`)
  - `CanvasGroup.alpha = 0 → 1`, 살짝 회전(`DORotate`, -15°→0)
- 동시에 효과음 재생(`AudioSource.PlayOneShot`, 클립은 `Sounds/`).
  사운드 클립이 없으면 무음으로 스킵(가드).
- 이미 `rewardClaimed`인 카드는 연출 없이 도장 즉시 표시(Refresh 시).

---

## 5. 프리팹 만드는 법 (베이크 절차)

Skills/Jukebox와 동일하게 **코드로 빌드 → 정적 프리팹 베이크 → 런타임은 BindExisting**.

1. `MissionView.cs`에 `Build()`(전체 계층 코드 생성) + `BindExisting()`(베이크된 자식에 참조만 연결)
   + `HasBakedHierarchy()`(특정 자식 존재 여부로 판별) + `#if UNITY_EDITOR EditorBuild()` 작성.
   → `SkillView.cs`의 구조를 그대로 따른다.
2. 카드/탭은 보조 프리팹으로 분리(`MissionCardRow.prefab`, `MissionTab.prefab`).
   메인은 이 템플릿을 `Instantiate`해서 채운다(Jukebox의 TrackRow 방식).
3. `MissionViewPrefabBuilder.cs`:
   - `[MenuItem("Tools/Mission/Build Mission Prefabs")]`
   - 보조(Row/Tab) → 메인(MissionView) 순으로 베이크, 메인이 보조 템플릿을 참조.
   - 빌트인 `UI/Skin/UISprite.psd` 9-slice를 `GetBuiltinExtraResource`로 받아 패널에 지정.
   - `PrefabUtility.SaveAsPrefabAsset` → `AssetDatabase.SaveAssets/Refresh`.
4. **사용자가 메뉴 실행** → 프리팹이 에디터에서 보이고 편집 가능.

> 손으로 prefab YAML을 쓰지 않는다(fileID/GUID 깨짐 위험). 모두 빌더 메뉴로 굽는다.

---

## 6. 저장 (진행 상태 + 인벤토리)

핵심 분리:
- **미션 정의(제목/목표/보상)** = 코드(`MissionCatalog`)에 박혀 빌드에 출하 → **외부에서 수정 불가**.
- **진행 상태**(누적 카운트/수령 여부)만 JSON 저장.
- **인벤토리**(gold/item1~3) = 별도 JSON으로 CRUD (§6.2).

### 6.1 진행 상태 저장 — **현재: 평문 JSON / develop: HMAC (보류)**

> **확정**: 1차 구현은 **평문 JSON**(`AlarmRepository`와 동일 방식). 변조 방지(HMAC)는
> **develop 버전 전까지 보류**한다. 따라서 `MissionSaveData.sig` 필드는 지금은 두지 않거나
> 빈 채로 둔다(추후 HMAC 도입 시 활성화).

- 경로: `Application.persistentDataPath/missions.json`.
- `MissionRepository`는 `AlarmRepository`를 거의 그대로 복제(load/save). 서명/검증 없음.

옵션 비교(추후 결정용 참고):

| 방식 | 변조 난이도 | 비고 |
|------|------------|------|
| **평문 JSON** ✅현재 | 낮음 | 가장 단순. 메모장으로 수정 가능. develop 전까지 이걸로 |
| JSON + HMAC 서명 (develop 후보) | 중간 | secret으로 서명, 로드 시 검증→불일치면 초기화. 손편집 차단 |
| JSON 본문 AES 암호화 | 중간+ | 사람이 못 읽음. 디버깅 불편 |
| PlayerPrefs | 낮음 | 레지스트리/plist, 역시 변조 가능 |

#### (참고) HMAC 서명이란 — develop에서 도입할 방식

평문 JSON은 유저가 메모장으로 `currentCount`/`rewardClaimed`를 고쳐 거저 보상을 받을 수 있다.
**HMAC(Hash-based Message Authentication Code)** 은 "이 파일이 **우리 앱이 쓴 그대로**인지"를
검증하는 짧은 서명이다.

```
sig = HMAC_SHA256(secret_key, sig_제외한_JSON_본문)
```

- `secret_key`: 앱에 내장된 비밀 문자열(우리만 앎).
- **저장 시**: 본문으로 `sig`를 계산해 JSON에 함께 기록.
- **로드 시**: 본문으로 `sig`를 **다시 계산** → 파일의 `sig`와 비교.
  - 일치 → 정상. 사용.
  - 불일치 → 본문이 변조됨(secret 없이는 유효 `sig` 위조 불가) → **무시하고 빈 데이터로 초기화**.

해시 특성상 본문 1글자만 바뀌어도 `sig`가 완전히 달라진다.
구현은 .NET 기본 `System.Security.Cryptography.HMACSHA256` 사용(외부 의존성 없음).

**한계(정직하게)**: secret이 클라이언트 바이너리에 있으므로 디컴파일로 추출하면 뚫린다.
즉 "메모장 손편집·캐주얼 치팅 차단" 수준이며, 작정한 해커는 못 막는다. 진짜 보안은
서버가 보상을 검증해야 가능(범위 밖). 캐주얼 앱 보상엔 비용 대비 적정.
develop에서 도입 시 `MissionRepository.Save`에서 `sig`를 채우고 `Load`에서 검증하도록 확장.

### 6.2 인벤토리 CRUD — `inventory.json`

미션 보상은 **인벤토리에 적립**된다. gold/item1~3을 담는 독립 저장소를 둔다.
미션 시스템뿐 아니라 다른 기능도 재화를 쓰고 줄 수 있도록 **범용 인벤토리**로 설계한다.

- 경로: `Application.persistentDataPath/inventory.json`
- 저장 형식(평문 JSON, 진행 상태와 동일 정책):

```csharp
[Serializable] public class InventoryData {
    public int gold;
    public int item1;
    public int item2;
    public int item3;
    // (develop: HMAC sig 추가 후보)
}
```

- `InventoryRepository`: `AlarmRepository` 복제. `Load()`(없으면 0으로) / `Save(InventoryData)`.
- `InventoryManager`(싱글톤, `*Manager` 패턴) — **CRUD API**:

```csharp
public static InventoryManager Instance { get; }
public event Action InventoryChanged;             // HUD/지갑 UI가 구독

// Read
public int Gold { get; }
public int GetItem(int slot);                     // slot 1~3
public InventoryData GetSnapshot();

// Create/Update (적립·차감). 음수 차감은 0 미만으로 안 내려가게 클램프, 부족하면 false.
public void AddGold(int amount);
public bool SpendGold(int amount);                // 잔액 부족이면 false
public void AddItem(int slot, int amount);
public bool SpendItem(int slot, int amount);
public void AddReward(MissionReward reward);      // 미션 보상 일괄 적립 (gold/item1~3)

// Delete (디버그/리셋용)
public void ResetAll();
```

- **미션 연동**: `MissionManager.ClaimReward(id)` 성공 시 내부에서
  `InventoryManager.Instance.AddReward(def.reward)` 호출 → `inventory.json` 갱신.
  매 변경마다 즉시 `Save`(알람 방식) 또는 디바운스 저장.
- `cha_gold_*`(골드 모으기) 미션은 인벤토리 gold 변동을 구독해 `Report`로 환산할 수 있으나,
  "보상 gold가 다시 골드미션을 채우는 순환"을 피하려면 **누적 획득 gold** 카운터를 별도로
  둘지 정책 결정 필요(→ MISSION_Catalog.md 메모 참조).

---

## 7. 작업 순서 (제안)

1. (이 문서) ✅ 설계 확정
2. `MissionData.cs` + `MissionCatalog.cs`(미션 정의: MISSION_Catalog.md) — 컴파일만 되는 토대
3. `InventoryRepository.cs` + `InventoryManager.cs` — gold/item CRUD + 저장
4. `MissionRepository.cs` + `MissionManager.cs` — 상태/저장/이벤트/Report API + ClaimReward→Inventory
5. `MissionTabButton.cs` / `MissionCardRow.cs` — 행·탭 컨트롤러
6. `MissionView.cs` — Build/BindExisting + 도장 DOTween + 사운드
7. `MissionViewPrefabBuilder.cs` — 베이크 메뉴
8. (사용자) `Tools → Mission → Build Mission Prefabs` 실행 → 프리팹 확인
9. 마지막에 batchmode 1회로 컴파일 검증 + 베이크
10. (추후/범위 밖) UIManager·UIPositionManager 연동, 실제 게임 이벤트 → `Report` 훅 연결

---

## 8. 미션 카탈로그 (별도 문서)

미션 정의와 카테고리 분류는 **[MISSION_Catalog.md](MISSION_Catalog.md)** 로 분리했다(초안, 대화하며 수정 중).
편집·확정은 그 문서에서 진행하고, 확정되면 `MissionCatalog.cs`(읽기 전용 코드)로 옮겨 박는다.

요약:
- 카테고리(탭) **5개**: 첫걸음(OB) · 대화(CV) · 교감(AF) · 생활(PR) · 도전(CH). (수집 폐지)
- id는 **2영문+4숫자 6글자**(예 `CV0002`), 옛 식별자는 `name`(메타데이터)으로 보존.
- 제목은 **ko/en/ja 3개 언어**. type은 **OneTime/Increment/Tiered**(전부 int 목표). Increment는 `aN+b` 식.
- 분포(현재 초안): 8 + 9 + 5 + 8 + 8 = **38**.
- 메타 미션: `CH0002`(미션 달성 수) + `CH0003~0006`(첫걸음/대화/교감/생활 **카테고리 전체 달성**).
  다른 미션 수령 시 `MissionManager`가 내부 `Report` 연쇄 호출(메타가 메타를 트리거하지 않도록 가드).

---

## 9. 범위 밖 (이번 작업 제외)

- UIManager / UIPositionManager 연계 (열기/위치) — Skills CLAUDE.md 절차로 추후 연결
- 실제 게임 이벤트 → `MissionManager.Report(...)` 훅 배선 (대화 감정 분류, 캐릭터 변경 핸들러 등)
- item1~3의 실제 정체(무슨 아이템인지)와 인벤토리 소비처(상점 등) — 지금은 정수 보유/적립까지만
- 진행/인벤토리 저장 변조 방지(HMAC) — **develop 버전 전까지 보류**
- 도장 이미지/효과음 에셋 제작 (사용자가 `Sprites/`,`Sounds/`에 배치; 없으면 색상/무음 대체)
- 서버 권위 기반 보상 검증

> **이번 작업에 포함(범위 안)**: gold/item1~3 인벤토리 적립·차감 CRUD(`inventory.json`)와
> 미션 보상 → 인벤토리 적립 연결은 구현 범위에 포함한다(§6.2).
</content>
</invoke>
