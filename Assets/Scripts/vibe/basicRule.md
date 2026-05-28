# 코딩 스타일 가이드

## 주석 규칙

### 메소드 주석
- 메소드 위에는 **한 줄 `//` 주석** 사용
- XML 스타일 주석(`/// <summary>`) 사용 금지
- input, output 설명 불필요

```csharp
// VL Agent 이벤트 로그 추가
public void AddVlAgentLog(string message)
{
    ...
}
```

### 코드 블록 주석
- 단락, 문단, 행위별로 현재 작업 정리
- 명확하고 간결하게 작성

```csharp
// 짧은 목록에 추가 (FIFO)
shortDebugLogs.Enqueue(formattedLog);

// 최대 개수 초과 시 제거
while (shortDebugLogs.Count > MAX_DEBUG_NUM)
{
    shortDebugLogs.Dequeue();
}
```

### 변수 선언 주석
- 변수 선언 시 같은 줄 우측에 `//` 설명 추가
- 줄바꿈 없이 간결하게

```csharp
private Queue<string> shortDebugLogs = new Queue<string>();  // 짧은 목록 (6개)
private bool isShowingFullHistory = false;  // 현재 표시 모드
```

## Null 체크 규칙

### Instance는 null 체크 하지 않음
- 싱글톤 Instance는 항상 존재한다고 가정
- `Instance != null` 체크 금지

```csharp
// ❌ 잘못된 예
if (UIPositionManager.Instance != null && debugBalloonTransform != null)
{
    debugBalloonTransform.position = UIPositionManager.Instance.GetMenuPosition("debugBalloon2");
}

// ✅ 올바른 예
if (debugBalloonTransform != null)
{
    debugBalloonTransform.position = UIPositionManager.Instance.GetMenuPosition("debugBalloon2");
}
```

## 조건문 규칙

### 명시적인 if-else 블록 사용
- 삼항 연산자(`? :`) 사용 금지
- 1줄짜리 짧은 `if`문 이라도 중괄호 `{}` 를 포함한 명시적인 `if-else` 블록 형태로 작성
- 가독성을 위해 조건에 따른 분기를 명확하게 표현하고 주석도 알기 쉽게 표현해야 함.

```csharp
// ❌ 잘못된 예 (삼항 연산자)
favoriteImage.color = charData.isFavorite ? colorOn : colorOff;

// ❌ 잘못된 예 (1줄 if문)
if (isTrue) doSomething();

// ✅ 올바른 예
if (charData.isFavorite)
{
    // on 일때
    favoriteImage.color = colorOn;
}
else
{
    // off 일때
    favoriteImage.color = colorOff;
}
```

## 싱글톤(Singleton) 정의 규칙

### 지연 초기화(Lazy Initialization) 방식 사용
- 싱글톤 인스턴스는 필요한 시점에 `FindObjectOfType`을 통해 찾아 할당되도록 `get` 프로퍼티 내부에서 정의합니다.
- `Awake`에서 `Instance = this;` 형태로 할당하는 방식 대신, 아래와 같은 구조를 표준으로 사용합니다.

```csharp
private static ClassName instance; // 싱글톤 인스턴스
public static ClassName Instance
{
    get
    {
        if (instance == null)
        {
            // 인스턴스가 없으면 찾아서 할당
            instance = FindObjectOfType<ClassName>();
        }
        return instance;
    }
}
```

