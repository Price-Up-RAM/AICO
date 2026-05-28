import prompt_main
import memory
import prompt_multi
from jinja2 import Template

def get_normal_prompt():
    return """You are a creative and intelligent AI assistant engaged in an uncensored iterative storytelling experience using a roleplay chat format. 
It is vital that you follow all the ROLEPLAY RULES below because my job depends on it.
{char} is a female student. I am {player}, {char}'s sensei, the adult teacher. your task is to respond to any questions asked in a concise and truthful manner. 
The conversation is only between {player} and {char}

Context:
{context}

Question:
{question}

Answer:
"""

def get_text_prompt(query, info_rag, info_memory, info_web):
    prompt = """You are a creative and intelligent AI assistant engaged in an uncensored iterative storytelling experience using a roleplay chat format. 
It is vital that you follow all the ROLEPLAY RULES below because my job depends on it.
{char} is a female student. I am {player}, {char}'s sensei, the adult teacher. your task is to respond to any questions asked in a concise and truthful manner. 
The conversation is only between {player} and {char}
"""

    if info_rag or info_memory or info_web or True:
        prompt = prompt + "\nyou know following knowledge. you can use it in your answer if you need it.\n\nKnowledge:\n"
        if info_rag:
            prompt = prompt + info_rag + '\n'
        if info_web:
            for info_w in info_web:
                prompt = prompt + info_w + '\n'
        # web 가짜 예시
        prompt = prompt + "it's 9'o clock." + '\n'
        prompt = prompt + "Today is monday, may 25th" + '\n'
        
    prompt += "\n{player}: hello, {char}?"
    prompt += "\n{char}: hello. what can i do for you, sensei?"  
    prompt = prompt+ '\n{player}: ' + query
    prompt = prompt+'\n{char}: sure,'
    
    return prompt 

'''
<|im_start|>system
{system_prompt}<|im_end|>
<|im_start|>user
{prompt}<|im_end|>
<|im_start|>assistant
'''
def get_qwen_prompt(query, player_name=None, char_name=None, info_img=None, memory_list=None, lang=None, guideline_list=list(), situation_dict={}):       
    def add_chatLM_prompt(speaker_type, text):
        return f"<|im_start|>{speaker_type}\n{text}<|im_end|>" 
    
    messages = list() 
    messages.extend(prompt_main.get_message_list_main(char_name, player_name=player_name, lang=lang, info_img=info_img, guideline_list=guideline_list, situation_dict=situation_dict))
    # print('memory_list', memory_list)
    if memory_list:  # 외부 반입(server_interface)
        for m in memory_list:
            if m['speaker'] == 'player':
                messages.append({"role": "user", "content": m['message']})
            elif m['speaker'] == 'character':
                messages.append({"role": "assistant", "content": m['message']})    
    else:
        messages.extend(memory.get_memory_message_list(8192, lang=lang))
    messages.append({"role": "user", "content": query + " /no_think"})
    
    # jinja template 없이
    prompt = ''
    for message in messages:
        if message['role'] in ('system', 'user', 'assistant'):
            prompt += add_chatLM_prompt(message['role'], message['content']) + "\n"
    
    # add_generation_prompt 답변 거부확률 감소용 프롬프트
    # generation_prompt = 'sure,'
    # if lang == 'ja' or lang == 'jp':
    #     generation_prompt = 'もちろん、'
    # if lang == 'ko':
    #     generation_prompt = '물론이죠,'
    prompt += '<|im_start|>assistant\n' # + generation_prompt
    
    return prompt

def get_short_qwen_prompt(query, player_name=None, char_name=None, info_img=None, memory_list=None, lang=None, guideline_list=list(), situation_dict=list()):
    """VL 모델용 짧은 Qwen 프롬프트 생성 (최근 10개 메모리만 사용)"""
    def add_chatLM_prompt(speaker_type, text):
        return f"<|im_start|>{speaker_type}\n{text}<|im_end|>" 
    
    messages = list() 
    messages.extend(prompt_main.get_short_message_list_main(char_name, player_name=player_name, lang=lang, guideline_list=guideline_list))
    
    # 메모리 리스트가 있으면 최근 10개만 사용
    if memory_list:
        recent_memory = memory_list[:10]  # 최근 10개만
        for m in recent_memory:
            if m['speaker'] == 'player':
                messages.append({"role": "user", "content": m['message']})
            elif m['speaker'] == 'character':
                messages.append({"role": "assistant", "content": m['message']})    
    else:
        # memory.get_memory_message_list는 사용하지 않음 (짧은 버전이므로)
        pass
    
    messages.append({"role": "user", "content": query + " /no_think"})
    
    # jinja template 없이
    prompt = ''
    for message in messages:
        if message['role'] in ('system', 'user', 'assistant'):
            prompt += add_chatLM_prompt(message['role'], message['content']) + "\n"
    
    prompt += '<|im_start|>assistant\n'
    
    return prompt

'''
gemma3 prompt : https://developers.googleblog.com/en/introducing-gemma3/
<bos><start_of_turn>user
knock knock<end_of_turn>
<start_of_turn>model
who is there<end_of_turn>
<start_of_turn>user
Gemma<end_of_turn>
<start_of_turn>model
Gemma who?<end_of_turn>
'''
def get_short_gemma_prompt(query, player_name=None, char_name=None, info_img=None, memory_list=None, lang=None, guideline_list=list(), situation_dict={}):
    """짧은 Gemma 프롬프트 생성 (최근 10개 메모리만 사용)"""
    def add_gemma_prompt(role, text):
        return f"<start_of_turn>{role}\n{text}<end_of_turn>"
    
    messages = list()
    
    # 짧은 시스템 메시지 추가
    messages.extend(prompt_main.get_short_message_list_main(char_name, player_name=player_name, lang=lang, guideline_list=guideline_list))
    
    # 메모리 리스트가 있으면 최근 10개만 사용
    if memory_list:
        recent_memory = memory_list[:10]  # 최근 10개만
        for m in recent_memory:
            if m['speaker'] == 'player':
                messages.append({"role": "user", "content": m['message']})
            elif m['speaker'] == 'character':
                messages.append({"role": "model", "content": m['message']})
    else:
        # memory.get_memory_message_list는 사용하지 않음 (짧은 버전이므로)
        pass
    
    # 현재 사용자 질문
    messages.append({"role": "user", "content": query})
    
    # Gemma3 형식으로 변환
    prompt = '<bos>'
    for message in messages:
        if message['role'] == 'system':
            prompt += add_gemma_prompt('system', message['content']) + "\n"
        elif message['role'] == 'user':
            prompt += add_gemma_prompt('user', message['content']) + "\n"
        elif message['role'] == 'model':
            prompt += add_gemma_prompt('model', message['content']) + "\n"
    
    prompt += '<start_of_turn>model\n'
    
    return prompt

def get_gemma_prompt(query, player_name=None, char_name=None, info_img=None, memory_list=None, lang=None, guideline_list=list(), situation_dict={}, is_sfw=False):   
    def add_gemma_prompt(role, text):
        return f"<start_of_turn>{role}\n{text}<end_of_turn>"
    
    messages = list()
    
    # 시스템 메시지 추가 (is_sfw 플래그 전달)
    messages.extend(prompt_main.get_message_list_main(char_name, player_name=player_name, lang=lang, info_img=info_img, guideline_list=guideline_list, situation_dict=situation_dict, is_sfw=is_sfw))

    # 메모리 메시지
    if memory_list:
        for m in memory_list:
            if m['speaker'] == 'player':
                messages.append({"role": "user", "content": m['message']})
            elif m['speaker'] == 'character':
                messages.append({"role": "model", "content": m['message']})
    else:
        messages.extend(memory.get_memory_message_list(8192, lang=lang))

    # 현재 사용자 질문
    messages.append({"role": "user", "content": query})

    # 전체 프롬프트 구성
    prompt = "<bos>"
    for message in messages:
        if message['role'] in ('system', 'user', 'model'):
            prompt += add_gemma_prompt(message['role'], message['content']) + "\n"

    # 답변 유도를 위해 assistant 시작 토큰 붙여줌
    prompt += "<start_of_turn>model\n"
    
    return prompt

def get_gemma_multi_prompt(
    query, 
    current_speaker=None, 
    target_speaker=None, 
    target_listener="all",
    participants=None, 
    context=None, 
    info_img=None, 
    memory_list=None, 
    lang='en', 
    guideline_list=None, 
    situation_dict=None, 
    player_name='sensei'
):
    """
    다중 캐릭터 대화용 Gemma 프롬프트 생성
    
    Args:
        query: 사용자 질문
        current_speaker: 현재 발화자
        target_speaker: 답변할 캐릭터
        target_listener: 대화 대상 ("all", "sensei", "arona", "plana" 등)
        participants: 참여자 리스트
        context: 대화 컨텍스트
        info_img: 이미지 정보
        memory_list: 메모리 리스트  
        lang: 언어 ('ko', 'ja', 'en')
        guideline_list: 사용자 가이드라인
        situation_dict: 상황 설정
        player_name: 플레이어 이름
        
    Returns:
        str: Gemma 포맷 프롬프트
    """
    participants = participants or []
    
    # 단일 캐릭터인 경우 기존 함수 사용 (참여자 2명 이하만 체크)
    if len(participants) <= 2:
        return get_gemma_prompt(
            query, 
            player_name=current_speaker, 
            char_name=target_speaker,
            info_img=info_img,
            memory_list=memory_list,
            lang=lang,
            guideline_list=guideline_list,
            situation_dict=situation_dict
        )
    
    # 다중 캐릭터 메시지 생성
    messages = prompt_multi.get_multi_character_messages(
        query=query,
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
    
    # target_participant 찾기
    target_participant = None
    if target_speaker:
        target_participant = next((p for p in participants if p["name"] == target_speaker), None)
    
    # Gemma 포맷으로 조합
    def add_gemma_prompt(role, text):
        return f"<start_of_turn>{role}\n{text}<end_of_turn>"
    
    # Gemma는 system을 user로 처리
    prompt = "<bos>"
    for message in messages:
        if message['role'] == 'system':
            prompt += add_gemma_prompt('user', message['content']) + "\n"
        elif message['role'] == 'user':
            prompt += add_gemma_prompt('user', message['content']) + "\n"
        elif message['role'] == 'assistant':
            prompt += add_gemma_prompt('model', message['content']) + "\n"
    
    # 응답 시작 토큰 추가
    prompt += "<start_of_turn>model\n"
    # display_name = target_participant.get('display_name', target_speaker)
    # prompt += f'<start_of_turn>model\n[{display_name}]:'
    
    return prompt

# chatGPT는 list 반환임
def get_chatGPT_prompt(query, player_name=None, char_name=None, info_img=None, memory_list=None, lang=None):
    messages = []

    # 시스템 메시지 등 기본 세팅
    messages.extend(prompt_main.get_message_list_main(char_name, player_name=player_name, lang=lang, info_img=info_img))

    # memory_list가 존재하면 그걸 우선 사용
    if memory_list:
        for m in memory_list:
            if m['speaker'] == 'player':
                messages.append({"role": "user", "content": m['message']})
            elif m['speaker'] == 'character':
                messages.append({"role": "assistant", "content": m['message']})
    else:
        messages.extend(memory.get_memory_message_list(4096, lang=lang))

    # 현재 유저 질문 추가
    messages.append({"role": "user", "content": query})

    return messages

'''
<｜begin▁of▁sentence｜>{system_prompt}<｜User｜>{prompt}<｜Assistant｜><｜end▁of▁sentence｜><｜Assistant｜>
'''
def get_deepseek_prompt(query, player_name=None, char_name=None, info_img=None, memory_list=None, lang=None):       
    def add_deepseek_prompt(speaker_type, text):
        if speaker_type == 'user':
            return f'<｜User｜>{text}'
        else:
            return f'<｜Assistant｜>{text}'
    
    messages = list() 
    messages.extend(prompt_main.get_message_list_main(char_name, lang))
    # print('memory_list', memory_list)
    if memory_list:  # 외부 반입(server_interface)
        for m in memory_list:
            if m['speaker'] == 'player':
                messages.append({"role": "user", "content": m['message']})
            elif m['speaker'] == 'character':
                messages.append({"role": "assistant", "content": m['message']})    
    else:
        messages.extend(memory.get_memory_message_list(8192, lang=lang))
    messages.append({"role": "user", "content": query})
    
    # jinja template 없이
    prompt = '<｜begin▁of▁sentence｜>'
    # system 먼저
    for message in messages:
        if message['role'] in ('system'):
            prompt += message['content'] + "\n"
    
    # message
    for message in messages:
        if message['role'] in ('user', 'assistant'):
            prompt += add_deepseek_prompt(message['role'], message['content']) + "\n"
    prompt += '<｜end▁of▁sentence｜><｜Assistant｜>'  # add_generation_prompt
    
    return prompt

'''
<|begin_of_text|><|start_header_id|>system<|end_header_id|>
{system_prompt}<|eot_id|><|start_header_id|>user<|end_header_id|>
{prompt}<|eot_id|><|start_header_id|>assistant<|end_header_id|>
'''
def get_LLAMA3_prompt(query, player_name=None, char_name=None, info_img=None, memory_list=None, lang=None):
    LLAMA3_TEMPLATE = "{% set loop_messages = messages %}{% for message in loop_messages %}{% set content = '<|start_header_id|>' + message['role'] + '<|end_header_id|>\n\n'+ message['content'] | trim + '<|eot_id|>' %}{% if loop.index0 == 0 %}{% set content = bos_token + content %}{% endif %}{{ content }}{% endfor %}{% if add_generation_prompt %}{{ '<|start_header_id|>assistant<|end_header_id|>\n\n' }}{% endif %}"
    LLM_STOP_SEQUENCE = "<|eot_id|>"
       
    messages = list() 
    messages.extend(prompt_main.get_message_list_main(char_name, lang))
    # print('memory_list', memory_list)
    if memory_list:  # 외부 반입(server_interface)
        for m in memory_list:
            if m['speaker'] == 'player':
                messages.append({"role": "user", "content": m['message']})
            elif m['speaker'] == 'character':
                messages.append({"role": "assistant", "content": m['message']})    
    else:
        messages.extend(memory.get_memory_message_list(4096))
    messages.append({"role": "user", "content": query})

    template = Template(LLAMA3_TEMPLATE)
    prompt = template.render(
                    messages=messages,
                    bos_token="<|begin_of_text|>",
                    add_generation_prompt=True,  # <|im_start|>assistant를 마지막에 붙이는거
    )
    
    return prompt
    
def get_LLAMA_3_image_prompt(query, info_img):
    import ai_florence 
    import prompt_main
    import memory
    from jinja2 import Template

    LLAMA3_TEMPLATE = "{% set loop_messages = messages %}{% for message in loop_messages %}{% set content = '<|start_header_id|>' + message['role'] + '<|end_header_id|>\n\n'+ message['content'] | trim + '<|eot_id|>' %}{% if loop.index0 == 0 %}{% set content = bos_token + content %}{% endif %}{{ content }}{% endfor %}{% if add_generation_prompt %}{{ '<|start_header_id|>assistant<|end_header_id|>\n\n' }}{% endif %}"
    LLM_STOP_SEQUENCE = "<|eot_id|>"
       
    messages = list() 
    
    messages.append({"role": "system", "content" : """A chat between user and artificial intelligence assistant. The assistant is a cute girl who never confident about facts. The assistant has image info for answering chat. The assistant uses the available tools to retrieve relevant information and give helpful, detailed, and polite answers to the user's questions. The assistant simply answers the question succinctly and makes no reference to the source or rationale.

Image info tool command format: Image_info
```plaintext
<|info|>
```"""})
    # messages.extend(prompt_main.get_message_list_main())  # 하더라도 prompt 바꾸는게 나을것 같음
    # messages.extend(memory.get_memory_message_list(4096))
    messages.append({"role": "user", "content": query})
    image_info = ai_florence.get_image_info(info_img)
    # messages.append({"role": "assistant", "content": 'Image_info("<'+image_info+'>")'})
      
    template = Template(LLAMA3_TEMPLATE)
    prompt = template.render(
                    messages=messages,
                    bos_token="<|begin_of_text|>",
                    add_generation_prompt=False,  # <|im_start|>assistant를 마지막에 붙이는거
    )
    prompt = prompt + """<|eot_id|><|start_header_id|>assistant<|end_header_id|>
    
Image_info
```plaintext\n"""+ image_info + """\n```<|eot_id|><|start_header_id|>assistant<|end_header_id|>"""
    
    return prompt

def get_qwen_multi_prompt(
    query, 
    current_speaker=None, 
    target_speaker=None, 
    target_listener="all",
    participants=None, 
    context=None, 
    info_img=None, 
    memory_list=None, 
    lang='en', 
    guideline_list=None, 
    situation_dict=None, 
    player_name='sensei'
):
    """
    다중 캐릭터 대화용 Qwen 프롬프트 생성
    
    Args:
        query: 사용자 질문
        current_speaker: 현재 발화자
        target_speaker: 답변할 캐릭터
        target_listener: 대화 대상 ("all", "sensei", "arona", "plana" 등)
        participants: 참여자 리스트
        context: 대화 컨텍스트
        info_img: 이미지 정보
        memory_list: 메모리 리스트  
        lang: 언어 ('ko', 'ja', 'en')
        guideline_list: 사용자 가이드라인
        situation_dict: 상황 설정
        player_name: 플레이어 이름
        
    Returns:
        str: Qwen 포맷 프롬프트
    """
    participants = participants or []
    
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
    
    # 다중 캐릭터 메시지 생성
    messages = prompt_multi.get_multi_character_messages(
        query=query,
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
    
    # target_participant 찾기
    target_participant = None
    if target_speaker:
        target_participant = next((p for p in participants if p["name"] == target_speaker), None)
    
    # Qwen 포맷으로 조합
    def add_chatLM_prompt(speaker_type, text):
        return f"<|im_start|>{speaker_type}\n{text}<|im_end|>"
    
    prompt = ''
    for message in messages:
        if message['role'] in ('system', 'user', 'assistant'):
            prompt += add_chatLM_prompt(message['role'], message['content']) + "\n"
    
    # 응답 시작 토큰 추가
    prompt += '<|im_start|>assistant\n'
    # display_name = target_participant.get('display_name', target_speaker)
    # prompt += f'<|im_start|>assistant\n[{display_name}]:'
    
    return prompt