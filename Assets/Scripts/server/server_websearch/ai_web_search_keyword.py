from threading import Lock

import state

from ai_singleton import check_llm, get_llm

import util_string

generation_lock = Lock()

def load_model(is_use_cuda=False):
    get_llm()
    # if state.get_use_gpu_percent() != 0:  # gpu 사용여부 확인 (0이 아님)
    #     llm = get_llm()
    # elif not check_llm() or is_use_cuda:  # 초기화 여부
    #     llm = get_llm()
    # else:
    #     from ai_llama_cpp_model import LlamaCppModel 
    #     llm, tokenizer = LlamaCppModel.from_pretrained('./model/Meta-Llama-3.1-8B-Instruct-Q4_K_M.gguf')

'''
<|im_start|>system
{system_prompt}<|im_end|>
<|im_start|>user
{prompt}<|im_end|>
<|im_start|>assistant
'''
def get_qwen_prompt(question, description, lang='en'):
    def get_ko_rule():
        return """당신은 이미지 설명과 질문을 기반으로 관련된 검색 키워드를 생성하는 AI입니다.
키워드는 핵심적인 정보만 간결하게 포함해야 하며, 일반적인 문장이 아닌 검색 엔진에서 사용할 짧은 형태로 작성되어야 합니다.
이미지와 질문을 모두 고려해, 실제 검색 엔진에서 유용하게 사용할 수 있는 키워드를 만들어주세요.
결과는 다음 형식으로만 응답하십시오. 추가 설명은 포함하지 마십시오:  
keyword: 생성키워드

예시:  

설명: The image is a black rectangular logo with the word "Nvidia" written in white capital letters in the center. The word "Cuda" is written in a larger font size than the rest of the text. On the left side of the logo, there is a green logo of the NVIDIA brand, which is a stylized letter "N" with a curved line running through it. The logo is set against a black background. The overall design is simple and modern.
질문: 이 회사 주식 가격 좀 알려줘.
결과:
keyword: Nvidia 주가

설명: The image shows a bowl of colorful salad with chopped vegetables, grains, and a bottle of olive oil on the side. Ingredients like quinoa, tomato, and avocado are clearly visible.
질문: 이거 만드는 법 알려줘.
결과:
keyword: 퀴노아 샐러드 레시피

설명: The image displays a new smartphone with triple cameras, a shiny black finish, and a visible brand logo reading “Galaxy S23 Ultra”.
질문: 가격 좀 알려줘.
결과:
keyword: 갤럭시 S23 Ultra 가격

설명: The image contains a block of text written in Latin script that says “Carpe Diem” in large calligraphy, with a background of an old manuscript.
질문: 이거 무슨 뜻이야?
결과:
keyword: Carpe Diem 뜻"""

    def get_jp_rule():
        return """あなたは画像の説明と質問に基づいて、関連する検索キーワードを生成するAIです。
キーワードは、重要な情報のみを含む簡潔な表現で、検索エンジンで使用できる短い形式で記述してください。
画像と質問の両方を考慮し、実際の検索で役立つキーワードを作成してください。
以下の形式でのみ返答してください。説明などは一切加えないでください：  
keyword: 生成されたキーワード

例：

説明: The image is a black rectangular logo with the word "Nvidia" written in white capital letters in the center. The word "Cuda" is written in a larger font size than the rest of the text. On the left side of the logo, there is a green logo of the NVIDIA brand, which is a stylized letter "N" with a curved line running through it. The logo is set against a black background. The overall design is simple and modern.
質問: この会社の株価を教えて。  
結果:  
keyword: Nvidia 株価

説明: The image shows a bowl of colorful salad with chopped vegetables, grains, and a bottle of olive oil on the side. Ingredients like quinoa, tomato, and avocado are clearly visible.
質問: 作り方を教えて。  
結果:  
keyword: キヌア サラダ レシピ

説明: The image displays a new smartphone with triple cameras, a shiny black finish, and a visible brand logo reading “Galaxy S23 Ultra”.
質問: これの値段は？  
結果:  
keyword: Galaxy S23 Ultra 価格

説明: The image contains a block of text written in Latin script that says “Carpe Diem” in large calligraphy, with a background of an old manuscript.
質問: どういう意味？  
結果:  
keyword: Carpe Diem 意味"""
    
    
    def get_en_rule():
        return """You are an AI that generates relevant search keywords based on an image description and a user question.
The keyword should be concise, focused only on the essential information, and written in a short phrase format suitable for search engines.
Consider both the image and the question to create a useful keyword for real-world search.
Respond only in the following format. Do not include any explanation:  
keyword: [generated_keyword]

Examples:

Description: The image is a black rectangular logo with the word "Nvidia" written in white capital letters in the center. The word "Cuda" is written in a larger font size than the rest of the text. On the left side of the logo, there is a green logo of the NVIDIA brand, which is a stylized letter "N" with a curved line running through it. The logo is set against a black background. The overall design is simple and modern.  
Question: Tell me the stock price of this company.  
Result:  
keyword: Nvidia stock price

Description: The image shows a bowl of colorful salad with chopped vegetables, grains, and a bottle of olive oil on the side. Ingredients like quinoa, tomato, and avocado are clearly visible.  
Question: How do I make this?  
Result:  
keyword: quinoa salad recipe

Description: The image displays a new smartphone with triple cameras, a shiny black finish, and a visible brand logo reading “Galaxy S23 Ultra”.  
Question: What is the price?  
Result:  
keyword: Galaxy S23 Ultra price

Description: The image contains a block of text written in Latin script that says “Carpe Diem” in large calligraphy, with a background of an old manuscript.  
Question: What does this mean?  
Result:  
keyword: Carpe Diem meaning"""
    
    def get_ko_prompt_body(question, description):
        return f"""<|im_start|>user
설명: "{description}"
질문: "{question}"<|im_end|>
<|im_start|>assistant
결과:"""

    def get_jp_prompt_body(question, description):
        return f"""<|im_start|>user
説明: "{description}"
質問: "{question}"<|im_end|>
<|im_start|>assistant
結果:"""

    def get_en_prompt_body(question, description):
        return f"""<|im_start|>user
Description: "{description}"
Question: "{question}"<|im_end|>
<|im_start|>assistant
Result:"""
    
    
    rule = get_en_rule()  # 기본 영어 rule
    if lang == 'ko':
        rule = get_ko_rule()
    elif lang == 'ja' or lang == 'jp':
        rule = get_jp_rule()
        
    prompt_body = get_en_prompt_body(question, description)
    if lang == 'ko':
        prompt_body = get_ko_prompt_body(question, description)
    elif lang == 'ja' or lang == 'jp':
        prompt_body = get_jp_prompt_body(question, description)
    
    prompt = f"""<|im_start|>system
{rule}<|im_end|>
{prompt_body}
"""

    return prompt

'''
<think></think>가 있을 경우, 분리해서 reponse 부분과 분리한다.
'''
def parse_response(text: str) -> dict:
    if "</think>" in text:
        think_part, response_part = text.split("</think>", 1)
        think_part = think_part.replace("<think>", "").strip()
        response_part = response_part.strip()
    else:
        think_part = ""
        response_part = text.strip()

    return {"think": think_part, "response": response_part}

# 이미지 설명과 질문을 기반으로 검색 키워드 생성
def process(question, description, lang='en'):
    try:
        from jinja2 import Template
        llm = get_llm()
            
        prompt = get_qwen_prompt(question, description, lang)

        stop_keywords = ["<|im_end|>", "<|eot_id|>", "question:", "Question:"]
        state = {
            # max_tokens=128,  
            'max_new_tokens' : '4096',
            # stop=["Q:", "\n"],
            # stop=[f"sensei:",f"sensei(","<|im_","user:", "#", ":"],
            # 'stop':stop_keywords,
            'temperature' : '0'
        }
        output = llm.generate(
            prompt, state
        )
        # print('output :', output)
        # parsed_output = parse_response(output)
        
        output = util_string.remove_think_tag(output)
        
        # 단어있으면 최후의 체크
        # for stop_keyword in output:
        #     if stop_keyword in result:
        #         result = result.split(stop_keyword)[0]
        
        

        
        # 문장체크
        
        # 감정체크
        
        # 정제 [음성용으로 따로 뱉던가.]
        # import re
        # result = result.replace("\n",'')
        # result = re.sub(r'\([^)]*\)', '', result)  # ()와 안의 내용물 제거
        # result = re.sub(r'\[[^)]*\]', '', result)  # []와 안의 내용물 제거
        # result = re.sub(r'\*[^)]*\*', '', result)  # * *과 안의 내용물 제거
        
        # print('prompt', prompt)

        return output
    except Exception as e:
        print(f'[ERROR] Keyword generation failed: {e}')
        return ""  # 키워드 생성 실패 시 빈 문자열 반환


if __name__ == "__main__":
    # 모델 로딩
    # is_use_cuda=False
    is_use_cuda=True
    state.set_use_gpu_percent(8)
    # state.model_name='Qwen2.5-14B-Instruct-1M-Q4_K_M.gguf'
    state.model_name='Qwen3-14B-Q4_K_M.gguf'
    # state.model_name='Qwen3-32B-Q4_K_M.gguf'
    load_model(is_use_cuda)

    # 영어 테스트
    if True:
        question = "do you like coffee?"
        description = "The image is of a black briefcase. The briefcase is made of leather and has a shiny finish. It has a rectangular shape with rounded edges and a handle on top for easy carrying. "
        response = process(question, description)
        print('===============')
        print('question : ', question)
        print('description : ', description)
        print('response\n', response)
        
        question = "Can you see the time?"
        description = "The image is of a black briefcase. The briefcase is made of leather and has a shiny finish. It has a rectangular shape with rounded edges and a handle on top for easy carrying. "
        response = process(question, description)
        print('===============')
        print('question : ', question)
        print('description : ', description)
        print('response\n', response)    
        
        question = "where is capital of france?"
        description = "The image is of a black briefcase. The briefcase is made of leather and has a shiny finish. It has a rectangular shape with rounded edges and a handle on top for easy carrying. "
        response = process(question, description)
        print('===============')
        print('question : ', question)
        print('description : ', description)
        print('response\n', response)
        
        question = "do you no hoshino?"
        description = "The image is of a black briefcase. The briefcase is made of leather and has a shiny finish. It has a rectangular shape with rounded edges and a handle on top for easy carrying."
        response = process(question, description)
        print('===============')
        print('question : ', question)
        print('description : ', description)
        print('response\n', response)
        
        question = "Describe it"
        description = "The image is of a black briefcase. The briefcase is made of leather and has a shiny finish. It has a rectangular shape with rounded edges and a handle on top for easy carrying. "
        response = process(question, description)
        print('===============')
        print('question : ', question)
        print('description : ', description)
        print('response\n', response)
        
    # 일본어 테스트
    if True:
        question = "コーヒーは好きですか？"
        description = "画像は黒いブリーフケースです。革製で光沢があります。長方形の形状で、角が丸く、上部に持ち手があり持ち運びやすいです。"
        response = process(question, description, lang='jp')
        print('===============')
        print('質問 :', question)
        print('説明 :', description)
        print('応答\n', response)

        question = "時間が見えますか？"
        description = "画像は黒いブリーフケースです。革製で光沢があります。長方形の形状で、角が丸く、上部に持ち手があり持ち運びやすいです。"
        response = process(question, description, lang='jp')
        print('===============')
        print('質問 :', question)
        print('説明 :', description)
        print('応答\n', response)

        question = "フランスの首都はどこですか？"
        description = "画像は黒いブリーフケースです。革製で光沢があります。長方形の形状で、角が丸く、上部に持ち手があり持ち運びやすいです。"
        response = process(question, description, lang='jp')
        print('===============')
        print('質問 :', question)
        print('説明 :', description)
        print('応答\n', response)

        question = "星野を知っていますか？"
        description = "The image is of a black briefcase. The briefcase is made of leather and has a shiny finish. It has a rectangular shape with rounded edges and a handle on top for easy carrying."
        response = process(question, description, lang='jp')
        print('===============')
        print('質問 :', question)
        print('説明 :', description)
        print('応答\n', response)

        question = "説明してください。"
        description = "The image is of a black briefcase. The briefcase is made of leather and has a shiny finish. It has a rectangular shape with rounded edges and a handle on top for easy carrying."
        response = process(question, description, lang='jp')
        print('===============')
        print('質問 :', question)
        print('説明 :', description)
        print('応答\n', response)
    
    # 한국어 테스트
    if True:
        question = "커피 좋아하세요?"
        description = "이미지는 검정색 서류 가방입니다. 가방은 가죽으로 만들어졌고 광택이 있습니다. 직사각형 모양에 모서리가 둥글고, 위쪽에 손잡이가 있습니다."
        response = process(question, description, lang='ko')
        print('===============')
        print('질문:', question)
        print('설명:', description)
        print('응답\n', response)

        question = "시간 알 수 있을까?"
        description = "이미지는 검정색 서류 가방입니다. 가방은 가죽으로 만들어졌고 광택이 있습니다. 직사각형 모양에 모서리가 둥글고, 위쪽에 손잡이가 있습니다."
        response = process(question, description, lang='ko')
        print('===============')
        print('질문:', question)
        print('설명:', description)
        print('응답\n', response)

        question = "프랑스의 수도는 어디인가요?"
        description = "이미지는 검정색 서류 가방입니다. 가방은 가죽으로 만들어졌고 광택이 있습니다. 직사각형 모양에 모서리가 둥글고, 위쪽에 손잡이가 있습니다."
        response = process(question, description, lang='ko')
        print('===============')
        print('질문:', question)
        print('설명:', description)
        print('응답\n', response)

        question = "호시노를 아세요?"
        description = "The image is of a black briefcase. The briefcase is made of leather and has a shiny finish. It has a rectangular shape with rounded edges and a handle on top for easy carrying."
        response = process(question, description, lang='ko')
        print('===============')
        print('질문:', question)
        print('설명:', description)
        print('응답\n', response)

        question = "설명해 주세요."
        description = "The image is of a black briefcase. The briefcase is made of leather and has a shiny finish. It has a rectangular shape with rounded edges and a handle on top for easy carrying."
        response = process(question, description, lang='ko')
        print('===============')
        print('질문:', question)
        print('설명:', description)
        print('응답\n', response)
        
        import ai_singleton
        ai_singleton.release()
    
