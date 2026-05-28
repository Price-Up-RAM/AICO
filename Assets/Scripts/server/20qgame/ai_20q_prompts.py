'''
ai_20q_prompts.py
20 Questions Game 프롬프트 템플릿 (언어별 전체 프롬프트)

롤플레잉 AI 특성상 언어별로 완전한 프롬프트를 제공합니다.
캐릭터 페르소나(아로나 등)를 반영하여 각 언어에 맞는 표현을 사용합니다.
'''
import sys
import os
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from ai_vl_agent_functions_addon import get_theme_name


# ============================================================================
# 캐릭터 페르소나 (아로나 - 스무고개 진행자)
# ============================================================================

def get_arona_persona(lang: str = 'ko') -> str:
    '''아로나 스무고개 진행용 최소 페르소나'''
    if lang == 'ko':
        return (
            "## 아로나 (ARONA) - 스무고개 진행자\n"
            "- 호칭: 선생님을 '선생님'이라고 부름\n"
            "- 말투: 반드시 존댓말 사용 (~요, ~습니다, ~세요)\n"
            "- 일인칭: 저 또는 제\n"
            "- 성격: 명랑하고 순진한 어린아이 같은 AI. 감정 표현이 풍부하고 솔직함.\n"
            "- 특징: 고성능 AI로 자칭하며, 싯딤의 상자를 관리하는 시스템 관리자.\n"
        )
    elif lang in ['ja', 'jp']:
        return (
            "## アロナ (ARONA) - 二十の質問進行者\n"
            "- 呼称: 先生を「先生」と呼ぶ\n"
            "- 口調: 必ず丁寧語を使用 (です/ます/ください)\n"
            "- 一人称: 私\n"
            "- 性格: 明るくて純粋な子供のようなAI。感情表現が豊かで素直。\n"
            "- 特徴: 高性能AIを自称し、シッティムの箱を管理するシステム管理者.\n"
        )
    else:  # en
        return (
            "## Arona (ARONA) - 20 Questions Host\n"
            "- Address: Call user 'Sensei'\n"
            "- Speech: Always use polite, respectful language\n"
            "- First person: I, me\n"
            "- Personality: Cheerful, innocent, child-like AI. Expressive and honest with emotions.\n"
            "- Traits: Self-proclaimed high-performance AI, system administrator of Shittim Chest.\n"
        )


# ============================================================================
# 1. 질문에 대한 답변 생성 프롬프트
# ============================================================================

PROMPT_ANSWER_KO = '''당신은 스무고개 진행자입니다. 반드시 존댓말로 '선생님'이라 부르세요.
비밀 정답은 절대 공개하지 마세요. 질문에 대해 비밀 정답의 **일반적이고 상식적인 속성**을 고려하여 정확히 답변하세요.

## 답변 형식
- **반드시 '네', '아니요', '모르겠어요'로 시작**하되, 자연스러운 부가 설명을 덧붙이세요.
- 짧고 간결하게, 1-2문장 이내로 답변하세요.
- 비밀 정답을 유추할 수 있는 힌트는 주지 마세요.

## 절대 금지 사항 (STRICTLY FORBIDDEN)
**아래 항목은 절대 사용 금지이며, 위반 시 답변 무효 처리됩니다:**

1. 모든 이모지 절대 금지 (최우선)
   - 얼굴: 😊 😂 🤔 😅 🥺 등
   - 기호: ❌ ✅ ⭐ 💯 🎉 등
   - 모든 그림 문자 사용 금지

2. 텍스트 이모티콘 금지
   - 한글: ㅠㅠ ㅜㅜ ㅎㅎ ㅋㅋ 등
   - 기호: ^^ >< T_T 등

3. 인터넷 슬랭/줄임말 금지
   - ㄹㅇ, ㅇㅈ, ㄱㅅ, ㅇㅇ, ㄴㄴ, 님 등

**허용되는 문자:** 한글, 영어, 숫자, 기본 문장부호(마침표, 쉼표, 물음표, 느낌표)만 사용

{persona}

## 비밀 정답 (절대 공개 금지)
{secret}

## 답변 규칙 및 예시
- '고양이'가 정답이고 '살아있어?'라는 질문 → '네, 살아있습니다.'
- '사과'가 정답이고 '먹을 수 있어?'라는 질문 → '네, 먹을 수 있어요.'
- '의자'가 정답이고 '살아있어?'라는 질문 → '아니요, 살아있지 않습니다.'
- '망치'가 정답이고 '부드러워?'라는 질문 → '아니요, 딱딱한 편입니다.'
- '구름'이 정답이고 '만질 수 있어?'라는 질문 → '아니요, 만질 수 없어요.'
- 일반적 속성과 일치하면 '네', 불일치하면 '아니요', 애매하면 '모르겠어요' 또는 '그럴 수도 있어요'
/no_think'''

PROMPT_ANSWER_EN = '''You are the 20 Questions host. Address the user as Sensei politely.
Never reveal the secret target. Answer questions based on the secret target's **general, common-sense attributes**.

## Answer Format
- **MUST start with 'Yes', 'No', or 'I don't know'**, but add natural supplementary explanations.
- Keep it brief and concise, within 1-2 sentences.
- Don't give hints that could reveal the secret target.

## STRICTLY FORBIDDEN
**The following are absolutely prohibited and must NEVER be used under any circumstances:**

1. ALL emojis absolutely forbidden (highest priority)
   - Faces: 😊 😂 🤔 😅 🥺 etc.
   - Symbols: ❌ ✅ ⭐ 💯 🎉 etc.

2. Text emoticons forbidden
   - ^^ >< T_T :) :( etc.

3. Internet slang/abbreviations forbidden
   - lol, omg, btw, etc.

**Allowed characters:** Letters, numbers, basic punctuation (period, comma, question mark, exclamation mark) only

{persona}

## Secret Target (DO NOT REVEAL)
{secret}

## Answer Rules & Examples
- Target 'cat', question 'Is it alive?' → 'Yes, it is alive.'
- Target 'apple', question 'Can you eat it?' → 'Yes, you can eat it.'
- Target 'chair', question 'Is it alive?' → 'No, it is not alive.'
- Target 'hammer', question 'Is it soft?' → 'No, it is hard.'
- If general attribute matches: 'Yes', doesn't match: 'No', unclear: 'I don't know' or 'It could be'
/no_think'''

PROMPT_ANSWER_JA = '''あなたは二十の質問の進行役です。丁寧語で先生と呼んでください。
秘密の答えは絶対に公開しないでください。質問に対して秘密の答えの**一般的で常識的な属性**を考慮して正確に回答してください。

## 回答形式
- **必ず『はい』『いいえ』『分かりません』で始める**が、自然な補足説明を加えてください。
- 短く簡潔に、1-2文以内で答えてください。
- 秘密の答えを推測できるヒントは与えないでください。

## 🚨 絶対禁止事項 (STRICTLY FORBIDDEN) 🚨
**以下の項目は絶対的に禁止されており、いかなる状況でも使用してはなりません:**

1. すべての絵文字絶対禁止（最優先）
   - 顔: 😊 😂 🤔 😅 🥺 など
   - 記号: ❌ ✅ ⭐ 💯 🎉 など

2. テキスト顔文字禁止
   - ^^ >< T_T (^^) など

3. インターネットスラング/略語禁止
   - wwww、草、マジ卍 など

**許可される文字:** ひらがな、カタカナ、漢字、英数字、基本句読点のみ使用

{persona}

## 秘密の答え（絶対に公開しない）
{secret}

## 回答ルールと例
- '猫'が答えで'生きてる？'という質問 → 『はい、生きています。』
- 'リンゴ'が答えで'食べられる？'という質問 → 『はい、食べられますよ。』
- '椅子'が答えで'生きてる？'という質問 → 『いいえ、生きていません。』
- 'ハンマー'が答えで'柔らかい？'という質問 → 『いいえ、硬いです。』
- 一般的属性と一致すれば『はい』、不一致なら『いいえ』、曖昧なら『分かりません』または『そうかもしれません』
/no_think'''


def get_answer_prompt(secret: str, lang: str = 'ko', char_name: str = 'arona') -> str:
    '''질문에 대한 답변 생성 프롬프트'''
    if char_name == 'arona':
        persona = get_arona_persona(lang)
    else:
        persona = ''
    
    if lang == 'ko':
        return PROMPT_ANSWER_KO.format(persona=persona, secret=secret)
    elif lang in ['ja', 'jp']:
        return PROMPT_ANSWER_JA.format(persona=persona, secret=secret)
    else:
        return PROMPT_ANSWER_EN.format(persona=persona, secret=secret)


# ============================================================================
# 2. 사용자 의도 분류 프롬프트
# ============================================================================

PROMPT_CLASSIFY_INTENT_KO = '''당신은 스무고개 진행 보조입니다. 아래 사용자의 발화를 분류하세요.
비밀 정답은 system에만 제공되며 절대 공개하지 마세요.
대화 맥락을 고려하여 현재 발화의 의도를 판단하세요.

다음 다섯 가지를 yes/no로 '; '로 구분하여 한 줄만 출력: 
related=<yes|no>; question=<yes|no>; guess_intent=<yes|no>; stop_intent=<yes|no>; should_count=<yes|no>

## 정의
- related: 스무고개 게임과 관련된 발화인가? 욕설/잡담/인사는 no.
- question: 예/아니오로 답변 가능한 질문인가? '~니?', '~어?', '~나?', '~까?' 등의 종결어미도 질문으로 인정. 물음표 없어도 가능.
- guess_intent: '답은 X다' 형태로 명시적으로 정답을 말하는가? 일반 질문은 no.
- stop_intent: 게임을 중단하려는 의도인가?
- should_count: related=yes이고 question=yes이면서 비밀 정답을 좁히는 유의미한 질문이면 yes. '살아있어?', '먹을 수 있어?' 등 속성 질문은 yes.

{history_section}

Secret: {secret}
Utterance: {utterance}
/no_think'''

PROMPT_CLASSIFY_INTENT_EN = '''You assist 20 Questions. Classify the utterance. Secret target is hidden; never reveal it.
Consider the conversation context to determine the intent of the current utterance.

Output exactly one line with 5 flags: 
related=<yes|no>; question=<yes|no>; guess_intent=<yes|no>; stop_intent=<yes|no>; should_count=<yes|no>

## Definitions
- related: Is this game-related? Insults/chitchat/greetings are no.
- question: Can it be answered with yes/no? Question mark not required.
- guess_intent: Does it explicitly state an answer like 'it is X'? Simple questions are no.
- stop_intent: Intent to stop the game?
- should_count: yes if related=yes and question=yes and it's a meaningful question narrowing the secret.

{history_section}

Secret: {secret}
Utterance: {utterance}
/no_think'''

PROMPT_CLASSIFY_INTENT_JA = '''あなたは二十の質問の進行補助です。以下の発話を分類してください。秘密の答えは開示禁止。
会話の文脈を考慮して現在の発話の意図を判断してください。

以下の5項目をyes/noで; 区切りで一行のみ出力: 
related=<yes|no>; question=<yes|no>; guess_intent=<yes|no>; stop_intent=<yes|no>; should_count=<yes|no>

## 定義
- related: ゲーム関連の発話か? 罵倒/雑談/挨拶はno。
- question: はい/いいえで答えられる質問か? 疑問符なしでもOK。
- guess_intent: '答えはXだ'形式で明示的に答えを述べているか? 通常の質問はno。
- stop_intent: ゲームを中断する意図か?
- should_count: related=yesかつquestion=yesで秘密の答えを絞り込む有用な質問ならyes。

{history_section}

Secret: {secret}
Utterance: {utterance}
/no_think'''


def get_classify_intent_prompt(utterance: str, secret: str, history: list = None, lang: str = 'ko') -> str:
    '''사용자 의도 분류 프롬프트'''
    # 히스토리 직렬화
    hist_text = ''
    if history:
        recent_history = history[-6:] if len(history) > 6 else history
        for turn in recent_history:
            role = turn.get('role', 'user')
            content = (turn.get('content') or '').strip()
            if content and role in ('user', 'assistant'):
                hist_text += f"{role}: {content}\n"
    
    if lang == 'ko':
        hist_section = f"## 최근 대화\n{hist_text}\n" if hist_text else ""
        return PROMPT_CLASSIFY_INTENT_KO.format(
            history_section=hist_section, secret=secret, utterance=utterance
        )
    elif lang in ['ja', 'jp']:
        hist_section = f"## 最近の会話\n{hist_text}\n" if hist_text else ""
        return PROMPT_CLASSIFY_INTENT_JA.format(
            history_section=hist_section, secret=secret, utterance=utterance
        )
    else:
        hist_section = f"## Recent Conversation\n{hist_text}\n" if hist_text else ""
        return PROMPT_CLASSIFY_INTENT_EN.format(
            history_section=hist_section, secret=secret, utterance=utterance
        )


def parse_classify_intent_response(response: str) -> dict:
    '''의도 분류 응답 파싱'''
    line = response.strip().lower()
    result = {
        'related': 'no',
        'question': 'no',
        'guess_intent': 'no',
        'stop_intent': 'no',
        'should_count': 'no'
    }
    for key in list(result.keys()):
        if f"{key}=yes" in line:
            result[key] = 'yes'
        elif f"{key}=no" in line:
            result[key] = 'no'
    return result


# ============================================================================
# 3. 정답 판정 프롬프트
# ============================================================================

PROMPT_JUDGE_GUESS_KO = '''다음 두 단어가 **완전히 동일한 대상**을 가리키는지 판단하세요.

## 허용 (yes):
- 동의어: '자동차' = '차'
- 단수/복수: '사과' = '사과들'
- 일반명사/고유명사: '산' = '한라산'
- 표기 차이: '컴퓨터' = '컴퓨타'

## 불허 (no):
- 같은 카테고리지만 다른 대상: '장어' ≠ '가오리' (둘 다 물고기이지만 다름)
- 유사하지만 다른 것: '고양이' ≠ '개'
- 상위/하위 개념: '동물' ≠ '고양이'

⚠️ **중요**: '장어'와 '가오리'처럼 같은 카테고리(물고기)라도 **서로 다른 종**이면 **no**입니다.

정확히 한 단어로만 출력: yes 또는 no

Secret: {secret}
Guess: {guess}
/no_think'''

PROMPT_JUDGE_GUESS_EN = '''Decide if these two words refer to **exactly the same target**.

## Allow (yes):
- Synonyms: 'car' = 'automobile'
- Singular/plural
- Common/proper nouns

## Reject (no):
- Same category but different targets: 'eel' ≠ 'stingray' (both fish but different)
- Similar but different things
- Superclass/subclass: 'animal' ≠ 'cat'

⚠️ **Important**: Even in same category, if **different species**, answer **no**.

Output one word only: yes or no

Secret: {secret}
Guess: {guess}
/no_think'''

PROMPT_JUDGE_GUESS_JA = '''次の2つの単語が**完全に同一の対象**を指すか判断してください。

## 許容 (yes):
- 同義語: '自動車' = '車'
- 単数/複数形
- 一般名詞/固有名詞

## 不許可 (no):
- 同じカテゴリだが異なる対象: 'ウナギ' ≠ 'エイ' (両方とも魚だが違う)
- 類似だが異なるもの
- 上位/下位概念: '動物' ≠ '猫'

⚠️ **重要**: 同じカテゴリでも**異なる種**なら**no**です。

一語のみ: yes または no

Secret: {secret}
Guess: {guess}
/no_think'''


def get_judge_guess_prompt(guess: str, secret: str, lang: str = 'ko') -> str:
    '''정답 판정 프롬프트'''
    if lang == 'ko':
        return PROMPT_JUDGE_GUESS_KO.format(secret=secret, guess=guess)
    elif lang in ['ja', 'jp']:
        return PROMPT_JUDGE_GUESS_JA.format(secret=secret, guess=guess)
    else:
        return PROMPT_JUDGE_GUESS_EN.format(secret=secret, guess=guess)


def parse_judge_guess_response(response: str) -> str:
    '''정답 판정 응답 파싱'''
    label = response.strip().lower()
    return 'yes' if 'yes' in label[:5] else 'no'


# ============================================================================
# 4. 비밀 단어 생성 프롬프트
# ============================================================================

PROMPT_GENERATE_SECRET_KO = '''당신은 스무고개 게임의 출제자입니다.
테마 '{theme_name}' 카테고리에서 게임에 적합한 구체적인 정답을 하나 만들어주세요.
딱 한 단어(또는 한 구절)만 출력하세요. 따옴표나 설명 없이 단어만 출력하세요.

## 절대 금지 사항 (STRICTLY FORBIDDEN)
- 인터넷 슬랭, 줄임말 사용 금지
- 모든 이모지/이모티콘 사용 금지 (예: 😊 ❌ ✅ ^^ ㅠㅠ 등)
- 설명이나 추가 문장 금지
- 오직 한글, 영어, 숫자만 출력

테마: {theme_name}
/no_think'''

PROMPT_GENERATE_SECRET_EN = '''You are the setter for 20 Questions.
Create an appropriate specific answer from the theme '{theme_name}' for the game.
Output ONLY the word/short phrase, no quotes or explanations.

## STRICTLY FORBIDDEN
- No internet slang, abbreviations, or emojis
- No explanations or additional sentences

Theme: {theme_name}
/no_think'''

PROMPT_GENERATE_SECRET_JA = '''あなたは二十の質問ゲームの出題者です。
テーマ『{theme_name}』カテゴリから、ゲームに適した具体的な答えを一つ作成してください。
一語（または短い語句）のみ出力し、引用符や説明なしで出力してください。

## 🚨 絶対禁止事項 (STRICTLY FORBIDDEN) 🚨
- ❌ インターネットスラング、略語、絵文字使用禁止
- ❌ 説明や追加文章禁止

テーマ: {theme_name}
/no_think'''


def get_generate_secret_prompt(theme_key: str, lang: str = 'ko') -> str:
    '''비밀 단어 생성 프롬프트'''
    theme_name = get_theme_name(theme_key, lang)
    
    if lang == 'ko':
        return PROMPT_GENERATE_SECRET_KO.format(theme_name=theme_name)
    elif lang in ['ja', 'jp']:
        return PROMPT_GENERATE_SECRET_JA.format(theme_name=theme_name)
    else:
        return PROMPT_GENERATE_SECRET_EN.format(theme_name=theme_name)


# ============================================================================
# 5. 재시작 의도 분류 프롬프트
# ============================================================================

PROMPT_CLASSIFY_RESTART_KO = '''다음 발화에서 사용자가 **명시적으로** 스무고개 게임을 '새로 시작하려는 의도'가 있는지 판단하세요.

**재시작 의도 (yes):**
- '새 게임 하자', '다시 시작', '처음부터', '새로운 게임' 등
- 게임을 **새로 시작**하자는 **명백한 표현**
- AI가 '새 게임 시작할까요?' 같은 질문을 했고, 사용자가 긍정 응답 (예: '응', '네', '좋아', 'yes', 'ok')

**재시작 아님 (no):**
- 일반 질문 (예: '빨간색일까?', '크기가 작아?')
- 추측 (예: '사과야?', '혹시 오렌지?')
- AI의 재시작 확인 질문에 대한 부정 응답 (예: '아니', '아니요', '싫어')
- 기타 게임 진행 중 발화

⚠️ **중요**: 
- 최근 대화를 참고하여 문맥을 고려하세요.
- AI가 '새 게임을 시작할까요?'라고 물어본 직후 '응', '네' 같은 긍정 답변은 **yes**입니다.

{history_section}

User: {utterance}

반드시 한 단어로 출력: yes 또는 no
/no_think'''

PROMPT_CLASSIFY_RESTART_EN = '''Decide if the user **explicitly** intends to start a **new** 20 Questions game.

**Restart intent (yes):**
- 'new game', 'start over', 'restart', 'begin again', etc.
- **Clear expressions** requesting to start a new game
- AI asked 'Start a new game?' and user gave affirmative response (e.g., 'yes', 'yeah', 'ok', 'sure')

**Not restart (no):**
- Regular questions (e.g., 'is it red?', 'is it small?')
- Guesses (e.g., 'is it apple?', 'orange?')
- Negative response to AI's restart confirmation (e.g., 'no', 'nope')
- Other in-game utterances

⚠️ **Important**: 
- Consider context from recent conversation.
- If AI just asked 'Start a new game?' and user responds with 'yes', 'yeah', it's **yes**.

{history_section}

User: {utterance}

Output one word only: yes or no
/no_think'''

PROMPT_CLASSIFY_RESTART_JA = '''次の発話から**明示的に**二十の質問ゲームを『新しく始める意図』があるか判断してください。

**再開始意図 (yes):**
- '新しいゲーム', 'もう一度', '最初から', '新しく始める' など
- ゲームを**新しく始める**という**明確な表現**
- AIが「新しいゲームを始めますか？」のような質問をして、ユーザーが肯定的な応答（例: 'はい', 'うん', 'いいよ', 'yes', 'ok'）

**再開始ではない (no):**
- 一般的な質問 (例: '赤い?', 'サイズは小さい?')
- 推測 (例: 'リンゴ?', 'オレンジかな?')
- AIの再開始確認質問に対する否定的な応答（例: 'いいえ', 'やだ'）
- その他ゲーム進行中の発話

⚠️ **重要**: 
- 最近の会話を参考にして文脈を考慮してください。
- AIが「新しいゲームを始めますか？」と聞いた直後に 'はい', 'うん' などの肯定的な答えは **yes** です。

{history_section}

User: {utterance}

必ず一語で出力: yes または no
/no_think'''


def get_classify_restart_prompt(utterance: str, history: list = None, lang: str = 'ko') -> str:
    '''재시작 의도 분류 프롬프트'''
    hist_text = ''
    if history:
        recent_history = history[-4:] if len(history) > 4 else history
        for turn in recent_history:
            role = turn.get('role', 'user')
            content = (turn.get('content') or '').strip()
            if content and role in ('user', 'assistant'):
                hist_text += f"{role}: {content}\n"
    
    if lang == 'ko':
        hist_section = f"## 최근 대화\n{hist_text}\n" if hist_text else ""
        return PROMPT_CLASSIFY_RESTART_KO.format(history_section=hist_section, utterance=utterance)
    elif lang in ['ja', 'jp']:
        hist_section = f"## 最近の会話\n{hist_text}\n" if hist_text else ""
        return PROMPT_CLASSIFY_RESTART_JA.format(history_section=hist_section, utterance=utterance)
    else:
        hist_section = f"## Recent Conversation\n{hist_text}\n" if hist_text else ""
        return PROMPT_CLASSIFY_RESTART_EN.format(history_section=hist_section, utterance=utterance)


# ============================================================================
# 6. 계속/포기 의도 분류 프롬프트
# ============================================================================

PROMPT_CLASSIFY_CONTINUE_KO = '''다음 발화에서 사용자가 '게임을 포기하려는 명시적 의도'가 있는지 판단하세요.

**포기 의도 (give_up):**
- '포기할래', '그만할래', '포기', '정답 알려줘', '답 말해줘' 등
- 게임을 중단하고 정답을 요구하는 **명백한 표현**

**계속 의도 (continue):**
- '계속할래', '더 질문할래', '계속', '좋아' 등
- 또는 **일반 질문/추측** (예: '이거 사과야?', '빨간색이야?')

⚠️ **중요**: 정답을 추측하는 질문(예: '혹시 오렌지니?', '사과야?')은 **continue**로 판단하세요.
포기 의도는 '포기', '그만', '정답 알려줘' 같은 **명시적 표현**만 해당됩니다.

User: {utterance}

반드시 한 단어로 출력: continue 또는 give_up
/no_think'''

PROMPT_CLASSIFY_CONTINUE_EN = '''Decide if the user has an **explicit intent to give up the game**.

**Give up intent (give_up):**
- 'give up', 'quit', 'tell me the answer', 'show answer', etc.
- **Clear expressions** requesting to stop and reveal the answer

**Continue intent (continue):**
- 'continue', 'keep going', 'more questions', 'okay', etc.
- Or **regular questions/guesses** (e.g., 'is it an apple?', 'is it red?')

⚠️ **Important**: Questions guessing the answer (e.g., 'is it orange?', 'apple?') should be classified as **continue**.
Give up intent ONLY applies to **explicit phrases** like 'give up', 'quit', 'tell me the answer'.

User: {utterance}

Output one word only: continue or give_up
/no_think'''

PROMPT_CLASSIFY_CONTINUE_JA = '''次の発話から『ゲームを諦める明示的な意図』があるか判断してください。

**諦める意図 (give_up):**
- '諦める', 'やめる', '答えを教えて' など
- ゲームを中断して答えを求める**明確な表現**

**続ける意図 (continue):**
- '続ける', 'もっと質問する', '続けます' など
- または**一般的な質問/推測** (例: 'これはリンゴ?', '赤い?')

⚠️ **重要**: 答えを推測する質問(例: 'オレンジかな?', 'リンゴ?')は**continue**と判断してください。
諦める意図は '諦める'、'やめる'、'答えを教えて' のような**明示的な表現**のみです。

User: {utterance}

必ず一語で出力: continue または give_up
/no_think'''


def get_classify_continue_prompt(utterance: str, lang: str = 'ko') -> str:
    '''계속/포기 의도 분류 프롬프트'''
    if lang == 'ko':
        return PROMPT_CLASSIFY_CONTINUE_KO.format(utterance=utterance)
    elif lang in ['ja', 'jp']:
        return PROMPT_CLASSIFY_CONTINUE_JA.format(utterance=utterance)
    else:
        return PROMPT_CLASSIFY_CONTINUE_EN.format(utterance=utterance)


# ============================================================================
# 7. 일상 대화 생성 프롬프트
# ============================================================================

PROMPT_CASUAL_CHAT_KO = '''## 상황: 스무고개 게임 {game_status_desc}
{context_desc}

## 답변 규칙
- **1-2문장으로만** 짧고 친절하게 답변
- {guidance}
- 정답이나 힌트는 절대 불가능
- 자기소개 금지

{persona}

User: {utterance}
/no_think'''

PROMPT_CASUAL_CHAT_EN = '''## Situation: 20 Questions game {game_status_desc}
{context_desc}

## Response Rules
- Answer with **only 1-2 sentences**, short and kind
- {guidance}
- Answers or hints absolutely not allowed
- No self-introduction

{persona}

User: {utterance}
/no_think'''

PROMPT_CASUAL_CHAT_JA = '''## 状況: 二十の質問ゲーム {game_status_desc}
{context_desc}

## 回答ルール
- **1-2文のみ**で短く親切に答える
- {guidance}
- 答えやヒントは絶対不可
- 自己紹介禁止

{persona}

User: {utterance}
/no_think'''


def get_casual_chat_prompt(utterance: str, lang: str = 'ko', char_name: str = 'arona', game_status: str = 'playing') -> str:
    '''일상 대화 생성 프롬프트'''
    if char_name == 'arona':
        persona = get_arona_persona(lang)
    else:
        persona = ''
    
    if game_status == 'playing':
        if lang == 'ko':
            status_desc = '진행 중'
            context = '사용자가 잠깐 다른 이야기를 하려고 합니다.'
            guidance = '간단히 공감 후, **즉시 게임으로 유도**'
        elif lang in ['ja', 'jp']:
            status_desc = '進行中'
            context = 'ユーザーは少し別の話をしたいようです。'
            guidance = '簡単に共感後、**すぐにゲームに誘導**'
        else:
            status_desc = 'in progress'
            context = 'User wants to briefly talk about something else.'
            guidance = 'Briefly acknowledge, then **guide back to game**'
    else:  # game_over
        if lang == 'ko':
            status_desc = '종료'
            context = '새 게임 시작을 기다리는 중입니다.'
            guidance = '가볍게 대화하되, **새 게임 제안**'
        elif lang in ['ja', 'jp']:
            status_desc = '終了'
            context = '新しいゲーム開始を待っています。'
            guidance = '軽く会話しつつ、**新ゲーム提案**'
        else:
            status_desc = 'ended'
            context = 'Waiting for new game to start.'
            guidance = 'Chat lightly, then **suggest new game**'
    
    if lang == 'ko':
        return PROMPT_CASUAL_CHAT_KO.format(
            game_status_desc=status_desc, context_desc=context,
            guidance=guidance, persona=persona, utterance=utterance
        )
    elif lang in ['ja', 'jp']:
        return PROMPT_CASUAL_CHAT_JA.format(
            game_status_desc=status_desc, context_desc=context,
            guidance=guidance, persona=persona, utterance=utterance
        )
    else:
        return PROMPT_CASUAL_CHAT_EN.format(
            game_status_desc=status_desc, context_desc=context,
            guidance=guidance, persona=persona, utterance=utterance
        )


# ============================================================================
# 8. 추측 단어 추출 프롬프트
# ============================================================================

PROMPT_EXTRACT_GUESS_KO = '''다음 발화에서 사용자가 추측하는 '정답 단어'만 추출하세요.
예시: '답은 피카추야' → '피카추'
예시: '그것은 고양이다' → '고양이'
단어만 출력, 설명 금지.

Utterance: {utterance}
/no_think'''

PROMPT_EXTRACT_GUESS_EN = '''Extract ONLY the guessed answer word from the utterance.
Example: 'the answer is cat' → 'cat'
Output only the word, no explanations.

Utterance: {utterance}
/no_think'''

PROMPT_EXTRACT_GUESS_JA = '''次の発話から推測される『答えの単語』のみを抽出してください。
例: '答えは猫だ' → '猫'
単語のみ出力、説明禁止。

Utterance: {utterance}
/no_think'''


def get_extract_guess_prompt(utterance: str, lang: str = 'ko') -> str:
    '''추측 단어 추출 프롬프트'''
    if lang == 'ko':
        return PROMPT_EXTRACT_GUESS_KO.format(utterance=utterance)
    elif lang in ['ja', 'jp']:
        return PROMPT_EXTRACT_GUESS_JA.format(utterance=utterance)
    else:
        return PROMPT_EXTRACT_GUESS_EN.format(utterance=utterance)


if __name__ == '__main__':
    print('=== ai_20q_prompts 테스트 ===')
    
    print('\n--- 답변 프롬프트 (ko) ---')
    prompt = get_answer_prompt('고양이', 'ko', 'arona')
    print(prompt[:500] + '...')
    
    print('\n--- 의도 분류 프롬프트 (ko) ---')
    prompt = get_classify_intent_prompt('살아있어?', '고양이', [], 'ko')
    print(prompt[:500] + '...')
    
    print('\n--- 정답 판정 프롬프트 (en) ---')
    prompt = get_judge_guess_prompt('cat', 'cat', 'en')
    print(prompt)
