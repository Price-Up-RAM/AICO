using System.Collections.Generic;
using UnityEngine;

// 대화 요청 단위 상태 객체 (병렬 스트리밍 충돌 방지)
public class AIChatSession
{
    public List<string> replyListKo = new List<string>();  // 한국어 답변 누적
    public List<string> replyListJp = new List<string>();  // 일본어 답변 누적
    public List<string> replyListEn = new List<string>();  // 영어 답변 누적

    public string query_origin = "";         // 사용자 원문 발화
    public string query_trans = "";          // 번역된 사용자 발화
    public string ai_language_out = "en";   // 메모리에 저장할 언어
    public string currentMemoryType = "conversation";  // 현재 응답 메모리 타입
    public string latestIntentSmallTalkAnswer = "off";  // SmallTalk 연관 답변 여부
    public string latestSmallTalkQuery = "";  // 저장할 SmallTalk 문장

    public List<string> recommendedChoices = new List<string>(); // 추천 답변 목록

    public bool isResponsedStarted = false;   // 첫 응답 청크 수신 여부
    public bool isFirstBalloonShown = false;  // 첫 문장 말풍선 표시 여부 (GeminiDirect용)

    public GameObject targetCharacter;  // 현재 세션의 타겟 캐릭터
    public int chatIdxNum;              // 세션에 할당된 고유 대화 번호

    // 이 스트림이 시작한 TTS 세션 id 스냅샷.
    // 사용자 정지(CancelTtsSession)나 다른 발화의 세션 시작 이후 도착한 문장은
    // 이 값과 현재 세션 id가 달라지므로 TTS를 생략한다 (-1 = 미설정, 게이트 없음)
    public int ttsSessionId = -1;

    public AIChatSession(GameObject targetCharacter = null, int chatIdxNum = -1)
    {
        this.targetCharacter = targetCharacter;
        this.chatIdxNum = chatIdxNum;
    }
}
