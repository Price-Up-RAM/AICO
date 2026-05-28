'''
순서 : DeepLFreeUrls > DeeplX > deepL > google
Translator은 기본 google을 사용하지만 DeepL이 있으면 시도는 해본다.
'''
import requests
import os
import json
import time
import threading

import deepl
import googletrans
try:
    from PyDeepLX import PyDeepLX
    PYDEEPLX_AVAILABLE = True
except ImportError as e:
    print(f"PyDeepLX 모듈을 import할 수 없습니다: {e}")
    PYDEEPLX_AVAILABLE = False

import util_proper_nouns
import state

# deeplx_python 모듈 추가
try:
    from deeplx_python import translate as deeplx_python_translate
    DEEPLX_PYTHON_AVAILABLE = True
except ImportError as e:
    print(f"deeplx_python 모듈을 import할 수 없습니다: {e}")
    DEEPLX_PYTHON_AVAILABLE = False

# ai_translate 모듈 추가
try:
    from ai_translate import translate as ai_translate
    AI_TRANSLATE_AVAILABLE = True
except ImportError as e:
    print(f"ai_translate 모듈을 import할 수 없습니다: {e}")
    AI_TRANSLATE_AVAILABLE = False

TRANSLATOR_CONFIG_PATH = "config/translator.json"
TRANSLATOR_FREEURLS_TXT = 'config/urls.txt'
TRANSLATOR_FREEURLS_BLACK_LIST = ['https://api.deeplx.org/translate']

# DeepLFreeUrls 응답 처리
def process_free_deepl_response_content(response_content: bytes):
    try:
        # 응답을 JSON으로 파싱
        response_json = json.loads(response_content)

        # print('###response_json', response_json)
        # 'alternative'와 'data' 추출
        alternatives = response_json.get("alternatives", None)
        data = response_json.get("data", None)

        # 'alternatives'가 문자열인지 리스트인지 확인 및 처리
        if isinstance(alternatives, list):
            # print("Alternatives (list):", alternatives)
            if alternatives and len(alternatives)>0:
                return alternatives[0]
        if isinstance(alternatives, str):
            # print("Alternative (string):", alternatives)
            return alternatives
        if data:
            # print("Data:", data)
            return data
        if state.get_DEV_MODE():
            print("No data found : ", response_json)
        return ''
    except json.JSONDecodeError as e:
        if state.get_DEV_MODE():
            print("Failed to parse JSON:", e)
    except Exception as e:
        print("An error occurred:", e)


# 번역기능 제공
class Translator:
    # 초기화
    def __init__(self):
        self.translator_Google = googletrans.Translator()
        self.translator_DeepL = None
        self.isDeepLActivate = True  # translator_DeepL 활성화 여부(DeeplX, DeeplFree 제외)
        self.freeDeepLFreeUrls = []

    # urls.txt에서 multithread로 현재 활성화된 url 가져오기
    def get_freeDeepLFreeUrls(self):               
        def check_url(url, results, lock):
            # TEST 데이터
            data = {
                "text": "I'm all ears!",
                "source_lang": "auto",
                "target_lang": "JA",
                "formality": "more"
            }
            retries = 1
            while retries > 0:
                try:
                    start_time = time.time()
                    response = requests.post(url, json=data, timeout=5)
                    response_time = time.time() - start_time
                    # print('###url', url, response, response.content)

                    if response.status_code == 200:
                        with lock:
                            response_content = process_free_deepl_response_content(response.content)
                            results.append((url, response_time, response_content))
                            # Test 출력용
                            # print('url : ', url, f'({response_time})')
                            # print('response_content : ', response.content)
                            # print('response_content_text  : ', response_content)
                            # print('------------')
                    break
                except:
                    retries -= 1
                    # time.sleep(1) 
                    pass 
            
        def check_urls_multithread(urls):
            results = []
            lock = threading.Lock()
            threads = []

            for url in urls:
                if url in TRANSLATOR_FREEURLS_BLACK_LIST:
                    continue
                thread = threading.Thread(target=check_url, args=(url, results, lock))
                thread.start()
                threads.append(thread)

            for thread in threads:
                thread.join()

            return results
        
        urls = []
        try:
            if os.path.exists(TRANSLATOR_FREEURLS_TXT):
                with open(TRANSLATOR_FREEURLS_TXT, 'r') as file:
                    urls = [line.strip() for line in file if line.strip()]
            else:
                print(f'no files : {TRANSLATOR_FREEURLS_TXT}')
        except:
            print(f"[{TRANSLATOR_FREEURLS_TXT} 로드 실패]: {e}")

        # dxpool의 최신 API 주소 추가
        try:
            dxpool_response = requests.get("https://dxpool.dattw.eu.org/all", timeout=5)
            if dxpool_response.status_code == 200:
                dxpool_json = dxpool_response.json()
                for base_url in dxpool_json.keys():
                    full_url = base_url.rstrip('/') + "/translate"
                    if full_url not in urls:
                        urls.append(full_url)
        except Exception as e:
            print("dxpool request failed:", e)

        results = check_urls_multithread(urls)

        # multithread를 썼으니 기본적으로 재정렬이 필요 없음
        for url, response_time, response_content in results:
            if response_content:
                if response_time <= 2:  # 2초안에는 답이 와줘야...
                    if state.get_DEV_MODE():
                        print(f"FAST : ({response_time:.2f}s) {url} ; {response_content}")
                    self.freeDeepLFreeUrls.append(url)
                else:
                    if state.get_DEV_MODE():
                        print(f"SLOW : ({response_time:.2f}s) {url} ; {response_content}")
                    pass
            else:
                if state.get_DEV_MODE():
                    print(f"NONE : ({response_time:.2f}s) {url} ; {response_content}")
                pass
                
        print('freeDeepLFreeUrls', self.freeDeepLFreeUrls)

    # JSON 파일에서 DeepL API 키를 읽어옴
    def load_deep_api_key(self):
        try:        
            with open(TRANSLATOR_CONFIG_PATH, 'r', encoding='utf-8') as file:
                config = json.load(file)
                key = config.get("deep_api_key", "")

                if key:
                    self.DEEPL_AUTH_KEY = key
                    self.translator_DeepL = deepl.Translator(self.DEEPL_AUTH_KEY)
                    self.isDeepLActivate = True  # TODO : chk용 함수 필요
        except:
            return ''
        
    # DeepL API 키를 JSON에 저장
    def save_deep_api_key(self, api_key):
        if not api_key:
            print('please input deepl api key')
            return
        try:
            with open(TRANSLATOR_CONFIG_PATH, 'r', encoding='utf-8') as file:
                config = json.load(file)
        except:
            config = {}
            
        config['deep_api_key'] = api_key
        # chk deepl connect and reconnect
        
        with open(TRANSLATOR_CONFIG_PATH, 'w', encoding='utf-8') as file:
            json.dump(config, file, ensure_ascii=False, indent=4)

    # JSON 파일에서 설정 정보를 utf-8로 읽어옴
    def get_setting_info_from_json(self):
        pass

    # DeepL 또는 Google을 사용해 텍스트 번역
    '''
    결과양식
    {
        'text' : 번역된 텍스트,
        'source' : DeeplFree, DeeplX, Deepl, Google, Failed 중 하나,
        'time' : '해당 번역 방법에 걸린 시간 / translate 함수 실행 중 걸린 총 시간'
    }
    '''
    def translate(self, origin_text, target_lang):    
        # 값이 비어있을 경우 그대로 반환
        if not origin_text or not origin_text.strip():
            return {'origin':origin_text, 'text': '', 'source': source, 'time': '0/0'}
           
        # 초기값 정리
        source = 'Failed'
        methods = [
            self.translate_with_deepl_x_python,  # 새로운 DeepLX Python (최우선)
            self.translate_with_ai_translate,  # AI 번역 (Qwen3VL 기반)
            self.translate_with_deepl,  # 존댓말 이슈
            self.translate_with_deepl_free_urls,
            # self.translate_with_deepl_x,
            self.translate_with_google,
        ]

        # 함수 실행
        init_time = time.time()
        for method in methods:
            try:
                start_time = time.time()
                result = method(origin_text, target_lang)
                
                if result:
                    total_time = time.time() - init_time
                    method_time = time.time() - start_time
                    
                    return {
                        'origin':origin_text, 
                        'text': result,
                        'source': method.__name__,
                        'time': str(method_time) + '/' + str(total_time) 
                    }
                        
            except Exception as e:
                pass

        total_time = time.time() - init_time
        return {'origin':origin_text, 'text': '', 'source': source, 'time': '0/'+str(total_time)}

    # en -> target_lang이라고 가정
    def translate_formality(self, text, target_lang):
        # 언어별 prefix를 추후 분리해도 되도록 함수로 분리
        def get_prefixes():
            return [
                # 한글
                "선생님, ", "선생, ", "센세, ", "교사님, ", "교사, ",  # 띄어쓰기 차이
                "선생님,", "선생,", "센세,", "교사님,", "교사,",
                # 일본어
                "教師、 ", "教師, ", "先生、", "せんせい、",
                "教師、", "教師,",  "先生,", "せんせい," # 공백/쉼표 변형
                # 영어
                "Teacher, ", "teacher, ", "sensei, ", "Sensei, ",
                "Teacher,", "teacher,", "sensei,", "Sensei, ",
                "Teacher、 ", "teacher、 ", "sensei、 ", "Sensei、 ",
                "Teacher、", "teacher、", "sensei、", "Sensei、",
            ]

        def remove_prefix(text):
            for prefix in get_prefixes():
                if text.startswith(prefix):
                    return text[len(prefix):]
            return text

        def apply_postprocess(result, target_lang):
            # 공용 후처리: 고유명사 처리
            result_text = result.get("text", "")
            result_text = util_proper_nouns.apply_proper_nouns(target_lang, result_text)
            result["text"] = result_text
            return result

        # 값이 비어있을 경우 그대로 반환
        if not text or not text.strip():
            return self.translate(text, target_lang)

        # DeepL Key 사용시: 번역기 자체의 존댓말 설정 사용
        if self.isDeepLActivate:
            result = self.translate(text, target_lang)
            return apply_postprocess(result, target_lang)

        # 이미 teacher / sensei / 선생 등 키워드가 있는 경우에는 굳이 prefix를 추가하지 않음
        t_words = ["teacher", "sensei", "선생", "교사", "先生", "教師"]
        test_text = text.lower()  # 한 번만 계산
        is_need_prefix = True
        for t in t_words:
            if t in test_text:
                is_need_prefix = False
                break

        # 필요시 teacher, prefix를 붙여서 번역기에게 존칭 대상 힌트 제공
        if is_need_prefix:
            prefixed_text = "teacher, " + text.lstrip()
            result = self.translate(prefixed_text, target_lang)
            translated = result.get("text", "")
            translated = remove_prefix(translated)
            result["text"] = translated
            result = apply_postprocess(result, target_lang)
        else:
            result = self.translate(text, target_lang)
            result = apply_postprocess(result, target_lang)

        return result

    # DeeplFreeUrls 번역 시도(일부언어 특히 KO 지원안할 수 있음)
    def translate_with_deepl_free_urls(self, text, target_lang, source_lang='auto'):
        if not self.freeDeepLFreeUrls:  # freeUrl이 없을 경우
            return None
        
        # 가장 빠른 url 최대 3회까지만 시도
        for i in range(min(3, len(self.freeDeepLFreeUrls))):  
            try:
                url = self.freeDeepLFreeUrls[i]
                data = {
                    "text": text,
                    # "source_lang": source_lang,
                    "target_lang": target_lang.upper(),  # 'ja'는 작동안하지만, 'JA'는 작동한다던가
                    "formality" : "more"  # 아마 작동 안할거임
                }

                response = requests.post(url, json=data, timeout=5)
                if response.status_code == 200:
                    response_content = process_free_deepl_response_content(response.content)
                    if response_content:
                        return response_content
            except Exception as e:
                pass
        
        return None                

    # DeeplX 번역 시도 (IP 차단이 평범하게 일반 사이트 접근도 막음)
    def translate_with_deepl_x(self, text, target_lang, sourceLang='en'):
        if not PYDEEPLX_AVAILABLE:
            return None
        
        try:
            result = PyDeepLX.translate(text, sourceLang=sourceLang, targetLang=target_lang.upper())
            return result
        except Exception as e:
            return ''

    # Deepl 번역 시도
    def translate_with_deepl(self, text, target_lang):
        if self.isDeepLActivate:
            try:
                result = self.translator_DeepL.translate_text(text, target_lang=target_lang.upper(), formality='more').text
                return result
            except Exception as e:
                return None
        else:
            return None

    # DeepLX Python 번역 시도
    def translate_with_deepl_x_python(self, text, target_lang):
        if not DEEPLX_PYTHON_AVAILABLE:
            return None
        
        try:
            # target_lang을 대문자로 변환 (예: 'ko' -> 'KO')
            target_lang_upper = target_lang.upper()
            result = deeplx_python_translate(text, target_lang_upper, formal=True)
            return result
        except Exception as e:
            return None

    # Google 번역 시도
    def translate_with_google(self, text, target_lang):
        try:
            result = self.translator_Google.translate(text, dest=target_lang).text
            return result
        except Exception as e:
            return None
    
    # AI 번역 시도 (Qwen3VL 기반)
    def translate_with_ai_translate(self, text, target_lang):
        if not AI_TRANSLATE_AVAILABLE:
            return None
        
        # 동적 상태 체크 (런타임 오류로 비활성화되었는지 확인)
        try:
            from ai_translate import is_available
            if not is_available():
                if state.get_DEV_MODE():
                    print("AI Translate 비활성화 상태 - 다음 번역 방법으로 fallback")
                return None
        except Exception:
            return None
        
        try:
            result = ai_translate(text, target_lang)
            # ai_translate는 dict로 리턴하므로 'text' 필드 추출
            if isinstance(result, dict) and 'text' in result:
                return result['text']
            return None
        except Exception as e:
            return None

    # 소스 언어 제외 한국어, 일본어, 영어로 번역
    def translate_all(self, source_lang, text):
        target_languages = ("ko", "jp", "en")
        results = {}
        for lang in target_languages:
            if source_lang.lower() != lang:
                results[lang] = self.translate(lang, text)
            else:
                results[lang] = text
        return results

    # TODO : DeepL 활성화 여부 확인
    def chkIsDeepLActivate(self):
        return self.isDeepLActivate

# 응답 처리 함수
    def process_response_content(self, response_content: bytes):
        try:
            response_json = json.loads(response_content)

            alternatives = response_json.get("alternatives", None)
            data = response_json.get("data", None)

            if isinstance(alternatives, list):
                print("Alternatives (list):", alternatives)
            elif isinstance(alternatives, str):
                print("Alternative (string):", alternatives)
            else:
                print("No alternatives found or invalid type.")

            if data:
                if state.get_DEV_MODE():
                    print("Data:", data)
            else:
                if state.get_DEV_MODE():
                    print("No data found.")

        except json.JSONDecodeError as e:
            if state.get_DEV_MODE():
                print("Failed to parse JSON:", e)
        except Exception as e:
            if state.get_DEV_MODE():
                print("An error occurred:", e)

if __name__ == "__main__":
    state.set_DEV_MODE(True)
    
    # AI Translate와 동일한 모델 사용
    state.set_use_gpu_percent(8)
    state.model_name = 'Qwen3VL-8B-Instruct-Q4_K_M.gguf'
    
    # Translator 클래스 초기화
    translator = Translator()
    
    # AI Translate Test
    print("=" * 50)
    print("AI Translate Test")
    print("=" * 50)
    if AI_TRANSLATE_AVAILABLE:
        print("[EN → KO]", translator.translate("Hello, how are you?", 'ko'))
        print("[EN → JA]", translator.translate("The weather is nice today.", 'ja'))
        print("[KO → EN]", translator.translate("안녕하세요, 어떻게 지내세요?", 'en'))
        print("[JA → KO]", translator.translate("こんにちは、お元気ですか？", 'ko'))
    else:
        print("AI Translate is not available")
    
    print("\n" + "=" * 50)
    print("Other Translation Tests")
    print("=" * 50)
    
    # DeepLFreeTest
    translator.get_freeDeepLFreeUrls()
    # print(translator.translate("I'm all ears!", 'ko'))
    # print(translator.translate("I'm all ears!", 'ja'))
    print(translator.translate_formality("I Played with sister...", 'ko'))  # proper_nouns test
    print(translator.translate_formality("I'm proud that i'm in sisterhood", 'ja'))  # proper_nouns test
    
    # DeepLX Python Test
    # print(translator.translate_formality("I'm all ears!", 'ja'))
    # print(translator.translate_formality("Fhew...", 'ko'))
    # print(translator.translate_formality("OH!!", 'ko'))
    # print(translator.translate_formality("Sensei has a good skill!", 'ko'))
    # print(translator.translate_formality("Why do you sleep allday?", 'ko'))
    # print(translator.translate_formality("I did my job", 'ko'))
    # print(translator.translate_formality("I'm all ears!", 'ja'))
    # print(translator.translate_formality("Fhew...", 'ja'))
    # print(translator.translate_formality("OH!!", 'ja'))
    # print(translator.translate_formality("Sensei has a good skill!", 'ja'))
    # print(translator.translate_formality("Why do you sleep allday?", 'ja'))
    # print(translator.translate_formality("I did my job", 'ja'))
    
    # DeepLX Test
    # print(translator.translate("I'm all ears!", 'ko'))
    # print(translator.translate_with_deepl_x("I'm all ears!", 'ja'))
    
    # DeepL Test
    # translator.save_deep_api_key('MY KEY')
    # translator.load_deep_api_key()
    # print(translator.translate_with_deepl("I'm all ears!", 'ko'))
    # print(translator.translate("I'm all ears!", 'ja'))
    
    # DeepL Free Test
    # print(translator.translate_with_deepl_free_urls("I'm all ears!", 'ko'))
    # print(translator.translate_with_deepl_free_urls("I'm all ears!", 'ja'))
    
    # Google Test
    # print(translator.translate_with_google("I'm all ears!", 'ko'))
    # print(translator.translate_with_google("I'm all ears!", 'ja'))

    # Long Text
#     text ='''
# 답변 퀄리티를 올리기 위해 필요한 것 중 하나가 번역기입니다.
# '''
#     translator.get_freeDeepLFreeUrls()
#     print(translator.translate(text, 'en'))
#     print(translator.translate(text, 'ja'))
