'''
ai_20q_game.py
20 Questions Game 메인 루프 (AgentEvent 기반)

Stateless 구조로 context_data를 통해 게임 상태를 관리합니다.
Unity 클라이언트와 스트리밍 방식으로 통신합니다.
'''
import sys
import os
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import random
from typing import Generator

# 타입 및 이벤트 상수
from ai_vl_agent_types_addon import (
    AgentEvent, ThinkEntry, append_think_log, restore_think_log,
    EVENT_KIND_GAME_START, EVENT_KIND_GAME_ANSWER, EVENT_KIND_AI_GUESS,
    EVENT_KIND_GUESS_RESULT, EVENT_KIND_CASUAL_CHAT, EVENT_KIND_WAITING_ANSWER,
    EVENT_KIND_GAME_OVER, EVENT_KIND_GUIDE_QUESTION,
    PHASE_CLASSIFY, PHASE_ANSWER, PHASE_JUDGE, PHASE_CHAT,
    create_20q_context_data, restore_20q_context, create_game_event
)

# Function 메타 정보
from ai_vl_agent_functions_addon import (
    THEME_KEYS, get_theme_name
)

# LLM 호출
from ai_20q_llm import (
    generate_answer, generate_answer_stream,
    classify_user_intent, judge_guess_correctness,
    generate_secret_target, classify_restart_intent,
    classify_continue_intent, generate_casual_chat,
    generate_casual_chat_stream, extract_guess_from_text
)


# ============================================================================
# 메인 게임 루프
# ============================================================================

def game_run_loop(query, context_data=None, lang='ko',
                   char_name='arona', server_type='Local', api_key=None,
                   history=None, history_question=None):
    '''20 Questions Game 메인 루프 - AgentEvent 생성'''
    think_log = []
    history = history or []
    history_question = history_question or []
    
    # 1. 상태 복원
    ctx = restore_20q_context(context_data)
    secret = ctx['secret']  # 비밀 단어
    theme_key = ctx['theme_key']  # 테마 키 (animal/food/object 등)
    question_count = ctx['question_count']  # 현재 질문 카운트
    max_questions = ctx['max_questions']  # 최대 질문 수 (기본 20)
    waiting_for = ctx['waiting_for']  # 대기 상태 (user_input/restart_confirm 등)
    game_status = ctx['game_status']  # 게임 상태 (playing/ended)
    game_result = ctx['game_result']  # 게임 결과 (win/lose/giveup)
    history_secret_list = ctx['history_secret_list']  # 이전 게임 정답 목록
    
    append_think_log(think_log, PHASE_CLASSIFY, f'상태 복원: secret={bool(secret)}, waiting_for={waiting_for}, game_status={game_status}')
    
    # 2. 새 게임 시작 (secret이 없으면)
    if not secret:
        yield from handle_new_game(
            think_log, lang, char_name, server_type, api_key,
            theme_key, history_secret_list, max_questions
        )
        return
    
    # 3. 빈 쿼리 처리
    if not query or not query.strip():
        query = '...'
    
    # 4. 특수 의도 처리 (재시작, 계속/포기)
    special_result = yield from handle_special_intents(
        query, think_log, lang, char_name, server_type, api_key,
        secret, theme_key, question_count, max_questions,
        waiting_for, game_status, game_result, history, history_secret_list
    )
    
    if special_result == 'handled':
        return
    
    # 5. 일반 의도 분류
    append_think_log(think_log, PHASE_CLASSIFY, f'의도 분류 시작: "{query[:30]}..."')
    
    cls = classify_user_intent(
        utterance=query,
        secret=secret,
        history=history_question,
        lang=lang,
        server_type=server_type,
        api_key=api_key
    )
    
    is_related = cls.get('related') == 'yes'
    is_question = cls.get('question') == 'yes'
    is_stop_intent = cls.get('stop_intent') == 'yes'
    is_guess_intent = cls.get('guess_intent') == 'yes'
    should_count = cls.get('should_count') == 'yes'
    
    append_think_log(think_log, PHASE_CLASSIFY, f'분류 결과: related={is_related}, question={is_question}, guess={is_guess_intent}')
    
    # 6. 게임 무관 발화 → 일상 대화
    if not is_related:
        yield from handle_casual_chat(
            query, think_log, lang, char_name, server_type, api_key,
            secret, theme_key, question_count, max_questions,
            history, history_secret_list, game_status='playing'
        )
        return
    
    # 7. 중단 의도 → 재시작 확인
    if is_stop_intent:
        yield from handle_stop_intent(
            query, think_log, lang,
            secret, theme_key, question_count, max_questions,
            history, history_secret_list
        )
        return
    
    # 8. 추측 의도 → 정답 판정
    if is_guess_intent:
        yield from handle_guess_intent(
            query, think_log, lang, char_name, server_type, api_key,
            secret, theme_key, question_count, max_questions,
            history, history_question, history_secret_list
        )
        return
    
    # 9. 유효하지 않은 질문 → 규칙 안내
    if not is_question or not should_count:
        yield from handle_guide_question(
            query, think_log, lang, char_name, server_type, api_key,
            secret, theme_key, question_count, max_questions,
            history, history_question, history_secret_list
        )
        return
    
    # 10. 유효한 질문 → 답변 생성
    yield from handle_valid_question(
        query, think_log, lang, char_name, server_type, api_key,
        secret, theme_key, question_count, max_questions,
        history, history_question, history_secret_list
    )


# ============================================================================
# 핸들러 함수들
# ============================================================================

def handle_new_game(think_log, lang, char_name,
                    server_type, api_key, theme_key,
                    history_secret_list, max_questions):
    '''새 게임 시작 처리'''
    append_think_log(think_log, PHASE_CLASSIFY, '새 게임 시작')
    
    # 테마 선택 (지정되지 않은 경우 랜덤)
    if not theme_key:
        theme_key = random.choice(THEME_KEYS)
    
    # 비밀 단어 생성
    secret = generate_secret_target(
        theme_key=theme_key,
        lang=lang,
        used_answers=history_secret_list,
        server_type=server_type,
        api_key=api_key
    )
    
    if secret and secret not in history_secret_list:
        history_secret_list.append(secret)
    
    theme_name = get_theme_name(theme_key, lang)
    append_think_log(think_log, PHASE_CLASSIFY, f'테마: {theme_name}, 정답: {secret}')
    
    # 환영 메시지
    if lang == 'ko':
        welcome_msg = f"스무고개 게임을 시작합니다! 테마는 '{theme_name}'입니다. 질문해주세요."
    elif lang in ['ja', 'jp']:
        welcome_msg = f"二十の質問ゲームを始めます！テーマは'{theme_name}'です。質問してください。"
    else:
        welcome_msg = f"Let's play 20 Questions! The theme is '{theme_name}'. Ask me questions."
    
    reply_list = [{
        'answer_ko': f"스무고개 게임을 시작합니다! 테마는 '{get_theme_name(theme_key, 'ko')}'입니다. 질문해주세요.",
        'answer_jp': f"二十の質問ゲームを始めます！テーマは'{get_theme_name(theme_key, 'ja')}'です。質問してください。",
        'answer_en': f"Let's play 20 Questions! The theme is '{get_theme_name(theme_key, 'en')}'. Ask me questions."
    }]
    
    new_context = create_20q_context_data(
        secret=secret,
        theme_key=theme_key,
        question_count=0,
        max_questions=max_questions,
        waiting_for=None,
        game_status='game_start',
        game_result=None,
        history_secret_list=history_secret_list
    )
    
    yield create_game_event(
        kind=EVENT_KIND_GAME_START,
        message=welcome_msg,
        think_log=think_log,
        reply_list=reply_list,
        context_data=new_context,
        theme_key=theme_key,
        theme=theme_name
    )


def handle_special_intents(query, think_log, lang,
                           char_name, server_type, api_key,
                           secret, theme_key, question_count,
                           max_questions, waiting_for, game_status,
                           game_result, history, history_secret_list):
    '''재시작/계속/포기 의도 처리'''
    
    # 재시작 의도 확인
    if waiting_for == 'restart' or game_status == 'playing':
        restart_intent = classify_restart_intent(
            utterance=query,
            history=history,
            lang=lang,
            server_type=server_type,
            api_key=api_key
        )
        
        if restart_intent == 'yes':
            append_think_log(think_log, PHASE_CLASSIFY, '재시작 의도 감지')
            
            # playing 중이고 waiting_for가 restart가 아니면 확인 요청
            if game_status == 'playing' and waiting_for != 'restart':
                if lang == 'ko':
                    msg = "정말 새 게임을 시작할까요? 현재 게임이 종료됩니다."
                elif lang in ['ja', 'jp']:
                    msg = "本当に新しいゲームを始めますか？現在のゲームが終了します。"
                else:
                    msg = "Start a new game? The current game will end."
                
                reply_list = [{
                    'answer_ko': "정말 새 게임을 시작할까요? 현재 게임이 종료됩니다.",
                    'answer_jp': "本当に新しいゲームを始めますか？現在のゲームが終了します。",
                    'answer_en': "Start a new game? The current game will end."
                }]
                
                new_context = create_20q_context_data(
                    secret=secret,
                    theme_key=theme_key,
                    question_count=question_count,
                    max_questions=max_questions,
                    waiting_for='restart',
                    game_status='playing',
                    game_result=None,
                    history_secret_list=history_secret_list
                )
                
                yield create_game_event(
                    kind=EVENT_KIND_WAITING_ANSWER,
                    message=msg,
                    think_log=think_log,
                    reply_list=reply_list,
                    context_data=new_context,
                    game_action='restart_confirm'
                )
                return 'handled'
            
            # 재시작 확정
            yield from handle_new_game(
                think_log, lang, char_name, server_type, api_key,
                None, history_secret_list, max_questions  # theme_key=None for random
            )
            return 'handled'
    
    # 계속/포기 의도 확인
    if waiting_for == 'continue_or_giveup':
        continue_intent = classify_continue_intent(
            utterance=query,
            lang=lang,
            server_type=server_type,
            api_key=api_key
        )
        
        if continue_intent == 'give_up':
            append_think_log(think_log, PHASE_CLASSIFY, '포기 의도 감지')
            
            if lang == 'ko':
                msg = f"정답은 '{secret}' 였습니다! 새로운 게임을 하시겠어요?"
            elif lang in ['ja', 'jp']:
                msg = f"答えは '{secret}' でした！新しいゲームをしますか？"
            else:
                msg = f"The answer was '{secret}'! Play a new game?"
            
            reply_list = [{
                'answer_ko': f"정답은 '{secret}' 였습니다! 새로운 게임을 하시겠어요?",
                'answer_jp': f"答えは '{secret}' でした！新しいゲームをしますか？",
                'answer_en': f"The answer was '{secret}'! Play a new game?"
            }]
            
            new_context = create_20q_context_data(
                secret=secret,
                theme_key=theme_key,
                question_count=question_count,
                max_questions=max_questions,
                waiting_for='restart',
                game_status='game_over',
                game_result='user_gave_up',
                history_secret_list=history_secret_list
            )
            
            yield create_game_event(
                kind=EVENT_KIND_GAME_OVER,
                message=msg,
                think_log=think_log,
                reply_list=reply_list,
                context_data=new_context,
                game_action='give_up',
                secret=secret
            )
            return 'handled'
        
        else:  # continue
            append_think_log(think_log, PHASE_CLASSIFY, '계속 의도 감지')
            
            if lang == 'ko':
                msg = "좋습니다! 계속 질문해주세요."
            elif lang in ['ja', 'jp']:
                msg = "良いですね！続けて質問してください。"
            else:
                msg = "Great! Keep asking questions."
            
            reply_list = [{
                'answer_ko': "좋습니다! 계속 질문해주세요.",
                'answer_jp': "良いですね！続けて質問してください。",
                'answer_en': "Great! Keep asking questions."
            }]
            
            new_context = create_20q_context_data(
                secret=secret,
                theme_key=theme_key,
                question_count=question_count,
                max_questions=max_questions,
                waiting_for=None,
                game_status='playing',
                game_result=None,
                history_secret_list=history_secret_list
            )
            
            yield create_game_event(
                kind=EVENT_KIND_WAITING_ANSWER,
                message=msg,
                think_log=think_log,
                reply_list=reply_list,
                context_data=new_context,
                game_action='continue'
            )
            return 'handled'
    
    return 'not_handled'


def handle_casual_chat(query, think_log, lang,
                       char_name, server_type, api_key,
                       secret, theme_key, question_count,
                       max_questions, history, history_secret_list,
                       game_status):
    '''일상 대화 처리'''
    append_think_log(think_log, PHASE_CHAT, '일상 대화 생성')
    
    response = generate_casual_chat(
        utterance=query,
        lang=lang,
        char_name=char_name,
        game_status=game_status,
        server_type=server_type,
        api_key=api_key
    )
    
    reply_list = [{
        'answer_ko': response if lang == 'ko' else response,
        'answer_jp': response if lang in ['ja', 'jp'] else response,
        'answer_en': response if lang == 'en' else response
    }]
    
    new_context = create_20q_context_data(
        secret=secret,
        theme_key=theme_key,
        question_count=question_count,
        max_questions=max_questions,
        waiting_for=None,
        game_status=game_status,
        game_result=None,
        history_secret_list=history_secret_list
    )
    
    yield create_game_event(
        kind=EVENT_KIND_CASUAL_CHAT,
        message=response,
        think_log=think_log,
        reply_list=reply_list,
        context_data=new_context,
        game_action='casual_chat'
    )


def handle_stop_intent(query, think_log, lang,
                       secret, theme_key, question_count,
                       max_questions, history, history_secret_list):
    '''중단 의도 처리'''
    append_think_log(think_log, PHASE_CLASSIFY, '중단 의도 감지')
    
    if lang == 'ko':
        msg = "정말 새 게임을 시작할까요? 현재 게임이 종료됩니다."
    elif lang in ['ja', 'jp']:
        msg = "本当に新しいゲームを始めますか？現在のゲームが終了します。"
    else:
        msg = "Start a new game? The current game will end."
    
    reply_list = [{
        'answer_ko': "정말 새 게임을 시작할까요? 현재 게임이 종료됩니다.",
        'answer_jp': "本当に新しいゲームを始めますか？現在のゲームが終了します。",
        'answer_en': "Start a new game? The current game will end."
    }]
    
    new_context = create_20q_context_data(
        secret=secret,
        theme_key=theme_key,
        question_count=question_count,
        max_questions=max_questions,
        waiting_for='restart',
        game_status='playing',
        game_result=None,
        history_secret_list=history_secret_list
    )
    
    yield create_game_event(
        kind=EVENT_KIND_WAITING_ANSWER,
        message=msg,
        think_log=think_log,
        reply_list=reply_list,
        context_data=new_context,
        game_action='restart_confirm'
    )


def handle_guess_intent(query, think_log, lang,
                        char_name, server_type, api_key,
                        secret, theme_key, question_count,
                        max_questions, history, history_question,
                        history_secret_list):
    '''추측 의도 처리'''
    append_think_log(think_log, PHASE_JUDGE, '추측 의도 감지')
    
    # 추측 단어 추출
    user_guess = extract_guess_from_text(
        utterance=query,
        lang=lang,
        server_type=server_type,
        api_key=api_key
    )
    
    append_think_log(think_log, PHASE_JUDGE, f'추출된 추측: {user_guess}')
    
    if not user_guess:
        # 추측을 파악하지 못함
        if lang == 'ko':
            msg = "추측을 명확히 파악하지 못했어요. 다시 말씀해주시겠어요?"
        elif lang in ['ja', 'jp']:
            msg = "推測をはっきり把握できませんでした。もう一度お願いします。"
        else:
            msg = "I couldn't understand your guess clearly. Could you say it again?"
        
        reply_list = [{
            'answer_ko': "추측을 명확히 파악하지 못했어요. 다시 말씀해주시겠어요?",
            'answer_jp': "推測をはっきり把握できませんでした。もう一度お願いします。",
            'answer_en': "I couldn't understand your guess clearly. Could you say it again?"
        }]
        
        new_context = create_20q_context_data(
            secret=secret,
            theme_key=theme_key,
            question_count=question_count,
            max_questions=max_questions,
            waiting_for=None,
            game_status='playing',
            game_result=None,
            history_secret_list=history_secret_list
        )
        
        yield create_game_event(
            kind=EVENT_KIND_GUIDE_QUESTION,
            message=msg,
            think_log=think_log,
            reply_list=reply_list,
            context_data=new_context,
            game_action='guess_unclear'
        )
        return
    
    # 정답 판정
    verdict = judge_guess_correctness(
        guess=user_guess,
        secret=secret,
        lang=lang,
        server_type=server_type,
        api_key=api_key
    )
    
    append_think_log(think_log, PHASE_JUDGE, f'판정 결과: {verdict}')
    
    if verdict == 'yes':
        # 정답!
        if lang == 'ko':
            msg = f"정답입니다! '{user_guess}' 맞아요! {question_count}번째 질문만에 성공하셨어요! 새로운 게임을 하시겠어요?"
        elif lang in ['ja', 'jp']:
            msg = f"正解です！'{user_guess}'です！{question_count}問目で正解しました！新しいゲームをしますか？"
        else:
            msg = f"Correct! It's '{user_guess}'! You got it in {question_count} questions! Play a new game?"
        
        reply_list = [{
            'answer_ko': f"정답입니다! '{user_guess}' 맞아요! {question_count}번째 질문만에 성공하셨어요! 새로운 게임을 하시겠어요?",
            'answer_jp': f"正解です！'{user_guess}'です！{question_count}問目で正解しました！新しいゲームをしますか？",
            'answer_en': f"Correct! It's '{user_guess}'! You got it in {question_count} questions! Play a new game?"
        }]
        
        new_context = create_20q_context_data(
            secret=secret,
            theme_key=theme_key,
            question_count=question_count,
            max_questions=max_questions,
            waiting_for='restart',
            game_status='game_over',
            game_result='user_won',
            history_secret_list=history_secret_list
        )
        
        yield create_game_event(
            kind=EVENT_KIND_GUESS_RESULT,
            message=msg,
            think_log=think_log,
            reply_list=reply_list,
            context_data=new_context,
            game_action='user_guess',
            user_guess=user_guess,
            verdict='correct'
        )
    else:
        # 오답
        if lang == 'ko':
            msg = f"'{user_guess}'는 틀렸어요. 계속 질문하시겠어요, 아니면 정답을 알려드릴까요?"
        elif lang in ['ja', 'jp']:
            msg = f"'{user_guess}'は違います。続けますか、それとも答えを教えましょうか？"
        else:
            msg = f"'{user_guess}' is wrong. Continue or give up?"
        
        reply_list = [{
            'answer_ko': f"'{user_guess}'는 틀렸어요. 계속 질문하시겠어요, 아니면 정답을 알려드릴까요?",
            'answer_jp': f"'{user_guess}'は違います。続けますか、それとも答えを教えましょうか？",
            'answer_en': f"'{user_guess}' is wrong. Continue or give up?"
        }]
        
        new_context = create_20q_context_data(
            secret=secret,
            theme_key=theme_key,
            question_count=question_count,
            max_questions=max_questions,
            waiting_for='continue_or_giveup',
            game_status='playing',
            game_result=None,
            history_secret_list=history_secret_list
        )
        
        yield create_game_event(
            kind=EVENT_KIND_GUESS_RESULT,
            message=msg,
            think_log=think_log,
            reply_list=reply_list,
            context_data=new_context,
            game_action='user_guess',
            user_guess=user_guess,
            verdict='wrong'
        )


def handle_guide_question(query, think_log, lang,
                          char_name, server_type, api_key,
                          secret, theme_key, question_count,
                          max_questions, history, history_question,
                          history_secret_list):
    '''규칙 안내 처리'''
    append_think_log(think_log, PHASE_CHAT, '규칙 안내 생성')
    
    if lang == 'ko':
        msg = "스무고개 질문으로 인식되지 않았어요. 예/아니오로 답변 가능한 질문을 해주세요."
    elif lang in ['ja', 'jp']:
        msg = "質問として認識されませんでした。はい/いいえで答えられる質問をしてください。"
    else:
        msg = "Not recognized as a valid question. Please ask a yes/no question."
    
    reply_list = [{
        'answer_ko': "스무고개 질문으로 인식되지 않았어요. 예/아니오로 답변 가능한 질문을 해주세요.",
        'answer_jp': "質問として認識されませんでした。はい/いいえで答えられる質問をしてください。",
        'answer_en': "Not recognized as a valid question. Please ask a yes/no question."
    }]
    
    new_context = create_20q_context_data(
        secret=secret,
        theme_key=theme_key,
        question_count=question_count,
        max_questions=max_questions,
        waiting_for=None,
        game_status='playing',
        game_result=None,
        history_secret_list=history_secret_list
    )
    
    yield create_game_event(
        kind=EVENT_KIND_GUIDE_QUESTION,
        message=msg,
        think_log=think_log,
        reply_list=reply_list,
        context_data=new_context,
        game_action='guide_question'
    )


def handle_valid_question(query, think_log, lang,
                          char_name, server_type, api_key,
                          secret, theme_key, question_count,
                          max_questions, history, history_question,
                          history_secret_list):
    '''유효한 질문 처리'''
    append_think_log(think_log, PHASE_ANSWER, f'질문 처리: "{query[:30]}..."')
    
    # 답변 생성
    answer = generate_answer(
        question=query,
        secret=secret,
        lang=lang,
        char_name=char_name,
        server_type=server_type,
        api_key=api_key
    )
    
    # 질문 카운트 증가
    question_count += 1
    
    append_think_log(think_log, PHASE_ANSWER, f'답변: {answer}, 질문 수: {question_count}/{max_questions}')
    
    reply_list = [{
        'answer_ko': answer if lang == 'ko' else answer,
        'answer_jp': answer if lang in ['ja', 'jp'] else answer,
        'answer_en': answer if lang == 'en' else answer
    }]
    
    # 한도 도달 확인
    if question_count >= max_questions:
        append_think_log(think_log, PHASE_ANSWER, '질문 한도 도달')
        
        if lang == 'ko':
            limit_msg = f"\n\n질문 한도에 도달했어요! 정답은 '{secret}' 였습니다. 새로운 게임을 하시겠어요?"
        elif lang in ['ja', 'jp']:
            limit_msg = f"\n\n質問上限に達しました！答えは '{secret}' でした。新しいゲームをしますか？"
        else:
            limit_msg = f"\n\nMax questions reached! The answer was '{secret}'. Play a new game?"
        
        full_msg = answer + limit_msg
        
        reply_list = [{
            'answer_ko': (answer if lang == 'ko' else answer) + f"\n\n질문 한도에 도달했어요! 정답은 '{secret}' 였습니다. 새로운 게임을 하시겠어요?",
            'answer_jp': (answer if lang in ['ja', 'jp'] else answer) + f"\n\n質問上限に達しました！答えは '{secret}' でした。新しいゲームをしますか？",
            'answer_en': (answer if lang == 'en' else answer) + f"\n\nMax questions reached! The answer was '{secret}'. Play a new game?"
        }]
        
        new_context = create_20q_context_data(
            secret=secret,
            theme_key=theme_key,
            question_count=question_count,
            max_questions=max_questions,
            waiting_for='restart',
            game_status='game_over',
            game_result='max_reached',
            history_secret_list=history_secret_list
        )
        
        yield create_game_event(
            kind=EVENT_KIND_GAME_OVER,
            message=full_msg,
            think_log=think_log,
            reply_list=reply_list,
            context_data=new_context,
            game_action='max_reached',
            secret=secret
        )
        return
    
    # 일반 답변
    new_context = create_20q_context_data(
        secret=secret,
        theme_key=theme_key,
        question_count=question_count,
        max_questions=max_questions,
        waiting_for=None,
        game_status='playing',
        game_result=None,
        history_secret_list=history_secret_list
    )
    
    yield create_game_event(
        kind=EVENT_KIND_GAME_ANSWER,
        message=answer,
        think_log=think_log,
        reply_list=reply_list,
        context_data=new_context,
        game_action='answer',
        question_count=question_count,
        max_questions=max_questions
    )


if __name__ == '__main__':
    print('=== ai_20q_game 테스트 ===')
    print('실제 테스트는 서버에서 실행하세요.')
    
    # 기본 구조 테스트
    print('\n--- 새 게임 시작 테스트 ---')
    for event in game_run_loop(query='', context_data=None, lang='ko'):
        print(f'Event: {event.kind} - {event.message[:50]}...')
        break
