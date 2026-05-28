# Animation2DMotionController 사용 설명서

## 1. 목적

`Animation2DMotionController` 는 정적인 2D 캐릭터 이미지에 아주 가벼운 절차적 모션을 부여하기 위한 컨트롤러입니다.

이 컴포넌트의 목적은 다음과 같습니다.

* 별도의 애니메이션 클립을 추가로 만들지 않고도 2D 이미지에 생동감을 준다
* 기본 Animator 와 충돌하지 않는 선에서 캐릭터의 대기 상태, 대화 상태, 부유감 같은 연출을 만든다
* 외부 시스템에서 현재 상태에 맞는 모션 타입만 바꿔 호출하면 되도록 단순한 인터페이스를 제공한다
* 같은 모션을 다시 요청했을 때 트윈을 불필요하게 재시작하지 않고, 현재 상태를 안정적으로 유지한다

이 시스템은 "프레임마다 직접 계산해서 흔드는 시스템" 이 아니라, "현재 상태에 맞는 트윈을 한 번 적용하고 유지하다가 상태가 바뀌면 그때만 교체하는 시스템" 으로 사용합니다.

## 2. 적용 대상 구조

현재 기준 구조는 아래와 같습니다.

```text
2D_General
├─ Collider
└─ Size
   └─ Image
```

각 계층의 역할은 다음과 같습니다.

* `2D_General`
  최상위 오브젝트입니다.
  `Animation2DMotionController` 가 부착되는 위치입니다.

* `Size`
  기존 Animator 가 관리하는 계층입니다.
  캐릭터 기본 애니메이션, 크기, 블렌드 관련 변화는 이쪽에서 처리합니다.

* `Image`
  실제 렌더링되는 이미지 계층입니다.
  `Animation2DMotionController` 는 이 `Image` 의 `localScale` 과 필요 시 위치 보정을 다룹니다.

현재 구조에서는 `Size` 를 절차적 모션 대상으로 사용하지 않습니다.
이유는 `Size.localScale` 이 이미 Animator 에 의해 덮어써질 수 있기 때문입니다.

따라서 이 시스템은 `Size/Image` 를 찾아서 `Image` 쪽만 모션 대상으로 사용합니다.

## 3. 왜 Image를 직접 조절하는가

초기에는 `Size.localScale` 을 직접 조절하는 방식도 검토했지만, 실제 적용 시 `Animator` 와 같은 속성을 동시에 건드리게 되어 충돌이 발생했습니다.

그래서 현재 방식은 다음 원칙으로 정리되었습니다.

* `Animator` 는 `Size` 를 담당한다
* `Animation2DMotionController` 는 `Image` 를 담당한다

이렇게 역할을 나누면 기존 애니메이션 시스템을 유지하면서도, 이미지에만 별도의 breathing, talking 같은 보정 모션을 얹을 수 있습니다.

즉 최종적인 연출은

* 상위 `Size` 의 애니메이션 결과
* 하위 `Image` 의 절차적 모션 결과

가 합쳐져서 보이게 됩니다.

## 4. 발 위치가 고정되어야 하는 이유

단순히 `Image.localScale` 만 중앙 기준으로 확대, 축소하면 이미지 중심을 기준으로 커졌다 작아지기 때문에, 캐릭터의 발이 바닥에서 뜨거나 내려앉는 느낌이 생길 수 있습니다.

현재 이 문제는 다음 개념으로 해결합니다.

* `Image` 의 pivot 을 아래쪽 기준으로 조정한다
* 스케일 변화에 맞춰 Y 위치를 함께 보정한다

즉, 단순 scale 변화가 아니라 "발 위치는 유지하고 위쪽이 늘어나는 것처럼 보이는 방식" 으로 처리합니다.

이 구조 덕분에 Breathing, Talking 같은 모션이 캐릭터 위쪽 중심으로 자연스럽게 보이도록 만들 수 있습니다.

## 5. 핵심 타입

### 5.1 Animation2DMotionType

현재 모션 종류는 아래처럼 관리합니다.

```csharp
public enum Animation2DMotionType
{
    None,         // 모션 없음
    Breathing,    // 천천히 살아있는 느낌
    IdleBounce,   // 통통 튀는 느낌
    Floating,     // 둥실둥실 떠있는 느낌
    Talking       // 말할 때 뿌요뿌요한 느낌
}
```

각 타입의 의미는 다음과 같습니다.

* `None`
  모션을 멈추고 기본 상태로 되돌립니다.

* `Breathing`
  캐릭터가 가만히 있어도 완전히 정지해 보이지 않도록, 느리고 부드럽게 숨 쉬는 느낌을 줍니다.

* `IdleBounce`
  가볍게 통통 튀는 대기 느낌입니다. 마스코트나 사물형 캐릭터에 더 잘 어울립니다.

* `Floating`
  위아래로 둥실둥실 떠 있는 느낌입니다. 캐릭터보다는 부유물, 아이콘, 사물 계열에 더 잘 맞습니다.

* `Talking`
  대화 중일 때 더 빠르고 더 말랑하게 눌렸다 펴지는 느낌입니다. `Breathing` 보다 훨씬 리듬감 있게 움직입니다.

## 6. 모션 설정 데이터

각 모션 타입의 세부 수치는 `Animation2DMotionTypeInfo` 로 관리합니다.

```csharp
[System.Serializable]
public struct Animation2DMotionTypeInfo
{
    public Animation2DMotionType MotionType;
    public float ScaleX;
    public float ScaleY;
    public float MoveY;
    public float Duration;
    public Ease Ease;
}
```

각 값의 의미는 다음과 같습니다.

* `MotionType`
  이 설정이 어떤 모션 타입용인지 구분하는 키입니다.

* `ScaleX`
  X축 목표 배율입니다.

* `ScaleY`
  Y축 목표 배율입니다.

* `MoveY`
  Y축 이동량입니다.
  주로 `IdleBounce`, `Floating` 같은 위치 이동형 모션에서 사용합니다.

* `Duration`
  한 방향으로 이동하는 데 걸리는 시간입니다.
  왕복 모션의 경우 실제 한 사이클은 대략 `Duration x 2` 로 보면 됩니다.

* `Ease`
  DOTween easing 방식입니다.
  모션의 리듬과 감촉을 좌우합니다.

## 7. 기본 동작 방식

이 컨트롤러는 다음 흐름으로 동작합니다.

### 7.1 Awake

컴포넌트 초기화 시점에 다음 작업을 수행합니다.

* `Size/Image` 를 모션 대상으로 찾는다
* `Image` 의 기본 스케일과 기본 위치를 저장한다
* 기본 모션 정보가 없으면 `InitAnimation2DMotionTypeInfo()` 로 기본값을 넣는다

### 7.2 OnEnable

오브젝트가 활성화되면 현재 요청된 모션 타입을 기준으로 필요한 트윈을 적용합니다.

### 7.3 SetMotionType

외부에서 상태를 바꾸고 싶을 때는 `SetMotionType(motionType)` 하나만 호출하면 됩니다.

이 함수는 내부적으로 현재 요청 상태를 바꾸고, 현재 적용 상태와 비교한 뒤 정말 변경이 필요할 때만 트윈을 교체합니다.

즉 같은 모션 타입을 다시 넣으면 아무 일도 하지 않습니다.

### 7.4 OnDisable / OnDestroy

오브젝트가 비활성화되거나 파괴될 때는 현재 트윈을 정리하고, 이미지의 scale과 position을 기본 상태로 되돌립니다.

## 8. 왜 SetMotionType 하나만 쓰는가

외부 시스템에서 모션을 제어할 때는 전용 함수 여러 개를 만들지 않고, 아래 함수 하나만 사용합니다.

```csharp
SetMotionType(Animation2DMotionType motionType)
```

이렇게 한 이유는 다음과 같습니다.

* 외부 호출 방식이 단순해집니다
* 상태 전환 로직이 한 곳으로 모입니다
* `Breathing`, `Talking`, `None` 등을 모두 같은 진입점으로 다룰 수 있습니다
* 추후 타입이 늘어나도 함수 개수를 계속 추가할 필요가 없습니다

즉 외부에서는 다음처럼만 사용하면 됩니다.

```csharp
motionController.SetMotionType(Animation2DMotionType.Breathing);
motionController.SetMotionType(Animation2DMotionType.Talking);
motionController.SetMotionType(Animation2DMotionType.None);
```

## 9. 현재 적용 상태와 요청 상태

내부적으로는 다음 두 상태를 구분합니다.

* `requestedMotionType`
  지금 외부가 원하고 있는 목표 상태

* `currentMotionType`
  지금 실제로 적용되어 있는 상태

이 두 값을 비교해서,
정말 바뀌었을 때만 기존 트윈을 죽이고 새 트윈을 적용합니다.

이 구조 덕분에 다음 문제가 줄어듭니다.

* 같은 모션을 반복 호출할 때 매번 트윈이 다시 시작되는 문제
* 상태 갱신이 자주 들어와도 모션이 끊기는 문제
* 불필요한 Kill / Create 반복

즉 이 시스템의 목표는 "항상 다시 재생" 이 아니라 "현재 상태에 맞는 모션을 안정적으로 유지" 입니다.

## 10. 각 모션의 기본 의도

### 10.1 Breathing

천천히 살아있는 느낌을 만드는 기본 대기 모션입니다.

권장 성격

* 변화량이 작다
* 속도가 느리다
* 오래 켜도 피로하지 않다
* 캐릭터 기본 idle 상태에 적합하다

기본값 예시

* `ScaleX = 0.99`
* `ScaleY = 1.02`
* `Duration = 1.4`
* `Ease = InOutSine`

### 10.2 IdleBounce

통통 튀는 느낌을 만드는 모션입니다.

권장 성격

* `Breathing` 보다 가볍고 튀는 느낌
* Y 이동을 함께 사용
* SD 캐릭터나 마스코트에 적합

### 10.3 Floating

둥실둥실 떠다니는 느낌을 만드는 모션입니다.

권장 성격

* scale보다 위치 변화가 중심
* 사물, 아이콘, 부유 오브젝트에 적합
* 캐릭터 본체에 쓰면 발 접지감이 약해질 수 있음

### 10.4 Talking

말할 때 뿌요뿌요한 느낌을 만드는 모션입니다.

권장 성격

* `Breathing` 보다 더 빠르다
* 더 탄력 있게 늘어나고 줄어든다
* 대화 중이나 음성 재생 중에 적합하다

기본값 예시

* `ScaleX = 0.95`
* `ScaleY = 1.08`
* `Duration = 0.18`
* `Ease = OutQuad`

## 11. Ease 설정 의미

현재 기본적으로 쓰는 `Ease` 값의 해석은 다음과 같습니다.

* `Ease.InOutSine`
  천천히 시작하고 천천히 끝나는 부드러운 왕복입니다.
  `Breathing`, `Floating`, `IdleBounce` 처럼 자연스럽고 끊김 없는 루프에 잘 맞습니다.

* `Ease.OutQuad`
  시작이 조금 더 빠르고, 끝으로 갈수록 부드럽게 풀립니다.
  `Talking` 처럼 더 탄력 있고 말랑한 느낌에 잘 맞습니다.

즉 모션의 느낌은 단순히 `ScaleX`, `ScaleY` 만이 아니라 `Ease` 에도 크게 영향을 받습니다.

## 12. 사용 예시

### 12.1 기본 대기 상태

캐릭터가 평상시 가만히 있을 때는 `Breathing` 을 사용합니다.

```csharp
motionController.SetMotionType(Animation2DMotionType.Breathing);
```

### 12.2 대화 시작

음성 출력이나 텍스트 재생이 시작되면 `Talking` 으로 바꿉니다.

```csharp
motionController.SetMotionType(Animation2DMotionType.Talking);
```

### 12.3 대화 종료

말이 끝났으면 다시 `Breathing` 으로 돌립니다.

```csharp
motionController.SetMotionType(Animation2DMotionType.Breathing);
```

### 12.4 완전 정지

숨쉬기 포함 모든 보정을 끄고 기본 상태로 돌리고 싶을 때는 `None` 을 사용합니다.

```csharp
motionController.SetMotionType(Animation2DMotionType.None);
```

## 13. 적용 시 주의점

### 13.1 Size는 Animator가 관리한다

`Size` 계층은 기존 Animator 와 충돌할 수 있으므로 절차적 모션 대상이 아닙니다.

### 13.2 Image를 모션 대상으로 사용한다

`Animation2DMotionController` 는 `Size/Image` 의 `localScale` 과 위치 보정을 사용합니다.

### 13.3 Pivot과 위치 보정이 중요하다

단순 중앙 기준 scale만 쓰면 발이 뜨는 느낌이 생길 수 있으므로, pivot과 Y 보정 방식이 함께 고려되어야 합니다.

### 13.4 같은 상태를 반복 요청해도 재시작하지 않는다

이건 의도된 동작입니다.
모션 안정성을 위해 현재 적용 상태와 같은 요청은 무시합니다.

## 14. 어떤 상황에 붙이면 좋은가

이 시스템은 아래 상황에 적합합니다.

* 정적인 2D 캐릭터를 조금 더 살아있게 보이게 하고 싶을 때
* 기존 Animator 를 유지한 채 추가 연출만 덧붙이고 싶을 때
* 대기 상태와 대화 상태를 코드로 쉽게 전환하고 싶을 때
* 한 장짜리 이미지 기반 캐릭터에 가벼운 procedural motion 을 넣고 싶을 때

반대로, 아래 같은 경우는 별도 방식이 더 적합할 수 있습니다.

* 파츠가 분리된 상체, 하체, 표정, 장식까지 각각 따로 흔들어야 할 때
* 말할 때 입만 별도로 움직여야 할 때
* 랜덤 위상, 잡음 기반 흔들림, 복합 상태 합성이 필요한 경우

## 15. 정리

`Animation2DMotionController` 는
현재 구조를 크게 바꾸지 않고도 2D 캐릭터 이미지에 breathing, talking 같은 절차적 모션을 붙이기 위한 공용 컨트롤러입니다.

핵심은 다음과 같습니다.

* 구조는 `2D_General -> Size -> Image`
* `Size` 는 Animator 담당
* `Image` 는 절차적 모션 담당
* 외부에서는 `SetMotionType(Animation2DMotionType motionType)` 하나만 호출
* 같은 상태는 재시작하지 않고 유지
* `Breathing` 은 느리고 부드럽게
* `Talking` 은 더 빠르고 말랑하게
* 발 위치는 pivot 및 위치 보정으로 안정화

이 기준으로 사용하면,
평소에는 `Breathing`,
대화 중에는 `Talking`,
필요 시 `None` 으로 정지시키는 방식으로 일관되게 운용할 수 있습니다.

원하시면 다음에는 이 문서를 마크다운 파일 형태로 바로 붙여넣기 좋게 다듬어서 다시 정리해드리겠습니다.
