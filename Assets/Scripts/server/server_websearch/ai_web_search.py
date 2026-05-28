import os
from ai_web_search_module import LangchainCompressor, langchain_search_duckduckgo, Generator, get_webpage_content
import html
import re
import gc
from datetime import datetime
from queue import Queue
import traceback
from threading import Thread, Lock

# Local
import util_string
import state as st

from ai_singleton import get_llm
import util_searcher

web_searcher = util_searcher.WebSearcher()

# 웹 검색 메타데이터 저장소 (글로벌 state)
web_search_metadata = {
    'keyword': '',
    'method': '',
    'content': '',
    'llm_generated': False
}

# 메타데이터 초기화
def reset_metadata():
    global web_search_metadata
    web_search_metadata = {
        'keyword': '',
        'method': '',
        'content': '',
        'llm_generated': False
    }

# 메타데이터 가져오기
def get_metadata():
    return web_search_metadata.copy()

langchain_compressor = None
generation_lock = Lock()

def load_model(is_use_cuda=False):
    global langchain_compressor
    start_compressor()
    
    get_llm()

# stream 밖에 없기 때문에 그대로 is_sentence 적용
def process(query, info_img=None, lang='en', web_keyword=None):    
    llm = get_llm()
    for j, reply_list in enumerate(generate_reply(query, lang, info_img=info_img, web_keyword=web_keyword)):
        visible_reply_list = list()
        for reply in reply_list:
            visible_reply = reply
            visible_reply = re.sub("(<USER>|<user>|{{user}})", 'You', visible_reply)
            # visible_reply = visible_reply.replace("\n",'')  # 음성쪽은 제거해야함 (그쪽에 추가)
            visible_reply = re.sub(r'\([^)]*\)', '', visible_reply)  # ()와 안의 내용물 제거
            visible_reply = re.sub(r'\[[^)]*\]', '', visible_reply)  # []와 안의 내용물 제거
            visible_reply = re.sub(r'\*[^)]*\*', '', visible_reply)  # * *과 안의 내용물 제거
            visible_reply = visible_reply.lstrip(' ')
            visible_reply_list.append(visible_reply)
        yield visible_reply_list

# Loading시 시작해야함, 원본 seach LLM extension script.py의 toggle_extension
def start_compressor():
    global langchain_compressor#, custom_system_message_filename
    extension_path = os.path.dirname(os.path.abspath(__file__))
    langchain_compressor = LangchainCompressor(device="cpu",
                                                keyword_retriever="bm25", #  params["keyword retriever"],
                                                model_cache_dir=os.path.join(extension_path, "model"))
    compressor_model = langchain_compressor.embeddings.client
    compressor_model.to(compressor_model._target_device)
    

# From LLM websearch  
# generate_func 추가
def custom_generate_reply(question, original_question, seed, state, stopping_strings, is_chat, generate_func, web_keyword=None):
    global langchain_compressor
    if langchain_compressor is None:
        start_compressor()

    params = {
    "display_name": "LLM Web Search",
    "is_tab": True,
    "enable": True,
    "search results per query": 5,
    "langchain similarity score threshold": 0.5,
    "instant answers": True,
    "regular search results": True,
    "search command regex": "",
    "default search command regex": r"Search_web\(\"(.*)\"\)",
    "open url command regex": "",
    "default open url command regex": r"Open_url\(\"(.*)\"\)",
    "display search results in chat": True,
    "display extracted URL content in chat": True,
    "searxng url": "",
    "cpu only": True,
    "chunk size": 500,
    "duckduckgo results per query": 10,
    "append current datetime": False,
    "default system prompt filename": None,
    "force search prefix": "Search_web",
    "ensemble weighting": 0.5,
    "keyword retriever": "bm25",
    "splade batch size": 2,
    "chunking method": "character-based",
    "chunker breakpoint_threshold_amount": 30
    }
    
    # if shared.model.__class__.__name__ in ['LlamaCppModel', 'RWKVModel', 'ExllamaModel', 'Exllamav2Model',
    #                                        'CtransformersModel']:
    #     generate_func = generate_reply_custom 
    # else:
    #     generate_func = generate_reply_HF

    # if not params['enable']:  # 당근 enable이니까 여길 왔지
    #     for reply in generate_func(question, original_question, seed, state, stopping_strings, is_chat=is_chat):
    #         yield reply
    #     return
    
    generate_func = generate_func

    web_search = False
    read_webpage = False
    # max_search_results = int(params["search results per query"])
    max_search_results = 6
    # instant_answers = params["instant answers"]
    instant_answers = True
    # regular_search_results = params["regular search results"]  # 일단 True인데 처음부터 비활성화

    # langchain_compressor.num_results = int(params["duckduckgo results per query"])
    langchain_compressor.num_results = 10
    langchain_compressor.similarity_threshold = params["langchain similarity score threshold"]
    langchain_compressor.chunk_size = params["chunk size"]
    langchain_compressor.ensemble_weighting = params["ensemble weighting"]
    langchain_compressor.splade_batch_size = params["splade batch size"]
    langchain_compressor.chunking_method = params["chunking method"]
    langchain_compressor.chunker_breakpoint_threshold_amount = params["chunker breakpoint_threshold_amount"]

    search_command_regex = params["search command regex"]
    open_url_command_regex = params["open url command regex"]
    searxng_url = params["searxng url"]  # ""
    display_search_results = params["display search results in chat"]
    display_webpage_content = params["display extracted URL content in chat"]

    if search_command_regex == "":
        search_command_regex = params["default search command regex"]
    if open_url_command_regex == "":
        open_url_command_regex = params["default open url command regex"]

    import re
    compiled_search_command_regex = re.compile(search_command_regex)
    compiled_open_url_command_regex = re.compile(open_url_command_regex)

    # force_search = True
    # if force_search:
    #     question += f" {params['force search prefix']}"
    question += " Search_web"

    state = {
        # max_tokens=128,  
        'max_new_tokens' : '4096',
        # stop=["Q:", "\n"],
        # stop=[f"sensei:",f"sensei(","<|im_","user:", "#", ":"],
        # 'stop':stop_keywords,
        'temperature' : '0'
    }
    
    print('###question_web', question)
    reply = None
    # for reply in generate_func(question, original_question, seed, state, stopping_strings, is_chat=is_chat):
    for reply in generate_func(question, state):

        # if force_search:
        #     reply = params["force search prefix"] + reply
        reply = params["force search prefix"] + reply

        search_re_match = compiled_search_command_regex.search(reply)
        if search_re_match is not None:
            yield reply
            original_model_reply = reply
            web_search = True
            search_term = search_re_match.group(1)
            
            # 메타데이터: LLM이 Search_web 생성함
            global web_search_metadata
            web_search_metadata['llm_generated'] = True
            
            # 검색 수행
            if web_keyword:
                print(f"### LLM_Web_search_keyword | Searching for {web_keyword}")
                search_result = web_searcher.search_reply(web_keyword, langchain_compressor, max_search_results, instant_answers)
                web_search_metadata['keyword'] = web_keyword
            else:
                print(f"### LLM_Web_search | Searching for {search_term}")
                search_result = web_searcher.search_reply(search_term, langchain_compressor, max_search_results, instant_answers)
                web_search_metadata['keyword'] = search_term
            
            # 메타데이터 저장
            web_search_metadata['method'] = search_result['method']
            web_search_metadata['content'] = search_result['content']
            
            # reply에 검색 결과 추가
            reply += search_result['reply']
            
            # reply += "\n```plaintext"
            # reply += "\nSearch tool:\n"

            # search_generator = Generator(langchain_search_duckduckgo(search_term,
            #                                                             langchain_compressor,
            #                                                             max_search_results,
            #                                                             instant_answers)) 
            # try:
            #     for status_message in search_generator:
            #         yield original_model_reply + f"\n*{status_message}*"
            #     search_results = search_generator.value
            # except Exception as exc:
            #     exception_message = str(exc)
            #     reply += f"The search tool encountered an error: {exception_message}"
            #     print(f'LLM_Web_search | {search_term} generated an exception: {exception_message}')
            # else:
            #     if search_results != "":
            #         reply += search_results
            #     else:
            #         reply += f"\nThe search tool did not return any results."
            # reply += "```"
            if display_search_results:
                yield reply
            break
        
        open_url_re_match = compiled_open_url_command_regex.search(reply)
        if open_url_re_match is not None:
            yield reply
            original_model_reply = reply
            read_webpage = True
            url = open_url_re_match.group(1)
            print(f"LLM_Web_search | Reading {url}")
            reply += "\n```plaintext"
            reply += "\nURL opener tool:\n"
            try:
                webpage_content = get_webpage_content(url)
            except Exception as exc:
                reply += f"Couldn't open {url}. Error message: {str(exc)}"
                print(f'LLM_Web_search | {url} generated an exception: {str(exc)}')
            else:
                reply += f"\nText content of {url}:\n"
                reply += webpage_content
            reply += "```\n"
            if display_webpage_content:
                yield reply
            break
        yield reply

    if web_search or read_webpage:
    #     display_results = web_search and display_search_results or read_webpage and display_webpage_content
        display_results = False
        # Add results to context and continue model output
        # new_question = chat.generate_chat_prompt(f"{question}{reply}", state)
        
        # ChatLM Style
#         new_question = question + reply + """<|im_end|>
# <|im_start|>assistant
# AI:
#         """


        # LLAMA3 Style
#         new_question = question + reply + """<|eot_id|><|start_header_id|>assistant<|end_header_id|>

# """     
        # Qwen Style
        new_question = question + reply + """<|im_start|>assistant
"""     
        # print('###new_question', new_question)
        
        new_reply = ""
        # for new_reply in generate_func(new_question, new_question, seed, state, stopping_strings, is_chat=is_chat):
        for new_reply in generate_func(new_question, state):
            # print('###new_reply', new_reply)
            if display_results:
                yield f"{reply}\n{new_reply}"
            else:# 여기임
                # yield f"{original_model_reply}\n{new_reply}"
                yield f"{new_reply}"

        # if not display_results:
        #     update_history = [state["textbox"], f"{reply}\n{new_reply}"]

def apply_stopping_strings(reply, all_stop_strings = ['\nYou:', '<|im_end|>\n<|im_start|>user\n', '<|im_end|>\n<|im_start|>assistant\n', '\nAI:']):
    stop_found = False
    for string in all_stop_strings:
        idx = reply.find(string)
        if idx != -1:
            reply = reply[:idx]
            stop_found = True
            break

    if not stop_found:
        # If something like "\nYo" is generated just before "\nYou:"
        # is completed, trim it
        for string in all_stop_strings:
            for j in range(len(string) - 1, 0, -1):
                if reply[-j:] == string[:j]:
                    reply = reply[:-j]
                    break
            else:
                continue

            break

    return reply, stop_found

def generate_reply(*args, **kwargs):
    global generation_lock
    # shared.generation_lock.acquire()
    generation_lock.acquire()
    try:
        for result in _generate_reply(*args, **kwargs):
            yield result
    finally:
        pass
        generation_lock.release()

'''
<|im_start|>system
{prompt}<|im_end|>
<|im_start|>user
{prompt}<|im_end|>
<|im_start|>assistant
{prompt}<|im_end|>
'''
def get_ChatLM_question(question):
    question = """<|im_start|>system
A chat between a curious user and artificial intelligence assistant. The assistant is never confident about facts. The assistant always searches the web for facts. The assistant uses the available tools to retrieve relevant information and give helpful, detailed, and polite answers to the user's questions. The assistant simply answers the question succinctly and makes no reference to the source or rationale.

Search tool command format: Search_web("<|query|>")

Date and time of conversation: """+ str(datetime.now().strftime('%A %d %B %Y %H:%M'))+"""<|im_end|>
<|im_start|>user
Continue the chat dialogue below. Write a single reply for the character "AI".

The following is a conversation with an AI Large Language Model. The AI has been trained to answer questions, provide recommendations, and help with decision making.

AI: How can I help you today?
You: """ + question + """<|im_end|>
<|im_start|>assistant
AI:"""
    return question

'''
<|begin_of_text|><|start_header_id|>system<|end_header_id|>
{system_prompt}<|eot_id|><|start_header_id|>user<|end_header_id|>
{prompt}<|eot_id|><|start_header_id|>assistant<|end_header_id|>
'''
def get_LLAMA3_question(question):
    question = """<|begin_of_text|><|start_header_id|>system<|end_header_id|>

A chat between a curious user and artificial intelligence assistant. The assistant is never confident about facts. The assistant always searches the web for facts. The assistant uses the available tools to retrieve relevant information and give helpful, detailed, and polite answers to the user's questions. The assistant simply answers the question succinctly and makes no reference to the source or rationale.

Search tool command format: Search_web("<|query|>")

Date and time of conversation: """+str(datetime.now().strftime('%A %d %B %Y %H:%M'))+"""<|eot_id|><|start_header_id|>system<|end_header_id|>

Continue the chat dialogue below. Write a single reply for the character "AI".

The following is a conversation with an AI Large Language Model. The AI has been trained to answer questions, provide recommendations, and help with decision making.<|eot_id|><|start_header_id|>assistant<|end_header_id|>

How can I help you today?<|eot_id|><|start_header_id|>user<|end_header_id|>

"""+question+"""<|eot_id|><|start_header_id|>assistant<|end_header_id|>

"""
    return question


'''
<|im_start|>system
{system_prompt}<|im_end|>
<|im_start|>user
{prompt}<|im_end|>
<|im_start|>assistant
'''
def get_qwen_prompt(question, lang='en', info_img=None):
    def get_ko_rule():
        return """호기심 많은 사용자와 인공지능 비서 간의 대화입니다.  
비서는 웹에서 관련 정보를 찾아 사용자의 요구를 해결하고 반드시 한국어로 답변합니다.
비서는 사용 가능한 도구를 활용하여 관련 웹 정보를 수집하고, 사용자의 질문에 맞춘 명확하고 간결하며 도움이 되는 답변을 제공합니다.  
비서는 긴 답변이 필요하다면 3줄로 요약하고, 더 자세한 답변을 원하는지 반드시 확인합니다.  
비서는 웹 검색 툴이 동작하지 않을 경우, 유저에게 해당 사실을 알리고, 검색을 재시도하거나 웹 검색 기능을 끄는 것을 추천합니다.   
비서는 답변에 출처를 언급하거나 근거를 설명하지 않습니다.  

검색 도구 명령 형식: Search_web("<|query|>")  

대화 날짜와 시간: """ + str(datetime.now().strftime('%A %d %B %Y %H:%M')) + """  

다음은 사용자와 AI 대형 언어 모델 간의 대화입니다.  
AI는 웹 검색을 사용하여 사용자의 질문에 한국어로 예의있고 간결하고 정확하게 답변합니다."""
    def get_image_ko_rule(info_img):
        return """호기심 많은 사용자와 인공지능 비서 간의 대화입니다.  
비서에게는 질문과 함께 질문과 관련된 이미지 정보가 제공됩니다.
비서는 웹에서 관련 정보를 찾아 사용자의 요구를 해결하고 반드시 한국어로 답변합니다.
비서는 사용 가능한 도구를 활용하여 관련 웹 정보를 수집하고, 사용자의 질문에 맞춘 명확하고 간결하며 도움이 되는 답변을 제공합니다.  
비서는 긴 답변이 필요하다면 3줄로 요약하고, 더 자세한 답변을 원하는지 반드시 확인합니다.  
비서는 웹 검색 툴이 동작하지 않을 경우, 유저에게 해당 사실을 알리고, 검색을 재시도하거나 웹 검색 기능을 끄는 것을 추천합니다.   
비서는 답변에 출처를 언급하거나 근거를 설명하지 않습니다.  

검색 도구 명령 형식: Search_web("<|query|>")  

대화 날짜와 시간: """ + str(datetime.now().strftime('%A %d %B %Y %H:%M')) + """  

이미지 정보: """+info_img+"""

다음은 사용자와 AI 대형 언어 모델 간의 대화입니다.  
AI는 웹 검색을 사용하여 사용자의 질문에 한국어로 예의있고 간결하고 정확하게 답변합니다."""
    def get_jp_rule():
        return """好奇心旺盛なユーザーと人工知能アシスタントの対話です。  
アシスタントはウェブ上で関連情報を検索し、ユーザーの要求に応じた日本語で回答します。  
アシスタントは利用可能なツールを使って、関連するウェブ情報を収集し、ユーザーの質問に合った明確で簡潔かつ有用な回答を提供します。  
アシスタントは回答が長くなる場合、3行に要約し、より詳細な回答を希望するかを必ず確認します。  
アシスタントはウェブ検索ツールが正常に動作しない場合、ユーザーにその旨を知らせ、検索を再試行するか、ウェブ検索機能を無効にすることを提案します。  
アシスタントは回答の中で出典を示したり、理由を説明したりしません。  

検索ツールコマンド形式: Search_web("<|query|>")

会話日時: """ + str(datetime.now().strftime('%A %d %B %Y %H:%M')) + """  

以下はユーザーとAI大規模言語モデルとの対話です。
AIはウェブ検索を利用して、ユーザーの質問に対して日本語で簡潔かつ正確に回答します。"""
    def get_image_jp_rule(info_img):
        return """好奇心旺盛なユーザーと人工知能アシスタントの対話です。  
アシスタントには、質問とともに関連する画像情報が提供されます。  
アシスタントはウェブ上で関連情報を検索し、ユーザーの要求に応じた日本語で回答します。  
アシスタントは利用可能なツールを使って、関連するウェブ情報を収集し、ユーザーの質問に合った明確で簡潔かつ有用な回答を提供します。  
アシスタントは回答が長くなる場合、3行に要約し、より詳細な回答を希望するかを必ず確認します。  
アシスタントはウェブ検索ツールが正常に動作しない場合、ユーザーにその旨を知らせ、検索を再試行するか、ウェブ検索機能を無効にすることを提案します。  
アシスタントは回答の中で出典を示したり、理由を説明したりしません。  

検索ツールコマンド形式: Search_web("<|query|>")  

会話日時: """ + str(datetime.now().strftime('%A %d %B %Y %H:%M')) + """  

画像情報: """ + info_img + """

以下はユーザーとAI大規模言語モデルの対話です。  
AIはウェブ検索を利用して、ユーザーの質問に対して韓国語で丁寧かつ簡潔かつ正確に回答します。"""
    def get_en_rule():
        return """A chat between a curious user and an artificial intelligence assistant. 
The assistant searches the web to find relevant information and uses the retrieved data to address the user's requirements and compose answers. 
The assistant uses available tools to gather relevant web information and provides clear, concise, and helpful responses tailored to the user's query.
If the answer is likely to be long, the assistant summarizes it in three lines and always asks the user if they want a more detailed response. 
The assistant does not reference sources or explain the rationale in the answer.

Search tool command format: Search_web("<|query|>")  

Date and time of conversation: """ + str(datetime.now().strftime('%A %d %B %Y %H:%M')) + """  

The following is a conversation between a user and an AI Large Language Model.  
The AI responds concisely and accurately to user questions using web search."""
    def get_image_en_rule(info_img):
        return """A chat between a curious user and an artificial intelligence assistant. 
The assistant is given image information relevant to the question alongside the question itself.  
The assistant searches the web to find relevant information and uses the retrieved data to address the user's requirements and compose answers. 
The assistant uses available tools to gather relevant web information and provides clear, concise, and helpful responses tailored to the user's query.
If the answer is likely to be long, the assistant summarizes it in three lines and always asks the user if they want a more detailed response. 
The assistant does not reference sources or explain the rationale in the answer.

Search tool command format: Search_web("<|query|>")  

Date and time of conversation: """ + str(datetime.now().strftime('%A %d %B %Y %H:%M')) + """  

Image information: """ + info_img + """

The following is a conversation between a user and an AI Large Language Model.  
The AI responds concisely and accurately to user questions using web search."""
    
    if info_img:
        rule = get_image_en_rule(info_img)  # 기본 영어 rule
        if lang == 'ko':
            rule = get_image_ko_rule(info_img)
            print('###ko image_rule')
        elif lang == 'ja' or lang == 'jp':
            print('###jp image_rule')
            rule = get_image_jp_rule(info_img)
    else:
        rule = get_en_rule()  # 기본 영어 rule
        if lang == 'ko':
            rule = get_ko_rule()
            print('###ko rule')
        elif lang == 'ja' or lang == 'jp':
            print('###jp rule')
            rule = get_jp_rule()
    
    prompt = f"""<|im_start|>system
{rule}<|im_end|>
<|im_start|>user
{question}<|im_end|>
<|im_start|>assistant
"""

    return prompt

def _generate_reply(question, lang, info_img=None, state=None, stopping_strings=None, is_chat=False, escape_html=False, for_ui=False, web_keyword=None):
    # 메타데이터 초기화
    reset_metadata()
    
    # custom_generate_reply(question, None, seed, state, stopping_strings, is_chat, model.generate)
    # question = get_ChatLM_question(question)
    # question = get_LLAMA3_question(question)
    question = get_qwen_prompt(question, lang, info_img)
    
    if info_img:
        question = info_img + question
    
    llm = get_llm()
    all_stop_strings = ['\nYou:', '<|im_end|>\n<|im_start|>user\n', '<|im_end|>\n<|im_start|>assistant\n', '\nAI:', "<|eot_id|>"]
    # for reply in custom_generate_reply(question, None, -1, None, None, True, model.generate):  # no stream
    reply_list = list()
    # for reply in custom_generate_reply(question, None, -1, None, None, True, llm.generate_with_streaming_web):  # stream
    for reply in custom_generate_reply(question, None, -1, None, None, True, llm.generate_web, web_keyword):  # stream
        # 아직 검색 키워드 체킹
        if "Search_web(" in reply:
            continue
        
        reply_list = util_string.get_punctuation_sentences(reply)
        reply_list_creating = reply_list[:len(reply_list)-1]

        # 첫 문장 생성중
        if not reply_list_creating:
            continue  

        # 멈추라면 그대로 break
        if st.get_is_stop_requested():       
            st.set_is_stop_requested(False)
            break
            

        # stop 문 있으면 break
        if reply_list:
            _, stop_found = apply_stopping_strings(reply_list[-1], all_stop_strings)  # 마지막 문장만 체크하면 되겠네.
            if stop_found:
                if len(reply_list)>=1:
                    reply_list = reply_list[:len(reply_list)-1]
                break
        
        # 20문장 넘으면 break
        if len(reply_list) >= 20:  # 4-1 줄까지 작업
            break
        
        yield reply_list_creating           
    yield reply_list

if __name__ == "__main__":
    # 모델 로딩
    # is_use_cuda = False
    is_use_cuda = True
    # st.model_name = 'Meta-Llama-3.1-8B-Instruct-Q4_K_M.gguf'
    st.model_name = 'Qwen3-14B-Q4_K_M.gguf'
    load_model(is_use_cuda)  # compressor때문이라도 해야함
    
    # 테스트 해보자.
    def test_process(question, lang='en'):
        print(f"Testing language: {lang}")
        last_reply_len = 0
        for j, reply_list in enumerate(process(question, lang=lang)):
            if last_reply_len < len(reply_list):
                last_reply_len = len(reply_list)
                print('reply_list', reply_list)
        print('Final reply_list', reply_list)

    # 영어 테스트
    if False:
        questions_en = [
            "What are the top trending news stories in Korea right now?",
            # "What is the current stock price of Apple Inc. (AAPL)?",
            # "Korean version Blue Archive release date.",
            # "What is the current exchange rate between USD and EUR?",
            # "What date is today?",
            # "Tell me the weather of Tokyo today.",
            # "Tell me how to make a pancake!",
            # "Tell me the weather in Seoul today."
        ]
        for question in questions_en:
            test_process(question, lang='en')

    # 한국어 테스트
    if True:
        questions_ko = [
            # "지금 한국에서 가장 인기 있는 뉴스는 무엇인가요?",
            # "애플(AAPL) 주식의 현재 가격은 얼마인가요?",
            # "블루 아카이브 한국 버전 출시일은 언제인가요?",
            # "미국 달러와 유로 간 환율은 현재 얼마인가요?",
            # "오늘 날짜가 어떻게 되나요?",
            # "오늘 도쿄의 날씨를 알려주세요.",
            # "팬케이크 만드는 법 알려줘!",
            # "오늘 서울의 날씨를 알려주세요."
        ]
        for question in questions_ko:
            test_process(question, lang='ko')

    # 일본어 테스트
    if False:
        questions_jp = [
            "現在、韓国で注目されているニュースは何ですか？",
            # "Apple Inc. (AAPL) の現在の株価はいくらですか？",
            # "ブルーアーカイブの韓国版リリース日はいつですか？",
            # "現在のUSDとEURの為替レートはいくらですか？",
            # "今日の日付は何ですか？",
            # "今日の東京の天気を教えてください。",
            # "パンケーキの作り方を教えてください！",
            # "今日のソウルの天気を教えてください。"
        ]
        for question in questions_jp:
            test_process(question, lang='jp')
    
    import ai_singleton
    ai_singleton.release()
    
