# 🎮 Unity UI & Camera 시스템 구축 보고서

## 1. 프로젝트 개요
*   **목적:** 투명 윈도우 환경에서 캐릭터(3D/물리)와 UI가 겹칠 때, 렌더링 순서를 보장하고 클릭(Raycast) 우선순위를 올바르게 설정함.
*   **환경:** Unity URP (Universal Render Pipeline).

## 2. 카메라 시스템 설정 (Camera Stacking)
URP에서는 `Depth` 대신 **Camera Stack**을 사용하여 렌더링 순서를 제어합니다.

### **A. Main Camera (Base)**
*   **역할:** 배경 및 캐릭터(3D 물체) 렌더링 담당.
*   **Render Type:** `Base`
*   **Culling Mask:** `Char` 레이어만 체크 (UI 제외).
*   **Stack:** `UI Camera`를 리스트에 추가하여 UI가 캐릭터 위에 그려지도록 설정.
*   **Physics Raycaster:** 캐릭터 클릭을 위해 유지하되, **Event Mask**에서 `UI`는 체크 해제.

### **B. UI Camera (Overlay)**
*   **역할:** UI 전용 렌더링 및 UI 클릭 감지.
*   **Render Type:** `Overlay`
*   **Culling Mask:** `UI` 레이어만 체크.
*   **Physics Raycaster:** 불필요하므로 **삭제**.
*   **Audio Listener:** 중복 방지를 위해 **삭제**.
*   **Tag:** `MainCamera` 중복 시 원본 카메라가 삭제될 수 있으므로 `Untagged` 권장.

## 3. 캔버스(Canvas) 구성 및 최적화
각 용도에 맞게 캔버스를 분리하고 레이캐스트 설정을 최적화했습니다.

| 항목 | 메인 UI 캔버스 (`Canvas`) | 캐릭터 UI 캔버스 (`Canvas_Char`) |
| :--- | :--- | :--- |
| **Render Mode** | `Screen Space - Camera` | `Screen Space - Camera` |
| **Render Camera** | **UI Camera** | **Main Camera** |
| **Plane Distance** | 100 (캐릭터와 동일하게 유지) | 100 |
| **Order in Layer** | **10 (높게 설정하여 우선권 획득)** | **0 (기본값)** |
| **Graphic Raycaster** | `Blocking Objects: None` | 필요 없을 경우 삭제 가능 |

## 4. 레이캐스트(클릭) 충돌 해결 전략
캐릭터가 UI보다 먼저 클릭되거나, UI의 빈 공간이 캐릭터 클릭을 방해하는 문제 해결법입니다.

### **✅ UI 우선순위 확보 방법**
1.  **Order in Layer 차별화:** `Plane Distance`가 같을 때, `Order in Layer` 숫자가 높은 캔버스가 클릭을 먼저 받습니다.
2.  **투명 배경 처리:** UI 캔버스 내 화면 전체를 덮는 **Panel/Image**가 있다면, 반드시 **`Raycast Target`을 체크 해제**해야 합니다. (체크 시 뒤에 있는 캐릭터 클릭 차단)
3.  **레이어 분리:** `Graphic Raycaster`의 `Blocking Objects`를 **`None`**으로 설정하여 UI가 물리 환경의 방해를 받지 않게 합니다.

## 5. 개발 생산성 및 안정성 팁

### **코드 내 오브젝트 참조 (`[SerializeField]`)**
*   `FindObjectOfType<Canvas>()`는 이름으로 찾을 수 없고 성능이 낮으며 비활성화된 개체를 찾지 못합니다.
*   **해결:** `private` 변수 위에 `[SerializeField]`를 붙여 인스펙터에서 직접 드래그 앤 드롭으로 할당합니다.
    ```csharp
    [SerializeField] private Canvas uiCanvas;
    [SerializeField] private Canvas charCanvas;
    ```
