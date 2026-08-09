# AICO 데스크톱 개발 — MR 호환성 규칙

**대상**: 데스크톱 버전(`SampleScene` / `SampleSceneKAI`)을 개발하는 사람 및 AI 에이전트
**목적**: 데스크톱 쪽 변경이 MR 포팅(`SampleSceneKAI-MR`)에 재작업을 유발하지 않도록 한다
**최종 수정**: 2026-08-02

---

## 0. 배경 — 왜 이 문서가 필요한가

AICO는 **데스크톱(Windows)** 과 **Meta Quest MR** 두 방향으로 빌드된다. 두 빌드는 다음을 공유한다:

- `Assets/Migration/Root260616.prefab` (씬 본체, 커스텀 스크립트 120종)
- `Assets/Char/*.prefab` (캐릭터)
- `Assets/Scripts/*.cs` 대부분
- Python 백엔드

**에셋과 로직은 자동으로 공유되지만, 플랫폼 가정은 공유되지 않는다.** 데스크톱 전용 코드가 가드 없이 들어오면 MR 빌드에서 크래시하거나, 조용히 성능을 갉아먹는다.

실제 사례 — MR 포팅 Phase 1에서 발견한 것:

| 증상 | 원인 | 비용 |
|---|---|---|
| 프레임의 60%가 대기 상태 | `TransparentWindow.PickColorCoroutine()`이 매 프레임 GPU readback | **14.9 ms** |
| GC 폭주, 초당 예외 82회 | `MenuTriggerKAI`가 캔버스 하위 위젯을 널 체크 없이 참조 | 프레임당 11.3 KB |
| CPU 스파이크 초당 4회 | `KAIManager`가 `FindObjectsByType(Include inactive)` 전수 스캔 | **3.9 ms** |

`TransparentWindow`는 **`DllImport`에 이미 `#if UNITY_STANDALONE_WIN` 가드가 있었는데도** 문제가 됐다. Win32 호출은 막았지만 **코루틴 로직 자체는 계속 돌았기** 때문이다. 이 문서의 규칙 대부분은 이 사건에서 나왔다.

---

## 1. 필수 규칙

### 1-1. 데스크톱 전용 로직은 P/Invoke가 없어도 가드한다

**가장 중요한 규칙.** `DllImport`만 가드하는 것으로는 부족하다.

Win32 API를 직접 호출하지 않더라도, 아래에 의존하는 **로직 전체**를 가드해야 한다:

- 데스크톱 화면 / 모니터 해상도 / 마우스 커서 좌표
- 창(window) · 작업표시줄 · 트레이 · 클립보드
- 화면 캡처 · 픽셀 읽기(`ReadPixels` / `AsyncGPUReadback` / `Texture2D.ReadPixels`)
- 파일 탐색기 · 외부 프로세스 실행

```csharp
// ❌ 나쁨 — P/Invoke만 가드. 코루틴은 MR에서도 계속 돈다
#if UNITY_STANDALONE_WIN
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT p);
#endif

    private IEnumerator PickColorCoroutine()
    {
        while (true)
        {
            yield return new WaitForEndOfFrame();
            tex.ReadPixels(...);       // ← Win32가 아니지만 MR에서 무의미하고 매우 비싸다
        }
    }
```

```csharp
// ✅ 좋음 — 로직 진입점을 가드
    private IEnumerator PickColorCoroutine()
    {
#if !UNITY_STANDALONE_WIN
        yield break;   // MR/Android: 대상 화면이 없다
#else
        while (true) { ... }
#endif
    }
```

또는 아예 코루틴을 시작하지 않는다:

```csharp
    private void Start()
    {
#if UNITY_STANDALONE_WIN
        StartCoroutine(PickColorCoroutine());
#endif
    }
```

### 1-2. P/Invoke 선언은 스텁으로 대체한다 (호출부 무수정 패턴)

이 프로젝트는 이미 이 패턴을 쓴다. **`DllImport` 선언만 감싸고, 같은 시그니처의 안전한 스텁을 `#else`에 둔다.** 그러면 호출부를 한 줄도 고치지 않아도 된다.

```csharp
#if UNITY_STANDALONE_WIN
    [DllImport("user32.dll")]
    private static extern IntPtr FindWindow(string cls, string name);
#else
    // 창이 존재하지 않으므로 항상 "못 찾음"
    private static IntPtr FindWindow(string cls, string name) => IntPtr.Zero;
#endif
```

기존 실패 처리 경로(`if (handle == IntPtr.Zero) return;`)가 자연스럽게 받아준다.

**스텁 반환값 가이드:**

| 반환형 | 스텁 값 |
|---|---|
| `IntPtr` (핸들) | `IntPtr.Zero` |
| `bool` (성공 여부) | `false` |
| `out` 구조체 | `default` + `return false` |
| 화면 크기 | `Screen.width` / `Screen.height` |
| 열거 콜백 (`EnumWindows`) | 콜백 호출 없이 `false` |

### 1-3. `System.Drawing` / `System.Windows.Forms`는 `using`부터 가드한다

Android(IL2CPP)에는 이 어셈블리가 **없다.** 컴파일 자체가 실패한다.

```csharp
using System;
using UnityEngine;
#if UNITY_STANDALONE_WIN
using System.Drawing;
using System.Windows.Forms;
#endif
```

메서드 본문도 함께 가드하되, **`public` 시그니처와 `[SerializeField]` 필드는 모든 플랫폼에 남긴다.** 안 그러면 프리팹 직렬화 데이터가 깨진다.

```csharp
public class TrayIconManager : MonoBehaviour
{
    public UnityEngine.UI.Image iconImage;   // ← 플랫폼 무관하게 유지 (직렬화)

#if UNITY_STANDALONE_WIN
    private NotifyIcon trayIcon;
    public void HideWindow() { /* 실제 구현 */ }
#else
    public void HideWindow() { }             // ← 시그니처 유지, no-op
#endif
}
```

### 1-4. `Instance` 싱글턴은 살려둔다 — 클래스 전체를 `#if`로 날리지 말 것

클래스를 통째로 제거하면 `SomeManager.Instance`를 참조하는 모든 곳이 컴파일 에러 또는 런타임 NRE가 된다.

**클래스와 싱글턴은 유지하고, 구현부만 가드한다.**

```csharp
// ❌ 나쁨
#if UNITY_STANDALONE_WIN
public class WindowCollisionManager : MonoBehaviour { ... }
#endif

// ✅ 좋음 — 클래스는 항상 존재, 내부만 가드
public class WindowCollisionManager : MonoBehaviour
{
    public static WindowCollisionManager Instance { get; private set; }

    public float GetTopOfCollisionRect(Vector2 pos)
    {
#if UNITY_STANDALONE_WIN
        ... 실제 판정 ...
#endif
        return -99999f;   // 충돌 없음 — 호출부가 이미 이 값을 처리한다
    }
}
```

### 1-5. 외부 참조는 반드시 널 체크한다

MR 씬에서는 캔버스·카메라·매니저가 비활성이거나 아예 없을 수 있다. **`Find` 계열로 얻은 참조는 전부 널 체크한다.**

```csharp
// ❌ 나쁨 — 위젯이 없는 씬에서 매 프레임 예외
    private void Update()
    {
        if (m_RadialMenu.IsVisible) { ... }
    }

// ✅ 좋음
    private void Update()
    {
        if (m_RadialMenu != null && m_RadialMenu.IsVisible) { ... }
    }
```

`Update()` 안의 예외는 특히 치명적이다. 예외 자체보다 **스택 트레이스 문자열 생성이 프레임당 수 KB를 할당**해 GC를 폭주시킨다. 실측에서 초당 800 KB였다.

---

## 2. 성능 규칙

### 2-1. `Update()`에서 `FindObjectsByType` / `FindObjectOfType` 금지

특히 `FindObjectsInactive.Include`는 **비활성 오브젝트까지 전수 순회**한다. `Root260616`은 GameObject가 2588개다. Quest에서 1회 3.9 ms가 나왔다.

```csharp
// ❌ 나쁨
    private void Update() { var x = FindObjectsByType<Foo>(FindObjectsInactive.Include, ...); }

// ✅ 좋음 — 캐시하거나, 이벤트 기반으로, 아니면 아주 느린 주기로
    private void Start() { _foos = FindObjectsByType<Foo>(...); }
```

부득이하게 주기적 스캔이 필요하면:
- 주기를 **5초 이상**으로
- `FindAnyObjectByType`으로 존재 여부를 먼저 확인해 배열 할당을 피할 것
- 가능하면 상태 변화 감지(O(1))로 대체 — 예: `CharManager.Instance.GetCurrentCharacter()`가 바뀌었는지만 비교

### 2-2. 매 프레임 GPU readback 금지

`Texture2D.ReadPixels`, `RenderTexture` 읽기, `Gfx.ReadbackImage`는 **CPU가 GPU를 기다리게 만든다.** 데스크톱에서는 티가 안 나지만 Quest에서는 프레임을 통째로 날린다.

필요하면 `AsyncGPUReadback`을 쓰고, 주기를 낮추고, **반드시 `#if UNITY_STANDALONE_WIN`으로 감쌀 것.**

### 2-3. UI를 하나의 거대 캔버스에 몰지 말 것

`Canvases/Canvas`에 UI 요소가 **2029개** 있다. 요소 하나만 바뀌어도 캔버스 전체가 dirty가 되어 배치를 재구성한다. Quest에서 프레임당 4.2 ms였다.

새 UI 패널을 추가할 때는:
- **패널 단위로 자식 `Canvas` 컴포넌트를 붙여 리빌드 범위를 격리**할 것
- 자주 갱신되는 요소(타이머·게이지)는 정적 요소와 다른 캔버스로 분리

이건 데스크톱 성능에도 이득이다.

### 2-4. 항상 도는 코루틴/타이머는 끌 수 있게 만들 것

`[SerializeField] private bool enableXxx = true;` 같은 플래그를 두거나, 최소한 `enabled = false`로 멈출 수 있게 한다. MR 쪽에서 `MRSceneStripper`가 컴포넌트 단위로 끄기 때문이다.

---

## 3. 씬 / 프리팹 규칙

### 3-1. 새 매니저는 기존 매니저 오브젝트에 붙인다

`Root260616/Manager/GameManager` 또는 `.../UIManager`. MR 쪽 `MRSceneStripper`가 **이 두 오브젝트를 감사**해서 새 컴포넌트를 자동 감지한다.

다른 곳에 새 루트 오브젝트를 만들면 감지 대상에서 빠져 조용히 MR로 흘러 들어간다. 부득이하면 MR 담당자에게 알릴 것.

### 3-2. `Root260616.prefab` 구조를 크게 바꾸지 말 것

MR 씬은 이 프리팹을 **Unpack하지 않고 인스턴스로 참조**한다(동기화 유지 목적). 다만 MR 씬에는 다음 오버라이드가 걸려 있다:

- `Cameras/Main Camera` · `UI Camera` · `Effect Camera` — 비활성
- `Legacy/PIP` · `Tester` · `Manager/DevManager` — 비활성
- 세 `Canvas`의 `renderMode` (Phase 3에서 World Space로 전환 예정)

**위 경로의 오브젝트를 삭제하거나 이름을 바꾸면 MR 씬의 오버라이드가 깨진다.** 변경이 필요하면 미리 공유할 것.

### 3-3. 캐릭터 프리팹의 루트 구조를 바꾸지 말 것

`Aico.prefab`의 루트는 `RectTransform`(`localScale 120`)이다. MR에서는 이 캐릭터를 **런타임에 월드 루트로 리페어런트**해서 쓴다(`worldPositionStays: true`). 프리팹은 수정하지 않는다.

루트를 `Transform`으로 바꾸거나 계층을 재구성하면 MR 쪽 리페어런트 로직이 깨진다.

### 3-4. 캐릭터에 새 입력/물리 컴포넌트를 붙였다면 알릴 것

현재 `Aico.prefab`에는 `ClickHandler` · `DragHandler` · `WheelHandler` · `FallingObject` · `MenuTrigger`가 붙어 있고, MR에서는 이들을 런타임에 **비활성화한 뒤 MR 대체 컴포넌트로 교체**한다.

새 컴포넌트를 추가하면 `MRSceneStripper.CharacterDesktopTypes`에도 등록해야 한다.

---

## 4. 절대 하면 안 되는 것

| 금지 | 이유 |
|---|---|
| `Assets/Scripts/MR/**` 수정 | MR 전용. 데스크톱과 무관 |
| `Assets/Scripts/KAI/**` 수정 | KAI 제출용 프로토타입 전용 |
| 기존 `#if UNITY_STANDALONE_WIN` / `#if !UNITY_ANDROID` 가드 제거 | MR 빌드가 깨진다 |
| `Assets/Scenes/SampleSceneKAI-MR.unity` 수정 | MR 담당 영역 |
| 클래스 이름 변경 | `MRSceneStripper`가 `typeof()`로 참조한다. 이름을 바꾸면 컴파일 에러가 난다 (이건 의도된 안전장치이니, 에러가 나면 MR 담당자에게 알릴 것) |

---

## 5. 커밋 전 자가 점검

새 코드를 추가했다면:

- [ ] `DllImport`를 썼는가? → `#if UNITY_STANDALONE_WIN` + `#else` 스텁 (§1-2)
- [ ] `System.Drawing` / `System.Windows.Forms`를 썼는가? → `using`부터 가드 (§1-3)
- [ ] 화면 좌표 · 창 · 커서 · 클립보드 · 화면 캡처에 의존하는가? → **P/Invoke가 없어도 로직을 가드** (§1-1)
- [ ] `Update()` / 코루틴에서 `Find*` 계열을 호출하는가? → 캐시 또는 이벤트 기반으로 (§2-1)
- [ ] `Update()`에서 외부 참조를 역참조하는가? → 널 체크 (§1-5)
- [ ] `GameManager` / `UIManager` 외의 새 루트 오브젝트를 만들었는가? → MR 담당자에게 공유 (§3-1)
- [ ] 캐릭터 프리팹에 컴포넌트를 추가했는가? → MR 담당자에게 공유 (§3-4)

**빠르게 확인하는 법**: Build Settings에서 플랫폼을 **Android로 전환**해 컴파일이 통과하는지 본다. 대부분의 `#if` 누락은 여기서 잡힌다. (로직 가드 누락은 안 잡히므로 §1-1은 사람이 판단해야 한다.)

---

## 6. AI 에이전트에게 주는 요약 프롬프트

데스크톱 개발을 AI에게 맡길 때 아래를 컨텍스트로 함께 제공할 것:

```
이 프로젝트(AICO)는 Windows 데스크톱과 Meta Quest MR 두 플랫폼으로 빌드된다.
코드와 프리팹 대부분을 공유하므로, 데스크톱 전용 코드를 작성할 때 아래를 반드시 지켜라.

1. Win32 P/Invoke(DllImport)는 #if UNITY_STANDALONE_WIN으로 감싸고,
   #else에 같은 시그니처의 안전한 스텁을 둔다(호출부를 고치지 않기 위해).
   IntPtr→IntPtr.Zero, bool→false, 화면크기→Screen.width/height.

2. P/Invoke가 없더라도 데스크톱 화면·창·마우스 커서·클립보드·화면 캡처에
   의존하는 "로직 전체"를 가드하라. 특히 매 프레임 도는 코루틴과
   ReadPixels 계열 GPU readback. (이걸 놓쳐서 MR에서 프레임의 60%를 날린 전례가 있다)

3. System.Drawing / System.Windows.Forms는 Android에 없다. using부터 가드하되,
   public 메서드 시그니처와 [SerializeField] 필드는 모든 플랫폼에 남겨라
   (프리팹 직렬화가 깨진다).

4. 클래스 전체를 #if로 제거하지 마라. 싱글턴 Instance가 null이 되어
   호출부가 전부 터진다. 클래스는 남기고 구현부만 가드하라.

5. Update()에서 FindObjectsByType/FindObjectOfType을 호출하지 마라.
   특히 FindObjectsInactive.Include는 씬 전체(2588 오브젝트)를 순회한다.

6. Update()에서 외부 참조(WidgetUtility.Find 등으로 얻은 것)를 역참조하기 전에
   반드시 null 체크하라. MR 씬에서는 캔버스가 비활성일 수 있다.

7. 새 매니저는 Root260616/Manager/GameManager 또는 UIManager에 붙여라.
   Assets/Scripts/MR/** 와 Assets/Scripts/KAI/** 는 수정하지 마라.

작업 후 Build Settings를 Android로 전환해 컴파일이 통과하는지 확인하라.
```

---

## 참고

- MR 포팅 설계서: `Assets/Scripts/Plan/SampleSceneKAI_MR_Port_Plan.md`
- Phase 1 결과·실측: `Assets/Scripts/Plan/SampleSceneKAI_MR_Phase1_Checklist.md`
- MR 컴포넌트 분류 목록: `Assets/Scripts/MR/MRSceneStripper.cs`
- KAI 프로토타입 원본 불변 원칙: `Assets/Scripts/KAI/WORKLOG.md`
