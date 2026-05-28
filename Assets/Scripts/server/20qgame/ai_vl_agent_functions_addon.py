'''
ai_vl_agent_functions_addon.py
20 Questions Game용 Function 메타 정보 확장

20Q 게임에서 사용하는 Function들의 메타 정보를 정의합니다.
VL(화면 분석) 기능은 사용하지 않지만, 일관성을 위해 Function 메타 패턴을 유지합니다.
'''
import sys
import os
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


# ============================================================================
# 20 Questions Game Function 이름 상수
# ============================================================================

# 질문에 대한 답변 생성
FUNC_GENERATE_ANSWER = 'function_20q_generate_answer'

# 사용자 의도 분류
FUNC_CLASSIFY_INTENT = 'function_20q_classify_intent'

# 정답 판정
FUNC_JUDGE_GUESS = 'function_20q_judge_guess'

# 비밀 단어 생성
FUNC_GENERATE_SECRET = 'function_20q_generate_secret'

# AI 추측 생성
FUNC_GENERATE_GUESS = 'function_20q_generate_guess'

# 일상 대화 생성
FUNC_GENERATE_CASUAL_CHAT = 'function_20q_generate_casual_chat'

# 재시작 의도 분류
FUNC_CLASSIFY_RESTART = 'function_20q_classify_restart'

# 계속/포기 의도 분류
FUNC_CLASSIFY_CONTINUE = 'function_20q_classify_continue'


# ============================================================================
# Function 설명 (다국어)
# ============================================================================

FUNCTION_DESCRIPTIONS_KO = {
    FUNC_GENERATE_ANSWER: {
        'name': 'function_20q_generate_answer',
        'description': '스무고개 질문에 대해 예/아니오/모르겠어요 답변 생성'
    },
    FUNC_CLASSIFY_INTENT: {
        'name': 'function_20q_classify_intent',
        'description': '사용자 발화의 의도 분류 (질문/추측/잡담/중단)'
    },
    FUNC_JUDGE_GUESS: {
        'name': 'function_20q_judge_guess',
        'description': '사용자의 추측이 정답인지 판정'
    },
    FUNC_GENERATE_SECRET: {
        'name': 'function_20q_generate_secret',
        'description': '새로운 비밀 단어 생성 (테마 기반)'
    },
    FUNC_GENERATE_GUESS: {
        'name': 'function_20q_generate_guess',
        'description': 'AI가 대화 기록 기반으로 정답 추측'
    },
    FUNC_GENERATE_CASUAL_CHAT: {
        'name': 'function_20q_generate_casual_chat',
        'description': '게임 무관 발화에 대한 일상 대화 생성'
    },
    FUNC_CLASSIFY_RESTART: {
        'name': 'function_20q_classify_restart',
        'description': '재시작 의도 분류 (새 게임 시작 여부)'
    },
    FUNC_CLASSIFY_CONTINUE: {
        'name': 'function_20q_classify_continue',
        'description': '계속/포기 의도 분류'
    }
}

FUNCTION_DESCRIPTIONS_EN = {
    FUNC_GENERATE_ANSWER: {
        'name': 'function_20q_generate_answer',
        'description': 'Generate yes/no/unknown answer for 20 Questions'
    },
    FUNC_CLASSIFY_INTENT: {
        'name': 'function_20q_classify_intent',
        'description': 'Classify user utterance intent (question/guess/chat/stop)'
    },
    FUNC_JUDGE_GUESS: {
        'name': 'function_20q_judge_guess',
        'description': 'Judge if user guess is correct'
    },
    FUNC_GENERATE_SECRET: {
        'name': 'function_20q_generate_secret',
        'description': 'Generate new secret word (theme-based)'
    },
    FUNC_GENERATE_GUESS: {
        'name': 'function_20q_generate_guess',
        'description': 'AI generates guess based on conversation history'
    },
    FUNC_GENERATE_CASUAL_CHAT: {
        'name': 'function_20q_generate_casual_chat',
        'description': 'Generate casual chat for non-game utterances'
    },
    FUNC_CLASSIFY_RESTART: {
        'name': 'function_20q_classify_restart',
        'description': 'Classify restart intent (new game start)'
    },
    FUNC_CLASSIFY_CONTINUE: {
        'name': 'function_20q_classify_continue',
        'description': 'Classify continue/give up intent'
    }
}

FUNCTION_DESCRIPTIONS_JA = {
    FUNC_GENERATE_ANSWER: {
        'name': 'function_20q_generate_answer',
        'description': '二十の質問に対して、はい/いいえ/わかりません回答を生成'
    },
    FUNC_CLASSIFY_INTENT: {
        'name': 'function_20q_classify_intent',
        'description': 'ユーザー発話の意図を分類（質問/推測/雑談/中断）'
    },
    FUNC_JUDGE_GUESS: {
        'name': 'function_20q_judge_guess',
        'description': 'ユーザーの推測が正解かどうか判定'
    },
    FUNC_GENERATE_SECRET: {
        'name': 'function_20q_generate_secret',
        'description': '新しい秘密の単語を生成（テーマベース）'
    },
    FUNC_GENERATE_GUESS: {
        'name': 'function_20q_generate_guess',
        'description': 'AIが会話履歴に基づいて答えを推測'
    },
    FUNC_GENERATE_CASUAL_CHAT: {
        'name': 'function_20q_generate_casual_chat',
        'description': 'ゲーム無関係な発話に対する日常会話を生成'
    },
    FUNC_CLASSIFY_RESTART: {
        'name': 'function_20q_classify_restart',
        'description': '再開始意図を分類（新しいゲーム開始）'
    },
    FUNC_CLASSIFY_CONTINUE: {
        'name': 'function_20q_classify_continue',
        'description': '続ける/諦める意図を分類'
    }
}


def get_function_descriptions(lang: str = 'en') -> dict:
    '''언어에 따른 Function 설명 dict 반환'''
    if lang == 'ko':
        return FUNCTION_DESCRIPTIONS_KO
    elif lang in ['ja', 'jp']:
        return FUNCTION_DESCRIPTIONS_JA
    else:
        return FUNCTION_DESCRIPTIONS_EN


def get_all_functions_list(lang: str = 'en') -> str:
    '''모든 Function에 대한 설명 프롬프트 반환'''
    descriptions = get_function_descriptions(lang)
    lines = []
    for func_name, desc in descriptions.items():
        lines.append(f"- {desc['name']}: {desc['description']}")
    return '\n'.join(lines)


# ============================================================================
# 테마 관련 정보 (ASIS2의 ai_game_20questions_answers.py 참조)
# ============================================================================

# 테마 목록 (영어 키 -> 다국어 이름)
THEME_LIST = {
    'animal': {'ko': '동물', 'en': 'Animal', 'ja': '動物'},
    'fruit': {'ko': '과일', 'en': 'Fruit', 'ja': '果物'},
    'food': {'ko': '음식', 'en': 'Food', 'ja': '食べ物'},
    'object': {'ko': '사물', 'en': 'Object', 'ja': '物'},
    'place': {'ko': '장소', 'en': 'Place', 'ja': '場所'},
    'character': {'ko': '캐릭터', 'en': 'Character', 'ja': 'キャラクター'},
    'vehicle': {'ko': '탈것', 'en': 'Vehicle', 'ja': '乗り物'},
    'sport': {'ko': '스포츠', 'en': 'Sport', 'ja': 'スポーツ'}
}

THEME_KEYS = list(THEME_LIST.keys())


def get_theme_name(theme_key: str, lang: str = 'ko') -> str:
    '''테마 키에서 언어별 이름 반환'''
    if theme_key not in THEME_LIST:
        return theme_key
    
    lang_key = 'ja' if lang in ['ja', 'jp'] else lang
    return THEME_LIST[theme_key].get(lang_key, THEME_LIST[theme_key]['en'])


def get_theme_list_prompt(lang: str = 'ko') -> str:
    '''테마 목록을 프롬프트용 문자열로 반환'''
    lines = []
    for key, names in THEME_LIST.items():
        lang_key = 'ja' if lang in ['ja', 'jp'] else lang
        name = names.get(lang_key, names['en'])
        lines.append(f"- {key}: {name}")
    return '\n'.join(lines)


if __name__ == '__main__':
    print('=== ai_vl_agent_functions_addon (20Q) 테스트 ===')
    
    print('\n--- Function 목록 (ko) ---')
    print(get_all_functions_list('ko'))
    
    print('\n--- 테마 목록 (ko) ---')
    print(get_theme_list_prompt('ko'))
    
    print('\n--- 테마 이름 ---')
    print(f"animal (ko): {get_theme_name('animal', 'ko')}")
    print(f"animal (en): {get_theme_name('animal', 'en')}")
    print(f"animal (ja): {get_theme_name('animal', 'ja')}")
