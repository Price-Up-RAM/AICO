# 웹 검색 메타 정보 - Unity 참고 문서

## 추가된 intent_info 필드

웹 검색 요청(`intent_web = "force"` or `"on"`) 시 응답의 `intent_info`에 3개 필드가 추가됩니다.

### 1. web_search_keyword
- **타입**: `string`
- **설명**: LLM이 생성한 실제 검색 키워드
- **예시**: `"테슬라 주가 오늘 실시간"`, `"서울 날씨"`
- **빈 문자열인 경우**: 키워드 생성 실패 또는 LLM이 Search_web 명령을 생성하지 못함

### 2. web_search_method
- **타입**: `string`
- **설명**: 사용된 검색 엔진/방법
- **가능한 값**:
  - `"langchain_search_duckduckgo"` - DuckDuckGo 검색 성공
  - `"langchain_search_Tavily"` - Tavily 검색 성공
  - `"langchain_search_GoogleCSE"` - Google CSE 검색 성공
  - `"langchain_search_serper"` - Serper 검색 성공
  - `"langchain_search_brave"` - Brave 검색 성공
  - `"Fail(Keyword)"` - 키워드 생성 실패
  - `"Fail(LLM)"` - LLM이 Search_web 명령 생성 실패
  - `"Fail"` - 모든 검색 엔진 실패
  - `""` (빈 문자열) - 기본값 (웹 검색 미실행)

### 3. web_search_content
- **타입**: `string`
- **설명**: 검색된 원본 내용 (요약본, 최대 3000자)
- **예시**: `"Tesla Inc (TSLA) Stock Price & News..."`
- **빈 문자열인 경우**: 검색 실패

---

## 응답 JSON 예시

### 정상 케이스
```json
{
  "reply_list": [...],
  "intent_info": {
    "is_intent_web": "on",
    "web_search_keyword": "테슬라 주가 오늘 실시간",
    "web_search_method": "langchain_search_duckduckgo",
    "web_search_content": "Tesla Inc (TSLA) Stock Price...",
    "web_search_detail": "false",
    ...
  },
  "ai_info": {...}
}
```

### 실패 케이스 1: 키워드 생성 실패
```json
{
  "intent_info": {
    "web_search_keyword": "",
    "web_search_method": "Fail(Keyword)",
    "web_search_content": ""
  }
}
```

### 실패 케이스 2: LLM이 Search_web 생성 실패
```json
{
  "intent_info": {
    "web_search_keyword": "",
    "web_search_method": "Fail(LLM)",
    "web_search_content": ""
  }
}
```

### 실패 케이스 3: 검색 엔진 전체 실패
```json
{
  "intent_info": {
    "web_search_keyword": "테슬라 주가",
    "web_search_method": "Fail",
    "web_search_content": ""
  }
}
```

## 참고 Python 파일

| 파일 | 역할 |
|------|------|
| `server_interface_func.py` | `intent_info` 기본 구조 정의 (L232-245) |
| `server_interface.py` | 웹 검색 처리 및 메타데이터 설정 (L587-670) |
| `ai_web_search.py` | 메타데이터 생성 및 관리 (L20-40, L506, L187-207) |
| `util_searcher.py` | 검색 수단 실행 및 결과 반환 (L45-101) |
| `ai_web_search_keyword.py` | 키워드 생성 (L172-223) |

---

## 기존 필드와의 관계

- **기존**: `web_search_keyword` (이미 존재했던 필드)
- **신규**: `web_search_method`, `web_search_content`

`is_intent_web = "on"`일 때만 메타데이터가 채워집니다.
