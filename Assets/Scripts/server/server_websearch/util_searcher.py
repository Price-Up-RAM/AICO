'''
ai_web_module 정리하고, WebSearcher로 class 정리
'''
web_searcher = None
import os
import time

from ai_web_search_module import (
    langchain_search_duckduckgo,
    langchain_search_Tavily,
    langchain_search_GoogleCSE,
    langchain_search_serper,
    langchain_search_brave,
    Generator
)

class WebSearcher:
    def search(self, query, langchain_compressor, max_results=5, instant_answers=True):
        source = "Failed"
        methods = [
            langchain_search_duckduckgo,
            langchain_search_Tavily,
            langchain_search_GoogleCSE,
            langchain_search_serper,
            langchain_search_brave,
        ]

        init_time = time.time()
        for method in methods:
            try:
                print(f"[INFO] Trying method: {method.__name__}")
                start_time = time.time()
                result = method(query, langchain_compressor, max_results, instant_answers)
                if result:
                    end_time = time.time()
                    time_len = str(end_time - start_time) + '/' + str(end_time - init_time) 
                    source = method.__name__
                    print('##search', source, time_len)
                    return result
            except Exception as e:
                print(f"[FAIL] {method.__name__} failed: {e}")

        return 'failed'
    
    # 성공, 실패 여부에 따라 dict 반환
    def search_reply(self, query, langchain_compressor, max_results=5, instant_answers=True):
        method_name = "Fail"  # 기본값: 실패
        search_content = ""  # 검색 원본 내용
        methods = [
            langchain_search_duckduckgo,
            langchain_search_Tavily,
            langchain_search_GoogleCSE,
            langchain_search_serper,
            langchain_search_brave,
        ]
        init_time = time.time()        
        
        reply = ""
        reply += "\n```plaintext"
        reply += "\nSearch tool:\n"

        reply_content = ""
        for method in methods:
            print(f"[INFO] Trying method: {method.__name__}")
            start_time = time.time()
            reply_content = ""
            search_generator = Generator(method(query, langchain_compressor, max_results, instant_answers)) 
            
            try:
                for status_message in search_generator:
                    pass    
                search_results = search_generator.value
            except Exception as exc:
                exception_message = str(exc)
                print(f"The search tool encountered an error: {exception_message}")
                reply_content = exception_message
                continue
            else:
                if search_results != "":
                    reply += search_results
                    # 성공 시 메타 정보 설정
                    end_time = time.time()
                    time_len = str(end_time - start_time) + '/' + str(end_time - init_time) 
                    method_name = method.__name__
                    search_content = search_results[:1000]  # 첫 1000자만 저장 (크기 제한)
                    print('##search_reply', method_name, time_len)
                    
                    # dict 형태로 반환
                    return {
                        'reply': reply,
                        'method': method_name,
                        'content': search_content
                    }
                else:
                    continue
        
        # 전체 실패
        reply += reply_content
        reply += "```"

        return {
            'reply': reply,
            'method': "Fail",
            'content': ""
        }
        
        
        

# 테스트
if __name__ == "__main__":   
    from ai_web_search_module import LangchainCompressor
    
    web_searcher = WebSearcher()
    
    extension_path = os.path.dirname(os.path.abspath(__file__))
    langchain_compressor = LangchainCompressor(device="cpu",
                                                keyword_retriever="bm25", #  params["keyword retriever"],
                                                model_cache_dir=os.path.join(extension_path, "model"))
    compressor_model = langchain_compressor.embeddings.client
    compressor_model.to(compressor_model._target_device)
    
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
    
    langchain_compressor.num_results = 10
    langchain_compressor.similarity_threshold = params["langchain similarity score threshold"]
    langchain_compressor.chunk_size = params["chunk size"]
    langchain_compressor.ensemble_weighting = params["ensemble weighting"]
    langchain_compressor.splade_batch_size = params["splade batch size"]
    langchain_compressor.chunking_method = params["chunking method"]
    langchain_compressor.chunker_breakpoint_threshold_amount = params["chunker breakpoint_threshold_amount"]

    # 검색기능 테스트
    if False:
        reply = ""
        reply += "\n```plaintext"
        reply += "\nSearch tool:\n"

        search_generator = Generator(web_searcher.search(
            "weather of Seoul",
            langchain_compressor,
            5,
            True
        )) 
        search_results = ""
        try:
            # 순회해서 value 속성을 생성
            for _ in search_generator:
                pass
            search_results = search_generator.value
        except Exception as exc:
            exception_message = str(exc)
            reply += f"The search tool encountered an error: {exception_message}"
            # print(f'LLM_Web_search | {search_term} generated an exception: {exception_message}')
        else:
            if search_results != "":
                reply += search_results
            else:
                reply += f"\nThe search tool did not return any results."
        reply += "```"
        print('###reply\n', reply)
    
    # 검색+reply 생성기능 테스트
    if True:
        print(web_searcher.search_reply(
            "weather of Seoul",
            langchain_compressor,
            5,
            True
        ))