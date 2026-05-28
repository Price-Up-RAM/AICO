# 개요

시나리오는 미리 만들어둔 음성을 사용하고 UI에 표기해줘야 함.
음성은 python등을 활용한 별도의 파이썬 코드인 voice_factory등에서 작업하고,
UI표기에는 현 프로젝트 LanguageData.cs 에서 관리하고 있음. 한국어 외 일본어 영어도 지원해야 함.

**너의 역할**: 시나리오 코드를 보고 LanguageData.cs와 voice_factory용 Python 코드를 생성하는 것

## 작업 내용
1. **LanguageData.cs**: 모든 대사의 한국어/일본어/영어 번역 추가
2. **voice_factory용 코드**: Python 음성 생성 스크립트용 데이터 생성

## 주의사항
- **기존 시나리오 변경 시**: ID를 절대 바꾸지 말 것. 기존 ID를 그대로 유지해야 음성 파일 매칭됨
- **새로운 시나리오 추가 시**: 새로운 ID를 부여하되, 기존 ID와 중복되지 않도록 확인
- **번역 일관성**: "선생님"은 항상 "先生" (jp), "Sensei" (en)로 번역

- **시나리오 ID**: `{카테고리}{번호}_{설명}` (예: `I00_greeting_1`, `S01_need_image_1`)
  - `I`: Installer 관련
  - `C`: Common 관련
  - `T`: Tutorial 관련
  - `S`: Ask (질문/요청) 관련
- **선택지 ID**: `{카테고리}{번호}_{설명}` (예: `S00_change_model`, `S01_need_image`)
- 각 대사마다 고유한 ID를 부여하되, 연속된 대사는 `_1`, `_2`, `_3` 등으로 구분

## 작업 순서

1. 시나리오 코드 확인
2. LanguageData.cs 업데이트: 모든 대사의 한국어/일본어/영어 번역 추가
3. ChoiceData.cs 업데이트 (선택지 있는 경우): 선택지 텍스트 추가
4. voice_factory용 코드 생성: Python 음성 생성 스크립트용 데이터

## 예시 1: 기본 시나리오 (선택지 없음)

- 변경 전 : 주어진 시나리오 내용

```C#
public IEnumerator Scenario_I00_Greeting()
{
    float d1 = ScenarioUtil.Narration("I00_greeting_1", "선생님, 안녕하세요.");
    ScenarioUtil.ShowEmotion("smile");
    yield return new WaitForSeconds(d1);

    yield return StartCoroutine(Scenario_I00_CurrentCheckVersion());
}

public IEnumerator Scenario_I00_CurrentCheckSample()
{
    float d1 = ScenarioUtil.Narration("I00_current_check_sample_1", "현재 Sample Edition을 사용 중이시네요.");
    ScenarioUtil.ShowEmotion("relax");
    yield return new WaitForSeconds(d1);
}
```

- LanguageData.cs : 번역 데이터 추가

```C#
        // I00 - 인사 및 버전 체크
        new Dictionary<string, string> { { "ko", "선생님, 안녕하세요." }, { "jp", "先生、こんにちは。" }, { "en", "Hello, Sensei." } },
        new Dictionary<string, string> { { "ko", "현재 Sample Edition을 사용 중이시네요." }, { "jp", "現在はSample Editionを使用中ですね。" }, { "en", "You're currently using the Sample Edition." } },

```

- voice_factory용 코드

```python
    # I00 - 인사 및 버전 체크
    audio_list.append(('I00_greeting_1', '선생님, 안녕하세요.', 'ko'))
    audio_list.append(('I00_greeting_1', '先生、こんにちは。', 'ja'))
    audio_list.append(('I00_greeting_1', 'Hello, Sensei.', 'en'))

    audio_list.append(('I00_current_check_sample_1', '현재 샘플 에디션을 사용 중이시네요.', 'ko'))
    audio_list.append(('I00_current_check_sample_1', '現在はサンプルエディションを使用中ですね。', 'ja'))
    audio_list.append(('I00_current_check_sample_1', 'You are currently using the Sample Edition.', 'en'))
```
