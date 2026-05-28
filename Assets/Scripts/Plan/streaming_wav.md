# 생성 병렬, 재생만 순차 Flow Plan (GeminiDirect 포함)

## 배경 문제 요약(정리)
- ProcessReply에서 스트리밍 응답을 받을 때마다, 누적된 텍스트를 기준으로 “새로 완성된 문장”을 뽑아 TTS 요청을 보냅니다.
- 이 TTS 요청들은 모두 비동기로 병렬 실행됩니다.
- 그 결과, 문장 길이가 길어 생성이 늦게 끝나는 문장이 뒤로 밀리지 않고, “완료된 순서대로” 재생 큐에 들어가 버립니다.
- 그래서 실제로는 의도한 문장 순서(1 2 3)가 아니라, 예를 들어 1 3 2처럼 재생되는 문제가 자주 발생합니다.
- 목표는 “TTS 생성은 병렬로 유지하되, 재생은 문장 순서대로 강제”하는 것입니다.

## 목적
- TTS 생성은 문장별로 병렬 요청
- 재생은 seq 기준으로 순차 보장
- 자기 차례에 준비가 안 됐으면 최대 2초만 대기
- 500, 실패 응답, 예외 등 확실한 실패면 대기 없이 즉시 스킵
- CallConversationStreamGeminiDirect 경로에도 동일한 순서 재생 Flow 적용
- 한 컴포넌트가 여러 일을 한 번에 하지 않도록(단일 책임) 역할을 분리

## 핵심 아이디어
- 스트리밍으로 문장이 확정될 때마다 seq를 할당하고 TTS 요청은 병렬로 발사한다
- 재생은 nextSeqToPlay만 바라보며 순차 처리한다
- 내 차례인데 아직 준비가 안 됐으면 최대 2초만 기다린다
- 실패가 확정된 경우는 기다리지 않고 즉시 스킵한다
- 대화가 바뀌면 sessionId를 증가시키고 세션 데이터를 리셋해서, 늦게 도착한 과거 결과를 무시한다

## SessionDataTTS 구조체 개념
- type용 클래스는 두지 않고 state를 문자열로만 관리한다
- 데이터는 seq를 key로 하는 Dictionary들로 구성한다

예시 구조체(개념)
```csharp
using System.Collections.Generic;

public struct SessionDataTTS
{
    // 세션 격리용
    public int sessionId;      // 대화 시작 시 증가
    public int chatIdxNum;     // 풍선 기준 대화 번호(선택)

    // 순서 제어
    public int nextSeqToPlay;      // 다음 재생 대상
    public int nextSeqToAllocate;  // 새 문장 등록 시 seq 부여

    // seq별 데이터
    public Dictionary<int, string> textBySeq;          // 확정된 재생 텍스트
    public Dictionary<int, byte[]> wavBySeq;           // TTS 성공 결과
    public Dictionary<int, string> stateBySeq;         // "pending","in_flight","ready","failed","skipped","played"
    public Dictionary<int, float> waitStartTimeBySeq;  // 내 차례에서 대기 시작 시간

    // 초기화
    public void Reset(int newSessionId, int newChatIdxNum)
    {
        sessionId = newSessionId;
        chatIdxNum = newChatIdxNum;

        nextSeqToPlay = 0;
        nextSeqToAllocate = 0;

        textBySeq = new Dictionary<int, string>();
        wavBySeq = new Dictionary<int, byte[]>();
        stateBySeq = new Dictionary<int, string>();
        waitStartTimeBySeq = new Dictionary<int, float>();
    }
}
````

state 문자열 규칙(권장)

* pending: 문장 등록만 됨, 요청 전
* in_flight: 요청 발사됨, 응답 대기
* ready: wav 준비됨
* failed: 확실 실패(500, 예외, 서버 error)
* skipped: 타임아웃 등으로 스킵
* played: 큐 삽입 완료(또는 실제 재생 완료)

## 단일 책임 분리(여러 일을 한 번에 하지 않기)

아래 3개 블록이 서로 역할을 섞지 않게 분리한다.

1. 문장 등록 및 요청 발사 담당

* 위치 후보: ProcessReply, ProcessReplyGeminiDirect
* 하는 일

  * 새 문장 확정 시 seq 할당(nextSeqToAllocate 증가)
  * textBySeq[seq] 저장
  * stateBySeq[seq]를 pending으로 두고, 즉시 in_flight로 전환한 뒤 TTS 요청을 병렬 발사
* 하지 않는 일

  * VoiceManager 큐에 wav를 넣지 않음
  * 재생 순서를 판단하지 않음
  * 2초 대기 로직을 여기서 처리하지 않음

2. TTS 요청 결과 반영 담당

* 위치 후보: GetKoWavFromAPI / GetJpWavFromAPI 내부를 래핑하거나, 결과를 bytes로 돌려받는 경로
* 하는 일

  * 성공이면 wavBySeq[seq] 저장 + stateBySeq[seq] = ready
  * 확실 실패면 stateBySeq[seq] = failed
  * sessionId가 다르면(대화가 이미 바뀐 뒤 도착) 결과를 무시
* 하지 않는 일

  * 재생 순서 판단 금지
  * VoiceManager 조작 금지
  * UI 갱신 금지

3. 순차 재생 코디네이터

* 위치 후보: APIManager.Update 또는 VoiceManager 쪽의 전용 Coordinator
* 하는 일

  * nextSeqToPlay만 바라보고 처리

    * ready면 VoiceManager 큐에 삽입하고 played 처리 후 nextSeqToPlay++
    * failed면 대기 없이 즉시 스킵(nextSeqToPlay++)
    * pending 또는 in_flight면 최대 2초 대기
    * 2초 넘으면 skipped 처리 후 nextSeqToPlay++
* 하지 않는 일

  * TTS 요청 발사 금지
  * 문장 텍스트 갱신 금지
  * 세션 리셋 금지(리셋은 한 군데에서만)

## 2초 안전망 규칙

* 대상은 오직 내 차례(seq == nextSeqToPlay)만
* 내 차례 상태가 ready면 즉시 재생
* 내 차례 상태가 failed면 2초 기다리지 않고 즉시 스킵
* 내 차례 상태가 pending 또는 in_flight면

  * waitStartTimeBySeq에 시작 시각을 기록(없으면 기록)
  * now - start가 2초 이하이면 대기 유지
  * 2초를 초과하면 skipped 처리 후 다음 seq로 진행

## 세션 리셋 규칙(과거 결과 섞임 방지)

세션 리셋은 반드시 한 메소드로만 수행한다.

* 예: BeginTtsSession(chatIdxNum) 같은 단일 함수

BeginTtsSession이 하는 일

* sessionId 증가
* SessionDataTTS.Reset(sessionId, chatIdxNum)
* VoiceManager.ResetAudio()는 기존 정책대로 유지하되, 호출 위치는 중복되지 않게 정리

세션 리셋 호출 지점 후보

* FetchStreamingData 경로

  * chatIdxBalloon이 바뀌는 순간(이미 VoiceManager.ResetAudio() 하는 지점)에서 BeginTtsSession 호출
* CallConversationStreamGeminiDirect 경로

  * 함수 시작 직후 BeginTtsSession 호출
  * GeminiDirect는 FetchStreamingData를 거치지 않으므로 별도 리셋이 반드시 필요

## 기존 흐름에 붙일 위치(개괄)

ProcessReply(일반 스트림)

* reply_list에서 answerVoice가 확정되는 시점에만

  * seq 발급 및 문장 등록
  * TTS 병렬 요청 발사

ProcessReplyGeminiDirect(GeminiDirect)

* 번역이 끝나 answerVoice가 확정되는 시점에만

  * 동일하게 seq 발급 및 문장 등록
  * TTS 병렬 요청 발사

TTS 결과 반영

* GetKoWavFromAPI / GetJpWavFromAPI 응답을 받는 지점에서

  * 성공이면 wavBySeq[seq] 저장, state ready
  * 500, 예외, 서버 에러 등 확실 실패면 state failed
  * sessionId 불일치면 무시

재생 코디네이터

* Update에서 매 프레임 또는 일정 주기로

  * EvaluateNext 같은 함수로 nextSeqToPlay를 판단
  * ready면 VoiceManager 큐에 삽입하고 nextSeqToPlay++
  * failed면 즉시 nextSeqToPlay++
  * pending 또는 in_flight면 최대 2초 대기 후 타임아웃 스킵

## CallConversationStreamGeminiDirect 경로에도 동일 Flow 적용

* GeminiDirect는 onChunkReceived로 ProcessReplyGeminiDirect가 반복 호출되므로

  * 문장 확정 시점마다 seq 기반으로 병렬 요청 발사
* 재생은 일반 스트림과 동일하게 nextSeqToPlay만 바라보는 코디네이터가 담당
* 세션 리셋은 GeminiDirect 시작 직후 반드시 수행
* sessionId로 늦게 도착한 과거 wav 결과를 무시

## 적용 완료 후 확인 체크리스트

문제가 없이 적용되었을 경우 아래가 성립해야 한다.

1. 순서 보장

* 생성 완료 순서와 무관하게, VoiceManager 큐에는 항상 0,1,2 순으로 들어간다

2. 실패 즉시 스킵

* 특정 seq가 500 또는 예외를 받으면

  * state가 failed가 되고 2초 대기 없이 즉시 다음 seq로 진행한다

3. 느림 2초 타임아웃

* 특정 seq가 늦게 오면

  * 내 차례가 된 시점부터 최대 2초까지만 기다리고
  * 이후 skipped 처리 후 다음 seq로 넘어간다

4. 세션 격리

* 새 대화가 시작되면 sessionId가 바뀌고

  * 이전 대화의 늦게 도착한 wav 결과는 무시된다

5. GeminiDirect 동일 동작

* CallConversationStreamGeminiDirect에서도 1)~4)가 동일하게 성립한다

필요한 최소 로그 예시(개념)

* BeginTtsSession sessionId=... chatIdx=...
* Register seq=... textLen=...
* TTS start seq=...
* TTS ready seq=... bytes=...
* Playback wait seq=... elapsed=...
* Playback enqueue seq=...
* Playback skip seq=... reason=failed 또는 reason=timeout
