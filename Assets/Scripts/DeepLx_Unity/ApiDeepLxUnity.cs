using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// DeepLX Unity - 메인 번역 클래스
// Python deeplx_python/translate.py 포팅
// Standalone 모듈로 다른 의존성 없이 사용 가능
public class ApiDeepLxUnity
{
    private const int MAX_RETRIES = 3;
    
    // 번역 메인 함수 (간편 버전)
    // formal: true=격식, false=비격식, null=기본값(undefined)
    public static async Task<string> Translate(string text, string targetLang, bool? formal = true)
    {
        var result = await TranslateByDeepLx(null, targetLang, text, formal);
        return result.Data;
    }
    
    // 번역 메인 함수 (상세 결과)
    public static async Task<DeepLxTranslationResult> TranslateDetailed(
        string text, 
        string targetLang, 
        string sourceLang = null,
        bool? formal = true)
    {
        return await TranslateByDeepLx(sourceLang, targetLang, text, formal);
    }
    
    // DeepLX를 사용하여 텍스트 번역
    private static async Task<DeepLxTranslationResult> TranslateByDeepLx(
        string sourceLang,
        string targetLang,
        string text,
        bool? formal = true)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new DeepLxTranslationResult
            {
                Code = DeepLxConstants.HTTP_STATUS_NOT_FOUND,
                Message = "번역할 텍스트가 없습니다"
            };
        }
        
        // 텍스트를 줄 단위로 분할
        string[] textParts = DeepLxUtils.SplitAndProcess(text);
        List<string> translatedParts = new List<string>();
        List<List<string>> allAlternatives = new List<List<string>>();
        
        string detectedSourceLang = sourceLang;
        
        foreach (string part in textParts)
        {
            if (string.IsNullOrWhiteSpace(part))
            {
                translatedParts.Add("");
                allAlternatives.Add(new List<string> { "" });
                continue;
            }
            
            // 소스 언어: 명시적으로 지정되지 않으면 auto 사용 (Python과 동일)
            string sourceLangCode = "auto";
            if (!string.IsNullOrEmpty(detectedSourceLang) && detectedSourceLang != "auto")
            {
                sourceLangCode = DeepLxUtils.AbbreviateLanguage(detectedSourceLang);
                if (string.IsNullOrEmpty(sourceLangCode))
                {
                    sourceLangCode = detectedSourceLang.ToUpper();
                }
            }
            
            // 대상 언어 코드 처리
            string targetLangCode = DeepLxUtils.AbbreviateLanguage(targetLang);
            if (string.IsNullOrEmpty(targetLangCode))
            {
                targetLangCode = targetLang.ToUpper();
            }
            
            bool hasRegionalVariant = false;
            if (targetLang.Contains("-"))
            {
                string[] parts = targetLang.Split('-');
                targetLangCode = parts[0].ToUpper();
                hasRegionalVariant = true;
            }
            
            // 작업 준비
            var job = new JObject
            {
                ["kind"] = "default",
                ["preferred_num_beams"] = 4,
                ["raw_en_context_before"] = new JArray(),
                ["raw_en_context_after"] = new JArray(),
                ["sentences"] = new JArray
                {
                    new JObject
                    {
                        ["prefix"] = "",
                        ["text"] = part,
                        ["id"] = 0
                    }
                }
            };
            
            // 요청 ID 생성
            long requestId = DeepLxUtils.GetRandomNumber();
            
            // 격식 설정 (Python과 동일: null이면 undefined)
            string formality = formal.HasValue ? (formal.Value ? "formal" : "informal") : "undefined";
            
            // 번역 요청 데이터 준비
            var postData = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["method"] = "LMT_handle_jobs",
                ["id"] = requestId,
                ["params"] = new JObject
                {
                    ["commonJobParams"] = new JObject
                    {
                        ["mode"] = "translate",
                        ["formality"] = formality,
                        ["transcribe_as"] = "romanize",
                        ["advancedMode"] = false,
                        ["textType"] = "plaintext",
                        ["wasSpoken"] = false
                    },
                    ["lang"] = new JObject
                    {
                        ["source_lang_user_selected"] = "auto",
                        ["target_lang"] = targetLangCode,
                        ["source_lang_computed"] = sourceLangCode
                    },
                    ["jobs"] = new JArray { job },
                    ["timestamp"] = DeepLxUtils.GetTimestamp(DeepLxUtils.GetICount(part))
                }
            };
            
            // 지역 변형 추가
            if (hasRegionalVariant)
            {
                postData["params"]["commonJobParams"]["regionalVariant"] = targetLang;
            }
            
            // 번역 요청
            JObject response;
            try
            {
                response = await MakeRequest(postData);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DEEPLX] MakeRequest 실패: {ex.Message}");
                return new DeepLxTranslationResult
                {
                    Code = DeepLxConstants.HTTP_STATUS_SERVICE_UNAVAILABLE,
                    Message = $"번역 요청 실패: {ex.Message}"
                };
            }
            
            // 번역 결과 처리
            string partTranslation = "";
            List<string> partAlternatives = new List<string>();
            
            try
            {
                var translations = response["result"]?["translations"] as JArray;
                
                if (translations != null && translations.Count > 0)
                {
                    // 주 번역
                    foreach (var translation in translations)
                    {
                        var beams = translation["beams"] as JArray;
                        if (beams != null && beams.Count > 0)
                        {
                            var sentences = beams[0]["sentences"] as JArray;
                            if (sentences != null && sentences.Count > 0)
                            {
                                partTranslation += sentences[0]["text"]?.ToString() + " ";
                            }
                        }
                    }
                    partTranslation = partTranslation.Trim();
                    
                    // 대안 번역
                    var firstTranslation = translations[0];
                    var firstBeams = firstTranslation["beams"] as JArray;
                    if (firstBeams != null)
                    {
                        int numBeams = firstBeams.Count;
                        for (int i = 1; i < numBeams; i++)  // 0번은 주 번역
                        {
                            string altText = "";
                            foreach (var translation in translations)
                            {
                                var beams = translation["beams"] as JArray;
                                if (beams != null && i < beams.Count)
                                {
                                    var sentences = beams[i]["sentences"] as JArray;
                                    if (sentences != null && sentences.Count > 0)
                                    {
                                        altText += sentences[0]["text"]?.ToString() + " ";
                                    }
                                }
                            }
                            if (!string.IsNullOrWhiteSpace(altText))
                            {
                                partAlternatives.Add(altText.Trim());
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ApiDeepLxUnity] Parse error: {ex.Message}");
            }
            
            if (string.IsNullOrEmpty(partTranslation))
            {
                return new DeepLxTranslationResult
                {
                    Code = DeepLxConstants.HTTP_STATUS_SERVICE_UNAVAILABLE,
                    Message = "번역에 실패했습니다"
                };
            }
            
            translatedParts.Add(partTranslation);
            allAlternatives.Add(partAlternatives);
        }
        
        // 모든 번역 부분을 결합
        string translatedText = string.Join("\n", translatedParts);
        
        // 대안 번역 결합
        List<string> combinedAlternatives = new List<string>();
        int maxAlts = 0;
        foreach (var alts in allAlternatives)
        {
            if (alts.Count > maxAlts)
                maxAlts = alts.Count;
        }
        
        for (int i = 0; i < maxAlts; i++)
        {
            List<string> altParts = new List<string>();
            for (int j = 0; j < allAlternatives.Count; j++)
            {
                if (i < allAlternatives[j].Count)
                {
                    altParts.Add(allAlternatives[j][i]);
                }
                else if (translatedParts[j].Length == 0)
                {
                    altParts.Add("");
                }
                else
                {
                    altParts.Add(translatedParts[j]);
                }
            }
            combinedAlternatives.Add(string.Join("\n", altParts));
        }
        
        return new DeepLxTranslationResult
        {
            Code = DeepLxConstants.HTTP_STATUS_OK,
            Id = DeepLxUtils.GetRandomNumber(),
            Data = translatedText,
            Alternatives = combinedAlternatives,
            SourceLang = detectedSourceLang,
            TargetLang = targetLang,
            Method = "Free"
        };
    }
    
    // DeepL API에 HTTP 요청 (재시도 로직 포함)
    private static async Task<JObject> MakeRequest(JObject postData)
    {
        long requestId = postData["id"].Value<long>();
        
        for (int attempt = 0; attempt < MAX_RETRIES; attempt++)
        {
            try
            {
                // JSON 직렬화
                string jsonString = JsonConvert.SerializeObject(postData, Formatting.None);
                
                // 특수 포맷팅 적용
                jsonString = DeepLxUtils.FormatPostString(jsonString, requestId);
                
                // HTTP 요청 생성
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(DeepLxConstants.API_URL);
                request.Method = "POST";
                request.Timeout = 30000;  // 30초
                
                // 압축 응답 자동 해제 (Python httpx는 자동 처리함)
                request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                
                // 헤더 설정
                foreach (var header in DeepLxConstants.COMMON_HEADERS)
                {
                    if (header.Key == "Content-Type")
                        request.ContentType = header.Value;
                    else if (header.Key == "User-Agent")
                        continue;  // User-Agent는 아래에서 덮어씀
                    else if (header.Key == "Accept-Encoding")
                        continue;  // AutomaticDecompression이 자동으로 처리
                    else if (header.Key == "Accept")
                        request.Accept = header.Value;
                    else if (header.Key == "Referer")
                        request.Referer = header.Value;
                    else
                        request.Headers.Add(header.Key, header.Value);
                }
                
                // 🔥 핵심 수정: 차단되지 않는 User-Agent 사용 (Python과 동일)
                request.UserAgent = "Mozilla/5.0 (iPhone; CPU iPhone OS 18_4 like Mac OS X) AppleWebKit/605.1.15";
                
                // Request body 전송
                byte[] byteArray = Encoding.UTF8.GetBytes(jsonString);
                request.ContentLength = byteArray.Length;
                
                using (Stream dataStream = await request.GetRequestStreamAsync())
                {
                    await dataStream.WriteAsync(byteArray, 0, byteArray.Length);
                }
                
                // 응답 수신
                using (WebResponse response = await request.GetResponseAsync())
                using (Stream responseStream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(responseStream))
                {
                    string responseText = await reader.ReadToEndAsync();
                    return JObject.Parse(responseText);
                }
            }
            catch (WebException ex)
            {
                if (ex.Response != null)
                {
                    var httpResponse = (HttpWebResponse)ex.Response;
                    
                    // 429 에러 처리 (Too Many Requests)
                    if ((int)httpResponse.StatusCode == DeepLxConstants.HTTP_STATUS_TOO_MANY_REQUESTS)
                    {
                        if (attempt < MAX_RETRIES - 1)
                        {
                            int waitTime = (int)Math.Pow(2, attempt);  // 지수 백오프
                            Debug.LogWarning($"[ApiDeepLxUnity] 429 에러, {waitTime}초 후 재시도 ({attempt + 1}/{MAX_RETRIES})");
                            await Task.Delay(waitTime * 1000);
                            continue;
                        }
                    }
                }
                
                // 일반 네트워크 에러
                if (attempt < MAX_RETRIES - 1)
                {
                    int waitTime = 1 + attempt;
                    Debug.LogWarning($"[ApiDeepLxUnity] 연결 오류, {waitTime}초 후 재시도: {ex.Message}");
                    await Task.Delay(waitTime * 1000);
                    continue;
                }
                else
                {
                    throw;
                }
            }
        }
        
        throw new Exception("모든 재시도 실패");
    }
}
