import re
import asyncio
import warnings
import logging

import aiohttp
import requests
from bs4 import BeautifulSoup
from langchain_classic.retrievers.document_compressors import DocumentCompressorPipeline
from langchain_classic.retrievers.ensemble import EnsembleRetriever
from langchain_classic.retrievers.document_compressors.embeddings_filter import EmbeddingsFilter
from langchain_classic.retrievers import ContextualCompressionRetriever
from langchain_core.documents import Document
from langchain_text_splitters import RecursiveCharacterTextSplitter
from langchain_community.embeddings import HuggingFaceEmbeddings
from langchain_community.vectorstores import FAISS
from langchain_community.document_transformers import EmbeddingsRedundantFilter
from langchain_community.retrievers import BM25Retriever
from transformers import AutoTokenizer, AutoModelForMaskedLM
import optimum.bettertransformer.transformation
try:
    from qdrant_client import QdrantClient, models
except ImportError:
    qrant_client = None

# from .qdrant_retriever import MyQdrantSparseVectorRetriever
# from .semantic_chunker import BoundedSemanticChunker


class LangchainCompressor:

    def __init__(self, device="cuda", num_results: int = 5, similarity_threshold: float = 0.5, chunk_size: int = 500,
                 ensemble_weighting: float = 0.5, splade_batch_size: int = 2, keyword_retriever: str = "bm25",
                 model_cache_dir: str = None, chunking_method: str = "character-based",
                 chunker_breakpoint_threshold_amount: int = 10):
        self.device = device
        self.embeddings = HuggingFaceEmbeddings(model_name="all-MiniLM-L6-v2", model_kwargs={"device": device},
                                                cache_folder=model_cache_dir)
        if keyword_retriever == "splade":
            if "QdrantClient" not in globals():
                raise ImportError("Package qrant_client is missing. Please install it using 'pip install qdrant-client")
            self.splade_doc_tokenizer = AutoTokenizer.from_pretrained("naver/efficient-splade-VI-BT-large-doc",
                                                                      cache_dir=model_cache_dir)
            self.splade_doc_model = AutoModelForMaskedLM.from_pretrained("naver/efficient-splade-VI-BT-large-doc",
                                                                         cache_dir=model_cache_dir).to(self.device)
            self.splade_query_tokenizer = AutoTokenizer.from_pretrained("naver/efficient-splade-VI-BT-large-query",
                                                                        cache_dir=model_cache_dir)
            self.splade_query_model = AutoModelForMaskedLM.from_pretrained("naver/efficient-splade-VI-BT-large-query",
                                                                           cache_dir=model_cache_dir).to(self.device)
            optimum_logger = optimum.bettertransformer.transformation.logger
            original_log_level = optimum_logger.level
            # Set the level to 'ERROR' to ignore "The BetterTransformer padding during training warning"
            optimum_logger.setLevel(logging.ERROR)
            self.splade_doc_model.to_bettertransformer()
            self.splade_query_model.to_bettertransformer()
            optimum_logger.setLevel(original_log_level)
            self.splade_batch_size = splade_batch_size

        self.spaces_regex = re.compile(r" {3,}")
        self.num_results = num_results
        self.similarity_threshold = similarity_threshold
        self.chunking_method = chunking_method
        self.chunk_size = chunk_size
        self.chunker_breakpoint_threshold_amount = chunker_breakpoint_threshold_amount
        self.ensemble_weighting = ensemble_weighting
        self.keyword_retriever = keyword_retriever

    def preprocess_text(self, text: str) -> str:
        text = text.replace("\n", " \n")
        text = self.spaces_regex.sub(" ", text)
        text = text.strip()
        return text

    def retrieve_documents(self, query: str, url_list: list[str]) -> list[Document]:
        yield "Downloading webpages"
        html_url_tupls = zip(asyncio.run(async_fetch_urls(url_list)), url_list)
        html_url_tupls = [(content, url) for content, url in html_url_tupls if content is not None]
        if not html_url_tupls:
            return []

        documents = [html_to_plaintext_doc(html, url) for html, url in html_url_tupls]
        if self.chunking_method == "semantic":
            text_splitter = BoundedSemanticChunker(self.embeddings, breakpoint_threshold_type="percentile",
                                                        breakpoint_threshold_amount=self.chunker_breakpoint_threshold_amount,
                                                        max_chunk_size=self.chunk_size)
        else:
            text_splitter = RecursiveCharacterTextSplitter(chunk_size=self.chunk_size, chunk_overlap=10,
                                                                separators=["\n\n", "\n", ".", ", ", " ", ""])
        yield "Chunking page texts"
        split_docs = text_splitter.split_documents(documents)
        yield "Retrieving relevant results"
        # filtered_docs = pipeline_compressor.compress_documents(documents, query)
        faiss_retriever = FAISS.from_documents(split_docs, self.embeddings).as_retriever(
            search_kwargs={"k": self.num_results}
        )

        #  The sparse keyword retriever is good at finding relevant documents based on keywords,
        #  while the dense retriever is good at finding relevant documents based on semantic similarity.
        if self.keyword_retriever == "bm25":
            keyword_retriever = BM25Retriever.from_documents(split_docs, preprocess_func=self.preprocess_text)
            keyword_retriever.k = self.num_results
        elif self.keyword_retriever == "splade":
            client = QdrantClient(location=":memory:")
            collection_name = "sparse_collection"
            vector_name = "sparse_vector"

            client.create_collection(
                collection_name,
                vectors_config={},
                sparse_vectors_config={
                    vector_name: models.SparseVectorParams(
                        index=models.SparseIndexParams(
                            on_disk=False,
                        )
                    )
                },
            )

            keyword_retriever = MyQdrantSparseVectorRetriever(
                splade_doc_tokenizer=self.splade_doc_tokenizer,
                splade_doc_model=self.splade_doc_model,
                splade_query_tokenizer=self.splade_query_tokenizer,
                splade_query_model=self.splade_query_model,
                device=self.device,
                client=client,
                collection_name=collection_name,
                sparse_vector_name=vector_name,
                sparse_encoder=None,
                batch_size=self.splade_batch_size,
                k=self.num_results
            )
            keyword_retriever.add_documents(split_docs)
        else:
            raise ValueError("self.keyword_retriever must be one of ('bm25', 'splade')")

        redundant_filter = EmbeddingsRedundantFilter(embeddings=self.embeddings)
        embeddings_filter = EmbeddingsFilter(embeddings=self.embeddings, k=None,
                                             similarity_threshold=self.similarity_threshold)
        pipeline_compressor = DocumentCompressorPipeline(
            transformers=[redundant_filter, embeddings_filter]
        )

        compression_retriever = ContextualCompressionRetriever(base_compressor=pipeline_compressor,
                                                               base_retriever=faiss_retriever)

        ensemble_retriever = EnsembleRetriever(
            retrievers=[compression_retriever, keyword_retriever],
            weights=[self.ensemble_weighting, 1 - self.ensemble_weighting]
        )
        compressed_docs = ensemble_retriever.invoke(query)

        # Ensemble may return more than "num_results" results, so cut off excess ones
        return compressed_docs[:self.num_results]




async def async_download_html(url, headers):
    async with aiohttp.ClientSession(headers=headers, timeout=aiohttp.ClientTimeout(10)) as session:
        try:
            resp = await session.get(url)
            return await resp.text()
        except UnicodeDecodeError:
            # print(f"LLM_Web_search | {url} generated an exception: Expected content type text/html. Got {resp.headers['Content-Type']}.")
            pass
        except TimeoutError as exc:
            # print('LLM_Web_search | %r did not load in time' % url)
            pass
        except Exception as exc:
            # print('LLM_Web_search | %r generated an exception: %s' % (url, exc))
            pass
    return None


async def async_fetch_urls(urls):
    headers = {"User-Agent": "Mozilla/5.0 (X11; Linux x86_64; rv:120.0) Gecko/20100101 Firefox/120.0",
               "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8",
               "Accept-Language": "en-US,en;q=0.5"}
    webpages = await asyncio.gather(*[(async_download_html(url, headers)) for url in urls])
    return webpages


def docs_to_pretty_str(docs) -> str:
    ret_str = ""
    for i, doc in enumerate(docs):
        ret_str += f"Result {i+1}:\n"
        ret_str += f"{doc.page_content}\n"
        ret_str += f"Source URL: {doc.metadata['source']}\n\n"
    return ret_str


def download_html(url: str) -> bytes:
    headers = {"User-Agent": "Mozilla/5.0 (X11; Linux x86_64; rv:120.0) Gecko/20100101 Firefox/120.0",
               "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8",
               "Accept-Language": "en-US,en;q=0.5"}

    response = requests.get(url, headers=headers, verify=True, timeout=8)
    response.raise_for_status()

    content_type = response.headers.get("Content-Type", "")
    if not content_type.startswith("text/html"):
        raise ValueError(f"Expected content type text/html. Got {content_type}.")
    return response.content


def html_to_plaintext_doc(html_text: str or bytes, url: str) -> Document:
    # with warnings.catch_warnings(action="ignore"):  # 버전 이슈
        # soup = BeautifulSoup(html_text, features="lxml")
    soup = BeautifulSoup(html_text, features="lxml")
    for script in soup(["script", "style"]):
        script.extract()

    strings = '\n'.join([s.strip() for s in soup.stripped_strings])
    webpage_document = Document(page_content=strings, metadata={"source": url})
    return webpage_document


############################################
from duckduckgo_search import DDGS

class Generator:
    """Allows a generator method to return a final value after finishing
    the generation. Credit: https://stackoverflow.com/a/34073559"""
    def __init__(self, gen):
        self.gen = gen

    def __iter__(self):
        self.value = yield from self.gen
        return self.value

def dict_list_to_pretty_str(data: list[dict]) -> str:
    ret_str = ""
    if isinstance(data, dict):
        data = [data]
    if isinstance(data, list):
        for i, d in enumerate(data):
            ret_str += f"Result {i+1}\n"
            ret_str += f"Title: {d['title']}\n"
            ret_str += f"{d['body']}\n"
            ret_str += f"Source URL: {d['href']}\n"
        return ret_str
    else:
        raise ValueError("Input must be dict or list[dict]")

def langchain_search_duckduckgo(query: str, langchain_compressor: LangchainCompressor, max_results: int,
                                instant_answers: bool):
    documents = []
    query = query.strip("\"'")
    yield f'Getting results from DuckDuckGo'
    with DDGS() as ddgs:
        if instant_answers:
            answer_list = ddgs.text(query)
            if answer_list:
                if max_results > 1:
                    max_results -= 1  # We already have 1 result now
                answer_dict = answer_list[0]
                instant_answer_doc = Document(page_content=answer_dict["body"],
                                              metadata={"source": answer_dict["href"]})
                documents.append(instant_answer_doc)

        results = []
        result_urls = []
        for result in ddgs.text(query, region='wt-wt', safesearch='moderate', timelimit=None,
                                max_results=langchain_compressor.num_results):
            results.append(result)
            result_urls.append(result["href"])
    # print('retrieval_gen', query, result_urls)
    retrieval_gen = Generator(langchain_compressor.retrieve_documents(query, result_urls))
    for status_message in retrieval_gen:
        yield status_message
    documents.extend(retrieval_gen.value)
    if not documents:    # Fall back to old simple search rather than returning nothing
        # print("LLM_Web_search | Could not find any page content "
        #       "similar enough to be extracted, using basic search fallback")
        return dict_list_to_pretty_str(results[:max_results])
    return docs_to_pretty_str(documents[:max_results])

from tavily import TavilyClient
from kei import AI_WEB_TAVILY_API_KEY

def langchain_search_Tavily(query: str, langchain_compressor: LangchainCompressor, max_results: int,
                             instant_answers: bool):
    documents = []
    query = query.strip("\"'")

    yield f"Getting results from Tavily"

    tavily_api_key = AI_WEB_TAVILY_API_KEY
    if not tavily_api_key:
        raise ValueError("TAVILY_API_KEY environment variable is not set.")
    
    client = TavilyClient(api_key=tavily_api_key)

    # API 호출
    result = client.search(query=query, max_results=langchain_compressor.num_results)

    # result = {
    #   "query": "example",
    #   "results": [{"url": "...", "content": "...", "title": "..."}]
    # }

    results = result.get("results", [])
    result_urls = [r["url"] for r in results]

    if instant_answers and results:
        top = results[0]
        instant_doc = Document(
            page_content=top.get("content", ""),
            metadata={"source": top.get("url", "")}
        )
        documents.append(instant_doc)
        if max_results > 1:
            max_results -= 1

    # 문서 압축 및 추출
    retrieval_gen = Generator(langchain_compressor.retrieve_documents(query, result_urls))
    for status_message in retrieval_gen:
        yield status_message

    documents.extend(retrieval_gen.value)

    if not documents:
        return dict_list_to_pretty_str(results[:max_results])
    
    return docs_to_pretty_str(documents[:max_results])

from kei import AI_WEB_GOOGLE_CSE_API_KEY, AI_WEB_GOOGLE_CSE_CX

def langchain_search_GoogleCSE(query: str, langchain_compressor, max_results: int, instant_answers: bool):
    documents = []
    query = query.strip("\"'")
    yield "Getting results from Google Custom Search API"

    api_key = AI_WEB_GOOGLE_CSE_API_KEY
    cx = AI_WEB_GOOGLE_CSE_CX
    if not api_key or not cx:
        raise ValueError("GOOGLE_CSE_API_KEY or GOOGLE_CSE_CX is not set in environment variables.")

    url = "https://www.googleapis.com/customsearch/v1"
    params = {
        "key": api_key,
        "cx": cx,
        "q": query,
        "num": min(max_results, 10)  # Google CSE는 1회 요청에 최대 10개
    }

    response = requests.get(url, params=params)
    if response.status_code != 200:
        raise RuntimeError(f"Google CSE error: {response.status_code} - {response.text}")

    data = response.json()
    items = data.get("items", [])
    result_urls = [item.get("link") for item in items if "link" in item]

    if instant_answers and items:
        top = items[0]
        content = f"{top.get('title', '')}\n\n{top.get('snippet', '')}"
        source = top.get("link", "Google CSE")
        documents.append(Document(page_content=content, metadata={"source": source}))
        if max_results > 1:
            max_results -= 1

    retrieval_gen = Generator(langchain_compressor.retrieve_documents(query, result_urls))
    for status_message in retrieval_gen:
        yield status_message

    documents.extend(retrieval_gen.value)

    if not documents:
        return dict_list_to_pretty_str(items[:max_results])
    
    return docs_to_pretty_str(documents[:max_results])

from kei import AI_WEB_SERPER_API_KEY

def langchain_search_serper(query: str, langchain_compressor, max_results: int, instant_answers: bool):
    documents = []
    query = query.strip("\"'")
    yield "Getting results from Serper.dev"

    serper_api_key = AI_WEB_SERPER_API_KEY
    if not serper_api_key:
        raise ValueError("SERPER_API_KEY not set.")

    headers = {"X-API-KEY": serper_api_key}
    json_data = {
        "q": query,
        "gl": "us", "hl": "en"
    }

    response = requests.post("https://google.serper.dev/search", headers=headers, json=json_data)
    if response.status_code != 200:
        raise RuntimeError(f"Serper API Error {response.status_code}: {response.text}")

    data = response.json()
    results = data.get("organic", [])
    result_urls = [item.get("link") for item in results if "link" in item]

    if instant_answers and results:
        top = results[0]
        content = f"{top.get('title', '')}\n\n{top.get('snippet', '')}"
        source = top.get("link")
        documents.append(Document(page_content=content, metadata={"source": source}))
        if max_results > 1:
            max_results -= 1

    retrieval_gen = Generator(langchain_compressor.retrieve_documents(query, result_urls))
    for status in retrieval_gen:
        yield status
    documents.extend(retrieval_gen.value)

    if not documents:
        return dict_list_to_pretty_str(results[:max_results])
    return docs_to_pretty_str(documents[:max_results])

from kei import AI_WEB_BRAVE_API_KEY

def langchain_search_brave(query: str, langchain_compressor, max_results: int, instant_answers: bool):
    documents = []
    query = query.strip("\"'")
    yield "Getting results from Brave Search API"

    brave_api_key = AI_WEB_BRAVE_API_KEY
    if not brave_api_key:
        raise ValueError("BRAVE_API_KEY not set.")

    headers = {"X-Subscription-Token": brave_api_key}
    params = {
        "q": query,
        "count": langchain_compressor.num_results,
        "source": "web"
    }

    response = requests.get("https://api.search.brave.com/res/v1/web/search", headers=headers, params=params)
    if response.status_code != 200:
        raise RuntimeError(f"Brave API Error {response.status_code}: {response.text}")

    data = response.json()
    results = data.get("web", {}).get("results", [])
    result_urls = [item.get("url") for item in results if "url" in item]

    if instant_answers and results:
        first = results[0]
        doc = Document(page_content=first.get("description", ""), metadata={"source": first.get("url")})
        documents.append(doc)
        if max_results > 1:
            max_results -= 1

    retrieval_gen = Generator(langchain_compressor.retrieve_documents(query, result_urls))
    for status in retrieval_gen:
        yield status
    documents.extend(retrieval_gen.value)

    if not documents:
        return dict_list_to_pretty_str(results[:max_results])
    return docs_to_pretty_str(documents[:max_results])

def get_webpage_content(url: str) -> str:
    headers = {"User-Agent": "Mozilla/5.0 (X11; Linux x86_64; rv:120.0) Gecko/20100101 Firefox/120.0",
               "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8",
               "Accept-Language": "en-US,en;q=0.5"}
    if not url.startswith("https://"):
        try:
            response = requests.get(f"https://{url}", headers=headers)
        except:
            response = requests.get(url, headers=headers)
    else:
        response = requests.get(url, headers=headers)

    soup = BeautifulSoup(response.content, features="lxml")
    for script in soup(["script", "style"]):
        script.extract()

    strings = soup.stripped_strings
    return '\n'.join([s.strip() for s in strings])

if __name__ == "__main__":
    import os
    
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
    
    
    # DuckDuckgo 검색 테스트
    if False:                
        reply = ""
        reply += "\n```plaintext"
        reply += "\nSearch tool:\n"
        # print('searxng_url', searxng_url)  # 비어있음 여기 안탐
        # if searxng_url == "":                
        #     search_generator = Generator(langchain_search_duckduckgo(search_term,
        #                                                              langchain_compressor,
        #                                                              max_search_results,
        #                                                              instant_answers))
        # else:
        #     search_generator = Generator(langchain_search_searxng(search_term,
        #                                                           searxng_url,
        #                                                           langchain_compressor,
        #                                                           max_search_results))
        search_generator = Generator(langchain_search_duckduckgo("weather of Seoul",
                                                                    langchain_compressor,
                                                                    5,
                                                                    True)) 
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

    # Tavily 검색 테스트
    if False:        
        reply = ""
        reply += "\n```plaintext"
        reply += "\nSearch tool:\n"
        # print('searxng_url', searxng_url)  # 비어있음 여기 안탐
        # if searxng_url == "":                
        #     search_generator = Generator(langchain_search_duckduckgo(search_term,
        #                                                              langchain_compressor,
        #                                                              max_search_results,
        #                                                              instant_answers))
        # else:
        #     search_generator = Generator(langchain_search_searxng(search_term,
        #                                                           searxng_url,
        #                                                           langchain_compressor,
        #                                                           max_search_results))
        search_generator = Generator(langchain_search_Tavily("weather of Seoul",
                                                                    langchain_compressor,
                                                                    5,
                                                                    True)) 
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

    # Google Custom Search 검색 테스트
    if True:                
        reply = ""
        reply += "\n```plaintext"
        reply += "\nSearch tool:\n"
        # print('searxng_url', searxng_url)  # 비어있음 여기 안탐
        # if searxng_url == "":                
        #     search_generator = Generator(langchain_search_duckduckgo(search_term,
        #                                                              langchain_compressor,
        #                                                              max_search_results,
        #                                                              instant_answers))
        # else:
        #     search_generator = Generator(langchain_search_searxng(search_term,
        #                                                           searxng_url,
        #                                                           langchain_compressor,
        #                                                           max_search_results))
        search_generator = Generator(langchain_search_GoogleCSE("weather of Seoul",
                                                                    langchain_compressor,
                                                                    5,
                                                                    True)) 
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

    # Serper 검색 테스트
    if False:                
        reply = ""
        reply += "\n```plaintext"
        reply += "\nSearch tool:\n"
        # print('searxng_url', searxng_url)  # 비어있음 여기 안탐
        # if searxng_url == "":                
        #     search_generator = Generator(langchain_search_duckduckgo(search_term,
        #                                                              langchain_compressor,
        #                                                              max_search_results,
        #                                                              instant_answers))
        # else:
        #     search_generator = Generator(langchain_search_searxng(search_term,
        #                                                           searxng_url,
        #                                                           langchain_compressor,
        #                                                           max_search_results))
        search_generator = Generator(langchain_search_serper("weather of Seoul",
                                                                    langchain_compressor,
                                                                    5,
                                                                    True)) 
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
        
    # Brave Search 검색 테스트
    if True:                
        reply = ""
        reply += "\n```plaintext"
        reply += "\nSearch tool:\n"
        # print('searxng_url', searxng_url)  # 비어있음 여기 안탐
        # if searxng_url == "":                
        #     search_generator = Generator(langchain_search_duckduckgo(search_term,
        #                                                              langchain_compressor,
        #                                                              max_search_results,
        #                                                              instant_answers))
        # else:
        #     search_generator = Generator(langchain_search_searxng(search_term,
        #                                                           searxng_url,
        #                                                           langchain_compressor,
        #                                                           max_search_results))
        search_generator = Generator(langchain_search_brave("weather of Seoul",
                                                                    langchain_compressor,
                                                                    5,
                                                                    True)) 
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
