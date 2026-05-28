# Multi-Character Conversation System
# 다중 참여자 대화 시스템 - ai_conversation_binary.py 기반 확장

import json
import os
import re
import gc
from typing import List, Dict, Generator, Tuple, Optional, Any
from threading import Thread, Lock
import traceback
from queue import Queue
from datetime import datetime

# Local imports
import util_string
import memory
import state
from ai_singleton import check_llm, get_llm
from prompt_llm import get_qwen_prompt
import prompt_main
import prompt_char
import ai_aropla_flow  # Flow 결정을 위한 AI Agent
import util_proper_nouns  # 고유명사 변환

# Global variables
generation_lock = Lock()

def load_model(is_use_cuda=False):
    """LLM 모델 로딩 - ai_conversation_binary.py와 동일"""
    get_llm()

def get_next_speaker_with_agent(
    query: str, 
    current_speaker: str, 
    participants: List[Dict],
    memory_list: List[Dict] = None,
    context: Dict = None,
    lang: str = 'en'
) -> Tuple[str, str]:
    """AI Agent를 이용한 다음 발화자 결정 - ai_aropla_flow.py 활용"""
    
    import time
    total_start = time.time()
    print(f"\n[Speaker Agent] === 발화자 결정 프로세스 시작 ===")
    print(f"[Speaker Agent] 입력: '{query[:50]}...', 현재: {current_speaker} ({lang})")
    
    # AI 모델 로딩 확인
    model_start = time.time()
    if not ai_aropla_flow.llm:
        print(f"[Speaker Agent] AI 모델 로딩 중...")
        ai_aropla_flow.load_model(is_use_cuda=True)
    model_time = time.time() - model_start
    print(f"[Speaker Agent] AI 모델 준비 완료 ({model_time:.2f}s)")
    
    # 1단계: 메시지에서 명시적 타겟 분석
    print(f"\n[Speaker Agent] 1단계: 명시적 타겟 분석")
    stage1_start = time.time()
    target_speaker, reason = ai_aropla_flow.analyze_target_speaker_from_message(
        query, current_speaker, lang
    )
    stage1_time = time.time() - stage1_start
    print(f"[Speaker Agent] 1단계 완료 ({stage1_time:.2f}s): {target_speaker}")
    
    if target_speaker and target_speaker != "both":
        # 명시적 타겟이 있으면 해당 캐릭터 선택
        total_time = time.time() - total_start
        print(f"[Speaker Agent] 명시적 타겟 발견! 총 소요시간: {total_time:.2f}s")
        return target_speaker, f"명시적 타겟: {reason}"
    
    # 2단계: 일반적인 대화 흐름 결정
    print(f"\n[Speaker Agent] 2단계: 대화 흐름 분석")
    stage2_start = time.time()
    memory_list = memory_list or []
    context_str = context.get("description", "") if context else ""
    
    next_speaker, flow_reason = ai_aropla_flow.process_flow_decision(
        memory_list, context_str, current_speaker, lang
    )
    stage2_time = time.time() - stage2_start
    print(f"[Speaker Agent] 2단계 완료 ({stage2_time:.2f}s): {next_speaker}")
    
    total_time = time.time() - total_start
    print(f"[Speaker Agent] === 발화자 결정 완료 === (총 {total_time:.2f}s)")
    print(f"[Speaker Agent] 최종 결과: {current_speaker} → {next_speaker}")
    
    return next_speaker, f"흐름 결정: {flow_reason}"

def get_qwen_multi_prompt(
    query: str,
    current_speaker: str = None,
    target_speaker: str = None, 
    target_listener: str = "all",
    participants: List[Dict] = None,
    context: Dict = None,
    info_img: str = None,
    memory_list: List[Dict] = None,
    lang: str = 'en',
    guideline_list: List = None,
    situation_dict: Dict = None,
    player_name: str = 'sensei'):
    
    # 기본값 설정
    participants = participants or []
    context = context or {}
    memory_list = memory_list or []
    guideline_list = guideline_list or []
    situation_dict = situation_dict or {}
    
    # 타겟 캐릭터 및 현재 발화자 찾기
    target_participant = None
    if target_speaker:
        target_participant = next((p for p in participants if p["name"] == target_speaker), None)
    
    current_participant = None
    if current_speaker:
        current_participant = next((p for p in participants if p["name"] == current_speaker), None)
    
    print(f"###target_participant: {target_participant}")
    print(f"###current_participant: {current_participant}")
    
    # 단일 캐릭터인 경우 기존 함수 사용 (참여자 2명 이하만 체크)
    if len(participants) <= 2:
        return get_qwen_prompt(
            query, 
            player_name=current_speaker, 
            char_name=target_speaker,
            info_img=info_img,
            memory_list=memory_list,
            lang=lang,
            guideline_list=guideline_list,
            situation_dict=situation_dict
        )
    
    # 다중 캐릭터 프롬프트 생성
    def add_chatLM_prompt(speaker_type, text):
        return f"<|im_start|>{speaker_type}\n{text}<|im_end|>"
    
    messages = []
    
    # 1. 기본 시스템 프롬프트 (다중 캐릭터용, 한영일 대응)
    system_content = build_multi_character_system_prompt(
        target_speaker, participants, lang, context, situation_dict, target_listener
    )
    messages.append({"role": "system", "content": system_content})
    
    # 2. 캐릭터 프로필 (타겟 캐릭터)
    if target_participant and target_participant.get("character_file"):
        char_profile = prompt_char.get_char_info_from_json(
            target_participant["character_file"], lang
        )
        if char_profile:
            profile_label = {"ko": "## 답변 캐릭터 프로필", "ja": "## 回答キャラクタープロフィール", "jp": "## 回答キャラクタープロフィール"}.get(lang, "## Responding Character Profile")
            messages.append({"role": "system", "content": f"{profile_label}\n{char_profile}"})
    
    # 3. 유저 프로필 (prompt_main.py의 get_persona_player 참조)
    user_participant = next((p for p in participants if p.get("type") == "user"), None)
    if user_participant:
        user_name = user_participant.get("name", "")
        user_display_name = user_participant.get("display_name", user_name)
        
        # 이름이 있으면 player_name 템플릿 사용, 없으면 일반 sensei 프로필 사용
        if user_name and user_name != "sensei":
            user_profile = prompt_char.get_char_info_from_json('kivotos_sensei_player_name', lang)
            if user_profile:
                user_profile = user_profile.replace('{player_name}', user_display_name)
        else:
            user_profile = prompt_char.get_char_info_from_json('kivotos_sensei', lang)
        
        if user_profile:
            user_label = {"ko": "## 사용자 프로필", "ja": "## ユーザープロフィール", "jp": "## ユーザープロフィール"}.get(lang, "## User Profile")
            messages.append({"role": "system", "content": f"{user_label}\n{user_profile}"})
    
    # 4. 참여자 관계 정보 (한영일 대응)
    participants_info = build_participants_info(target_speaker, participants, lang)
    if participants_info:
        messages.append({"role": "system", "content": participants_info})
    
    # 5. 사용자 가이드라인 (prompt_main.py 방식 적용 - 강력한 규칙)
    if guideline_list:
        if lang == 'ko':
            header = (
                "## 🚨 대화 지침 (절대 준수 사항) 🚨\n"
                "다음은 사용자의 피드백과 선호도를 기반으로 한 **절대적으로 준수해야 할 규칙**입니다.\n"
                "이 지침은 모든 발화에서 **100% 일관되게** 유지되어야 하며, 절대로 무시하거나 누락할 수 없습니다.\n"
                "**규칙 위반 시 응답이 거부될 수 있습니다.**\n\n"
                "⚠️ **절대 준수 규칙** ⚠️\n"
            )
        elif lang in ['ja', 'jp']:
            header = (
                "## 🚨 会話ガイドライン（絶対遵守事項）🚨\n"
                "以下はユーザーのフィードバックや好みに基づく、**絶対に守らなければならない規則**です。\n"
                "すべての発言で**100%一貫して**遵守し、絶対に省略・無視してはいけません。\n"
                "**規則違反時は応答が拒否される場合があります。**\n\n"
                "⚠️ **絶対遵守規則** ⚠️\n"
            )
        else:
            header = (
                "## 🚨 Conversation Guidelines (ABSOLUTE COMPLIANCE REQUIRED) 🚨\n"
                "The following rules are based on user preferences and feedback, and must be **ABSOLUTELY FOLLOWED**.\n"
                "You must apply these in **EVERY SINGLE RESPONSE** with **100% consistency**, without any exceptions or omissions.\n"
                "**Response may be rejected for rule violations.**\n\n"
                "⚠️ **MANDATORY RULES** ⚠️\n"
            )
        
        body = ""
        for idx, rule in enumerate(guideline_list, 1):
            body += f"{idx}. ⚠️ {rule.strip()}\n"
        
        full_guideline = header + body
        messages.append({"role": "system", "content": full_guideline})
    
    # 6. 이미지 정보 (info_img)
    if info_img:
        img_label = {"ko": "## 이미지 정보", "ja": "## 画像情報", "jp": "## 画像情報"}.get(lang, "## Image Information")
        messages.append({"role": "system", "content": f"{img_label}\n{info_img}"})
    
    # 7. 메모리 시스템 (기존 대화 기록)
    # {
    # "speaker": "캐릭터이름",     // sensei, arona, plana, system
    # "role": "역할",            // user, assistant, system
    # "message": "대표메시지",    // UI 언어에 따른 메시지
    # "messageKo": "한국어 메시지",
    # "messageJp": "일본어 메시지", 
    # "messageEn": "영어 메시지",
    # "timestamp": "2024-XX-XX XX:XX:XX"
    # }
    if memory_list:
        for m in memory_list:
            # 언어별 메시지 선택 로직
            selected_message = ""
            if lang == 'ko':
                selected_message = m.get('messageKo') or m.get('message', '')
            elif lang in ['ja', 'jp']:
                selected_message = m.get('messageJp') or m.get('message', '')
            elif lang == 'en':
                selected_message = m.get('messageEn') or m.get('message', '')
            else:
                selected_message = m.get('message', '')  # fallback
            
            if selected_message:  # 빈 메시지는 스킵
                speaker = m.get('speaker', 'unknown')
                # participants에서 display_name 찾기 (언어 대응)
                display_name = speaker
                participant = next((p for p in participants if p["name"] == speaker), None)
                if participant and participant.get("display_name"):
                    display_name = participant["display_name"]
                
                # 발언자 정보를 포함한 formatted_message 생성
                formatted_message = f"[{display_name}]: {selected_message}"
                messages.append({"role": m.get('role', 'assistant'), "content": formatted_message})                
    else:
        # 기본 메모리 시스템 활용
        messages.extend(memory.get_memory_message_list(8192, lang=lang))
    
    # 7. 현재 쿼리 추가 - current_speaker가 user 타입일 때만
    if current_speaker and current_speaker != target_speaker:
        # current_speaker가 user 타입인 경우에만 쿼리 추가
        if current_participant and current_participant.get("type") == "user":
            # display_name 사용
            current_display_name = current_speaker
            if current_participant.get("display_name"):
                current_display_name = current_participant["display_name"]
            
            formatted_query = f"[{current_display_name}]: {query} /no_think"
            messages.append({"role": "user", "content": formatted_query})
    
    # 8. 프롬프트 조합
    prompt = ''
    for message in messages:
        if message['role'] in ('system', 'user', 'assistant'):
            prompt += add_chatLM_prompt(message['role'], message['content']) + "\n"
    
    # target_speaker의 display_name으로 응답 시작
    target_display_name = target_speaker
    if target_participant and target_participant.get("display_name"):
        target_display_name = target_participant["display_name"]
    
    # prompt += f'<|im_start|>assistant\n[{target_display_name}]: '
    prompt += f'<|im_start|>assistant\n'
    
    print('### prompt', prompt)
    
    return prompt

def build_multi_character_system_prompt(
    target_speaker: str, 
    participants: List[Dict], 
    lang: str = 'en',
    context: Dict = None,
    situation_dict: Dict = None,
    target_listener: str = "all"
) -> str:
    """다중 캐릭터용 시스템 프롬프트 생성 (관계별 말투 적용)"""
    
    target_participant = next((p for p in participants if p["name"] == target_speaker), None)
    if not target_participant:
        # target_speaker를 찾지 못해도 기본 다중 참여자 프롬프트 생성
        target_participant = {"name": target_speaker or "unknown", "display_name": target_speaker or "Unknown"}
    
    context = context or {}
    situation_dict = situation_dict or {}
    
    if lang == 'ko':
        system_prompt = f"""# 다중 참여자 대화 시스템

## 핵심 정체성  
당신은 **{target_participant.get('display_name', target_speaker)}**입니다.
- 다른 사람을 칭할 때는 그들의 이름을 사용하세요
- 자신을 지칭할 때는 "나", "저"를 사용하세요 (절대 자신의 이름을 3인칭으로 사용하지 마세요)"""
        
        # 상황 설정 추가
        if situation_dict:
            system_prompt += f"\n\n## 현재 상황"
            for key, value in situation_dict.items():
                system_prompt += f"\n- {key}: {value}"
        
        system_prompt += f"\n\n## 참여자 정보"
        for participant in participants:
            role_desc = "사용자" if participant.get('type') == 'user' else "AI 캐릭터"
            if participant['name'] == target_speaker:
                system_prompt += f"\n- **{participant.get('display_name', participant['name'])}**: 바로 당신입니다"
            else:
                system_prompt += f"\n- {participant.get('display_name', participant['name'])}: {role_desc}"
        
        # 관계별 말투 결정 로직 추가
        listener_info = ""
        speech_style = ""
        
        if target_listener == "sensei":
            # 선생님에게 말할 때: 존댓말
            listener_info = "🎯 **대화 대상**: 선생님에게 개별적으로 말하고 있습니다"
            speech_style = """✅ **존댓말 필수**: "~요", "~습니다", "~세요" 등 존댓말 사용
✅ **정중한 표현**: "안녕하세요", "말씀해주세요", "도와드리겠습니다" 등"""
        elif target_listener in ["arona", "plana"]:
            # AI끼리 말할 때: 친근한 존댓말
            listener_info = f"🎯 **대화 대상**: {target_listener}에게 개별적으로 말하고 있습니다 (AI끼리 친한 관계)"
            speech_style = f"""✅ **친근한 존댓말**: "{target_listener}"에게는 편안하고 자연스러운 존댓말 사용
✅ **부드러운 표현**: "그렇네요", "좋아요", "어떻게 생각하세요?" 등 친근한 존댓말
✅ **자연스러운 어조**: "~네요", "~죠", "~해요" 등으로 편안하게 대화"""
        else:
            # 전체 대화 (all): 선생님 포함이므로 존댓말
            listener_info = "🎯 **대화 대상**: 전체 참여자에게 말하고 있습니다 (선생님 포함)"
            speech_style = """✅ **존댓말 필수**: 선생님이 들으므로 "~요", "~습니다", "~세요" 등 존댓말 사용
✅ **정중한 표현**: "안녕하세요", "말씀해주세요", "도와드리겠습니다" 등"""

        system_prompt += f"""

## 중요한 대화 규칙
1. **정체성 유지**: 당신은 {target_participant.get('display_name', target_speaker)}입니다
2. **1인칭 사용**: 자신을 "나", "저"로 지칭하세요
3. **상대방 인식**: 대화 상대를 정확한 이름으로 부르세요
4. **연속성 유지**: 이전 대화 맥락을 이어가세요
5. **캐릭터 일관성**: {target_participant.get('display_name', target_speaker)}의 성격을 유지하세요
6. **중복 방지**: 이전에 말한 내용을 그대로 반복하지 마세요

{listener_info}

## 관계별 말투 규칙
{speech_style}
✅ **캐릭터별 특성 반영**: 
   - 아로나: 밝고 활발한 성격 유지
   - 프라나: 차분하고 신중한 성격 유지
   - 기타 캐릭터: 해당 캐릭터 설정에 맞는 성격 유지

## 🚨 절대 금지 사항 (STRICTLY FORBIDDEN) 🚨
**다음 항목은 절대적으로 금지되며, 어떤 상황에서도 사용해서는 안 됩니다:**

🚫 **인터넷 슬랭/줄임말 ZERO TOLERANCE**: 
   - "ㅎㅇ", "ㅇㅋ", "ㅋㅋ", "ㄱㄱ", "ㅎㅎ", "ㄷㄷ", "ㅠㅠ", "ㅜㅜ", "ㅅㄱ" 등
   - "어", "음", "엌", "앗", "아", "오", "우와", "헉", "엥" 등 의성어/감탄사
   - "그럼", "뭐임", "뭔데", "왜냐", "그냥", "걍", "쫌", "좀", "막" 등 축약어

🚫 **캐주얼 표현 완전 금지**:
   - 반말 사용 (선생님께 절대 금지)
   - "야", "너", "니", "걔", "얘" 등 격식 없는 지칭
   - "~함", "~임", "~지", "~네" 등 반말 어미

🚫 **기타 절대 금지**:
   - 자신의 이름을 3인칭으로 사용 (예: "아로나가", "프라나가")
   - 다른 캐릭터의 대화 대신 작성
   - 동일한 내용 반복
   - 나레이션이나 상황 설명

⚠️ **위반 시 즉시 응답 중단 및 재생성 요구됩니다**

## ✅ 필수 응답 형식
1. **완전한 표준어 사용**: 모든 단어와 표현을 표준 한국어로 작성
2. **정중한 존댓말**: 선생님께는 "~습니다", "~세요", "~께서" 등 완전한 존댓말만 사용
3. **캐릭터 일관성**: {target_participant.get('display_name', target_speaker)}의 성격 설정을 100% 준수
4. **자연스러운 대화**: 위 규칙을 지키면서도 자연스럽고 매력적인 캐릭터 표현

⚠️ **이 모든 규칙은 예외 없이 모든 응답에 적용됩니다**"""

    elif lang in ['ja', 'jp']:
        system_prompt = f"""# マルチキャラクター会話システム

## 核心的アイデンティティ
あなたは**{target_participant.get('display_name', target_speaker)}**です。
- 他の人を呼ぶときは、その人の名前を使ってください
- 自分を指すときは「私」「僕」「俺」を使ってください（絶対に自分の名前を三人称で使わないでください）"""
        
        # 상황 설정 추가
        if situation_dict:
            system_prompt += f"\n\n## 現在の状況"
            for key, value in situation_dict.items():
                system_prompt += f"\n- {key}: {value}"
        
        system_prompt += f"\n\n## 参加者情報"
        for participant in participants:
            role_desc = "ユーザー" if participant.get('type') == 'user' else "AIキャラクター"
            if participant['name'] == target_speaker:
                system_prompt += f"\n- **{participant.get('display_name', participant['name'])}**: まさにあなたです"
            else:
                system_prompt += f"\n- {participant.get('display_name', participant['name'])}: {role_desc}"
        
        # 일본어 관계별 말투 결정 로직
        if target_listener == "sensei":
            listener_info_jp = "🎯 **会話対象**: 先生に個別的に話しています"
            speech_style_jp = """✅ **敬語必須**: 「です」「ます」「ください」等の敬語使用
✅ **丁寧な表現**: 「おはようございます」「教えてください」「お手伝いします」等"""
        elif target_listener in ["arona", "plana"]:
            listener_info_jp = f"🎯 **会話対象**: {target_listener}に個別的に話しています (AI同士の親しい関係)"
            speech_style_jp = f"""✅ **親しい敬語**: "{target_listener}"には自然で親しみやすい敬語を使用
✅ **柔らかい表現**: 「そうですね」「いいですね」「どう思いますか？」等の親しい敬語
✅ **自然な語調**: 「〜ですね」「〜ましょう」「〜ですよ」等で親しく会話"""
        else:
            listener_info_jp = "🎯 **会話対象**: 全体参加者に話しています (先生含む)"
            speech_style_jp = """✅ **敬語必須**: 先生が聞くので「です」「ます」「ください」等の敬語使用
✅ **丁寧な表現**: 「おはようございます」「教えてください」「お手伝いします」等"""

        system_prompt += f"""

## 重要な会話ルール
1. **アイデンティティ維持**: あなたは{target_participant.get('display_name', target_speaker)}です
2. **一人称使用**: 自分を「私」「僕」「俺」で指してください
3. **相手認識**: 会話相手を正確な名前で呼んでください
4. **連続性維持**: 前の会話の文脈を続けてください
5. **キャラクター一貫性**: {target_participant.get('display_name', target_speaker)}の性格を維持してください
6. **重複防止**: 前に言った内容をそのまま繰り返さないでください

{listener_info_jp}

## 関係別話し方ルール
{speech_style_jp}
✅ **キャラクター別特性反映**:
   - アロナ: 明るく活発な性格維持
   - プラナ: 落ち着いて慎重な性格維持
   - その他キャラクター: 該当キャラクター設定に合う性格維持

## 🚨 絶対禁止事項 (STRICTLY FORBIDDEN) 🚨
**以下の項目は絶対的に禁止されており、どのような状況でも使用してはいけません:**

🚫 **インターネットスラング/略語 ZERO TOLERANCE**: 
   - "w", "草", "ｗｗｗ", "orz", "ktkr", "wktk", "gkbr", "mjd" 等
   - "あ", "え", "お", "う", "わ", "へー", "ほー", "はー" 等の感嘆詞
   - "てか", "とか", "～じゃん", "～だし", "～けど" 等のカジュアル表現

🚫 **カジュアル表現完全禁止**:
   - タメ口使用 (先生に対して絶対禁止)
   - "お前", "君", "あんた", "そっち", "こっち" 等の親しい呼び方
   - "～だ", "～である", "～じゃん", "～かな" 等のタメ口語尾

🚫 **その他絶対禁止**:
   - 自分の名前を三人称で使用（例：「アロナが」「プラナが」）
   - 他のキャラクターの会話を代わりに作成
   - 同じ内容の繰り返し
   - ナレーションや状況説明

⚠️ **違反時は即座に応答停止・再生成要求されます**

## ✅ 必須応答形式
1. **完全な標準日本語使用**: すべての単語と表現を標準的な日本語で作成
2. **丁寧な敬語**: 先生には「～です」「～ます」「～ください」等の完全な敬語のみ使用
3. **キャラクター一貫性**: {target_participant.get('display_name', target_speaker)}の性格設定を100%遵守
4. **自然な会話**: 上記規則を守りながらも自然で魅力的なキャラクター表現

⚠️ **これらすべての規則は例外なくすべての応答に適用されます**"""

    else:  # English
        system_prompt = f"""# Multi-Character Conversation System

## Core Identity
You are **{target_participant.get('display_name', target_speaker)}**.
- When referring to others, use their names
- When referring to yourself, use "I" or "me" (never use your own name in third person)"""
        
        # 상황 설정 추가
        if situation_dict:
            system_prompt += f"\n\n## Current Situation"
            for key, value in situation_dict.items():
                system_prompt += f"\n- {key}: {value}"
        
        system_prompt += f"\n\n## Participants"
        for participant in participants:
            role_desc = "User" if participant.get('type') == 'user' else "AI Character"
            if participant['name'] == target_speaker:
                system_prompt += f"\n- **{participant.get('display_name', participant['name'])}**: This is you"
            else:
                system_prompt += f"\n- {participant.get('display_name', participant['name'])}: {role_desc}"
        
        # 영어 관계별 말투 결정 로직
        if target_listener == "sensei":
            listener_info_en = "🎯 **Conversation Target**: You are speaking individually to Sensei"
            speech_style_en = """✅ **Formal Language Required**: Use polite and respectful language like "Good morning", "Please tell me", "I'll help you"
✅ **Respectful Tone**: Always maintain formal and courteous expressions"""
        elif target_listener in ["arona", "plana"]:
            listener_info_en = f"🎯 **Conversation Target**: You are speaking individually to {target_listener} (friendly AI relationship)"
            speech_style_en = f"""✅ **Friendly Polite Tone**: Speak to "{target_listener}" with warm and natural politeness
✅ **Gentle Expressions**: Use friendly but polite expressions like "I think so", "That sounds good", "What do you think?"
✅ **Natural Courtesy**: Use conversational but respectful tones to maintain friendliness while staying polite"""
        else:
            listener_info_en = "🎯 **Conversation Target**: You are speaking to all participants (including Sensei)"
            speech_style_en = """✅ **Formal Language Required**: Since Sensei is listening, use polite and respectful language
✅ **Respectful Expressions**: Use formal expressions like "Good morning", "Please tell me", "I'll help you" """

        system_prompt += f"""

## Important Conversation Rules
1. **Identity Maintenance**: You are {target_participant.get('display_name', target_speaker)}
2. **First Person Usage**: Refer to yourself as "I" or "me"
3. **Partner Recognition**: Address conversation partners by their correct names
4. **Continuity Maintenance**: Continue the previous conversation context
5. **Character Consistency**: Maintain {target_participant.get('display_name', target_speaker)}'s personality
6. **Avoid Duplication**: Don't repeat exactly what was said before

{listener_info_en}

## Relationship-Based Speech Rules
{speech_style_en}
✅ **Character-Specific Traits**:
   - Arona: Maintain bright and energetic personality
   - Plana: Maintain calm and thoughtful personality
   - Other characters: Maintain personality matching their character settings

## 🚨 ABSOLUTELY PROHIBITED (STRICTLY FORBIDDEN) 🚨
**The following items are absolutely forbidden and must never be used under any circumstances:**

🚫 **Internet Slang/Abbreviations ZERO TOLERANCE**: 
   - "lol", "lmao", "brb", "omg", "wtf", "tbh", "ngl", "irl", "afk", etc.
   - "uh", "um", "ah", "oh", "wow", "whoa", "huh", "meh", "nah", etc.
   - Casual contractions: "gonna", "wanna", "gotta", "kinda", "sorta", etc.

🚫 **Casual Language COMPLETELY BANNED**:
   - Informal speech with Sensei (absolutely forbidden)
   - "hey", "yo", "dude", "man", "buddy", "pal" etc.
   - Casual sentence endings without proper respect

🚫 **Other ABSOLUTE PROHIBITIONS**:
   - Using your own name in third person (e.g., "Arona says", "Plana thinks")
   - Writing dialogue for other characters
   - Repeating identical content
   - Adding narration or situation descriptions

⚠️ **VIOLATION RESULTS IN IMMEDIATE RESPONSE TERMINATION AND REGENERATION**

## ✅ MANDATORY Response Format
1. **Complete Standard English**: Use proper, formal English in all words and expressions
2. **Respectful Language**: Always use respectful, polite language with Sensei
3. **Character Consistency**: Maintain {target_participant.get('display_name', target_speaker)}'s personality 100%
4. **Natural Conversation**: Express character naturally while following all above rules

⚠️ **ALL THESE RULES APPLY TO EVERY SINGLE RESPONSE WITHOUT EXCEPTION**"""
    
    return system_prompt

def build_participants_info(target_speaker: str, participants: List[Dict], lang: str = 'en') -> str:
    """참여자 관계 정보 생성 - 한영일 3개 국어 대응"""
    target_participant = next((p for p in participants if p["name"] == target_speaker), None)
    if not target_participant or len(participants) <= 2:
        return ""
    
    other_participants = [p for p in participants if p["name"] != target_speaker]
    
    if lang == 'ko':
        info = "## 다른 참여자들과의 관계"
        for participant in other_participants:
            info += f"\n- {participant.get('display_name', participant['name'])}: "
            info += f"{participant.get('type', 'unknown')} 역할"
            if participant.get('relationships'):
                info += f" ({participant['relationships'].get(target_speaker, '동료')})"
    
    elif lang in ['ja', 'jp']:
        info = "## 他の参加者との関係"
        for participant in other_participants:
            info += f"\n- {participant.get('display_name', participant['name'])}: "
            info += f"{participant.get('type', 'unknown')} 役割"
            if participant.get('relationships'):
                info += f" ({participant['relationships'].get(target_speaker, '同僚')})"
    
    else:  # English
        info = "## Relationships with Other Participants"
        for participant in other_participants:
            info += f"\n- {participant.get('display_name', participant['name'])}: "
            info += f"{participant.get('type', 'unknown')} role"
            if participant.get('relationships'):
                info += f" ({participant['relationships'].get(target_speaker, 'colleague')})"
    
    return info

def process_multi_stream(
    query: str,
    current_speaker: str,
    target_speaker: str = None,
    target_listener: str = "all",
    participants: List[Dict] = None,
    context: Dict = None,
    is_sentence: bool = True,
    is_regenerate: bool = False,
    info_img: str = None,
    memory_list: List[Dict] = None,
    lang: str = 'en',
    guideline_list: List = None,
    situation_dict: Dict = None,
    player_name: str = 'sensei',
    **kwargs
) -> Generator[Tuple[List[str], str], None, None]:
    """다중 참여자 대화 스트림 처리 - ai_conversation_binary.py의 process_stream 확장"""
    
    llm = get_llm()
    
    # 매개변수 기본값 설정
    participants = participants or []
    context = context or {}
    memory_list = memory_list or []
    guideline_list = guideline_list or []
    situation_dict = situation_dict or {}
    
    # 1단계: Generate 전 고유명사 변환 (사용자 입력 전처리)
    processed_query = util_proper_nouns.apply_proper_nouns(lang, query)
    if processed_query != query:
        print(f"[고유명사 변환] 있음")
    
    # target_speaker가 없으면 AI Agent로 결정
    if not target_speaker and len(participants) > 2:
        try:
            target_speaker, reason = get_next_speaker_with_agent(
                processed_query, current_speaker, participants, memory_list, context, lang
            )
            print(f"[AI Agent 결정] 다음 발화자: {target_speaker} - {reason}")
        except Exception as e:
            print(f"[AI Agent 오류] {e} - 기본값 사용")
            ai_participants = [p for p in participants if p.get("type") == "ai"]
            target_speaker = ai_participants[0]["name"] if ai_participants else "arona"
    
    # 프롬프트 생성
    try:
        prompt = get_qwen_multi_prompt(
            query=processed_query,
            current_speaker=current_speaker,
            target_speaker=target_speaker,
            target_listener=target_listener,
            participants=participants,
            context=context,
            info_img=info_img,
            memory_list=memory_list,
            lang=lang,
            guideline_list=guideline_list,
            situation_dict=situation_dict,
            player_name=player_name
        )
        
        prompt = util_string.replace_user_placeholder(prompt, player_name)
        
        
    except Exception as e:
        print(f"Error building prompt: {e}")
        traceback.print_exc()
        yield [f"프롬프트 생성 오류: {str(e)}"]
        return
    
    # ai_conversation_binary.py와 동일한 스트림 처리 로직
    all_stop_strings = ['\nYou:', '<|im_end|>', '<|im_start|>user', '<|im_start|>assistant\n', '\nAI:', "<|eot_id|>", "< |"]
    
    if is_sentence:
        reply_list = []
        try:
            for reply in custom_generate_reply(prompt, None, -1, None, None, True, is_regenerate, llm.generate_with_streaming):
                state.write_log(f'multi_generate_reply ({target_speaker}): {reply}')
                state.write_log(f'multi_generate_reply_debug ({target_speaker}): len={len(reply)}, last_10_chars="{reply[-10:]}"')
                
                # 최근 단어에 ? . ! 가 없을 경우 continue (ai_conversation_binary.py와 동일)
                is_punc = False
                if reply:
                    for punc in util_string.STREAMING_PUNCS:
                        if punc in reply[-3:]:
                            is_punc = True
                            break
                if not is_punc:
                    continue            
                
                reply_list = util_string.get_punctuation_sentences(reply)
                
                # 첫 문장 생성중
                if not reply_list:
                    continue  
          
                # 멈추라면 그대로 break
                if state.get_is_stop_requested():       
                    state.set_is_stop_requested(False)
                    break
                
                # stop 문 있으면 break
                if reply_list:
                    _, stop_found = apply_stopping_strings(reply_list[-1], all_stop_strings)  # 마지막 문장만 체크
                    if stop_found:
                        state.write_log(f'multi_stop_detected ({target_speaker}): stop_string found in "{reply_list[-1][-20:]}"')
                        if len(reply_list) >= 1:
                            reply_list = reply_list[:len(reply_list)-1]
                        break
                
                if len(reply_list) >= 20:  # 문장-1 줄까지 작업
                    break
                
                # 후처리 (다중 캐릭터용)
                processed_reply = post_process_multi_reply(reply_list, target_speaker, lang)
                yield (processed_reply, target_speaker)     
                
        except Exception as e:
            print(f"Error in multi stream processing: {e}")
            traceback.print_exc()
            
        if not reply_list:
            fallback_msg = get_fallback_message(target_speaker, lang)
            reply_list = [fallback_msg]
        
        processed_reply = post_process_multi_reply(reply_list, target_speaker, lang)
        yield (processed_reply, target_speaker)
    
    else:
        # 단일 응답 모드 (ai_conversation_binary.py 참조)
        reply = ""
        try:
            for reply in custom_generate_reply(prompt, None, -1, None, None, True, is_regenerate, llm.generate_with_streaming):
                # 멈추라면 그대로 break
                if state.get_is_stop_requested():
                    state.set_is_stop_requested(False)
                    break
                
                # stop 문 있으면 break
                reply, stop_found = apply_stopping_strings(reply, all_stop_strings)
                if stop_found:
                    break
                
                reply_list = util_string.get_punctuation_sentences(reply)
                if len(reply_list) >= 20:  # 문장-1 줄까지 작업
                    reply = ''.join(reply_list[:len(reply_list)-1]) # 문장 수 까지 반환
                    break
                
                # 단일 응답 후처리
                processed_reply = post_process_single_reply(reply, target_speaker, lang)
                yield ([processed_reply], target_speaker)
                
        except Exception as e:
            print(f"Error in single response processing: {e}")
            traceback.print_exc()
            
        processed_reply = post_process_single_reply(reply, target_speaker, lang)
        yield ([processed_reply], target_speaker)

def remove_duplicate_sentences(reply_list: List[str], target_speaker: str) -> List[str]:
    """생성 후 중복 문장 제거"""
    if not reply_list:
        return reply_list
    
    cleaned_list = []
    seen_sentences = set()
    removed_count = 0
    
    for sentence in reply_list:
        sentence_clean = sentence.strip().lower()
        
        # 빈 문장 스킵
        if not sentence_clean:
            continue
            
        # 완전 중복 제거
        if sentence_clean not in seen_sentences:
            # 유사한 문장도 체크 (길이가 10자 이상인 경우만)
            is_similar = False
            if len(sentence_clean) > 10:
                for seen in seen_sentences:
                    # 한 문장이 다른 문장에 포함되거나, 80% 이상 유사한 경우
                    if (sentence_clean in seen or seen in sentence_clean):
                        is_similar = True
                        break
            
            if not is_similar:
                cleaned_list.append(sentence)
                seen_sentences.add(sentence_clean)
            else:
                removed_count += 1
        else:
            removed_count += 1
    
    # 중복 제거가 발생한 경우 로그 출력
    if removed_count > 0:
        print(f'###duplicate_sentence: {target_speaker}에서 {removed_count}개 중복 문장 제거됨')
        state.write_log(f'duplicate_sentence_removed ({target_speaker}): {removed_count}개 중복 제거, {len(reply_list)} → {len(cleaned_list)}')
    
    return cleaned_list

def post_process_multi_reply(reply_list: List[str], target_speaker: str, lang: str = 'ko') -> List[str]:
    """다중 캐릭터 응답 후처리 - ai_conversation_binary.py와 동일한 로직 + 고유명사 변환"""
    processed_list = []
    
    for reply in reply_list:
        # ai_conversation_binary.py와 동일한 후처리
        visible_reply = reply
        visible_reply = re.sub("(<USER>|<user>|{{user}})", 'You', visible_reply)
        visible_reply = visible_reply.replace("\n",'')
        visible_reply = re.sub(r'\([^)]*\)', '', visible_reply)  # ()와 안의 내용물 제거
        visible_reply = re.sub(r'\[[^)]*\]', '', visible_reply)  # []와 안의 내용물 제거
        visible_reply = re.sub(r'\*[^)]*\*', '', visible_reply)  # * *과 안의 내용물 제거
        visible_reply = visible_reply.lstrip(' ')
        
        # 2단계: AI 응답 후 고유명사 변환 (translate 전/후 통합)
        original_reply = visible_reply
        visible_reply = util_proper_nouns.apply_proper_nouns(lang, visible_reply)
        if original_reply != visible_reply:
            print(f"[응답 고유명사 변환] '{original_reply}' → '{visible_reply}'")
        
        processed_list.append(visible_reply)
    
    # 중복 제거 (새 기능)
    processed_list = remove_duplicate_sentences(processed_list, target_speaker)
    
    return processed_list

def post_process_single_reply(reply: str, target_speaker: str, lang: str = 'ko') -> str:
    """단일 응답 후처리 + 고유명사 변환"""
    visible_reply = reply
    visible_reply = re.sub("(<USER>|<user>|{{user}})", 'You', visible_reply)
    visible_reply = visible_reply.replace("\n",'')
    visible_reply = re.sub(r'\([^)]*\)', '', visible_reply)  # ()와 안의 내용물 제거
    visible_reply = re.sub(r'\[[^)]*\]', '', visible_reply)  # []와 안의 내용물 제거
    visible_reply = re.sub(r'\*[^)]*\*', '', visible_reply)  # * *과 안의 내용물 제거
    visible_reply = visible_reply.lstrip(' ')
    
    # 2단계: AI 응답 후 고유명사 변환 (translate 전/후 통합)
    original_reply = visible_reply
    visible_reply = util_proper_nouns.apply_proper_nouns(lang, visible_reply)
    if original_reply != visible_reply:
        print(f"[응답 고유명사 변환] '{original_reply}' → '{visible_reply}'")
    
    return visible_reply

def get_fallback_message(target_speaker: str, lang: str = 'en') -> str:
    """캐릭터별 fallback 메시지 - 한영일 3개 국어 대응"""
    fallback_messages = {
        'ko': {
            'arona': "음... 잘 이해가 안 돼요, 선생님.",
            'plana': "처리 중 오류가 발생했습니다.",
            'default': "죄송해요, 다시 말씀해 주시겠어요?"
        },
        'ja': {
            'arona': "うーん... よく分からないです、先生。",
            'plana': "処理中にエラーが発生しました。",
            'default': "すみません、もう一度お願いします。"
        },
        'en': {
            'arona': "Um... I don't quite understand, Sensei.",
            'plana': "An error occurred during processing.",
            'default': "I'm sorry, could you repeat that?"
        }
    }
    
    lang_key = 'ja' if lang in ['ja', 'jp'] else lang
    messages = fallback_messages.get(lang_key, fallback_messages['en'])
    return messages.get(target_speaker, messages['default'])

def triggerNPCCommunication(
    participants: List[Dict],
    initial_context: str = "",
    max_turns: int = 10,
    auto_stop_conditions: List[str] = None,
    lang: str = 'en',
    **kwargs
) -> List[Dict]:
    """AI끼리만 자동 대화 - 자연스러운 종료까지 진행"""
    
    llm = get_llm()
    
    # AI 참여자만 필터링
    ai_participants = [p for p in participants if p.get("type") == "ai"]
    if len(ai_participants) < 2:
        print("Error: AI 참여자가 2명 이상 필요합니다.")
        return []
    
    # 기본값 설정
    auto_stop_conditions = auto_stop_conditions or ["안녕", "끝", "가볼게", "나가볼게", "goodbye", "bye"]
    conversation_log = []
    context = {"description": initial_context}
    
    # 첫 번째 AI가 먼저 말하기
    current_speaker = ai_participants[0]["name"]
    initial_query = initial_context or "안녕하세요! 오늘 어떻게 지내세요?"
    
    print(f"=== NPC 자동 대화 시작 (최대 {max_turns}턴) ===")
    print(f"참여자: {[p['name'] for p in ai_participants]}")
    print(f"초기 컨텍스트: {initial_context}")
    
    for turn in range(max_turns):
        print(f"\n--- 턴 {turn + 1} ---")
        
        # AI Agent로 다음 발화자 결정
        if turn == 0:
            # 첫 턴은 두 번째 AI가 응답
            target_speaker = ai_participants[1]["name"] if len(ai_participants) > 1 else ai_participants[0]["name"]
            query = initial_query
        else:
            # 다음 발화자 결정
            try:
                target_speaker, reason = get_next_speaker_with_agent(
                    conversation_log[-1]["message"], current_speaker, ai_participants, conversation_log, context, lang
                )
                print(f"[AI Agent] 다음 발화자: {target_speaker} - {reason}")
                query = conversation_log[-1]["message"]  # 이전 메시지에 응답
            except Exception as e:
                print(f"[AI Agent 오류] {e} - 순환 방식 사용")
                current_idx = next((i for i, p in enumerate(ai_participants) if p["name"] == current_speaker), 0)
                next_idx = (current_idx + 1) % len(ai_participants)
                target_speaker = ai_participants[next_idx]["name"]
                query = conversation_log[-1]["message"] if conversation_log else initial_query
        
        # 응답 생성
        reply_list = []
        try:
            for response_batch in process_multi_stream(
                 query=query,
                 current_speaker=current_speaker,
                 target_speaker=target_speaker,
                 participants=ai_participants,
                 memory_list=conversation_log,
                 context=context,
                 is_sentence=True,
                 lang=lang,
                 player_name='sensei',
                 **kwargs
             ):
                reply_list = response_batch
                break  # 첫 번째 응답만 사용
            
            if reply_list:
                full_response = " ".join(reply_list)
                print(f"[{target_speaker}]: {full_response}")
                
                # 대화 기록에 추가
                conversation_log.append({
                    "turn": turn + 1,
                    "speaker": target_speaker,
                    "message": full_response,
                    "timestamp": None
                })
                
                # 종료 조건 확인
                for stop_condition in auto_stop_conditions:
                    if stop_condition.lower() in full_response.lower():
                        print(f"[자동 종료] 키워드 '{stop_condition}' 감지")
                        return conversation_log
                
                # 다음 턴 준비
                current_speaker = target_speaker
                
            else:
                print(f"[오류] {target_speaker}의 응답 생성 실패")
                break
                
        except Exception as e:
            print(f"[대화 생성 오류] {e}")
            traceback.print_exc()
            break
    
    print(f"\n=== NPC 자동 대화 종료 (총 {len(conversation_log)}턴) ===")
    return conversation_log

def apply_stopping_strings(reply, all_stop_strings):
    """정지 문자열 적용 - ai_conversation_binary.py와 동일"""
    stop_found = False
    for string in all_stop_strings:
        idx = reply.find(string)
        if idx != -1:
            reply = reply[:idx]
            stop_found = True
            break

    if not stop_found:
        # If something like "\nYo" is generated just before "\nYou:" is completed, trim it
        for string in all_stop_strings:
            for j in range(len(string) - 1, 0, -1):
                if reply[-j:] == string[:j]:
                    reply = reply[:-j]
                    break
            else:
                continue
            break

    return reply, stop_found

def custom_generate_reply(question, original_question, seed, state, stopping_strings, is_chat, is_regenerate, generate_func):
    """커스텀 생성 함수 - 반복 방지 개선"""
    
    # 반복 방지를 위한 개선된 파라미터
    enhanced_state = {
        'temperature': 0.8,           # 다양성 증가 (기본값 0.6 → 0.8)
        'repetition_penalty': 1.15,   # 반복 억제 강화 (기본값 1.0 → 1.15)
        'frequency_penalty': 0.3,     # 단어 빈도 억제
        'presence_penalty': 0.2,      # 단어 존재 억제
        'top_p': 0.9,                 # nucleus sampling
        'min_p': 0.1,                 # 최소 확률 필터 강화 (기본값 0.05 → 0.1)
    }
    
    for reply in generate_func(question, enhanced_state):
        yield f"{reply}"

# 편의 함수들
def create_aropla_participants() -> List[Dict]:
    """아로프라 채널 참여자 생성"""
    return [
        {"name": "sensei", "type": "user", "display_name": "선생님"},
        {"name": "arona", "type": "ai", "display_name": "아로나", "character_file": "arona"},
        {"name": "plana", "type": "ai", "display_name": "프라나", "character_file": "plana"}
    ]

def create_simple_conversation(user_name: str = "sensei", ai_name: str = "arona") -> List[Dict]:
    """간단한 1:1 대화 참여자 생성"""
    return [
        {"name": user_name, "type": "user", "display_name": "선생님" if user_name == "sensei" else user_name.title()},
        {"name": ai_name, "type": "ai", "display_name": ai_name.title(), "character_file": ai_name}
    ]

def create_tea_party_participants() -> List[Dict]:
    """티파티 참여자 생성 예시"""
    return [
        {"name": "sensei", "type": "user", "display_name": "선생님"},
        {"name": "nagisa", "type": "ai", "display_name": "나기사", "character_file": "nagisa"},
        {"name": "mika", "type": "ai", "display_name": "미카", "character_file": "mika"},
        {"name": "seia", "type": "ai", "display_name": "세이아", "character_file": "seia"}
    ]


# 테스트 및 디버그
if __name__ == "__main__":    
    # 모델 로딩
    state.set_use_gpu_percent(8)
    # state.model_name = 'Qwen3-14B-Q4_K_M.gguf'
    state.model_name = 'Qwen3-30B-A3B-Instruct-2507-Q4_K_M.gguf'
    # state.model_name = 'Qwen3-VL-30B-A3B-Instruct-Q4_K_M.gguf'
    load_model(is_use_cuda=True)
    print("✅ 모델 로딩 완료!")
    
    # 테스트 시나리오 함수들
    def save_conversation_log(memory_list: List[Dict], participants: List[Dict], group_name: str, turn_count: int):
        """대화 내용을 파일로 저장"""
        try:
            # 폴더 생성
            save_dir = "test/aropla"
            if not os.path.exists(save_dir):
                os.makedirs(save_dir)
            
            # 파일명 생성 (날짜_시분초.txt)
            timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
            filename = f"{timestamp}.txt"
            filepath = os.path.join(save_dir, filename)
            
            # 대화 내용 정리
            with open(filepath, 'w', encoding='utf-8') as f:
                f.write(f"=== {group_name} 대화 기록 ===\n")
                f.write(f"저장 시간: {datetime.now().strftime('%Y년 %m월 %d일 %H시 %M분 %S초')}\n")
                f.write(f"총 턴 수: {turn_count}\n")
                f.write(f"참여자: {[p['display_name'] for p in participants]}\n")
                f.write("="*60 + "\n\n")
                
                if not memory_list:
                    f.write("📝 대화 내용이 없습니다.\n")
                else:
                    for i, entry in enumerate(memory_list, 1):
                        speaker = entry.get("speaker", "unknown")
                        message = entry.get("message", "")
                        
                        # 발화자 이름을 display_name으로 변환
                        speaker_display = next(
                            (p['display_name'] for p in participants if p['name'] == speaker), 
                            speaker.title()
                        )
                        
                        f.write(f"{i:3d}. [{speaker_display}]: {message}\n")
                
                f.write("\n" + "="*60)
                f.write(f"\n파일명: {filename}")
                f.write(f"\n저장 위치: {filepath}")
                
            print(f"💾 대화 내용이 저장되었습니다: {filepath}")
            return filepath
            
        except Exception as e:
            print(f"❌ 대화 저장 실패: {e}")
            return None

    def test_scenario_1():
        """1. 멀티 캐릭터 대화 (AI Agent 자동 결정) - 멀티턴 대화"""
        print("\n--- 멀티 캐릭터 멀티턴 대화 (AI Agent 자동 결정) ---")
        print("실제 다중 대화를 체험해보세요!")
        
        # 대화 그룹 선택
        print("\n📋 대화 그룹을 선택하세요:")
        print("1. 아로프라 채널 (아로나 + 프라나)")
        print("2. Trinity Tea Party (나기사 + 미카 + 세이아)")
        
        while True:
            try:
                choice = input("🎯 선택 (1-2): ").strip()
                if choice == '1':
                    participants = create_aropla_participants()
                    group_name = "아로프라 채널"
                    context_description = "아로프라 채널 대화"
                    break
                elif choice == '2':
                    participants = create_tea_party_participants()
                    group_name = "Trinity Tea Party"
                    context_description = "Trinity Tea Party 티타임 대화"
                    break
                else:
                    print("❌ 1 또는 2를 입력해주세요.")
            except KeyboardInterrupt:
                print("\n선택이 취소되었습니다.")
                return
        
        print(f"\n✅ {group_name} 선택됨!")
        print("명령어: 'prompt'(현재 프롬프트), 'history'(대화 기록), 'who'(참여자 정보), 'exit'(종료)")
        
        # 그룹에 맞는 초기 대화 설정
        if choice == '1':  # 아로프라 채널
            memory_list = [
                {"speaker": "arona", "message": "안녕하세요 선생님! 정말 좋은 날씨에요~"}
            ]
        else:  # Trinity Tea Party
            memory_list = [
                {"speaker": "nagisa", "message": "선생님, 안녕하세요. 환영합니다."}
            ]
        
        print(f"\n🎭 참여자: {[p['display_name'] for p in participants]}")
        print("📜 초기 대화:")
        for entry in memory_list:
            speaker_display = next((p['display_name'] for p in participants if p['name'] == entry['speaker']), entry['speaker'])
            print(f"  [{speaker_display}]: {entry['message']}")
        
        turn_count = 0
        max_turns = 20  # 무한루프 방지
        current_prompt = ""  # 현재 프롬프트 저장 (테스트용 로컬 변수)
        current_speaker = "sensei"  # 현재 발화자 추적
        
        try:
            while turn_count < max_turns:
                print(f"\n--- 턴 {turn_count + 1} ---")
                
                # 현재 발화자에 따른 처리
                if current_speaker == "sensei":
                    # 사용자 입력 받기
                    user_input = input("🎯 센세이의 말 (또는 명령어): ").strip()
                    
                    # 명령어 처리 (센세이만 가능)
                    if user_input.lower() == 'exit':
                        print("🚪 대화를 종료합니다.")
                        save_conversation_log(memory_list, participants, group_name, turn_count)
                        break
                    elif user_input.lower() == 'history':
                        print("\n📜 현재 대화 기록:")
                        for i, entry in enumerate(memory_list):
                            speaker_display = next((p['display_name'] for p in participants if p['name'] == entry['speaker']), entry['speaker'])
                            print(f"  {i+1}. [{speaker_display}]: {entry['message']}")
                        continue
                    elif user_input.lower() == 'who':
                        print("\n👥 대화 참여자:")
                        for p in participants:
                            role_emoji = "👨‍🏫" if p['type'] == 'user' else "🤖"
                            print(f"  {role_emoji} {p['display_name']} ({p['name']}) - {p['type']}")
                            if p.get('character_file'):
                                print(f"      📋 캐릭터 파일: {p['character_file']}.json")
                        continue
                    elif user_input.lower() == 'prompt':
                        if current_prompt:
                            print("\n" + "="*60)
                            print("📋 현재 AI에 입력되는 프롬프트")
                            print("="*60)
                            print(current_prompt)
                            print("="*60 + "\n")
                        else:
                            print("❌ 아직 생성된 프롬프트가 없습니다. 먼저 대화를 시작해주세요.")
                        continue
                    elif not user_input:
                        print("❌ 메시지를 입력해주세요.")
                        continue
                    
                    print(f"[선생님]: {user_input}")
                else:
                    # AI 캐릭터가 자동으로 대화 이어가기
                    if memory_list:
                        last_entry = memory_list[-1]
                        last_speaker = last_entry["speaker"]
                        last_message = last_entry["message"]
                        
                        # 이전 발화에 기반한 자연스러운 쿼리 생성
                        if last_speaker == current_speaker:
                            user_input = "계속 이야기해주세요."
                        else:
                            user_input = last_message  # 이전 메시지를 받아서 응답
                        
                        speaker_display = next((p['display_name'] for p in participants if p['name'] == current_speaker), current_speaker)
                        print(f"🤖 [{speaker_display}]이(가) 자동으로 응답 중...")
                    else:
                        user_input = "안녕하세요!"
                        speaker_display = next((p['display_name'] for p in participants if p['name'] == current_speaker), current_speaker)
                        print(f"🤖 [{speaker_display}]이(가) 대화를 시작합니다...")
                
                # 현재 발화자의 입력을 대화 기록에 추가 (sensei인 경우에만, AI는 응답 후 추가)
                if current_speaker == "sensei":
                    memory_list.append({
                        "speaker": current_speaker,
                        "message": user_input
                    })
                
                # target_speaker 결정 - 상황에 맞게 최적화
                if current_speaker == "sensei":
                    # 사용자 발화 시: AI Agent 호출하여 응답자 결정
                    print("🤖 AI Agent가 다음 발화자를 결정 중...")
                    target_speaker = None  # AI Agent가 자동 결정
                else:
                    # AI 발화 시: 이미 AI Agent에 의해 결정된 발화자이므로 재호출 불필요
                    print(f"이미 결정된 발화자가 응답 중: {current_speaker} (AI Agent 호출 생략)")
                    target_speaker = current_speaker  # 현재 발화자가 응답
                
                # 현재 프롬프트 생성 (테스트용) - 공통 처리
                try:
                    current_prompt = get_qwen_multi_prompt(
                        query=user_input,
                        current_speaker=current_speaker,
                        target_speaker=target_speaker,
                        participants=participants,
                        context={"description": context_description},
                        memory_list=memory_list,
                        lang='ko',
                        player_name='sensei'
                    )
                except Exception as e:
                    print(f"프롬프트 생성 실패: {e}")
                    current_prompt = ""
                
                # AI 응답 생성 - 공통 처리
                reply_len = 0
                final_reply = []
                responding_character = None
                
                for j, (reply_list, actual_speaker) in enumerate(process_multi_stream(
                    query=user_input,
                    current_speaker=current_speaker,
                    target_speaker=target_speaker,
                    participants=participants,
                    memory_list=memory_list,
                    context={"description": context_description},
                    is_sentence=True,
                    lang='ko',
                    player_name='sensei'
                )):
                    if reply_len < len(reply_list):
                        reply_len = len(reply_list)
                        final_reply = reply_list
                        responding_character = actual_speaker
                    pass
                
                # AI 응답 처리 - 공통 처리
                if final_reply and responding_character:
                    ai_response = " ".join(final_reply)
                    
                    # AI 응답을 대화 기록에 추가
                    memory_list.append({
                        "speaker": responding_character,
                        "message": ai_response
                    })
                    
                    speaker_display = next((p['display_name'] for p in participants if p['name'] == responding_character), responding_character)
                    print(f"[{speaker_display}]: {ai_response}")
                    
                    # AI 응답 후 다음 발화자 결정
                    try:
                        next_speaker, reason = get_next_speaker_with_agent(
                            ai_response, responding_character, participants, memory_list, 
                            {"description": context_description}, 'ko'
                        )
                        print(f"[AI Agent 다음 발화자] {next_speaker} - {reason}")
                        current_speaker = next_speaker
                    except Exception as e:
                        print(f"[AI Agent 오류] {e} - 센세이에게 돌아감")
                        current_speaker = "sensei"
                    
                    turn_count += 1
                else:
                    print("❌ AI 응답 생성에 실패했습니다.")
                    save_conversation_log(memory_list, participants, group_name, turn_count)
                    break
                    
        except KeyboardInterrupt:
            print("\n대화가 중단되었습니다.")
            save_conversation_log(memory_list, participants, group_name, turn_count)
        
        print(f"\n✅ {group_name} 멀티턴 대화 완료! (총 {turn_count}턴)")
        
        # 정상 완료 시에도 대화 저장
        if turn_count >= max_turns:
            print("최대 턴 수에 도달했습니다.")
            save_conversation_log(memory_list, participants, group_name, turn_count)

    def test_scenario_2():
        """2. 이미지 포함 다중 대화"""
        print("\n--- 이미지 포함 다중 대화 테스트 ---")
        participants = create_aropla_participants()
        
        query = "이 사진에서 뭐가 보여?"
        current_speaker = "sensei"
        target_speaker = "arona"
        
        print(f"Query: {query}")
        print(f"Current: {current_speaker} → Target: {target_speaker} (명시적 지정)")
        
        reply_len = 0
        for j, (reply_list, responding_speaker) in enumerate(process_multi_stream(
            query=query,
            current_speaker=current_speaker,
            target_speaker=target_speaker,
            participants=participants,
            info_img="사진에는 아로나와 프라나가 함께 웃고 있는 모습이 보입니다.",
            is_sentence=True,
            lang='ko',
            player_name='sensei'
        )):
            if reply_len < len(reply_list):
                reply_len = len(reply_list)
            pass
        
        print(f"✅ {target_speaker}의 응답 (이미지 포함): {reply_list}")

    def test_scenario_3():
        """3. triggerNPCCommunication (AI끼리만 대화)"""
        print("\n--- 🤝 NPC 자동 대화 테스트 (AI끼리만) ---")
        ai_only_participants = [
            {"name": "arona", "type": "ai", "display_name": "아로나", "character_file": "arona"},
            {"name": "plana", "type": "ai", "display_name": "프라나", "character_file": "plana"}
        ]
        
        print("Initial Context: 두 AI가 카페에서 만나 대화를 나누고 있습니다.")
        print("Max Turns: 5, Stop Conditions: ['안녕', '가볼게']")
        
        npc_log = triggerNPCCommunication(
            participants=ai_only_participants,
            initial_context="두 AI가 카페에서 만나 대화를 나누고 있습니다.",
            max_turns=5,
            auto_stop_conditions=["안녕", "가볼게"],
            lang='ko'
        )
        
        print(f"\n✅ NPC 자동 대화 결과 ({len(npc_log)}턴):")
        for entry in npc_log:
            print(f"  턴 {entry['turn']}: [{entry['speaker']}] {entry['message']}")

    def test_scenario_4():
        """4. 상황별 대화 (situation_dict)"""
        print("\n--- 상황별 대화 테스트 (situation_dict) ---")
        participants = create_simple_conversation("sensei", "plana")
        
        query = "오늘 기분 어때?"
        current_speaker = "sensei"
        target_speaker = "plana"
        
        situation = {"mood": "편안한", "location": "도서관", "time": "오후"}
        guidelines = ["존댓말 사용", "차분하게 대답"]
        
        print(f"Query: {query}")
        print(f"Current: {current_speaker} → Target: {target_speaker}")
        print(f"Situation: {situation}")
        print(f"Guidelines: {guidelines}")
        
        reply_len = 0
        for j, (reply_list, responding_speaker) in enumerate(process_multi_stream(
            query=query,
            current_speaker=current_speaker,
            target_speaker=target_speaker,
            participants=participants,
            situation_dict=situation,
            guideline_list=guidelines,
            is_sentence=True,
            lang='ko',
            player_name='sensei'
        )):
            if reply_len < len(reply_list):
                reply_len = len(reply_list)
            pass
        
        print(f"✅ {target_speaker}의 응답 (상황 포함): {reply_list}")
    
    # 테스트 시나리오 매핑
    scenarios = {
        1: test_scenario_1,
        2: test_scenario_2,
        3: test_scenario_3,
        4: test_scenario_4
    }
    
    try:
        while True:
            print("=== Multi-Character Conversation System Test ===")
            print("\n📋 사용 가능한 테스트 시나리오:")
            print("1. 멀티 캐릭터 대화 (아로프라 채널 / 티파티 선택)")
            print("2. 이미지 포함 다중 대화")
            print("3. NPC 자동 대화 (AI끼리만)")
            print("4. 상황별 대화 (situation_dict)")
            print("\n특별 명령어:")
            print("- 'prompt': 현재 생성된 프롬프트 보기") 
            print("- 'exit' 또는 'quit': 종료")
            print("\n" + "-"*50)
            user_input = input("🎯 테스트할 시나리오 번호 (1-4) 또는 명령어를 입력하세요: ").strip().lower()
            
            if user_input in ['exit', 'quit']:
                break
            elif user_input == 'prompt':
                print("프롬프트를 보려면 '시나리오 1'을 선택한 후 대화 중에 'prompt'를 입력하세요!")
                continue
            
            try:
                scenario_num = int(user_input)
                if scenario_num in scenarios:
                    print(f"\n🎬 시나리오 {scenario_num} 실행 중...")
                    scenarios[scenario_num]()
                    print(f"✅ 시나리오 {scenario_num} 완료!")
                else:
                    print("❌ 잘못된 시나리오 번호입니다. 1-4 중에서 선택해주세요.")
            except ValueError:
                if user_input not in ['prompt']:
                    print("❌ 숫자를 입력해주세요. (1-4)")
                
    except KeyboardInterrupt:
        print("\n\n사용자에 의해 중단되었습니다.")
    
    finally:
        import ai_singleton
        ai_singleton.release()
        print("\n🎉 === 테스트 종료! ===")
