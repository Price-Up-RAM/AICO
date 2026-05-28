using System.Collections.Generic;

// DeepLX 번역 결과 클래스
// Python deeplx_python/translate.py의 DeepLXTranslationResult 포팅
public class DeepLxTranslationResult
{
    public int Code { get; set; }
    public string Message { get; set; }
    public string Data { get; set; }
    public List<string> Alternatives { get; set; }
    public string SourceLang { get; set; }
    public string TargetLang { get; set; }
    public string Method { get; set; }
    public long Id { get; set; }
    
    public DeepLxTranslationResult()
    {
        Code = 0;
        Message = "";
        Data = "";
        Alternatives = new List<string>();
        SourceLang = "";
        TargetLang = "";
        Method = "Free";
        Id = 0;
    }
}
