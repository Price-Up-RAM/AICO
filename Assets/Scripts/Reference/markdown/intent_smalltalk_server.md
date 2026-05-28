# intent_smalltalk_answer 구현 계획

## 개요
AI가 생성한 잡담(`query_smalltalk`)과 사용자 답변(`query`)의 연관성을 판단하는 intent 기능을 추가합니다.
`ai_chk_image_relevance.py`와 유사한 패턴으로 구현하며, force 옵션을 지원합니다.

## 배경
- AI는 `ai_trigger_small_talk.py`를 통해 특정 주제의 잡담을 랜덤하게 생성
- 사용자가 AI의 잡담에 대해 답변했는지, 아니면 다른 작업 요청을 했는지 판단 필요

## 구현 범위

### 1. 새로운 AI 모듈 생성
**파일**: `ai_chk_smalltalk_relevance.py` (신규 생성)

**역할**: 
- AI가 생성한 잡담(`query_smalltalk`)과 사용자 답변(`query`)의 연관성을 AI로 판단
- `related: True/False` 형식으로 응답

**참고**: `ai_chk_image_relevance.py`의 구조를 활용
- 언어별 프롬프트 (ko/ja/en)
- **함수 시그니처**: `process(query_smalltalk, query, lang='en')`

**판단 예시:**
- AI: "선생님, 오늘 날씨가 정말 좋네요!" (query_smalltalk)
  - User: "그러게요, 산책하기 딱 좋아요" (query) → **True** (잡담 답변)
  - User: "웹 검색해줘" (query) → **False** (작업 요청)
  
- AI: "선생님, 요즘 뭔가 재미있는 일 있으셨어요?" (query_smalltalk)
  - User: "응, 어제 영화 봤어" (query) → **True** (잡담 답변)
  - User: "알람 설정해줘" (query) → **False** (작업 요청)

---

### 2. server_interface_func.py 수정

#### 2-1. 파라미터 파싱 (이미 완료)
```python
# 79번 줄 - 이미 추가됨
params['intent_smalltalk_answer'] = request.form.get('intent_smalltalk_answer', 'off')
```

#### 2-2. 개별 intent 체크 함수 추가
**위치**: 348번 줄 이후 (check_individual_intent_image 함수 다음)

**함수명**: `check_individual_intent_smalltalk_answer(query, query_smalltalk, intent_smalltalk_answer, lang_infer_type)`

**로직**:
```python
def check_individual_intent_smalltalk_answer(query, query_smalltalk, intent_smalltalk_answer, lang_infer_type):
    """개별 잡담 답변 가능성 체크 - AI 생성 잡담과 사용자 답변의 연관성 판단"""
    if intent_smalltalk_answer == 'force':
        return True
    if intent_smalltalk_answer == 'off':
        return False
    
    # query_smalltalk이 없으면 체크할 수 없음
    if not query_smalltalk or not query_smalltalk.strip():
        return False
    
    import ai_chk_smalltalk_relevance
    intent_response = ai_chk_smalltalk_relevance.process(query_smalltalk, query, lang=lang_infer_type)
    if "related: True" in intent_response:  # AI 잡담과 사용자 답변이 연관됨
        return True
    return False
```

#### 2-3. process_intents 함수 수정
**위치**: 372번 줄

**변경 사항**:
1. 함수 시그니처에 `intent_smalltalk_answer`, `query_smalltalk` 파라미터 추가
2. 반환값에 `is_intent_smalltalk_answer` 추가
3. 개별 intent 체크 섹션(395-410번 줄)에 smalltalk 체크 추가

```python
def process_intents(query, query_en, intent_web, intent_image, intent_confirm, intent_confirm_type, 
                    image_info, lang_infer_type, server_type, intent_info, ai_info, image_info_text,
                    intent_smalltalk_answer, query_smalltalk):  # 추가
    """Intent 처리 통합 로직"""
    # ... 기존 코드 ...
    is_intent_smalltalk_answer = False  # 추가
    
    # ... 기존 intent 체크 로직 ...
    
    # 410번 줄 이후에 추가
    is_intent_smalltalk_answer = check_individual_intent_smalltalk_answer(
        query, query_smalltalk, intent_smalltalk_answer, lang_infer_type)
    if is_intent_smalltalk_answer:
        intent_info['is_intent_smalltalk_answer'] = 'on'
        intent_info['smalltalk_query'] = query_smalltalk  # AI가 생성한 잡담 내용 포함
    if state.get_DEV_MODE():
        print('### is_intent_smalltalk_answer :', is_intent_smalltalk_answer)
    
    return query_intent, is_intent_web, is_intent_image, is_intent_smalltalk_answer  # 반환값 수정
```

#### 2-4. intent_info 기본 형식 수정
**위치**: 231번 줄 `set_default_response_format_intent_info()` 함수

**추가**:
```python
intent_info['is_intent_smalltalk_answer'] = 'off'  # 잡담 답변 가능성
intent_info['smalltalk_query'] = ''  # AI가 생성한 잡담 내용
```

---

### 3. server_interface.py 수정

#### 3-1. 파라미터 추출 (이미 완료)
```python
# 250번 줄 - 이미 추가됨
intent_smalltalk_answer = params['intent_smalltalk_answer']
# 251번 줄 - 이미 추가됨
query_smalltalk = params['query_smalltalk']
```

#### 3-2. main_stream() 함수 수정
**위치**: 393-397번 줄

**변경 전**:
```python
query_intent, is_intent_web, is_intent_image = server_interface_func.process_intents(
    query, query_en, intent_web, intent_image, intent_confirm, intent_confirm_type, 
    image_info, lang_infer_type, server_type, intent_info, ai_info, image_info_text
)
```

**변경 후**:
```python
query_intent, is_intent_web, is_intent_image, is_intent_smalltalk_answer = server_interface_func.process_intents(
    query, query_en, intent_web, intent_image, intent_confirm, intent_confirm_type, 
    image_info, lang_infer_type, server_type, intent_info, ai_info, image_info_text,
    intent_smalltalk_answer, query_smalltalk  # 추가
)
```

#### 3-3. main_stream_gemini() 함수 수정
**위치**: 950번 줄 이후

**필요 작업**:
1. 파라미터 파싱에 `intent_smalltalk_answer`, `query_smalltalk` 추가
2. `intent_info` 기본 형식 설정
3. `process_intents` 호출 시 두 파라미터 추가

---

### 4. 다른 conversation 엔드포인트 적용

#### 대상 엔드포인트:
- `/conversation_stream_gemini` (950번 줄)
- 추후 추가되는 다른 conversation 엔드포인트

**적용 방법**: 위 3-3과 동일한 패턴 적용

---

## 구현 순서

1. `ai_chk_smalltalk_relevance.py` 신규 생성
   - `ai_chk_image_relevance.py`를 템플릿으로 활용
   - 잡담 연관성 판단 프롬프트 작성 (ko/ja/en)
   - `process(query_smalltalk, query, lang='en')` 함수 구현
   - AI 잡담과 사용자 답변의 연관성을 판단

2. `server_interface_func.py` 수정
   - 파라미터 파싱에 `query_smalltalk` 추가 (79번 줄 다음)
   - `check_individual_intent_smalltalk_answer(query, query_smalltalk, ...)` 함수 추가
   - `process_intents()` 함수 시그니처 및 로직 수정 (`query_smalltalk` 파라미터 추가)
   - `set_default_response_format_intent_info()` 수정 (`smalltalk_query` 필드 추가)

3. `server_interface.py` 수정
   - `main_stream()` 함수의 `process_intents()` 호출 수정
   - `main_stream_gemini()` 함수에 동일 패턴 적용

4. 테스트
   - force 옵션 테스트
   - on/off 옵션 테스트
   - AI 잡담과 사용자 답변의 연관성 판단 테스트
   - intent_info 응답에 올바르게 포함되는지 확인

---

## 예상 동작

### Case 1: intent_smalltalk_answer = 'force'
- AI 판단 없이 무조건 `is_intent_smalltalk_answer = True`
- `intent_info['is_intent_smalltalk_answer'] = 'on'`
- `intent_info['smalltalk_query'] = query_smalltalk` (AI가 생성한 잡담 포함)

### Case 2: intent_smalltalk_answer = 'on'
- **AI 잡담**: "선생님, 오늘 날씨가 좋네요!"
  - **사용자 답변**: "그러게요, 산책하기 좋아요" → `is_intent_smalltalk_answer = True`
  - **사용자 답변**: "웹 검색해줘" → `is_intent_smalltalk_answer = False`

### Case 3: intent_smalltalk_answer = 'off' (기본값)
- 체크하지 않음, `is_intent_smalltalk_answer = False`

### Case 4: query_smalltalk이 없는 경우
- intent_smalltalk_answer = 'on'이어도 체크 불가
- `is_intent_smalltalk_answer = False`

---

## AI 모듈 상세 (ai_chk_smalltalk_relevance.py)

### 프롬프트 예시 (한국어)

```python
"""AI가 생성한 잡담과 사용자 답변이 연관되어 있는지 판단하십시오.

AI 잡담: "선생님, 오늘 날씨가 정말 좋네요!"
사용자 답변: "{query}"

**연관됨(True) 기준:**
- 사용자가 AI의 잡담 주제에 대해 답변함
- 일상적인 대화 흐름을 이어감
- 잡담에 공감하거나 관련 이야기를 함

**연관되지 않음(False) 기준:**
- 잡담과 무관한 작업 요청 (웹 검색, 알람, 이미지 생성 등)
- 전혀 다른 주제로 대화를 전환
- 기능 실행 명령

결과는 다음 형식으로만 응답하십시오:
related: True/False

예시:

AI 잡담: "선생님, 오늘 날씨가 정말 좋네요!"
사용자 답변: "그러게요, 산책하기 딱 좋아요"
결과:
related: True

AI 잡담: "선생님, 오늘 날씨가 정말 좋네요!"
사용자 답변: "웹 검색해줘"
결과:
related: False

AI 잡담: "선생님, 요즘 뭔가 재미있는 일 있으셨어요?"
사용자 답변: "응, 어제 영화 봤어"
결과:
related: True

AI 잡담: "선생님, 요즘 뭔가 재미있는 일 있으셨어요?"
사용자 답변: "알람 설정해줘"
결과:
related: False
"""
```

---

## 참고 파일 및 관련 기능

### 기존 잡담 생성 시스템
- `ai_trigger_small_talk.py`: AI가 특정 주제로 잡담을 랜덤 생성
  - `process(purpose, character, lang)`: 목적과 캐릭터에 맞는 잡담 생성
  - 목적: greeting, small_talk, concern, encouragement 등

### 새로운 잡담 판단 시스템
- `ai_chk_smalltalk_relevance.py`: AI 잡담과 사용자 답변의 연관성 판단
  - `process(query_smalltalk, query, lang)`: 두 문장의 연관성 체크

### 기존 Intent 체크 패턴
- `ai_chk_image_relevance.py`: 이미지 설명과 질문의 연관성 판단 (구조 참고)
- `ai_intent_image.py`: 질문에 이미지가 필요한지 판단
- `ai_intent_web.py`: 질문에 웹 검색이 필요한지 판단

### 서버 처리 로직
- `server_interface_func.py`: 336-410번 줄 (intent 체크 로직)
- `server_interface.py`: 393-397번 줄 (process_intents 호출)

---

## 데이터 플로우

```
1. AI가 잡담 생성 (ai_trigger_small_talk.py)
   └─> query_smalltalk = "선생님, 오늘 날씨가 좋네요!"

2. 사용자 답변
   └─> query = "그러게요, 산책하기 좋아요"

3. 서버로 전송
   ├─> intent_smalltalk_answer = 'on'
   ├─> query_smalltalk = "선생님, 오늘 날씨가 좋네요!"
   └─> query = "그러게요, 산책하기 좋아요"

4. Intent 체크 (server_interface_func.py)
   └─> check_individual_intent_smalltalk_answer()
       └─> ai_chk_smalltalk_relevance.process(query_smalltalk, query)
           └─> "related: True"

5. Intent 정보 업데이트
   ├─> intent_info['is_intent_smalltalk_answer'] = 'on'
   └─> intent_info['smalltalk_query'] = query_smalltalk

6. 응답에 포함하여 반환
   └─> Unity/Client가 잡담 맥락을 유지하면서 대화 처리
```

---

## 실제 구현 결과

### Memory 처리 (server_interface.py)

**위치**: 427-453번 줄

`is_intent_smalltalk_answer`가 True일 때 AI 잡담을 memory에 자동 추가:

```python
# Intent smalltalk이 감지되었을 때, memory에 AI 잡담 추가
if is_intent_smalltalk_answer and query_smalltalk:
    from datetime import datetime
    timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    
    smalltalk_entry = {
        "speaker": "character",
        "message": query_smalltalk,
        "message_trans": query_smalltalk,
        "role": "assistant",
        "type": "conversation",
        "messageKo": query_smalltalk,
        "messageJp": query_smalltalk,
        "messageEn": query_smalltalk,
        "timestamp": timestamp
    }
    
    if memory is None:
        # memory가 없으면 새로 생성
        memory = [smalltalk_entry]
    else:
        # memory가 있으면 마지막에 추가
        memory.append(smalltalk_entry)
```

### 응답 형식

#### 1. Intent 정보 (intent_info)

```json
{
  "is_intent_web": "off",
  "web_info": "",
  "web_search_keyword": "",
  "web_search_detail": "false",
  "is_intent_image": "off",
  "image_info": "",
  "is_intent_smalltalk_answer": "on",
  "smalltalk_query": "선생님, 오늘 날씨가 좋네요!"
}
```

#### 2. Memory 자동 업데이트

연관성이 감지되면 memory에 AI 잡담이 자동 추가됨:

```json
[
  {
    "speaker": "character",
    "message": "선생님, 오늘 날씨가 좋네요!",
    "message_trans": "선생님, 오늘 날씨가 좋네요!",
    "role": "assistant",
    "type": "conversation",
    "messageKo": "선생님, 오늘 날씨가 좋네요!",
    "messageJp": "선생님, 오늘 날씨가 좋네요!",
    "messageEn": "선생님, 오늘 날씨가 좋네요!",
    "timestamp": "2025-12-22 03:09:22"
  }
]
```

### 실제 사용 시나리오

#### 시나리오 1: 잡담 연관성 있음

**요청**:
```json
{
  "query": "그러게요, 산책하기 좋아요",
  "query_smalltalk": "선생님, 오늘 날씨가 좋네요!",
  "intent_smalltalk_answer": "on",
  "memory": "[...]"
}
```

**서버 처리**:
1. `ai_chk_smalltalk_relevance.process()` 호출
2. 연관성 판단: `related: True`
3. `intent_info['is_intent_smalltalk_answer'] = 'on'`
4. `intent_info['smalltalk_query'] = query_smalltalk`
5. Memory에 AI 잡담 추가
6. AI가 사용자 답변에 대한 응답 생성 (memory에 잡담 포함)

**응답**:
```json
{
  "reply_list": [...],
  "intent_info": {
    "is_intent_smalltalk_answer": "on",
    "smalltalk_query": "선생님, 오늘 날씨가 좋네요!"
  }
}
```

#### 시나리오 2: 잡담 연관성 없음

**요청**:
```json
{
  "query": "웹 검색해줘",
  "query_smalltalk": "선생님, 오늘 날씨가 좋네요!",
  "intent_smalltalk_answer": "on",
  "memory": "[...]"
}
```

**서버 처리**:
1. `ai_chk_smalltalk_relevance.process()` 호출
2. 연관성 판단: `related: False`
3. `is_intent_smalltalk_answer = False`
4. Intent 정보 기본값 유지
5. Memory 수정 없음
6. AI가 사용자 요청을 독립적으로 처리

**응답**:
```json
{
  "reply_list": [...],
  "intent_info": {
    "is_intent_smalltalk_answer": "off",
    "smalltalk_query": ""
  }
}
```

#### 시나리오 3: Force 모드

**요청**:
```json
{
  "query": "알람 설정해줘",
  "query_smalltalk": "선생님, 오늘 날씨가 좋네요!",
  "intent_smalltalk_answer": "force"
}
```

**서버 처리**:
1. AI 판단 없이 무조건 `is_intent_smalltalk_answer = True`
2. Memory에 AI 잡담 추가
3. Intent 정보 업데이트

**응답**:
```json
{
  "reply_list": [...],
  "intent_info": {
    "is_intent_smalltalk_answer": "on",
    "smalltalk_query": "선생님, 오늘 날씨가 좋네요!"
  }
}
```

### 클라이언트(Unity) 활용 방안

1. **잡담 맥락 유지**:
   - `is_intent_smalltalk_answer`가 'on'이면 잡담 대화로 간주
   - UI에서 다른 스타일로 표시 가능

2. **Memory 동기화**:
   - 서버가 자동으로 memory에 추가하므로 클라이언트는 추가 작업 불필요
   - 다음 요청 시 업데이트된 memory 사용

3. **대화 흐름 제어**:
   - 연관성 없으면 AI 잡담 무시하고 새로운 주제 시작
   - 연관성 있으면 잡담 맥락 유지하면서 대화 진행
