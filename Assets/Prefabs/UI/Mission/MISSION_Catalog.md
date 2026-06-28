# Mission 카탈로그

> 미션 **정의 목록**의 단일 출처(SoT) 초안. `MISSION_Design.md` §8이 이 문서를 참조한다.
> 정의는 `MissionList.cs`(코드)에서 `BuildMissions()`로 **1줄씩** 직접 관리한다(JSON 없음). 이 표는 사람이 보기 위한 사본 — **여기서 다듬고 MissionList.cs에 반영.**
> 작성: 2026-06-23 · 상태: **초안 (대화하며 수정 예정)**

## 스키마 (열 의미)

- **id**: 저장 매칭 키. **2영문(카테고리) + 4숫자**, 총 6글자. 예) `OB0001`. **확정 후 변경 금지.**
  - 카테고리 코드: `OB`=첫걸음, `CV`=대화, `AF`=교감, `PR`=생활, `CH`=도전.
- **name**: 옛 식별자. 이제 **메타데이터(개발 가독성)용**으로만 둔다. 저장/매칭엔 쓰지 않음.
- **type**: 진행 구조(열거형). Flag/Counter 폐지, **목표는 전부 int**.
  - `OneTime` (일회용): 단일 단계. 목표=임의 정수, **1번 수령**. ("처음"은 1, "5개"는 5)
  - `Increment` (증가형, 무한 반복): 레벨 N(1부터)의 목표를 **1차식 `aN+b`** 로 둔다.
    `a` = **레벨이 오를 때마다 더 필요한 증가량**, `b` = 시작 보정값. 보상은 매 레벨 동일.
  - `Tiered` (열거형): **정해진 단계** 배열. 단계마다 목표·보상이 다름.
- **목표**: `OneTime`=단일 수치 / `Tiered`=`a / b / c` (단계별) / `Increment`=**실제 식**(`10N`, `2N+1` 등).
- **보상**: `g`=gold, `i1`~`i3`=item1~3. `Tiered`는 단계와 `/`로 정렬. `Increment`는 레벨당 보상.
- **한국어 / English / 日本語**: 표시용 다국어 제목. 앱 주언어 설정에 따라 노출.

> 보상이 여러 개(예: `g300, i2×1`)면 카드에서 보상 칩 클릭 시 **좌측으로 서랍이 열리며**
> 전체 보상을 보여준다(→ MISSION_Design.md §2.1 보상 서랍).
>
> ⚠️ 일부 미션은 미연동 시스템(감정 분류·친밀도 등)에 의존. 훅 없으면 카탈로그에만 두고
> `Report` 배선은 추후. View 자체는 샘플로 검증.

---

## 첫걸음 (OB) — 모두 OneTime

| id | name | type | 목표 | 보상 | 한국어 | English | 日本語 |
|----|------|------|------|------|--------|---------|--------|
| `OB0001` | ob_meet_aico | OneTime | 1 | g50 | 아이코를 처음 만나기 | Meet Aiko for the first time | アイコと初めて出会う |
| `OB0002` | ob_talk_first | OneTime | 1 | g50 | '아이코'와 처음 대화해보기 | Talk to Aiko for the first time | アイコと初めて会話する |
| `OB0003` | ob_change_char | OneTime | 1 | g50 | 캐릭터 변경해보기 | Change your character | キャラクターを変更する |
| `OB0004` | ob_lang_change | OneTime | 1 | g50 | 주언어 설정 변경해보기 (Preference 제외) | Change main language (excl. Preference) | 主言語設定を変更する(Preference除く) |
| `OB0005` | ob_head_pat | OneTime | 1 | i1×1 | 머리 쓰다듬어 보기 | Pat Aiko's head | 頭をなでてみる |
| `OB0006` | ob_open_settings | OneTime | 1 | g30 | 설정 화면 열어보기 | Open the settings screen | 設定画面を開く |
| `OB0007` | ob_accessory_first | OneTime | 1 | g50 | 액세서리 처음 착용해보기 | Equip an accessory for the first time | アクセサリーを初めて装着する |
| `OB0008` | ob_open_jukebox | OneTime | 1 | g30 | 주크박스 열어보기 | Open the jukebox | ジュークボックスを開く |

## 대화 (CV)

| id | name | type | 목표 | 보상 | 한국어 | English | 日本語 |
|----|------|------|------|------|--------|---------|--------|
| `CV0001` | talk_emotion | OneTime | 1 | g80 | 감정 표현이 포함된 대화해보기 | Have a conversation with emotion | 感情表現を含む会話をする |
| `CV0002` | talk_joy_5 | Tiered | 10 / 30 / 50 | g150 / g300,i2×1 / g500,i2×2 | "기쁨" 감정이 담긴 답변 받기 | Get joyful replies | 「喜び」がこもった返答をもらう |
| `CV0003` | talk_sad_5 | Tiered | 10 / 30 / 50 | g150 / g300 / g500 | "슬픔" 감정이 담긴 답변 받기 | Get sad replies | 「悲しみ」がこもった返答をもらう |
| `CV0004` | talk_choice | OneTime | 1 | g80 | 선택지로 답변해보기 | Answer with a choice option | 選択肢で答えてみる |
| `CV0005` | talk_choice_start | Tiered | 1 / 5 / 15 | g80 / g150 / g300 | 선택지로 대화 시작하기 | Start a conversation with a choice | 選択肢で会話を始める |
| `CV0006` | talk_banana | OneTime | 1 | g100, i3×1 | 답변에 '바나나' 포함하기 | Get "banana" in a reply | 返答に「バナナ」を含める |
| `CV0007` | talk_count_10 | Tiered | 10 / 50 / 100 | g100 / g300,i2×1 / g600,i2×2 | 대화하기 | Talk with Aiko | アイコと会話する |
| `CV0008` | talk_long | OneTime | 1 | g80 | 한 번에 긴 대화 나누기 | Have a long conversation at once | 一度に長い会話をする |
| `CV0009` | talk_choice_10 | Tiered | 10 / 30 | g200 / g400 | 선택지로 대화하기 | Have choice-based conversations | 選択肢で会話する |

## 교감 (AF)

| id | name | type | 목표 | 보상 | 한국어 | English | 日本語 |
|----|------|------|------|------|--------|---------|--------|
| `AF0001` | aff_pat | Increment | 10N | g100 (매 레벨) | 머리 쓰다듬기 | Pat Aiko's head | 頭をなでる |
| `AF0002` | aff_see_all_emotion | OneTime | 6 | g200, i2×1 | 모든 감정 표현 보기 | See all emotional expressions | 全ての感情表現を見る |
| `AF0003` | aff_char_change | OneTime | 5 | g120 | 캐릭터 변경 | Change your character | キャラクターを変更する |
| `AF0004` | aff_affinity_up | Increment | 2N+1 | g100 (매 레벨) | 인연도 레벨업 | Level up affinity | 親密度をレベルアップする |
| `AF0005` | aff_accessory_buy | Tiered | 5 / 15 / 30 | g150 / g300 / g600 | 액세서리 구매하기 | Buy accessories | アクセサリーを購入する |

## 생활 (PR)

| id | name | type | 목표 | 보상 | 한국어 | English | 日本語 |
|----|------|------|------|------|--------|---------|--------|
| `PR0001` | pro_alarm_create | Tiered | 1 / 5 / 10 | g50 / g120 / g250 | 알람 만들기 | Create alarms | アラームを作成する |
| `PR0002` | pro_timer_use | OneTime | 1 | g50 | 타이머 사용해보기 | Use a timer | タイマーを使ってみる |
| `PR0003` | pro_pomodoro_1 | Tiered | 1 / 5 / 20 | g80 / g200,i2×1 / g500 | 포모도로 완료하기 | Complete pomodoro sessions | ポモドーロを完了する |
| `PR0004` | pro_todo_add | OneTime | 1 | g40 | 할 일 추가하기 | Add a to-do | やることを追加する |
| `PR0005` | pro_todo_done_10 | Increment | 10N | g120 (매 레벨) | 할 일 완료하기 | Complete to-dos | やることを完了する |
| `PR0006` | pro_calendar_add | OneTime | 1 | g40 | 일정 추가하기 | Add a calendar event | 予定を追加する |
| `PR0007` | pro_calendar_open | OneTime | 1 | g30 | 캘린더 열어보기 | Open the calendar | カレンダーを開く |
| `PR0008` | pro_jukebox_play | OneTime | 1 | g50 | 음악 재생하기 | Play music | 音楽を再生する |

## 도전 (CH) — 누적·마일스톤·메타

| id | name | type | 목표 | 보상 | 한국어 | English | 日本語 |
|----|------|------|------|------|--------|---------|--------|
| `CH0001` | cha_gold | Tiered | 100 / 1000 / 5000 | i1×1 / i2×1,i3×1 / i3×3 | 골드 모으기 | Accumulate gold | ゴールドを貯める |
| `CH0002` | cha_mission_all | Tiered | 10 / 25 / 50 | g300 / g600,i3×1 / g1500,i3×2 | 미션 달성하기 | Clear missions | ミッションを達成する |
| `CH0003` | cha_clear_ob | OneTime | 1 | g300, i1×1 | '첫걸음' 미션 모두 달성 | Complete all Onboarding missions | 「はじめの一歩」を全て達成する |
| `CH0004` | cha_clear_cv | OneTime | 1 | g400, i2×1 | '대화' 미션 모두 달성 | Complete all Conversation missions | 「会話」ミッションを全て達成する |
| `CH0005` | cha_clear_af | OneTime | 1 | g400, i2×1 | '교감' 미션 모두 달성 | Complete all Affection missions | 「ふれあい」ミッションを全て達成する |
| `CH0006` | cha_clear_pr | OneTime | 1 | g400, i2×1 | '생활' 미션 모두 달성 | Complete all Productivity missions | 「生活」ミッションを全て達成する |
| `CH0007` | cha_gold_spend | Tiered | 100 / 1000 / 5000 | i1×1 / i2×1 / i3×1 | 골드 소비하기 | Spend gold | ゴールドを使う |
| `CH0008` | cha_item_own | Tiered | 5 / 20 / 50 | g300 / g800 / g2000 | 아이템 모으기 | Own items | アイテムを集める |

**합계: OB 8 + CV 9 + AF 5 + PR 8 + CH 8 = 38.** (수집 폐지, 접속/일자 삭제, 메타 카테고리별로 분리)

---

## 메모 (수정 시 참고 — 너와 대화하며 확정)

### 이번 패스 변경
- **수집(CL) 카테고리 폐지** → 5개 카테고리(첫걸음/대화/교감/생활/도전). 액세서리 행은 교감으로 이동.
- **인연도 레벨업** `AF0004` → **Increment 식 `2N+1`**(3,5,7,9…, 레벨당 +2). name=`aff_affinity_up`.
- **액세서리 구매하기** `AF0005`(옛 `CL0002`) → id/영(`Buy accessories`)/일(`アクセサリーを購入する`) 수정.
- **캐릭터 변경** `AF0003` name을 `aff_char_change`로 정정(옛 `aff_accessory_5` 혼동 제거).
- **머리 쓰다듬기/할 일 완료** → `Increment`(`10N`). name 숫자 접미사 제거(`aff_pat`).
- **선택지로 대화 시작** `CV0005` → Tiered `1/5/15`(옛 `cha_choice_start_5` 흡수).
- **생활에 '일정 추가하기'** `PR0006` 추가(캘린더 이벤트). Todo 추가는 기존 `PR0004`.
- **접속/일자 계열 삭제**(옛 `cha_login_30`, `aff_streak_3`). 잔여 없음.
- **'모두 달성' 메타를 카테고리별로**: `CH0003~0006`(첫걸음/대화/교감/생활). 도전 자체는 메타라 제외.

### 메타 미션 동작
- `CH0002`(미션 달성하기)=누적 달성 수, `CH0003~0006`=카테고리 전체 달성.
- 다른 미션 수령 시 `MissionList`가 내부 `UpdateDerived`로 메타/도전 진행도 재계산. **메타가 메타를 트리거하지 않도록 가드.**

### 열린 결정
- `Increment` 레벨 보상 점증 여부(현재 고정). `2N+1` 인연도 보상도 레벨마다 g100 고정.
- `CH0001`(골드 모으기) 순환: 보상 gold가 다시 채우는 루프 → "누적 **획득** gold" 별도 카운터?
- `CH0007`(골드 소비)=누적 소비량, `CH0008`(아이템 모으기)=보유 item1~3 합계 기준(추후 아이템별 분리 가능).
- 보상 수치(g/i1~3), item1~3의 정체, 영/일 번역 표현은 게임 톤에 맞춰 다듬기.
</content>
