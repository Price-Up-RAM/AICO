# Multi-Character Conversation Message Builder
# 다중 참여자 대화용 메시지 구성 함수들

from typing import List, Dict, Optional
import prompt_char
import memory


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
    
    # 8. 현재 쿼리 추가 - current_speaker가 user 타입일 때만
    if current_speaker and current_speaker != target_speaker:
        # current_speaker가 user 타입인 경우에만 쿼리 추가
        if current_participant and current_participant.get("type") == "user":
            # display_name 사용
            current_display_name = current_speaker
            if current_participant.get("display_name"):
                current_display_name = current_participant["display_name"]
            
            formatted_query = f"[{current_display_name}]: {query} /no_think"
            messages.append({"role": "user", "content": formatted_query})
    
    return messages


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
   - "～だ", "～である", "～じゃん", "～かな" 등のタメ口語尾

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
