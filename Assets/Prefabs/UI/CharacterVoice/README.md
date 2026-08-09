# Character Voice

`CharacterVoice`는 캐릭터별 알람·포모도로 대사를 생성하고, 해당 캐릭터의
음성으로 TTS를 생성하여 로컬에 보관하는 UI와 저장 계층이다.

캐릭터 식별에는 코드가 아니라 현재 캐릭터의 이름(닉네임)을 사용한다.
생성된 파일은 Unity 프로젝트의 `Assets` 아래가 아니라
`Application.persistentDataPath` 아래에 저장된다.

## 구성

```text
CharacterVoice/
├─ Scripts/
│  ├─ CharacterVoiceAlarmView.cs
│  ├─ CharacterVoiceAlarmConfirmView.cs
│  ├─ CharacterVoicePomodoroView.cs
│  ├─ CharacterVoicePomodoroConfirmView.cs
│  └─ CharacterVoiceViewLauncher.cs
├─ Resources/
│  ├─ CharacterVoice/
│  │  ├─ CharacterVoiceAlarmView.prefab
│  │  ├─ CharacterVoiceAlarmConfirmView.prefab
│  │  ├─ CharacterVoicePomodoroView.prefab
│  │  └─ CharacterVoicePomodoroConfirmView.prefab
│  └─ CharacterPomodoroVoiceCatalog.asset
└─ Editor/
   └─ CharacterVoicePrefabBuilder.cs
```

저장소와 기본 음성 카탈로그 타입은 다음 위치에 있다.

```text
Assets/Scripts/CharacterAlarmVoiceRepository.cs
Assets/Scripts/CharacterAlarmVoiceCatalog.cs
Assets/Scripts/CharacterPomodoroVoiceRepository.cs
Assets/Scripts/CharacterPomodoroVoiceCatalog.cs
```

알람 기본 카탈로그의 기본 Resource asset은 다음 위치에 있다.

```text
Assets/Prefabs/UI/CharacterVoice/Resources/CharacterAlarmVoiceCatalog.asset
```

## 전체 흐름

```text
CharacterDetail
  → CharacterVoiceViewLauncher
  → Alarm 또는 Pomodoro 관리 화면
  → 대사 생성 API
  → 후보 JSON 파싱
  → Confirm 화면에서 후보별 TTS 생성
  → 사용자가 복수 후보 확인·선택
  → 선택된 WAV와 대사를 Repository에 저장
  → Repository.Changed 이벤트
  → CharacterDetail의 기본/생성 개수 갱신
```

직접 입력은 대사 생성 API를 거치지 않는다. 입력한 대사를 바로
`/getSound`에 전달하고, 성공한 WAV와 대사를 Repository에 저장한다.

## 화면을 여는 값

`CharacterDetailController`가 다음 값을
`CharacterVoiceViewLauncher`에 전달한다.

| 값 | 출처 | 용도 |
| --- | --- | --- |
| `characterName` | 현재 캐릭터의 닉네임 | API 요청과 저장 폴더 식별 |
| `refId` | CharacterDetail의 음성 드롭다운 | 샘플 음성 및 TTS 음색 |
| `uiLanguage` | `settings.ui_language` | 랜덤 컨셉, 대사 생성 요청, 저장·표시 문장 언어 |
| `language` | `settings.sound_language` | 번역 성공 시 TTS 언어 |
| `speed` | `settings.sound_speedMaster` | TTS 속도 |
| catalog | Alarm/Pomodoro 기본 음성 SO | 기본 대사와 AudioClip 표시 |

언어 값이 `jp`이면 요청 전에 `ja`로 정규화한다. 랜덤 컨셉 문구와 컨셉
placeholder는 `ui_language`를 기준으로 한국어·일본어·영어 중 하나를
사용한다.

## 활성화와 비활성화

활성 상태는 해당 대사를 런타임 재생 후보에 포함할지를 나타낸다. 항목을
삭제하거나 숨기는 상태와는 별개다.

관리 화면에서 Alarm 또는 Pomodoro 행의 버튼이 아닌 본문을 짧게 클릭하면
해당 항목의 활성 상태가 반전된다. 변경 즉시 metadata에 저장하고
Repository의 `Changed` 이벤트를 발생시키므로 별도의 저장 버튼은 없다.

| 항목 종류 | 저장 위치 | 초기값과 우선순위 |
| --- | --- | --- |
| 생성 Alarm | `alarms[].enabled` | 생성 시 `true` |
| 기본 Alarm | `defaultAlarmStates[]` | override가 없으면 Alarm SO의 `enabled` 사용 |
| 생성 Pomodoro | `dialogues[].enabled` | 생성 시 `true` |
| 기본 Pomodoro | `defaultDialogueStates[]` | override가 없으면 Pomodoro SO의 `enabled` 사용 |

기본 항목의 metadata 상태에는 `hasEnabledOverride`와 `enabled`가 함께
저장된다. `hasEnabledOverride == true`이면 SO의 `enabled`보다 metadata
값이 우선한다. 따라서 이미 사용자가 상태를 변경한 기본 항목의 SO
`enabled`를 나중에 바꾸더라도 해당 사용자의 저장 상태는 바뀌지 않는다.

화면 표시는 다음과 같다.

- 활성: 녹색 계열 행 배경과 정상 불투명도
- 비활성: 어두운 회색 행 배경과 낮은 불투명도

행 색상과 투명도를 조정하는 위치:

```text
Assets/Prefabs/UI/CharacterVoice/Scripts/CharacterVoiceAlarmView.cs
Assets/Prefabs/UI/CharacterVoice/Scripts/CharacterVoicePomodoroView.cs
```

두 스크립트 상단의 `EnabledRowColor`, `DisabledRowColor`,
`DisabledRowAlpha`가 각각 활성 배경, 비활성 배경, 비활성 행 전체
불투명도를 결정한다. 활성/비활성 대사 글자색은
`MemoryArchiveUi.TextWhite`, `MemoryArchiveUi.TextMuted`를 사용하며 공통
색상 정의는 다음 파일 상단에 있다.

```text
Assets/Prefabs/UI/MemoryArchive/Scripts/MemoryArchiveUi.cs
```

비활성 항목도 목록에는 남아 있고 듣기, 재생성, 길게 눌러 대사 수정,
삭제가 가능하다. 활성 상태만 바꾸는 경우 대사와 WAV/AudioClip은
변경하지 않는다. 대사 수정도 활성 상태를 변경하지 않으며, 재생성은
기존 행의 활성 상태를 새 음성에 그대로 적용한다.

`GetPlayableCandidates`는 활성 상태이면서 비어 있지 않은 대사와 실제
음성이 모두 있는 항목만 반환한다. 생성 항목은 WAV 파일, 기본 항목은
SO의 AudioClip이 있어야 한다. Alarm 런타임은 이 결과만 랜덤 재생 후보로
사용한다. Pomodoro도 같은 필터 메서드를 제공하지만 현재 타이머의 실제
재생 코드에는 아직 연결되어 있지 않다.

삭제/숨김과 비활성의 차이:

- 생성 항목 삭제: metadata에서 항목을 제거하고 연결된 WAV도 삭제
- 기본 항목 삭제: SO는 유지하고 해당 ID를 metadata의 숨김 목록에 저장
- 비활성: 항목과 음성을 유지한 채 랜덤 재생 후보에서만 제외

## 기본 Alarm/Pomodoro 값 추가

기본 값은 생성 WAV와 달리 ScriptableObject와 Unity AudioClip으로
관리한다. `persistentDataPath`로 복사되지 않으며 빌드에는 SO가 참조하는
AudioClip이 포함된다.

현재 기본 카탈로그 에셋:

```text
Alarm
Assets/Prefabs/UI/CharacterVoice/Resources/CharacterAlarmVoiceCatalog.asset

Pomodoro
Assets/Prefabs/UI/CharacterVoice/Resources/CharacterPomodoroVoiceCatalog.asset
```

기존 에셋에 값을 추가하는 절차:

1. 사용할 음성 파일을 Unity 프로젝트의 `Assets` 아래로 가져와
   `AudioClip`으로 임포트한다.
2. Project 창에서 Alarm 또는 Pomodoro 카탈로그 에셋을 선택한다.
3. Inspector의 `Characters` 목록에 대상 캐릭터 항목을 추가한다.
4. `Character Name`에 `CharAttributes.nickname`과 같은 캐릭터 닉네임을
   입력한다. 비교는 대소문자를 구분하지 않는다.
5. Alarm은 해당 캐릭터의 `Alarms`, Pomodoro는 `Dialogues` 목록에 기본
   항목을 추가한다.
6. 각 항목의 `Id`, `Label`, `Message`, `Audio Clip`, `Enabled`를 채운다.
7. 에셋을 저장한다. SO 데이터만 바뀌므로 CharacterVoice 프리팹을 다시
   베이크할 필요는 없다.

Inspector 구조 예:

```text
Characters
└─ Element 0
   ├─ Character Name: ARONA
   └─ Alarms 또는 Dialogues
      ├─ Element 0
      │  ├─ Id: default_arona_01
      │  ├─ Label: 기본1
      │  ├─ Message: 시간이 되었습니다.
      │  ├─ Audio Clip: arona_alarm_01
      │  └─ Enabled: true
      └─ Element 1
         └─ ...
```

필드 규칙:

| 필드 | 규칙 |
| --- | --- |
| `characterName` | `CharAttributes.nickname`과 일치해야 함. 같은 캐릭터 행을 중복 생성하지 않음 |
| `id` | 해당 캐릭터 안에서 고유하고 영구적으로 유지할 값 |
| `label` | 관리용 이름. 예: `기본1`, `기본2` |
| `message` | 말풍선에 표시되고 AudioClip과 매핑되는 대사 |
| `audioClip` | 해당 대사를 말하는 기본 음성. 없으면 재생 후보에서 제외 |
| `enabled` | 아직 사용자 metadata override가 없을 때 적용할 최초 활성 상태 |

`id`는 저장 후 변경하지 않는 것이 원칙이다. 기본 항목의 활성 override,
수정 대사, 숨김 상태가 모두 이 ID를 기준으로 metadata에 저장된다. ID를
바꾸면 기존 사용자 metadata와 연결되지 않고 새 기본 항목으로 취급된다.
반대로 새 항목에 과거에 숨긴 ID를 재사용하면 해당 사용자의 화면에서
보이지 않을 수 있다.

같은 캐릭터의 기본 항목을 추가할 때는 새 `Characters` 행을 만들지 말고
기존 캐릭터 행의 `Alarms` 또는 `Dialogues` 배열 크기를 늘린다.
`GetDefaults`는 이름이 일치하는 첫 번째 캐릭터 행만 반환한다.

카탈로그 에셋이 없는 프로젝트에서 새로 만들 때는 Unity의
`Assets > Create > Jarvis > Character Alarm Voice Catalog` 또는
`Character Pomodoro Voice Catalog`를 사용한다. `LoadDefault()`가
`Resources.Load`로 찾으므로 에셋 파일명은 각각
`CharacterAlarmVoiceCatalog`, `CharacterPomodoroVoiceCatalog`여야 하며
반드시 어느 `Resources` 폴더의 바로 아래에 두어야 한다. 별도 하위
폴더에 넣으면 현재 `LoadDefault()`의 Resource 경로도 함께 바꿔야 한다.

## 대사 생성 요청

엔드포인트의 앞부분은 각 View의 `ResolveVoiceBaseUrl`로 결정한다.
로컬 음성 설정에서는 `http://127.0.0.1:5000`을 사용하고, 그 외에는
`ServerManager`를 통해 서버 URL을 얻는다.

### Alarm

```http
POST {baseUrl}/agent/alarm/make
Content-Type: multipart/form-data
```

| 필드 | 값 |
| --- | --- |
| `character_name` | 캐릭터 이름 |
| `lang` | 정규화된 `ui_language` |
| `num_alarms` | 요청 후보 수. 현재 UI 값은 3 |
| `custom_request` | 컨셉 입력값 |
| `player_name` | `settings.player_name`, 빈 값이면 `선생님` |

성공 응답에서 사용하는 형태:

```json
{
  "status": "success",
  "alarm_messages": [
    "첫 번째 알람 대사",
    "두 번째 알람 대사"
  ]
}
```

`status`가 `success`이고 `alarm_messages`가 JSON 배열이어야 한다.
비어 있지 않은 문자열만 후보로 사용하며 중복 문자열은 제거한다.
후보가 1개 또는 2개여도 실패로 취급하지 않는다.

### Pomodoro

```http
POST {baseUrl}/agent/pomodoro/make
Content-Type: multipart/form-data
```

| 필드 | 값 |
| --- | --- |
| `character_name` | 캐릭터 이름 |
| `lang` | 정규화된 `ui_language` |
| `num_dialogues` | 요청 후보 수. 현재 UI 값은 3 |
| `custom_request` | 컨셉 입력값 |
| `player_name` | `settings.player_name`, 빈 값이면 `선생님` |

성공 응답에서 사용하는 형태:

```json
{
  "status": "success",
  "dialogues": [
    "첫 번째 포모도로 대사",
    "두 번째 포모도로 대사"
  ]
}
```

`status`가 `success`이고 `dialogues`가 JSON 배열이어야 한다.
비어 있지 않은 문자열이 하나 이상 있으면 Confirm 화면을 연다.

## TTS와 샘플 음성

후보 Confirm, 직접 입력, 재생성은 다음 요청을 사용한다. 목록에서 대사
텍스트만 수정할 때는 이 요청을 보내지 않고 metadata만 저장한다.

`ui_language`와 `sound_language`가 다르면 `/getSound` 전에 다음 번역
요청을 보낸다.

```http
POST {baseUrl}/translate
Content-Type: application/json
```

```json
{
  "text": "표시할 원문",
  "source_lang": "ko",
  "target_lang": "ja"
}
```

서버는 `ko`, `ja`, `en`을 지원하며 성공 시 다음 핵심 값을 반환한다.

```json
{
  "status": "success",
  "original_text": "표시할 원문",
  "translated_text": "音声に使う翻訳文",
  "source_lang": "ko",
  "target_lang": "ja"
}
```

표시·저장 문장은 번역 결과와 관계없이 항상 `ui_language`로 만든 원문이다.
번역 성공 시에만 `translated_text`와 `sound_language`를 `/getSound`에
전달한다. HTTP 오류, 잘못된 JSON, 빈 번역문을 포함하여 번역이 실패하면
원문과 `ui_language`를 `/getSound`에 전달한다. 번역 실패 자체는 대사 생성
실패로 처리하지 않는다.

```http
POST {baseUrl}/getSound
Content-Type: application/json
```

```json
{
  "text": "음성으로 만들 대사",
  "char": "캐릭터 이름",
  "lang": "ko",
  "speed": "100",
  "chatIdx": "-1",
  "ref_id": "선택된 음성 ref id"
}
```

`ref_id`는 선택된 음성이 있을 때만 포함한다. 성공 응답 본문은 WAV
바이너리로 취급한다.

Confirm 화면은 모든 후보에 대해 TTS를 먼저 요청한다. 각 행에서 음성을
듣거나 재생성할 수 있고, 준비가 완료된 후보 중 복수 항목을 선택하여
한 번에 저장할 수 있다.

샘플 듣기는 선택된 `refId`가 있으면 다음 요청을 사용한다.

```http
POST {baseUrl}/getSampleVoice
Content-Type: application/json

{
  "ref_id": "선택된 음성 ref id"
}
```

`refId`가 없으면 `ui_language`로 고른 각 View의 고정 샘플 문구에도 같은
번역 및 TTS 폴백 규칙을 적용한 뒤 `/getSound`로 생성한다.

## 로컬 저장 위치

캐릭터 이름에서 파일명으로 사용할 수 없는 문자는 `_`로 치환한다.
이름이 비어 있으면 폴더명으로 `unknown`을 사용한다.

```text
Application.persistentDataPath/
└─ voice/
   └─ {characterName}/
      ├─ alarm/
      │  ├─ metadata.json
      │  ├─ generated_*.wav
      │  └─ ...
      └─ pomodoro/
         ├─ metadata.json
         ├─ generated_*.wav
         └─ ...
```

생성 ID와 파일명 형식:

```text
generated_{yyyyMMdd_HHmmss_fff}_{8자리 GUID}
generated_{yyyyMMdd_HHmmss_fff}_{8자리 GUID}.wav
```

`Application.persistentDataPath`의 실제 운영체제 경로는 플랫폼과
Player 설정에 따라 달라지므로 코드에서 해당 프로퍼티로 확인한다.

## Alarm metadata

파일:

```text
{persistentDataPath}/voice/{characterName}/alarm/metadata.json
```

형태:

```json
{
  "characterName": "ARONA",
  "customAlarmVoiceEnabled": true,
  "alarms": [
    {
      "id": "generated_20260728_010203_456_a1b2c3d4",
      "label": "생성1",
      "message": "선생님, 시간이 되었어요.",
      "audioFileName": "generated_20260728_010203_456_a1b2c3d4.wav",
      "source": "generated",
      "refId": "voice-reference-id",
      "language": "ko",
      "createdAtUtc": "2026-07-27T16:02:03.4560000Z",
      "enabled": true
    }
  ],
  "hiddenDefaultAlarmIds": [
    "default_2"
  ],
  "defaultAlarmStates": [
    {
      "id": "default_1",
      "hasEnabledOverride": true,
      "enabled": false,
      "hasMessageOverride": true,
      "message": "수정해서 저장한 기본 알람 대사"
    }
  ]
}
```

| 필드 | 의미 |
| --- | --- |
| `characterName` | 폴더와 데이터의 캐릭터 이름 |
| `customAlarmVoiceEnabled` | 생성 항목 추가 시 `true`로 저장되는 필드. 현재 랜덤 재생 후보 판정에는 사용하지 않음 |
| `alarms` | 생성된 대사와 WAV 매핑 목록 |
| `hiddenDefaultAlarmIds` | UI와 재생 후보에서 숨길 기본 SO 항목 ID |
| `defaultAlarmStates` | 기본 SO 항목의 캐릭터별 활성 상태·대사 override |
| `id` | 생성 항목의 고유 ID |
| `label` | 내부 표시용 생성 순번 라벨 |
| `message` | `ui_language`로 생성되어 말풍선과 관리 화면에 표시되는 원문 |
| `audioFileName` | 같은 폴더에 있는 WAV 파일명 |
| `source` | 생성 항목은 `generated` |
| `refId` | 마지막 생성에 사용한 음성 참조 |
| `language` | WAV 생성에 실제 사용한 언어. 번역 성공 시 `sound_language`, 번역 실패 시 `ui_language` |
| `createdAtUtc` | 생성 또는 재생성 시각의 UTC ISO 8601 문자열 |
| `alarms[].enabled` | 생성 항목의 활성 상태 |
| `defaultAlarmStates[].hasEnabledOverride` | 기본 SO의 `enabled` 대신 metadata 값을 사용할지 여부 |
| `defaultAlarmStates[].enabled` | 기본 항목의 캐릭터별 활성 상태 |
| `defaultAlarmStates[].hasMessageOverride` | 기본 SO의 `message` 대신 metadata 값을 사용할지 여부 |
| `defaultAlarmStates[].message` | 기본 항목의 캐릭터별 수정 대사 |

생성 음성을 추가하면 `customAlarmVoiceEnabled`는 `true`로 저장된다.
Alarm 관리 화면에서 행을 짧게 클릭하면 해당 항목의 활성 상태를 반전하고
즉시 metadata에 저장한다. 별도의 저장 버튼은 없다. 생성 항목은
`alarms[].enabled`, 기본 항목은 `defaultAlarmStates`에 저장된다.

현재 `GetPlayableCandidates`는 기본·생성 항목 모두 `enabled == true`인
경우만 사용한다. 생성 항목은 대사와 실제 WAV 파일의 존재도 확인하고,
기본 항목은 AudioClip 존재도 확인한다.

## Pomodoro metadata

파일:

```text
{persistentDataPath}/voice/{characterName}/pomodoro/metadata.json
```

형태:

```json
{
  "characterName": "ARONA",
  "dialogues": [
    {
      "id": "generated_20260728_010203_456_a1b2c3d4",
      "label": "생성1",
      "message": "집중할 시간이에요.",
      "audioFileName": "generated_20260728_010203_456_a1b2c3d4.wav",
      "source": "generated",
      "refId": "voice-reference-id",
      "language": "ko",
      "createdAtUtc": "2026-07-27T16:02:03.4560000Z",
      "enabled": true
    }
  ],
  "hiddenDefaultDialogueIds": [
    "default_1"
  ],
  "defaultDialogueStates": [
    {
      "id": "default_2",
      "hasEnabledOverride": true,
      "enabled": false,
      "hasMessageOverride": true,
      "message": "수정해서 저장한 기본 포모도로 대사"
    }
  ]
}
```

`dialogues`의 항목 필드는 Alarm의 `alarms` 항목과 같은 의미다.
`hiddenDefaultDialogueIds`는 숨길 Pomodoro 기본 SO 항목 ID 목록이다.
`defaultDialogueStates`는 기본 SO 항목의 캐릭터별 활성 상태와 수정 대사를
저장한다. 각 override의 적용 여부는 `hasEnabledOverride`,
`hasMessageOverride`로 구분한다.
Pomodoro metadata에는 `customAlarmVoiceEnabled` 필드가 없다.

Pomodoro 관리 화면에서도 행을 짧게 클릭하면 활성 상태가 반전되고 즉시
metadata에 저장된다. 생성 항목은 `dialogues[].enabled`, 기본 항목은
`defaultDialogueStates`에 저장된다. `GetPlayableCandidates`는 활성 상태이며
대사와 실제 음성이 모두 있는 항목만 반환한다.

## 기본 음성과 생성 음성

기본 음성은 ScriptableObject에서 가져오며 persistentDataPath에 복사하지
않는다.

기본 항목:

```text
id
label
message
audioClip
enabled
```

카탈로그의 `characterName`은 캐릭터 닉네임과 대소문자를 무시하고
비교한다. 기본 항목의 `id`는 캐릭터별 카탈로그 안에서 안정적으로
유지해야 한다. 이 ID가 metadata의 숨김 목록과 연결된다.

관리 화면의 표시 목록은 다음 두 소스를 합친다.

1. 카탈로그의 기본 항목
2. metadata의 생성 항목

기본 항목 삭제는 원본 SO나 AudioClip을 삭제하지 않고 해당 ID를 metadata
숨김 목록에 추가한다. 생성 항목 삭제는 metadata에서 항목을 제거한 뒤
연결된 WAV 파일을 삭제한다.

Alarm과 Pomodoro의 텍스트 수정은 TTS를 요청하지 않는다. Alarm 생성 항목은
`alarms[].message`만 수정하고 기존 WAV를 유지한다. 기본 항목은
`defaultAlarmStates[].message`에 수정값을 저장하고 기존 SO AudioClip을
유지한다. 따라서 텍스트와 기존 음성이 다를 수 있으며, 음성을 맞추려면
사용자가 해당 행의 재생성을 실행해야 한다.

Pomodoro 생성 항목은 `dialogues[].message`만 수정하고 기존 WAV를 유지한다.
기본 항목은 `defaultDialogueStates[].message`에 수정값을 저장하고 기존 SO
AudioClip을 유지한다.

재생성 버튼은 대사를 바꾸지 않고 TTS만 다시 요청한다. 생성 항목은 기존
WAV를 갱신한다. 기본 Alarm·Pomodoro 항목은 생성 항목을 추가한 뒤 기존
기본 항목 ID를 숨긴다. Alarm과 Pomodoro 재생성은 기존 행의 활성·비활성
상태를 유지한다.

Alarm과 Pomodoro의 관리 행은 활성 상태일 때 녹색 계열 배경과 정상
불투명도, 비활성 상태일 때 어두운 회색 배경과 낮은 불투명도를 사용한다.
비활성 행도 듣기, 재생성, 길게 눌러 수정하는 조작은 가능하다.

## 런타임 재생 상태

### Alarm

`AlarmManager`가 알람 발생 시 현재 캐릭터 이름으로
`CharacterAlarmVoiceRepository.GetPlayableCandidates`를 호출한다.

1. 기본 SO 음성과 생성 WAV를 하나의 후보 목록으로 만든다.
2. 실제 재생 가능한 후보 중 하나를 `Random.Range`으로 선택한다.
3. 기본 항목은 SO의 AudioClip을 사용한다.
4. 생성 항목은 저장된 WAV를 `UnityWebRequestMultimedia.GetAudioClip`으로 읽는다.
5. 대사를 `AnswerBalloonSimpleManager`에 표시한다.
6. 음성을 `AlarmAudioPlayer.PlayAlarmClip`으로 재생한다.

재생 가능한 후보가 없거나 선택된 대사·음성을 읽지 못하면
`시간이 되었습니다.`를 4초 동안 말풍선에 표시하며 별도 음성은 재생하지
않는다.

### Pomodoro

현재 Pomodoro는 생성, 확인, 저장, 목록 표시, 수정, 삭제, 미리 듣기까지
구현되어 있다. Pomodoro 타이머가 저장된 후보를 골라 실제 알림으로
재생하는 런타임 소비 코드는 아직 연결되어 있지 않다.

## 변경 알림과 CharacterDetail

Repository 저장 성공 시 다음 이벤트를 발생시킨다.

```csharp
CharacterAlarmVoiceRepository.Changed
CharacterPomodoroVoiceRepository.Changed
```

이벤트 인자는 변경된 캐릭터 이름이다. `CharacterDetailController`는 현재
표시 중인 캐릭터와 이름이 같으면 기본/생성 항목 개수를 다시 계산한다.

## 프리팹 생성

View 프리팹은 `CharacterVoicePrefabBuilder`가 각 View의 `EditorBuild`를
호출하여 `Resources/CharacterVoice` 아래에 저장한다.

런타임에서는 `CharacterVoiceViewLauncher`가 다음 Resource 경로로
프리팹을 찾고 `CanvasUI` 아래에 한 번만 생성한다.

```text
CharacterVoice/CharacterVoiceAlarmView
CharacterVoice/CharacterVoiceAlarmConfirmView
CharacterVoice/CharacterVoicePomodoroView
CharacterVoice/CharacterVoicePomodoroConfirmView
```

View 생성 코드나 필수 계층을 변경한 경우 스크립트만 수정하지 말고 실제
프리팹도 다시 생성하고 `CharacterVoicePrefabBuilder.ValidatePrefabs()`의
필수 오브젝트 검증을 통과해야 한다.

## 새 음성 종류를 추가할 때의 기준

Alarm 또는 Pomodoro와 같은 새 종류는 다음 책임을 분리한다.

1. 대사 생성 View: 컨셉과 생성 API 요청, 응답 배열 검증
2. Confirm View: 후보별 TTS 준비, 듣기, 재생성, 복수 선택
3. Repository: persistentDataPath 경로, WAV, metadata 읽기·쓰기
4. Catalog SO: 캐릭터별 기본 대사와 AudioClip
5. Runtime consumer: 저장 후보 선택, WAV 로드, 말풍선과 실제 재생
6. Launcher와 CharacterDetail: 화면 진입과 항목 개수 갱신

대사 생성 성공과 TTS 생성 성공은 별개의 단계다. metadata에는 WAV 저장이
성공한 항목만 추가해야 하며, 런타임 소비자는 metadata뿐 아니라 실제
AudioClip 또는 WAV 파일 존재도 확인해야 한다.
