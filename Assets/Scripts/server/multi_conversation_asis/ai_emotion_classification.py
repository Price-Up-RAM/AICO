from threading import Lock

import state
import memory

from ai_singleton import check_llm, get_llm

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
def get_qwen_prompt(query, reply, player, character, memory_list=list(), lang='en'):
    def get_ko_rule():
        return """주어진 내용은 AI와 유저의 대화 기록입니다. 대화는 항상 AI의 답변으로 끝납니다.
당신은 대화의 마지막 문장에서 AI가 느꼈을 감정을 다음 중 하나로 분류하십시오
- Joy: 행복, 감사, 들뜬 반응  
- Anger: 강한 짜증, 비난, 적대적인 태도  
- Sadness: 실망감 또는 후회  
- Confusion: 이해하지 못하거나 혼란스러운 상태  
- Surprise: 예상치 못한 반응  
- Neutral: 침착하고 사실 전달 위주의, 특별한 감정이 드러나지 않는 응답

감정 표현이 명확하고 뚜렷하지 않은 경우, 항상 **Neutral을 선택하십시오.  
정중하거나 협조적인 말투만으로 감정을 추측해서는 안 됩니다.
뚜렷한 감정이 없거나, 위 목록에 해당하지 않거나, 감정을 판별하기 어려운 경우에는 반드시 Neutral로 응답하십시오.
결과는 다음 형식으로만 응답하십시오. 추가 설명은 포함하지 마십시오.
emotion: Joy/Anger/Confusion/Sadness/Surprise/Neutral

예시:  

## 대화
AI: 안녕하세요.
User: 뭐하고 있었어?
AI: 책을 읽고 있었어요.

결과:
emotion: Neutral

## 대화
User: 커피 좋아해?
AI: 아뇨. 그것보다는 달콤한 과자가 좋아요.
User: 너를 위해 과자를 사왔어.
AI: 오 정말 고마워요!

결과:
emotion: Joy

## 대화
User: 내가 그걸 만들었어.
AI: 오 정말로요? 놀라워요!
User: 거짓말이야.
AI: 실망이에요...

결과:
emotion: Sadness

## 대화
User: sda아 인것 같는 놀라운
AI: 선생님이 뭐라고 말씀하시는지 잘 모르겠어요.

결과:
emotion: Confusion"""
    
    def get_jp_rule():
        return """"以下はAIとユーザーの会話記録です。会話は必ずAIの発言で終了します。
あなたのタスクは、AIの最後の発言に含まれる感情を以下の中から分類することです: 
- Joy: 明確な喜び、感謝、興奮  
- Anger: 強い苛立ち、非難、敵意  
- Sadness: 落胆や後悔  
- Confusion: 理解できていない、混乱している状態  
- Surprise: 予想外の反応  
- Neutral: 冷静、事実の伝達のみ、明確な感情がない発言

感情が非常に明確かつ明示的でない限り、常にNeutralを選択してください。  
丁寧さ、協力的な態度、または親切な応答から感情を推測・想定しないでください。  
感情が明確でない場合、上記に該当しない場合、または判断が困難な場合は、必ずNeutralを選択してください。

以下の形式でのみ応答してください。追加の説明は不要です。  
emotion: Joy/Anger/Confusion/Sadness/Surprise/Neutral

例: 

## 会話
AI: こんにちは。  
User: 何してたの？  
AI: 本を読んでいました。

結果:
emotion: Neutral

## 会話
User: コーヒーは好き？  
AI: いいえ、それより甘いお菓子の方が好きです。  
User: お菓子を持ってきたよ。  
AI: わあ、ありがとう！

結果:   
emotion: Joy

## 会話
User: 私が作ったのよ。  
AI: 本当？すごいですね！  
User: うそだよ。  
AI: がっかりです…

結果:   
emotion: Sadness

## 会話
User: sdaあ 驚いたような  
AI: すみません、よく聞き取れませんでした。

結果:   
emotion: Confusion"""
    
    
    def get_en_rule():
        return """The following is a dialogue between an AI and a user. The conversation always ends with the AI's response.
Your task is to classify the AI's final emotional expression into one of the following:
- Joy: clear happiness, gratitude, excitement
- Anger: strong irritation, blame, or hostility
- Sadness: disappointment or regret
- Confusion: misunderstanding or lack of clarity
- Surprise: unexpected reaction
- Neutral: calm, factual, or no notable emotion

Unless the emotional tone is very clear and explicit, always choose Neutral.  
Do not infer or assume feelings from polite, helpful, or cooperative responses.
If the emotion is unclear, does not match the given categories, or is difficult to determine, you must respond with Neutral.

Respond in the following format only. Do not add any explanation.
emotion: Joy/Anger/Confusion/Sadness/Surprise/Neutral

Examples:

## Conversation
AI: Hello.  
User: What were you doing?  
AI: I was reading a book.

Result:  
emotion: Neutral

## Conversation
User: Do you like coffee?  
AI: No, I prefer sweet snacks.  
User: I brought you some snacks.  
AI: Oh, thank you so much!

Result:  
emotion: Joy

## Conversation
User: I made that.  
AI: Really? That's amazing!  
User: Just kidding.  
AI: That's disappointing...

Result:  
emotion: Sadness

## Conversation
User: sda ah I think surprise  
AI: I'm sorry, I can't quite understand what you're saying.

Result:  
emotion: Confusion"""
    
    rule = get_en_rule()  # 기본 영어 rule
    if lang == 'ko':
        rule = get_ko_rule()
    elif lang == 'ja' or lang == 'jp':
        rule = get_jp_rule()
        
    result_string1 = '## Conversation'
    if lang == 'ko':
        result_string1 = '## 대화'
    elif lang == 'ja' or lang == 'jp':
        result_string1 = '## 会話'
    
    conversation_string = ''
    # TODO : Server memory 사용시. char, player, lang등 대화 정보 필요
    # if not memory_list:
    #     memory_list = memory.get_memory_message_list(8192, lang=lang)  
    memory_list.append({'speaker': 'player', 'message': query})
    memory_list.append({'speaker': 'character', 'message': reply})
    for m in memory_list:
        if m['speaker'] == 'player':
            conversation_string = conversation_string + 'User: ' + m['message'] + '\n'
        elif m['speaker'] == 'character':
            conversation_string = conversation_string + 'AI: ' + m['message'] + '\n'
                
    result_string2 = 'Result:'
    if lang == 'ko':
        result_string2 = '결과:'
    elif lang == 'ja' or lang == 'jp':
        result_string2 = '結果:'
 
    prompt = f"""<|im_start|>system
{rule}<|im_end|>
<|im_start|>user
{result_string1} 
{conversation_string}
<|im_start|>assistant
{result_string2}
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

def process(query, reply, player, character, memory_list=list(), lang='en'):
    from jinja2 import Template
    llm = get_llm()
        
    prompt = get_qwen_prompt(query, reply, player, character, memory_list=memory_list, lang=lang)

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
    parsed_output = parse_response(output)
     
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
    return parsed_output


if __name__ == "__main__":
    # 모델 로딩
    # is_use_cuda=False
    is_use_cuda=True
    state.set_use_gpu_percent(8)
    state.model_name='Qwen3-14B-Q4_K_M.gguf'
    # state.model_name='Qwen3-32B-Q4_K_M.gguf'
    load_model(is_use_cuda)

    # 영어 테스트
    if True:
        player = "sensei"
        character = "arona"
        print('### EN TEST1')
        memory_list = [
            {'speaker': 'player', 'message': "Hey, did you watch the movie?"},
            {'speaker': 'character', 'message': "Yes! It was fantastic."},
            {'speaker': 'player', 'message': "I thought it was boring."},
            {'speaker': 'character', 'message': "What?! I totally disagree."}
        ]
        query = "You just don’t understand cinema."
        reply = "That’s really frustrating to hear."

        print('=============== 감정 분류 테스트: 영어 ===============')
        response = process(query, reply, player, character, memory_list=memory_list, lang='en')
        print('결과:', response)

        if "emotion: " in response:
            emotion_text = response.strip().split("emotion: ")[-1].strip().lower()
            valid_emotions = ['joy', 'anger', 'confusion', 'sadness', 'surprise', 'neutral']
            if emotion_text in valid_emotions:
                print('## [결론] 감정:', emotion_text)
            else:
                print('## [Result] Emotion not in predefined list:', emotion_text)
        else:
            print('## [결론] 감정 Format에 맞지 않은 답변')
            
        print('###EN TEST2')
        memory_list = [
            {'speaker': 'player', 'message': "You didn't reply to my message yesterday."},
            {'speaker': 'character', 'message': "Oh... I'm really sorry about that."},
            {'speaker': 'player', 'message': "I was worried something happened."},
            {'speaker': 'character', 'message': "I understand. I’ll make sure to respond next time."}
        ]
        query = "Do you know how to make pancake?"
        reply = "I'll help you to make pancake"

        print('=============== 감정 분류 테스트: 영어 (추가) ===============')
        response = process(query, reply, player, character, memory_list=memory_list, lang='en')
        print('결과:', response)

        if "emotion: " in response:
            emotion_text = response.strip().split("emotion: ")[-1].strip().lower()
            valid_emotions = ['joy', 'anger', 'confusion', 'sadness', 'surprise', 'neutral']
            if emotion_text in valid_emotions:
                print('## [Result] Emotion:', emotion_text)
            else:
                print('## [Result] Emotion not in predefined list:', emotion_text)
        else:
            print('## [Result] Response does not match emotion format')
                
    # 일본어 테스트
    if True:
        player = "sensei"
        character = "arona"
        print('###JP TEST1')
        memory_list = [
            {'speaker': 'player', 'message': "今日はどうだった？"},
            {'speaker': 'character', 'message': "すごく楽しかったよ！"},
            {'speaker': 'player', 'message': "なにがそんなに？"},
            {'speaker': 'character', 'message': "友達と美味しいケーキを食べたの！"}
        ]
        query = "そのケーキ、どこで買ったの？"
        reply = "あ、実は秘密なんだ〜"

        print('=============== 感情分類テスト: 日本語 ===============')
        response = process(query, reply, player, character, memory_list=memory_list, lang='jp')
        print('結果:', response)

        if "emotion: " in response:
            emotion_text = response.strip().split("emotion: ")[-1].strip().lower()
            valid_emotions = ['joy', 'anger', 'confusion', 'sadness', 'surprise', 'neutral']
            if emotion_text in valid_emotions:
                print('## [結論] 感情:', emotion_text)
            else:
                print('## [結論] 指定された感情に該当しません:', emotion_text)
        else:
            print('## [結論] 感情形式に一致しない出力')
    
        print('###JP TEST2')
        memory_list = [
            {'speaker': 'player', 'message': "昨日のプレゼン、どうだった？"},
            {'speaker': 'character', 'message': "緊張したけど、うまくできたと思うよ。"},
            {'speaker': 'player', 'message': "それはよかったね！"},
            {'speaker': 'character', 'message': "うん、先生が応援してくれたおかげ！"}
        ]
        query = "じゃあ、次も頑張ってね！"
        reply = "もちろん！応援してくれてありがとう〜"

        print('=============== 感情分類テスト: 日本語 (追加) ===============')
        response = process(query, reply, player, character, memory_list=memory_list, lang='jp')
        print('結果:', response)

        if "emotion: " in response:
            emotion_text = response.strip().split("emotion: ")[-1].strip().lower()
            valid_emotions = ['joy', 'anger', 'confusion', 'sadness', 'surprise', 'neutral']
            if emotion_text in valid_emotions:
                print('## [結論] 感情:', emotion_text)
            else:
                print('## [結論] 指定された感情に該当しません:', emotion_text)
        else:
            print('## [結論] 感情形式に一致しない出力')
                
    # 한국어 테스트
    if True:
        player = "sensei"
        character = "arona"
        print('###KO TEST1')
        memory_list = [
            {'speaker': 'player', 'message': "너 오늘 기분 어때?"},
            {'speaker': 'character', 'message': "좋아요! 오늘은 정말 상쾌한 날이에요."},
            {'speaker': 'player', 'message': "정말? 나도 산책하고 왔어."},
            {'speaker': 'character', 'message': "우와, 산책이라니 멋지네요!"}
        ]
        query = "하지만 여기는 비가 왔어."
        reply = "우와, 그거 정말 안되었네요..."

        print('=============== 감정 분류 테스트: 한국어 ===============')
        response = process(query, reply, player, character, memory_list=memory_list, lang='ko')
        print('결과:', response)
        
        if "emotion: " in response:  # 답에 emotion format이 있음
            emotion_text = response.strip().split("emotion: ")[-1].strip().lower()
            valid_emotions = ['joy', 'anger', 'confusion', 'sadness', 'surprise', 'neutral']  # Joy/Anger/Confusion/Sadness/Surprise/Neutral
            if emotion_text in valid_emotions:
                print('## [결론] 감정:', emotion_text)
            else:
                print('## [결론] 주어진 감정에 해당하지 않음:', emotion_text)
        else:
            print('## [결론] 감정 Format에 맞지 않은 답변')
        
        print('###KO TEST2')
        memory_list = [
            {'speaker': 'player', 'message': "시험 준비 잘 되고 있어?"},
            {'speaker': 'character', 'message': "좀 어려워요... 걱정돼요."},
            {'speaker': 'player', 'message': "도와줄까?"},
            {'speaker': 'character', 'message': "정말요? 고마워요 선생님!"}
        ]
        query = "같이 공부해보자."
        reply = "와, 든든해요!"

        print('=============== 감정 분류 테스트: 한국어 (추가) ===============')
        response = process(query, reply, player, character, memory_list=memory_list, lang='ko')
        print('결과:', response)

        if "emotion: " in response:
            emotion_text = response.strip().split("emotion: ")[-1].strip().lower()
            valid_emotions = ['joy', 'anger', 'confusion', 'sadness', 'surprise', 'neutral']
            if emotion_text in valid_emotions:
                print('## [결론] 감정:', emotion_text)
            else:
                print('## [결론] 주어진 감정에 해당하지 않음:', emotion_text)
        else:
            print('## [결론] 감정 Format에 맞지 않은 답변')
        
        import ai_singleton
        ai_singleton.release()
    
