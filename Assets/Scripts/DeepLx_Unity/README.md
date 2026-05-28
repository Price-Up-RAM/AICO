# DeepLx_Unity - Standalone DeepLX C# Module

## 개요
Python `deeplx_python`을 Unity C#로 완전히 포팅한 standalone 모듈입니다.

## 파일 구조
```
DeepLx_Unity/
├── ApiDeepLxUnity.cs             # 메인 번역 클래스
├── DeepLxConstants.cs            # 상수 (URL, 헤더, 언어 매핑)
├── DeepLxUtils.cs                # 유틸리티 함수
├── DeepLxTranslationResult.cs    # 결과 데이터 클래스
└── README.md                     # 이 파일
```

## 사용법

### 간편 번역
```csharp
string translated = await ApiDeepLxUnity.Translate("Hello", "ko", formal: true);
// 결과: "안녕하세요"
```

### 상세 결과
```csharp
var result = await ApiDeepLxUnity.TranslateDetailed("Hello", "ko", formal: true);
Debug.Log($"번역: {result.Data}");
Debug.Log($"대안: {string.Join(", ", result.Alternatives)}");
Debug.Log($"언어: {result.SourceLang} → {result.TargetLang}");
```

## 주요 기능

### 1. iOS 앱 흉내 헤더
DeepL의 차단을 우회하기 위해 iOS 앱의 헤더를 사용합니다.

### 2. 특수 JSON 포맷팅
요청 ID에 따라 JSON 공백 위치를 조정하여 DeepL API 요구사항을 충족합니다.

### 3. 타임스탬프 생성
텍스트 내 'i' 문자 개수를 기반으로 특수한 타임스탬프를 생성합니다.

### 4. 재시도 로직
- 429 에러 시 지수 백오프 (1초, 2초, 4초)
- 일반 네트워크 에러 시 선형 재시도
- 최대 3회 재시도

### 5. 존댓말 처리
`formal` 파라미터로 격식 있는 번역을 요청할 수 있습니다.

## 지원 언어
한국어(ko), 일본어(ja), 영어(en), 중국어(zh), 독일어(de), 프랑스어(fr), 스페인어(es), 이탈리아어(it), 러시아어(ru), 포르투갈어(pt) 등 30개 이상 언어 지원

## 제한사항
- 인터넷 연결 필수
- DeepL API가 차단 로직을 업데이트하면 동작하지 않을 수 있음
- 무료 API이므로 429 에러 발생 가능

## 의존성
- Newtonsoft.Json (Unity에서 기본 제공)
- .NET 4.x 이상

## Python 원본 대비 차이점
- `langdetect` 라이브러리 사용 안 함 (자동 언어 감지 간소화)
- Vercel 백업 서비스 미포함 (DeepL API만 사용)
- 동기/비동기 모두 async/await 패턴 사용
