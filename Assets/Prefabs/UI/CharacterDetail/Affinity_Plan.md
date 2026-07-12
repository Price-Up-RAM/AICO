# Affinity(친밀도) 시스템 전환 계획

> 상태: **계획/브레인스토밍 단계** — 코드 수정 없음. 이 문서가 확정되면 구현 착수.

> **개정 이력**
> - 2026-07-12 — 한국어 표기를 구 "인연도" 에서 **친밀도** 로 개정 (표기 규칙: **친밀도(ko) / affinity(code)**). 파일명·코드 식별자(Affinity*/affinity*)는 유지. 용어 결정 조항의 개정은 `Affinity_Store_Integration.md` 용어 절 참조.
> - 2026-07-11 — 구 `Relationship_Plan.md` 에서 리네임. 사유: "relationship"은 향후 다른 시스템의 예약어가 되어, 코드 용어를 **affinity** 로 확정. 상점/재화 파트(구 5장)는 `CharacterDetail/Affinity_Store_Integration.md` 로 책임 이관.
> - 2026-07-11 — **1단계 구현 착수**: 표시 교체(Lv.N n/100 + 무지개 게이지 + 6단계 명칭, Lv.10은 Lv.MAX만) + 보상 수령 모달 프로토타입. 진행 상황은 `WORKLOG.md` 참조. 표시 형식이 계획(6장 표)과 다른 부분은 구현을 우선한다.

## 1. 확정 사항

- 기존 **호감도(affection, 0~300, 3단 바)** 시스템을 **친밀도(affinity)** 시스템으로 전면 교체한다.
- 코드/에셋 용어는 `affection` → `affinity` 로 전면 리네이밍.
- **`relationship` 은 예약어** — 향후 다른 시스템에서 사용할 이름이므로, 이 시스템의 어떤 코드/에셋/키(필드, enum, 프리팹 오브젝트명, 저장 키 등)에도 사용 금지.
- **100포인트마다 1레벨업, 최대 Lv.10** (총량 1000포인트).
- 기존 호감도 잔재(3단 바, 보통/친밀/매우 친밀 라벨 등)는 제거한다.
- **전용 장신구는 Lv.3 보상.**
- **Lv.10 도달 시 관계 명칭 커스텀 해금** — 기본 명칭을 교체하는 게 아니라 유저가 직접 지은 명칭(예: "찐친")을 **추가**해서 표시할 수 있다.
- **AI 행동 변화(호칭/말투/프롬프트 주입)는 보상에서 배제.** 구현 난도가 높아 호감도→친밀도 전환의 이유이기도 함. 보상은 눈에 보이는 치장/해금 계열로 한정.
- 마이그레이션 불필요: 기존 `settings_char.json`의 `affection`은 증감 코드가 없어 실사용 값이 항상 0이었다.

## 2. 레벨 모델 (미결: 저장 방식)

| 방식 | 저장 필드 | 장점 | 단점 |
|---|---|---|---|
| A. 누적 포인트 (권장) | `affinityPoints` (0~1000) | 필드 1개, 레벨은 `points / 100`으로 파생. 단순 | 레벨별 요구량 차등화 불가 |
| B. 레벨 + 경험치 | `affinityLevel` + `affinityExp` | 레벨별 요구 경험치 커브 조정 가능 | 필드 2개, 정합성 관리 필요 |

- 당장은 균등(100/레벨)이므로 **A안 권장**. 커브가 필요해지면 그때 B로 확장.
- 파생값: `affinityLevel = Mathf.Min(points / 100, 10)`, `maxAffinityLevel = 10`.
- 커스텀 명칭용 필드: `affinityCustomLabel` (string, 기본 ""). 비어 있으면 기본 명칭 표시.

## 3. 단계 명칭 (6단계, 연애 요소 배제, ko/jp/en)

레벨 구간: 0~1 / 2~3 / 4~5 / 6~7 / 8~9 / 10

### 확정안 — "둘도 없는 사이"가 최고 단계

| 레벨 | ko | jp | en | enum 후보 |
|---|---|---|---|---|
| Lv 0~1 | 낯선 사이 | 見知らぬ仲 | Stranger | `Stranger` |
| Lv 2~3 | 아는 사이 | 顔見知り | Acquaintance | `Acquaintance` |
| Lv 4~5 | 친한 사이 | 親しい仲 | Friend | `Friend` |
| Lv 6~7 | 허물없는 사이 | 気の置けない仲 | True Friend | `TrueFriend` |
| Lv 8~9 | 마음이 통하는 사이 | 心が通じ合う仲 | Kindred Spirits | `Kindred` |
| Lv 10 | 둘도 없는 사이 | かけがえのない仲 | One and Only | `OneAndOnly` |

- 배치 근거: 친한(호감) → 허물없는(편안함) → 마음이 통하는(교감) → 둘도 없는(유일함)으로 단계마다 감정 축이 상승.
- **예비 후보군** (교체 시 사용, 미사용): 각별한 사이(格別の仲, Cherished Friend) / 믿을 수 있는 사이(信頼できる仲, Trusted Friend) / 특별한 사이(特別な仲, Special Bond) / 가족 같은 사이(家族同然の仲, Like Family — Lv10보다 높아 보일 수 있음).
- 폐기: ~~평생의 인연~~ (낯간지러움).

### 커스텀 명칭 (Lv.10 해금)

- Lv.10 도달 시 CharacterDetail에서 유저가 직접 명칭 입력 가능 (예: "찐친").
- **추가 개념**: 기본 명칭은 데이터로 유지, 커스텀이 있으면 커스텀을 우선 표시. 언제든 비우면 기본 명칭으로 복귀.
- 커스텀 텍스트는 유저 입력이므로 LanguageData 미등록(번역 대상 아님). 글자수 제한(예: 12자) 필요.
- 저장: `settings_char.json`의 `affinityCustomLabel`.

### LanguageData 등록

- `Assets/Scripts/LanguageData.cs`의 `Texts` 리스트에 `{ ko, jp, en }` 딕셔너리로 추가 (키는 `"jp"` — `"ja"` 아님 주의).
- 등록 대상: 단계 명칭 6종 + "친밀도" 라벨 자체 (jp: 親密度, en: Affinity) + 커스텀 입력 UI 문구(placeholder, 확인/초기화 버튼).

## 4. 레벨업 보상

### 4.1 레벨 보상 구성 (확정)

레벨 보상은 **3종으로 단순화**: Gold / 전용 장신구(Lv3) / 카드 테두리(Lv4·7·10).

- **카드 테두리**: CharacterDetail 카드에 레벨 도달 시 테두리 표시 — **Lv4 동테 / Lv7 은테 / Lv10 금테**. 상점 판매 없는 친밀도 전용 상징. (배지·전용 연출·의상 해금은 구현 난도로 폐기)
- **전용 장신구**: 캐릭터별 시그니처, EquipSystem 신규 카탈로그 등록 원칙. 판매 불가.
- **Gold**: **모든 레벨 공통 지급** — 아이템 보상이 있는 레벨(3·4·7·10)에도 함께 지급. Lv1~9는 100G, **Lv10만 200G**.
- 의상/포즈/이펙트(쓰다듬기·클릭) 등 치장 계열은 레벨 보상에서 제외하고 **상점 판매 물품으로만** 다룬다 (5장 → `Affinity_Store_Integration.md` 참조).

### 4.2 레벨별 지급표 (확정)

| 레벨 | 보상 |
|---|---|
| Lv 1 | 100G |
| Lv 2 | 100G |
| **Lv 3** | 100G + **전용 장신구** |
| **Lv 4** | 100G + **동테** (카드 테두리) |
| Lv 5 | 100G |
| Lv 6 | 100G |
| **Lv 7** | 100G + **은테** (카드 테두리) |
| Lv 8 | 100G |
| Lv 9 | 100G |
| **Lv 10** | **200G** + **금테** (카드 테두리) + 명칭 커스텀 해금 |

- 레벨업 토스트 알림은 보상과 별개의 공통 연출.
- Mission AF0004 "친밀도 레벨업" 리포트 연결.

### 4.3 보상 모델 — 타입 확장 (2026-07-12 구현)

Mission은 보상을 **gold 단일 성분**으로 단순화했지만(`MissionReward.gold`, i1~3은 티어당 +100G 환산 흡수),
친밀도 보상은 전용 테두리·gem/crystal·악세서리·전용 악세서리·호칭 등으로 확장돼야 하므로
**타입 있는 보상 정의**를 도입한다 (`AffinityData.RewardsFor(level) : List<AffinityRewardDef>`):

| 타입 | id 공간 | 지급 경로 | 저장 |
|---|---|---|---|
| `Currency` | 재화 키 (`currency_gold`, 향후 `currency_gem` 등 — ItemCurrencyCatalog 등재로 추가) | `CurrencyManager.Earn` (골드는 내부에서 레거시 지갑 위임 → 미션 집계 유지) | `ItemSystem/currency.json` (+골드는 브리지로 `inventory.json`) |
| `Item` | 아이템 키 (ItemCatalog 공간 — 전용 장신구 포함, 판매불가는 `ItemEntry.isSellable=false`) | `ItemSystemManager.GrantItem` | `InventorySystem/main.json` |
| `Border` | 테두리 해금 id (`border_affinity_bronze/silver/gold`) | 캐릭터 단위 해금 | `settings_char.json` `affinityUnlockedIds` |
| `Title` | 호칭 해금 id (`title_affinity_custom` 등) | 캐릭터 단위 해금 | 〃 |

- 표시 문자열(`RewardDescFor`)은 정의 목록과 **수동 동기** (LanguageData 등록 문자열이라 자동 생성은 후속).
- Lv.3 전용 장신구는 캐릭터별 시그니처 키 카탈로그가 정해질 때까지 id 빈값 = 지급 보류(라우터가 스킵+로그).
- 동/은/금테 **표시**는 여전히 레벨 파생(`BorderTierFor`) — 해금 기록(`affinityUnlockedIds`)은 향후
  "테두리 변경 기능"의 소유 판정용. 표시를 "수령해야 적용" 모델로 바꿀지는 미결(7장).
- 테두리 구현 메모: `CharacterDetail.prefab` 카드(또는 초상화) 외곽에 테두리 Image 1개를 두고 동/은/금 스프라이트(또는 색상) 스왑, Lv4 미만은 비표시.

## 5. 상점/재화 — 책임 이관

상점·재화·선물(증정→친밀도 포인트) 설계는 이 문서에서 분리되어 **같은 폴더의 `Affinity_Store_Integration.md` 가 소유**한다. Store 프로토타입(`Assets/Prefabs/UI/Store`)이 이미 재화(Gold) 획득·소비 루프를 검증한 상태이므로, 재화 구조·선물 아이템 정의·상점 스코프 등의 미결 사항은 해당 문서에서 결정한다. 이 문서는 친밀도의 레벨 모델·명칭·레벨업 보상까지만 다룬다.

## 6. 교체/제거 대상 (호감도 잔재)

| 위치 | 현재 | 조치 |
|---|---|---|
| `Scripts/SettingCharManager.cs` | `CharCodeSetting.affection`, `AddAffection()` 0~300 클램프 | `affinityPoints` + `affinityCustomLabel`, `AddAffinityPoints()` 0~1000 클램프 |
| `Scripts/CharacterDetailStateManager.cs` | `affection`/`maxAffection=300`/라벨 3단계(보통·친밀·매우 친밀) | 포인트/레벨/6단계 명칭(+커스텀 우선) 계산으로 교체 |
| `CharacterDetailController.cs` | `SetAffection()` — "호감도 X/300" 텍스트 + 3단 바(Yellow/Orange/Red) | `SetAffinity()` — "친밀도 Lv.N" + 단계 명칭 + 현 레벨 진행도 게이지 |
| `CharacterDetail.prefab` | `AffectionContainer`, `AffectionValueText`("호감도 0/300"), `AffectionLabelText`("친밀"), `AffectionBarFillYellow/Orange/Red`, 직렬화 `maxAffection: 300` | `Affinity*` 리네이밍, 3단 바 → 단일 게이지 + 레벨 표기, Lv.10 커스텀 입력 UI 추가, 카드 테두리 Image(동/은/금) 추가 |
| `CharacterDetail.prefab` | `FeatureTag_호감도보유_Text` ("호감도 보유") | "친밀도 보유" 또는 태그 제거 |
| `Scripts/LanguageData.cs` | — | 단계 명칭 6종 + 친밀도 라벨 + 커스텀 UI 문구 등록 (`ko`/`jp`/`en`) |
| `MissionDatabase.cs` AF0004 | 영문 "Level up affinity" | **유지(변경 불필요)** — 영문이 이미 affinity 계열 |
| `settings_char.json` | `affection` 필드 | `affinityPoints`, `affinityCustomLabel`로 교체 (마이그레이션 없음) |
| `CharacterDetail_UI_Plan.md` | "호감도 0/100" 낡은 표기 | 문서 갱신 |

## 7. 미결정 사항

1. 저장 방식 A(누적) vs B(레벨+경험치) — A 권장.
2. ~~단계 명칭~~ → 6단계 전부 확정 (3장 표 참조). 예비 후보군만 유지.
3. 포인트 획득 규칙 — 무엇이 몇 포인트를 주는가 (대화 1회, 쓰다듬기, 선물, 미션, 일일 접속 등). **현재 포인트를 올리는 코드가 전무하므로 이 설계가 사실상 시스템의 본체.**
4. ~~재화 구조 / `MissionReward.item1~3` 정체 / 선물 시스템·상점 스코프~~ → `Affinity_Store_Integration.md` 로 이관.
5. 보상 지급 방식 — 레벨업 즉시 자동 지급 vs CharacterDetail에서 수령 버튼.
6. 캐릭터별 독립 친밀도(현행 char_code 단위) 유지 여부 — 복장이 다르면 친밀도가 분리되는 현행 키 구조가 의도인지 확인 필요.
