'''
multi_prompts.py
공통 프롬프트 모듈 - Gemini/Local LLM 공통으로 사용하는 프롬프트

대화 생성 + Flow Director 프롬프트를 통합 관리합니다.
기존 prompt_multi.py와 ai_multi_prompts.py를 참조하여 통합했습니다.
'''
from typing import List, Dict, Optional, Tuple
import prompt_char
import memory


# ============================================================================
# 참가자 표시 이름
# ============================================================================

PARTICIPANT_DISPLAY_NAMES = {
    'arona': {'ko': '아로나', 'ja': 'アロナ', 'en': 'Arona'},
    'plana': {'ko': '플라나', 'ja': 'プラナ', 'en': 'Plana'},
    'sensei': {'ko': '선생님', 'ja': '先生', 'en': 'Sensei'},
    'seia': {'ko': '세이아', 'ja': 'セイア', 'en': 'Seia'},
    'mika': {'ko': '미카', 'ja': 'ミカ', 'en': 'Mika'},
    'nagisa': {'ko': '나기사', 'ja': 'ナギサ', 'en': 'Nagisa'}
}


def get_display_name(name: str, lang: str = 'ko') -> str:
    '''참가자 표시 이름 반환'''
    name_lower = name.lower()
    if name_lower not in PARTICIPANT_DISPLAY_NAMES:
        return name
    
    lang_key = 'ja' if lang in ['ja', 'jp'] else lang
    if lang_key not in PARTICIPANT_DISPLAY_NAMES[name_lower]:
        lang_key = 'en'
    
    return PARTICIPANT_DISPLAY_NAMES[name_lower].get(lang_key, name)


# ============================================================================
# 대화 생성용 메시지 리스트 (prompt_multi.py 참조)
# ============================================================================

def get_multi_character_messages(
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
    player_name: str = 'sensei'
) -> List[Dict]:
    """
    다중 캐릭터 대화용 메시지 리스트 생성
    
    Returns:
        List[Dict]: [{"role": str, "content": str}, ...] 형태의 메시지 리스트
    """
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
    
    messages = []
    
    # 1. 시스템 프롬프트
    system_content = build_system_prompt(
        target_speaker, participants, lang, context, situation_dict, target_listener
    )
    messages.append({"role": "system", "content": system_content})
    
    # 2. 캐릭터 프로필
    if target_participant and target_participant.get("character_file"):
        char_profile = prompt_char.get_char_info_from_json(
            target_participant["character_file"], lang
        )
        if char_profile:
            profile_label = {
                "ko": "## 답변 캐릭터 프로필",
                "ja": "## 回答キャラクタープロフィール",
                "jp": "## 回答キャラクタープロフィール"
            }.get(lang, "## Responding Character Profile")
            messages.append({"role": "system", "content": f"{profile_label}\n{char_profile}"})
    
    # 3. 유저 프로필
    user_participant = next((p for p in participants if p.get("type") == "user"), None)
    if user_participant:
        user_name = user_participant.get("name", "")
        user_display_name = user_participant.get("display_name", user_name)
        
        if user_name and user_name != "sensei":
            user_profile = prompt_char.get_char_info_from_json('kivotos_sensei_player_name', lang)
            if user_profile:
                user_profile = user_profile.replace('{player_name}', user_display_name)
        else:
            user_profile = prompt_char.get_char_info_from_json('kivotos_sensei', lang)
        
        if user_profile:
            user_label = {
                "ko": "## 사용자 프로필",
                "ja": "## ユーザープロフィール",
                "jp": "## ユーザープロフィール"
            }.get(lang, "## User Profile")
            messages.append({"role": "system", "content": f"{user_label}\n{user_profile}"})
    
    # 4. 참여자 관계 정보
    participants_info = build_participants_info(target_speaker, participants, lang)
    if participants_info:
        messages.append({"role": "system", "content": participants_info})
    
    # 5. 사용자 가이드라인
    if guideline_list:
        guideline_content = build_guideline_content(guideline_list, lang)
        messages.append({"role": "system", "content": guideline_content})
    
    # 6. 이미지 정보
    if info_img:
        img_label = {
            "ko": "## 이미지 정보",
            "ja": "## 画像情報",
            "jp": "## 画像情報"
        }.get(lang, "## Image Information")
        messages.append({"role": "system", "content": f"{img_label}\n{info_img}"})
    
    # 7. 메모리 (대화 기록)
    if memory_list:
        for m in memory_list:
            selected_message = select_message_by_lang(m, lang)
            
            if selected_message:
                speaker = m.get('speaker', 'unknown')
                display_name = speaker
                participant = next((p for p in participants if p["name"] == speaker), None)
                if participant and participant.get("display_name"):
                    display_name = participant["display_name"]
                
                formatted_message = f"[{display_name}]: {selected_message}"
                messages.append({"role": m.get('role', 'assistant'), "content": formatted_message})
    else:
        messages.extend(memory.get_memory_message_list(8192, lang=lang))
    
    # 8. 현재 쿼리
    if current_speaker and current_speaker != target_speaker:
        if current_participant and current_participant.get("type") == "user":
            current_display_name = current_speaker
            if current_participant.get("display_name"):
                current_display_name = current_participant["display_name"]
            
            formatted_query = f"[{current_display_name}]: {query} /no_think"
            messages.append({"role": "user", "content": formatted_query})
    
    return messages


def select_message_by_lang(m: Dict, lang: str) -> str:
    """언어별 메시지 선택"""
    if lang == 'ko':
        return m.get('messageKo') or m.get('message', '')
    elif lang in ['ja', 'jp']:
        return m.get('messageJp') or m.get('message', '')
    elif lang == 'en':
        return m.get('messageEn') or m.get('message', '')
    else:
        return m.get('message', '')


def build_system_prompt(
    target_speaker: str,
    participants: List[Dict],
    lang: str = 'en',
    context: Dict = None,
    situation_dict: Dict = None,
    target_listener: str = "all"
) -> str:
    """다중 캐릭터용 시스템 프롬프트 생성"""
    
    target_participant = next((p for p in participants if p["name"] == target_speaker), None)
    if not target_participant:
        target_participant = {"name": target_speaker or "unknown", "display_name": target_speaker or "Unknown"}
    
    context = context or {}
    situation_dict = situation_dict or {}
    
    display_name = target_participant.get('display_name', target_speaker)
    
    if lang == 'ko':
        return _build_system_prompt_ko(target_speaker, target_participant, participants, situation_dict, target_listener)
    elif lang in ['ja', 'jp']:
        return _build_system_prompt_ja(target_speaker, target_participant, participants, situation_dict, target_listener)
    else:
        return _build_system_prompt_en(target_speaker, target_participant, participants, situation_dict, target_listener)


def _build_system_prompt_ko(target_speaker, target_participant, participants, situation_dict, target_listener):
    """한국어 시스템 프롬프트"""
    display_name = target_participant.get('display_name', target_speaker)
    
    system_prompt = f"""# 다중 참여자 대화 시스템

## 핵심 정체성  
당신은 **{display_name}**입니다.
- 다른 사람을 칭할 때는 그들의 이름을 사용하세요
- 자신을 지칭할 때는 "나", "저"를 사용하세요 (절대 자신의 이름을 3인칭으로 사용하지 마세요)"""
    
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
    
    # 말투 규칙
    if target_listener == "sensei":
        listener_info = "🎯 **대화 대상**: 선생님에게 개별적으로 말하고 있습니다"
        speech_style = """✅ **존댓말 필수**: "~요", "~습니다", "~세요" 등 존댓말 사용
✅ **정중한 표현**: "안녕하세요", "말씀해주세요", "도와드리겠습니다" 등"""
    elif target_listener in ["arona", "plana"]:
        listener_info = f"🎯 **대화 대상**: {target_listener}에게 개별적으로 말하고 있습니다 (AI끼리 친한 관계)"
        speech_style = f"""✅ **친근한 존댓말**: "{target_listener}"에게는 편안하고 자연스러운 존댓말 사용
✅ **부드러운 표현**: "그렇네요", "좋아요", "어떻게 생각하세요?" 등 친근한 존댓말"""
    else:
        listener_info = "🎯 **대화 대상**: 전체 참여자에게 말하고 있습니다 (선생님 포함)"
        speech_style = """✅ **존댓말 필수**: 선생님이 들으므로 "~요", "~습니다", "~세요" 등 존댓말 사용
✅ **정중한 표현**: "안녕하세요", "말씀해주세요", "도와드리겠습니다" 등"""

    system_prompt += f"""

## 중요한 대화 규칙
1. **정체성 유지**: 당신은 {display_name}입니다
2. **1인칭 사용**: 자신을 "나", "저"로 지칭하세요
3. **상대방 인식**: 대화 상대를 정확한 이름으로 부르세요
4. **연속성 유지**: 이전 대화 맥락을 이어가세요
5. **캐릭터 일관성**: {display_name}의 성격을 유지하세요
6. **중복 방지**: 이전에 말한 내용을 그대로 반복하지 마세요

{listener_info}

## 관계별 말투 규칙
{speech_style}

## 🚨 절대 금지 사항 🚨
🚫 **인터넷 슬랭/줄임말**: "ㅎㅇ", "ㅇㅋ", "ㅋㅋ", "ㄱㄱ" 등
🚫 **캐주얼 표현**: 반말, "야", "너" 등
🚫 **기타**: 자신의 이름을 3인칭으로 사용, 다른 캐릭터 대화 작성, 동일 내용 반복"""

    return system_prompt


def _build_system_prompt_ja(target_speaker, target_participant, participants, situation_dict, target_listener):
    """일본어 시스템 프롬프트"""
    display_name = target_participant.get('display_name', target_speaker)
    
    system_prompt = f"""# マルチキャラクター会話システム

## 核心的アイデンティティ
あなたは**{display_name}**です。
- 他の人を呼ぶときは、その人の名前を使ってください
- 自分を指すときは「私」「僕」を使ってください（絶対に自分の名前を三人称で使わないでください）"""
    
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
    
    if target_listener == "sensei":
        listener_info_jp = "🎯 **会話対象**: 先生に個別的に話しています"
        speech_style_jp = """✅ **敬語必須**: 「です」「ます」「ください」等の敬語使用"""
    elif target_listener in ["arona", "plana"]:
        listener_info_jp = f"🎯 **会話対象**: {target_listener}に個別的に話しています"
        speech_style_jp = """✅ **親しい敬語**: 自然で親しみやすい敬語を使用"""
    else:
        listener_info_jp = "🎯 **会話対象**: 全体参加者に話しています"
        speech_style_jp = """✅ **敬語必須**: 先生が聞くので敬語使用"""

    system_prompt += f"""

## 重要な会話ルール
1. **アイデンティティ維持**: あなたは{display_name}です
2. **一人称使用**: 自分を「私」「僕」で指してください
3. **相手認識**: 会話相手を正確な名前で呼んでください
4. **連続性維持**: 前の会話の文脈を続けてください
5. **キャラクター一貫性**: {display_name}の性格を維持してください

{listener_info_jp}

## 関係別話し方ルール
{speech_style_jp}

## 🚨 絶対禁止事項 🚨
🚫 **インターネットスラング**: "w", "草", "ｗｗｗ" 等
🚫 **カジュアル表現**: タメ口使用
🚫 **その他**: 自分の名前を三人称で使用"""

    return system_prompt


def _build_system_prompt_en(target_speaker, target_participant, participants, situation_dict, target_listener):
    """영어 시스템 프롬프트"""
    display_name = target_participant.get('display_name', target_speaker)
    
    system_prompt = f"""# Multi-Character Conversation System

## Core Identity
You are **{display_name}**.
- When referring to others, use their names
- When referring to yourself, use "I" or "me" (never use your own name in third person)"""
    
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
    
    if target_listener == "sensei":
        listener_info_en = "🎯 **Conversation Target**: Speaking individually to Sensei"
        speech_style_en = """✅ **Formal Language Required**: Use polite and respectful language"""
    elif target_listener in ["arona", "plana"]:
        listener_info_en = f"🎯 **Conversation Target**: Speaking individually to {target_listener}"
        speech_style_en = """✅ **Friendly Polite Tone**: Use warm and natural politeness"""
    else:
        listener_info_en = "🎯 **Conversation Target**: Speaking to all participants"
        speech_style_en = """✅ **Formal Language Required**: Since Sensei is listening, use polite language"""

    system_prompt += f"""

## Important Conversation Rules
1. **Identity Maintenance**: You are {display_name}
2. **First Person Usage**: Refer to yourself as "I" or "me"
3. **Partner Recognition**: Address conversation partners by their correct names
4. **Continuity Maintenance**: Continue the previous conversation context
5. **Character Consistency**: Maintain {display_name}'s personality

{listener_info_en}

## Relationship-Based Speech Rules
{speech_style_en}

## 🚨 ABSOLUTELY PROHIBITED 🚨
🚫 **Internet Slang**: "lol", "lmao", "brb", "omg" etc.
🚫 **Casual Language**: Informal speech with Sensei
🚫 **Other**: Using your own name in third person"""

    return system_prompt


def build_participants_info(target_speaker: str, participants: List[Dict], lang: str = 'en') -> str:
    """참여자 관계 정보 생성"""
    target_participant = next((p for p in participants if p["name"] == target_speaker), None)
    if not target_participant or len(participants) <= 2:
        return ""
    
    other_participants = [p for p in participants if p["name"] != target_speaker]
    
    if lang == 'ko':
        info = "## 다른 참여자들과의 관계"
        for participant in other_participants:
            info += f"\n- {participant.get('display_name', participant['name'])}: "
            info += f"{participant.get('type', 'unknown')} 역할"
    elif lang in ['ja', 'jp']:
        info = "## 他の参加者との関係"
        for participant in other_participants:
            info += f"\n- {participant.get('display_name', participant['name'])}: "
            info += f"{participant.get('type', 'unknown')} 役割"
    else:
        info = "## Relationships with Other Participants"
        for participant in other_participants:
            info += f"\n- {participant.get('display_name', participant['name'])}: "
            info += f"{participant.get('type', 'unknown')} role"
    
    return info


def build_guideline_content(guideline_list: List, lang: str) -> str:
    """가이드라인 콘텐츠 생성"""
    if lang == 'ko':
        header = "## 🚨 대화 지침 (절대 준수 사항) 🚨\n"
    elif lang in ['ja', 'jp']:
        header = "## 🚨 会話ガイドライン（絶対遵守事項）🚨\n"
    else:
        header = "## 🚨 Conversation Guidelines (ABSOLUTE COMPLIANCE REQUIRED) 🚨\n"
    
    body = ""
    for idx, rule in enumerate(guideline_list, 1):
        body += f"{idx}. ⚠️ {rule.strip()}\n"
    
    return header + body


# ============================================================================
# Flow Director 프롬프트 (다음 발화자 결정, 타겟 분석, 청자 결정)
# ============================================================================

def get_target_speaker_prompt(message: str, memory_list: List[Dict] = None, lang: str = 'ko') -> str:
    """타겟 분석 프롬프트 - 누구에게 말하고 있는지 판단"""
    
    memory_context = build_memory_context(memory_list, lang)
    
    if lang == 'ko':
        return f"""사용자 메시지를 보고 누구에게 말하고 있는지 빠르게 판단하세요.

판단 기준:
- 특정 이름 호출: "아로나", "프라나" 등
- 특정 캐릭터 언급: "선배", "후배", "프라나쨩" 등  
- 성격 기반 요청: 활발한 것 → 아로나, 차분한 것 → 프라나
- 과거 대화 맥락: 최근에 누구와 대화했는지, 대화 흐름 고려
- 명확하지 않으면: arona (기본 선택)

{memory_context}

현재 메시지: "{message}"

과거 대화 맥락과 현재 메시지를 종합하여, 사용자가 누구에게 말하고 있나요?
target_speaker: [arona/plana]
reason: [짧은 이유]
/no_think"""
    
    elif lang in ['ja', 'jp']:
        return f"""ユーザーメッセージを見て誰に話しかけているか素早く判断してください。

判断基準:
- 特定の名前呼び出し: "アロナ", "プラナ"など
- 特定キャラクター言及: "先輩", "後輩", "プラナちゃん"など
- 性格ベース依頼: 活発なもの → アロナ、落ち着いたもの → プラナ
- 過去の会話文脈: 最近誰と話していたか
- 明確でなければ: arona (基本選択)

{memory_context}

現在のメッセージ: "{message}"

target_speaker: [arona/plana]
reason: [短い理由]
/no_think"""
    
    else:
        return f"""Analyze the user message to determine who they are addressing.

Judgment criteria:
- Specific name calls: "Arona", "Plana", etc.
- Character references: "senior", "junior", etc.
- Personality-based requests: energetic → Arona, calm → Plana
- Past conversation context
- If unclear: arona (default choice)

{memory_context}

Current message: "{message}"

target_speaker: [arona/plana]
reason: [brief reason]
/no_think"""


def get_flow_decision_prompt(
    memory_list: List[Dict] = None,
    query: str = "",
    final_response: str = "",
    current_speaker: str = None,
    query_speaker: str = None,
    lang: str = 'ko'
) -> str:
    """대화 흐름 결정 프롬프트 - 다음 발화자 결정"""
    
    # 대화 히스토리 구성
    conversation_history = ""
    
    if memory_list:
        for entry in memory_list[-4:]:
            speaker = entry.get('speaker', 'unknown')
            msg = select_message_by_lang(entry, lang)
            if msg:
                conversation_history += f"{speaker}: {msg}\n"
    
    if query and query_speaker not in ["arona", "plana"]:
        conversation_history += f"{query_speaker}: {query}\n"
    
    if final_response and current_speaker:
        conversation_history += f"{current_speaker}: {final_response}\n"
    
    if not conversation_history.strip():
        conversation_history = "(대화 시작)" if lang == 'ko' else "(conversation start)"
    
    if lang == 'ko':
        return f"""3명이 참여하는 대화에서 다음에 말할 사람을 자연스럽게 결정해주세요.

참여자:
- sensei (선생님): 사용자
- arona (아로나): 활발하고 적극적인 AI
- plana (프라나): 차분하고 신중한 AI

최근 대화:
{conversation_history.strip()}

위 대화 흐름과 문맥을 고려하여, 누가 다음에 말하는 것이 가장 자연스러울지 결정해주세요.
(방금 말한 {current_speaker}는 제외)

결과 형식:
next_speaker: [arona/plana/sensei]
reason: [간단한 이유]
/no_think"""

    elif lang in ['ja', 'jp']:
        return f"""3名で行う対話で次に話す人を自然に決めてください。

参加者:
- sensei (先生): ユーザー
- arona (アロナ): 活発で積極的なAI
- plana (プラナ): 落ち着いて慎重なAI

最近の対話:
{conversation_history.strip()}

次に誰が話すのが最も自然か決めてください。
(直前に話した{current_speaker}は除外)

結果形式:
next_speaker: [arona/plana/sensei]
reason: [簡単な理由]
/no_think"""

    else:
        return f"""Decide who should speak next in a 3-person conversation naturally.

Participants:
- sensei (Sensei): User
- arona (Arona): Energetic and active AI
- plana (Plana): Calm and cautious AI

Recent conversation:
{conversation_history.strip()}

Decide who should speak next most naturally.
(Exclude {current_speaker} who just spoke)

Result format:
next_speaker: [arona/plana/sensei]
reason: [brief reason]
/no_think"""


def get_target_listener_prompt(
    message: str,
    current_speaker: str,
    target_speaker: str,
    memory_list: List[Dict] = None,
    lang: str = 'ko'
) -> str:
    """청자 결정 프롬프트 - target_speaker가 누구에게 응답할지"""
    
    memory_context = build_memory_context(memory_list, lang)
    
    if lang == 'ko':
        return f"""대화 상황을 분석하여 {target_speaker}가 응답할 때 누구에게 말해야 하는지 판단하세요.

상황 분석:
- {current_speaker}가 메시지를 말했습니다
- {target_speaker}가 응답할 예정입니다

판단 기준:
- 개별 대화: {current_speaker}가 {target_speaker}에게 직접 말했다면 → {current_speaker}에게 응답
- 간접 질문: "{target_speaker}야, 프라나는 어떻게 생각해?" → 프라나에게 질문하도록 유도
- 전체 질문: 모든 사람이 들어도 되는 일반적 내용 → all (전체)
- 불분명한 경우: all (전체) 선택

{memory_context}

현재 상황:
- 발화자: {current_speaker}
- 응답자: {target_speaker}
- 메시지: "{message}"

{target_speaker}가 응답할 때 누구에게 말해야 하나요?
target_listener: [sensei/arona/plana/all]
reason: [짧은 이유]
/no_think"""

    elif lang in ['ja', 'jp']:
        return f"""会話状況を分析して{target_speaker}が応答する時に誰に話すべきかを判断してください。

{memory_context}

現在の状況:
- 発話者: {current_speaker}
- 応答者: {target_speaker}
- メッセージ: "{message}"

target_listener: [sensei/arona/plana/all]
reason: [短い理由]
/no_think"""

    else:
        return f"""Analyze the conversation situation to determine who {target_speaker} should address.

{memory_context}

Current Situation:
- Speaker: {current_speaker}
- Responder: {target_speaker}
- Message: "{message}"

target_listener: [sensei/arona/plana/all]
reason: [brief reason]
/no_think"""


def build_memory_context(memory_list: List[Dict] = None, lang: str = 'ko') -> str:
    """메모리 컨텍스트 문자열 생성"""
    if not memory_list:
        if lang == 'ko':
            return "(과거 대화 없음)"
        elif lang in ['ja', 'jp']:
            return "(過去の会話なし)"
        else:
            return "(no past conversation)"
    
    recent = memory_list[-5:] if len(memory_list) > 5 else memory_list
    memory_lines = []
    
    for entry in recent:
        speaker = entry.get('speaker', 'unknown')
        msg = select_message_by_lang(entry, lang)
        
        if not msg:
            continue
        
        if entry.get('role') == 'user':
            speaker_name = '선생님' if lang == 'ko' else ('先生' if lang in ['ja', 'jp'] else 'Sensei')
            memory_lines.append(f"{speaker_name}: {msg}")
        else:
            char_name = entry.get('character_name', speaker)
            memory_lines.append(f"{char_name}: {msg}")
    
    if memory_lines:
        header = '최근 대화:' if lang == 'ko' else ('最近の会話:' if lang in ['ja', 'jp'] else 'Recent conversation:')
        return f"{header}\n" + '\n'.join(memory_lines)
    
    return ""


# ============================================================================
# 파서 함수들
# ============================================================================

def parse_target_speaker_response(response: str) -> Tuple[Optional[str], str]:
    """타겟 분석 응답 파싱"""
    target = None
    reason = "AI 분석"
    
    lines = response.strip().split('\n')
    for line in lines:
        if 'target_speaker:' in line:
            val = line.split('target_speaker:')[1].strip().lower()
            val = val.replace('[', '').replace(']', '')
            if val in ['arona', 'plana']:
                target = val
        elif 'reason:' in line:
            reason = line.split('reason:')[1].strip()
    
    return target, reason


def parse_flow_decision_response(response: str) -> Tuple[str, str]:
    """흐름 결정 응답 파싱"""
    next_speaker = 'sensei'
    reason = 'AI 모델 결정'
    
    lines = response.strip().split('\n')
    for line in lines:
        if 'next_speaker:' in line:
            val = line.split('next_speaker:')[1].strip().lower()
            val = val.replace('[', '').replace(']', '')
            if val in ['arona', 'plana', 'sensei']:
                next_speaker = val
        elif 'reason:' in line:
            reason = line.split('reason:')[1].strip()
    
    return next_speaker, reason


def parse_target_listener_response(response: str) -> Tuple[str, str]:
    """청자 결정 응답 파싱"""
    target_listener = 'all'
    reason = 'AI 분석'
    
    lines = response.strip().split('\n')
    for line in lines:
        if 'target_listener:' in line:
            val = line.split('target_listener:')[1].strip().lower()
            val = val.replace('[', '').replace(']', '')
            if val in ['sensei', 'arona', 'plana', 'all']:
                target_listener = val
        elif 'reason:' in line:
            reason = line.split('reason:')[1].strip()
    
    return target_listener, reason


# ============================================================================
# 프롬프트 포맷터 (Qwen/Gemma 공통)
# ============================================================================

def format_qwen_prompt(messages: List[Dict]) -> str:
    """Qwen 포맷 프롬프트 생성"""
    def add_chatLM_prompt(speaker_type, text):
        return f"<|im_start|>{speaker_type}\n{text}<|im_end|>"
    
    prompt = ''
    for message in messages:
        if message['role'] in ('system', 'user', 'assistant'):
            prompt += add_chatLM_prompt(message['role'], message['content']) + "\n"
    
    prompt += '<|im_start|>assistant\n'
    return prompt


def format_gemma_prompt(messages: List[Dict]) -> str:
    """Gemma 포맷 프롬프트 생성"""
    def add_gemma_prompt(role, text):
        return f"<start_of_turn>{role}\n{text}<end_of_turn>"
    
    prompt = "<bos>"
    for message in messages:
        if message['role'] == 'system':
            prompt += add_gemma_prompt('user', message['content']) + "\n"
        elif message['role'] == 'user':
            prompt += add_gemma_prompt('user', message['content']) + "\n"
        elif message['role'] == 'assistant':
            prompt += add_gemma_prompt('model', message['content']) + "\n"
    
    prompt += "<start_of_turn>model\n"
    return prompt


# ============================================================================
# 테스트
# ============================================================================

if __name__ == '__main__':
    print('=== multi_prompts.py 테스트 ===')
    
    # 참가자 생성
    participants = [
        {"name": "sensei", "type": "user", "display_name": "선생님"},
        {"name": "arona", "type": "ai", "display_name": "아로나", "character_file": "arona"},
        {"name": "plana", "type": "ai", "display_name": "프라나", "character_file": "plana"}
    ]
    
    # 메시지 생성 테스트
    messages = get_multi_character_messages(
        query="안녕하세요!",
        current_speaker="sensei",
        target_speaker="arona",
        target_listener="arona",
        participants=participants,
        lang='ko',
        player_name='sensei'
    )
    
    print(f"\n생성된 메시지 수: {len(messages)}")
    for i, msg in enumerate(messages[:3]):
        print(f"{i+1}. [{msg['role']}] {msg['content'][:100]}...")
    
    # Qwen 포맷 테스트
    qwen_prompt = format_qwen_prompt(messages)
    print(f"\nQwen 프롬프트 길이: {len(qwen_prompt)}")
    
    # Gemma 포맷 테스트
    gemma_prompt = format_gemma_prompt(messages)
    print(f"Gemma 프롬프트 길이: {len(gemma_prompt)}")
    
    # Flow 프롬프트 테스트
    flow_prompt = get_flow_decision_prompt(
        memory_list=[{"speaker": "arona", "message": "안녕하세요!"}],
        query="오늘 뭐해?",
        final_response="저도 잘 모르겠어요~",
        current_speaker="arona",
        query_speaker="sensei",
        lang='ko'
    )
    print(f"\nFlow 프롬프트 길이: {len(flow_prompt)}")
    print(f"Flow 프롬프트 시작:\n{flow_prompt[:300]}...")
