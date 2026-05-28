# BAReader 고도화 계획 (plan2.md)

## 현재 구현 상태

✅ 기본 BAReader 시나리오 완료:
- OpenCV 템플릿 매칭 (역삼각 감지)
- 2단계 분류 + OCR 파이프라인
- 2-엔드포인트 아키텍처 (`engine_stream` + `engine_form`)
- `ocr_history` stateless 누적
- 빈 actor → arona 기본값 처리

---

## 고도화 이슈 목록

### 1. 선택지 클릭 문제 ⚠️

**현상**: 
- 선택지 화면일 때 **역삼각**을 클릭하면 대사가 진행되지 않음
- **선택지 박스 중 하나**를 클릭해야 진행됨

**원인**:
```python
# action_S1() - 항상 역삼각 위치를 반환
triangle = find_triangle(image_path)
click_x = triangle['x']  # ← 선택지 화면에서도 역삼각 클릭!
click_y = triangle['y']
```

**해결 방안**:

#### A안: dialogue_type별 클릭 위치 분기

```python
if ocr_data['dialogue_type'] == 'choice':
    # 선택지 박스 위치 찾기 (템플릿 매칭)
    choice_box = find_choice_box(image_path)  # NEW
    click_x = choice_box['x']
    click_y = choice_box['y']
else:
    # 일반 대사: 역삼각 클릭
    triangle = find_triangle(image_path)
    click_x = triangle['x']
    click_y = triangle['y']
```

**필요 작업**:
- [ ] `prompt/extra/choice_box.png` 템플릿 추가 (사용자 제공)
- [ ] `ai_vl_engine_images.py`에 `find_choice_box()` 함수 추가
- [ ] `action_S1()`에서 dialogue_type별 분기 구현

---

### 2. AgentEvent action 타입 통일 문제 ⚠️

**현상**:
```python
return {
    'action': 'click',  # ← 엄격히는 틀림!
    'request_voice': True,  # 실제로는 form_request
    'voice_actor': ...,
    'voice_txt': ...,
}
```

**문제점**:
- `action: 'click'`은 **단순 좌표 클릭**을 의미
- 하지만 BAReader의 S1은:
  1. 대사 읽기 (OCR)
  2. 음성 요청 (`engine_form` 호출)
  3. 음성 재생 대기
  4. **그 후** 클릭
  
→ `click` + `request_voice`는 **두 가지 액션의 조합**이므로 애매함

**해결 방안**:

#### 채택: `request_form` 액션 타입 신설

```python
return {
    'action': 'request_form',  # NEW action type
    'x': click_x,
    'y': click_y,
    'voice_actor': ...,
    'voice_txt': ...,
    'dialogue_type': ...,
}
```

**의미**: 
- Unity에게 `engine_form` 호출 요청
- 음성 재생 완료 후 `(x, y)` 클릭 수행
- `request_voice: true` 플래그 불필요 (action 자체가 의미 전달)

**get_action() 구현**:
```python
if act == 'request_form':
    x = action.get('x')
    y = action.get('y')
    
    append_think_log(think_log, PHASE_ACT, f"request_form: ({x},{y})")
    
    data = {
        'action': 'request_form',
        'x': x,
        'y': y,
        'voice_actor': action.get('voice_actor', ''),
        'voice_txt': action.get('voice_txt', ''),
        'dialogue_type': action.get('dialogue_type'),
        'ocr_result': action.get('ocr_result'),
        'choices': action.get('choices'),
        'reason': reason,
        'agent_state': state
    }
    
    return AgentEvent(
        kind=EVENT_KIND_ACT,
        message=reason,
        think_log=think_log,
        data=data
    )
```

**필요 작업**:
- [ ] `get_action()`에 `request_form` 분기 추가
- [ ] `action_S1()`에서 `'action': 'request_form'` 반환
- [ ] `to_unity.md` 업데이트 (새 action 타입 설명)

---

### 3. OCR 중복 방지 문제 ⚠️

**현상**:
- 나레이션이나 긴 대사는 **화면에 계속 떠있음**
- 매 프레임마다 같은 대사를 OCR하여 **중복 읽기** 발생

**예시**:
```
[1] OCR: "(외회りを終えて帰ってくると...)"  ← 읽음
[2] 클릭 (역삼각)
[3] 화면 변화 없음 (같은 나레이션)
[4] OCR: "(외회りを終えて帰ってくると...)"  ← 중복!
```

**해결 방안**:

#### A안: 마지막 ocr_history와 비교

```python
# action_S1() 로직
ocr_data = classify_and_ocr(image_path)
ocr_history = state.get('ocr_history', [])

# 중복 체크
is_duplicate = False
if ocr_history:
    last_entry = ocr_history[-1]
    if last_entry['txt'] == ocr_data['txt']:
        print(f'[BAReader] 중복 감지: {ocr_data["txt"][:30]}...')
        is_duplicate = True

if is_duplicate:
    # wait 반환 (observe)
    return {
        'action': 'observe',
        'reason': '중복 대사 감지 - 대기',
        'retry_interval': 2.0,
        'expected_state': ['S1', 'S10'],
    }
else:
    # 정상 처리
    ocr_history.append(...)
```

**장점**: 간단하고 효과적
**단점**: 동일 대사가 연속으로 나오는 정상 케이스에서 오동작 가능


**필요 작업**:
- [ ] `action_S1()`에 중복 체크 로직 추가
- [ ] 중복 시 `action: 'observe'` 반환
- [ ] `retry_interval` 조정 (1~2초)

---

### 4. 대기(Observe) 시나리오 처리 미구현 ⚠️

**대기가 필요한 상황**:
1. **narration** — 나레이션은 클릭 없이 자동 전환 대기
2. **중복 OCR** — 같은 텍스트 감지 시 화면 변화 대기
3. **템플릿 매칭 실패** — 역삼각/선택지 박스 재감지 실패 시 재시도
4. **(추가 검토)** 기타 대기 필요 케이스

**BASkip의 observe 패턴** (기준):
```python
# action 함수에서 반환
return {
    'action': 'observe',
    'reason': '[actionS1] MomoTalk 아이콘을 찾을 수 없습니다 (재시도 3회 남음)',
    'expected_state': 'S1',  # 동일 시나리오 재확인
    'remain_retry_count': 3,
    'retry_interval': 2.0  # 초 단위
}

# get_action()에서 AgentEvent 조립
if act == 'observe':
    return AgentEvent(
        kind=EVENT_KIND_OBSERVE,
        message=reason,
        think_log=think_log,
        data={
            'reason': reason,
            'agent_state': state,
            'retry_interval': state['retry_interval']
        }
    )
```

**Unity 동작**:
- `retry_interval` 초 만큼 대기
- 대기 후 동일한 `expected_state`로 `engine_stream` 재호출
- `remain_retry_count` 감소 (Python이 관리)

**BAReader 대기 시나리오 요약**:

| 시나리오 | 조건 | retry_interval | 처리 |
|---------|------|----------------|------|
| 1. narration | `dialogue_type == 'narration'` | 3.0초 | ocr_history 추가 후 observe 반환 (음성/클릭 없음) |
| 2. 중복 OCR | `last_history.txt == current.txt` | 2.0초 | observe 반환, history 추가하지 않음 |
| 3. 템플릿 실패 | `find_triangle() == None` **and** `find_choice_box() == None` (양쪽 다 없음) | 1.0초 | observe 반환, 빠른 재시도 |
| 4. none | `dialogue_type == 'none'` 또는 `txt == ''` | 2.0초 | observe 반환 (화면 전환 대기) |

**공통 특징**: 모두 클릭하지 않고 화면 변화 대기

---
#### 시나리오 1: narration 처리

**특성**:
- 나레이션은 **클릭해도 화면 유지**되는 경우 많음
- 자동 전환 또는 시간 경과 대기 필요
- 중복 읽기 방지 필수

**구현**:
```python
# action_S1()
ocr_data = classify_and_ocr(image_path)

if ocr_data['dialogue_type'] == 'narration':
    # 중복 체크
    if is_duplicate_ocr(state, ocr_data):
        return {
            'action': 'observe',
            'reason': '나레이션 중복 - 화면 전환 대기',
            'expected_state': ['S1', 'S10'],
            'remain_retry_count': state.get('remain_retry_count', 5),
            'retry_interval': 3.0  # 나레이션은 긴 대기
        }
    
    # 신규 나레이션: ocr_history 추가 후 대기
    ocr_history.append({...})
    
    return {
        'action': 'observe',
        'reason': f'나레이션 읽기 완료 - 자동 전환 대기: {ocr_data["txt"][:30]}...',
        'expected_state': ['S1', 'S10'],
        'remain_retry_count': 5,
        'retry_interval': 3.0
    }
```

**중요**: narration일 때는 **request_form도 보내지 않음** (observe만 반환)

---

#### 시나리오 2: 중복 OCR 감지

**함수** `is_duplicate_ocr()`:
```python
def is_duplicate_ocr(state, ocr_data):
    '''마지막 ocr_history와 비교하여 중복 여부 판단'''
    ocr_history = state.get('ocr_history', [])
    
    if not ocr_history or not ocr_data['txt']:
        return False
    
    last_entry = ocr_history[-1]
    
    # txt가 정확히 일치하면 중복
    if last_entry['txt'] == ocr_data['txt']:
        return True
    
    return False
```

**적용**:
```python
# action_S1()
if is_duplicate_ocr(state, ocr_data):
    return {
        'action': 'observe',
        'reason': f'중복 OCR 감지 - 대기: {ocr_data["txt"][:30]}...',
        'expected_state': ['S1', 'S10'],
        'remain_retry_count': state.get('remain_retry_count', 5),
        'retry_interval': 2.0
    }
```

---

#### 시나리오 3: 템플릿 매칭 실패

**현상**:
```python
# S1 식별은 성공했지만 action에서 재감지 실패
triangle = find_triangle(image_path)
if not triangle:
    # x, y가 None → 클릭 불가
```

**해결**:
```python
# action_S1()
if ocr_data['dialogue_type'] == 'choice':
    choice_box = find_choice_box(image_path)
    if not choice_box:
        return {
            'action': 'observe',
            'reason': '선택지 박스 재감지 실패 - 재시도',
            'expected_state': ['S1', 'S10'],
            'remain_retry_count': state.get('remain_retry_count', 5),
            'retry_interval': 1.0  # 빠른 재시도
        }
    click_x = choice_box['x']
    click_y = choice_box['y']
else:
    triangle = find_triangle(image_path)
    if not triangle:
        return {
            'action': 'observe',
            'reason': '역삼각 재감지 실패 - 재시도',
            'expected_state': ['S1', 'S10'],
            'remain_retry_count': state.get('remain_retry_count', 5),
            'retry_interval': 1.0
        }
    click_x = triangle['x']
    click_y = triangle['y']
```

---

#### 시나리오 4: dialogue_type='none' 처리 ✅

**특성**:
- OCR 결과 텍스트 없음 (`txt == ''`)
- 화면 전환 중, 애니메이션, 빈 화면 등

**처리**:
```python
# action_S1()
ocr_data = classify_and_ocr(image_path)

if ocr_data['dialogue_type'] == 'none' or not ocr_data['txt']:
    # 텍스트 없으면 observe로 대기
    return {
        'action': 'observe',
        'reason': 'dialogue_type=none - 화면 전환 대기',
        'expected_state': ['S1', 'S10'],
        'remain_retry_count': state.get('remain_retry_count', 5),
        'retry_interval': 2.0
    }
```

**확정**: none도 observe 반환 (클릭하지 않음)

---

#### 추가 검토 케이스 (향후)

**1. 선택지 사용자 입력 대기** (현재 미구현)
- 자동 선택 vs 수동 선택
- 현재는 자동 선택(첫 번째 choice) 가정
- 향후: Unity에서 선택지 UI 표시 → 사용자 입력 → 선택한 위치 클릭

**2. 화면 로딩** (현재 미지원)
- 로딩 스피너 감지 → observe
- 복잡도 높음, 추후 고려

---

#### 통합 로직 (action_S1 개선안)

```python
@action_id('S1')
def action_S1(state, image_path):
    '''S1: 대사 읽기 — 분류 → OCR → 음성 요청 또는 대기'''
    
    # 2단계 분류 + OCR
    ocr_data = classify_and_ocr(image_path)
    
    # [체크 1] 중복 OCR
    if is_duplicate_ocr(state, ocr_data):
        return {
            'action': 'observe',
            'reason': f'중복 OCR - 대기: {ocr_data["txt"][:30]}...',
            'expected_state': ['S1', 'S10'],
            'remain_retry_count': state.get('remain_retry_count', 5),
            'retry_interval': 2.0
        }
    
    # [체크 2] 템플릿 매칭 (클릭 좌표 획득)
    if ocr_data['dialogue_type'] == 'choice':
        choice_box = find_choice_box(image_path)
        if not choice_box:
            return {
                'action': 'observe',
                'reason': '선택지 박스 재감지 실패',
                'expected_state': ['S1', 'S10'],
                'remain_retry_count': state.get('remain_retry_count', 5),
                'retry_interval': 1.0
            }
        click_x, click_y = choice_box['x'], choice_box['y']
    else:
        triangle = find_triangle(image_path)
        if not triangle:
            return {
                'action': 'observe',
                'reason': '역삼각 재감지 실패',
                'expected_state': ['S1', 'S10'],
                'remain_retry_count': state.get('remain_retry_count', 5),
                'retry_interval': 1.0
            }
        click_x, click_y = triangle['x'], triangle['y']
    
    # [처리 1] narration → observe (클릭 없음)
    if ocr_data['dialogue_type'] == 'narration':
        # ocr_history 추가
        ocr_history = state.get('ocr_history', [])
        ocr_history.append({
            'type': 'narration',
            'actor': '',
            'txt': ocr_data['txt']
        })
        
        # Unity에 음성 요청도 하지 않음 (narration은 무음 처리)
        # → 향후 narration 음성 추가 가능성 있으면 request_form 보내도 됨
        
        return {
            'action': 'observe',
            'reason': f'나레이션 - 자동 전환 대기: {ocr_data["txt"][:30]}...',
            'expected_state': ['S1', 'S10'],
            'remain_retry_count': 5,
            'retry_interval': 3.0  # 나레이션은 긴 대기
        }
    
    # [처리 2] 일반 대사/선택지 → request_form
    ocr_history = state.get('ocr_history', [])
    if ocr_data['txt']:
        history_entry = {
            'type': ocr_data['dialogue_type'],
            'actor': ocr_data['actor'],
            'txt': ocr_data['txt'],
        }
        if ocr_data['choices']:
            history_entry['choices'] = ocr_data['choices']
        ocr_history.append(history_entry)
    
    return {
        'reason': f'S1 대사 읽기: {ocr_data["dialogue_type"]} - {ocr_data["actor"]}: {ocr_data["txt"][:20]}',
        'action': 'request_form',  # NEW!
        'x': click_x,
        'y': click_y,
        'expected_state': ['S1', 'S10'],
        'remain_retry_count': 5,
        'retry_interval': 2.0,
        # request_form 전용 필드
        'voice_actor': ocr_data['actor'],
        'voice_txt': ocr_data['txt'],
        'dialogue_type': ocr_data['dialogue_type'],
        'ocr_result': {
            'actor': ocr_data['actor'],
            'txt': ocr_data['txt'],
        },
        'choices': ocr_data['choices'],
        'ocr_history': ocr_history,
    }
```

**필요 작업**:
- [ ] `is_duplicate_ocr()` 함수 추가
- [ ] `action_S1()` 통합 로직 구현
- [ ] narration 시 observe 반환 (클릭 없음)
- [ ] 템플릿 매칭 실패 시 observe 반환

---

## 추가 검토 사항

### 5. 템플릿 매칭 실패 시 처리

**시나리오**:
- S1 식별은 성공 (역삼각 감지)
- 하지만 action에서 `find_triangle()` 재호출 시 **실패** 가능

**현재 코드**:
```python
triangle = find_triangle(image_path)
click_x = triangle['x'] if triangle else None  # ← None 반환 가능
click_y = triangle['y'] if triangle else None
```

**문제**: `None` 좌표로 클릭 시도 → Unity 에러

**해결**:
```python
triangle = find_triangle(image_path)
if not triangle:
    print('[BAReader] WARNING: 역삼각 재감지 실패')
    # observe 또는 화면 중앙 클릭
    return {
        'action': 'observe',
        'reason': '역삼각 재감지 실패 - 재시도',
        'retry_interval': 1.0,
    }
```

---

### 6. 선택지 choices 리스트 활용

**현재**:
- `choices` 배열을 Unity로 전달만 하고 **활용 안함**

**향후 고도화**:
- Unity에서 선택지 UI 표시
- 사용자 선택 대기
- 선택한 choice의 위치로 클릭

→ 별도 이슈로 추후 처리

---

### 7. speed 파라미터 검증

**현재**: Unity에서 잘못된 speed(100.0) 전송 시 그대로 처리

**추가됨** (이미 수정됨):
```python
# server_interface_vl_engine_impl.py
speed = float(request.form.get('speed', '1.0'))
if speed < 0.5 or speed > 2.0:
    speed = 1.0
```

✅ 완료

---

## 우선순위

### High (필수)
1. **중복 OCR 방지** — 가장 빈번한 문제
2. **선택지 클릭 위치** — 선택지에서 진행 불가

### Medium (권장)
3. **action 타입 통일** — 의미론 명확화
4. **narration 처리** — 사용자 경험 개선

### Low (선택)
5. 템플릿 매칭 실패 방어 코드
6. choices 리스트 활용

---

## 제안 구현 순서

### Phase 1: 핵심 버그 수정
1. 중복 OCR 방지 (`is_duplicate` 체크)
2. 선택지 클릭 (`find_choice_box()` 추가)

### Phase 2: 구조 개선
3. action 타입 통일 (`read_dialogue`)
4. narration 특수 처리

### Phase 3: 방어 코드
5. 템플릿 매칭 실패 처리
6. 기타 엣지 케이스

---

## 구현 파일 영향도

| 파일 | Issue 1 | Issue 2 | Issue 3 | Issue 4 |
|------|---------|---------|---------|---------|
| `ai_vl_engine_images.py` | ✅ | - | - | - |
| `ai_vl_scenario_action_BARead.py` | ✅ | ✅ | ✅ | ✅ |
| `to_unity.md` | ✅ | ✅ | - | ✅ |
| Unity 코드 | - | ✅ | - | ⚠️ |

---

## User Review Required

> [!IMPORTANT]
> **전체 계획 검토 완료 ✅**
> 
> 4가지 주요 이슈 + 3가지 추가 검토사항을 분석했습니다:
> 1. 선택지 클릭 문제 — `find_choice_box()` 추가
> 2. action 타입 통일 — `request_form` 신설 (확정)
> 3. OCR 중복 방지 — `is_duplicate_ocr()` 추가
> 4. 대기 시나리오 — narration, 중복, 템플릿 실패 시 `observe` 반환
> 
> BASkip의 `observe` 패턴을 기준으로 설계했습니다.

### 사용자 답변 반영

**A1. narration 음성 처리** ✅
- **확정**: narration은 **음성 없이** observe만 반환
- 다른 화면 나올 때까지 대기

**A2. dialogue_type='none' 처리** ✅
- **확정**: none 케이스도 존재함, observe만 반환
- 다른 화면 나올 때까지 대기

**A3. choice_box.png 템플릿** ✅
- **경로**: `./prompt/extra/choice_box.png`
- `triangle.png`와 같은 디렉토리

**A4. 중복 OCR 판단 기준** ✅
- **확정**: txt 정확 일치만 사용
- 타임스탬프 기반 제거 (불필요)

**A5. narration/none 화면 동작** ✅
- **확정**: 자동 전환까지 대기
- 클릭하지 않고 observe로 화면 변화 대기

---

### 추가 요구사항

**6. OCR 로그 저장** 🆕
- **요구**: `verbose=True`일 때 ocr_history를 txt 파일로 저장
- **참고**: BASkip에 관련 패턴 있음
- **구현**:
  ```python
  # ai_vl_scenario_action_BARead.py
  def action_S1(state, image_path):
      # ... OCR 처리 ...
      
      # verbose 모드일 때 ocr_history 저장
      if ai_vl_engine_scanner.verbose_mode:
          save_ocr_history_to_file(ocr_history)
  ```

**저장 경로**: `./test/vl_agent/` 또는 `./test/ocr_log/` 디렉토리
**파일명**: `ocr_history_YYYYMMDD_HHMMSS.txt`

---

## 최종 구현 체크리스트

### Phase 1: 핵심 기능 (High Priority)
- [ ] `ai_vl_engine_images.py`
  - [ ] `TEMPLATE_CHOICE_BOX` 상수 추가
  - [ ] `find_choice_box()` 함수 추가
- [ ] `ai_vl_scenario_action_BARead.py`
  - [ ] `is_duplicate_ocr()` 함수 추가
  - [ ] `action_S1()` 통합 로직 구현:
    - [ ] 중복 OCR 체크
    - [ ] dialogue_type별 템플릿 매칭 (choice vs 일반)
    - [ ] narration → observe 반환
    - [ ] none → observe 반환
    - [ ] 일반 대사/선택지 → request_form 반환
  - [ ] `get_action()`에 `request_form` 분기 추가
  - [ ] verbose 모드 시 ocr_history txt 저장

### Phase 2: 문서화
- [ ] `to_unity.md` 업데이트
  - [ ] `request_form` action 타입 설명
  - [ ] `observe` AgentEvent 구조 설명
  - [ ] narration/none 처리 설명

### Phase 3: 템플릿 이미지 (사용자)
- [ ] `prompt/extra/choice_box.png` 배치

---

## 구현 승인 ✅

> [!NOTE]
> **모든 질문 답변 완료**
> 
> 사용자 피드백 반영 완료. 구현 준비 완료되었습니다.
> 승인 시 Phase 1부터 순차 구현 시작합니다.
