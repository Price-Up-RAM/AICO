// using System;
// using System.Collections;
// using System.Collections.Generic;
// using System.Linq;
// using System.Runtime.InteropServices;
// using UnityEngine;
// using UnityEngine.UI;
// using System.IO;
// using DevionGames.UIWidgets;
// using TMPro;

// public class ScreenshotOCRManagerLegacy : MonoBehaviour
// {
//     // Windows API 선언
//     [DllImport("user32.dll")]
//     private static extern IntPtr GetDesktopWindow();

//     [DllImport("user32.dll")]
//     private static extern IntPtr GetWindowDC(IntPtr hWnd);

//     [DllImport("gdi32.dll")]
//     private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

//     [DllImport("gdi32.dll")]
//     private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

//     [DllImport("gdi32.dll")]
//     private static extern IntPtr SelectObject(IntPtr hdc, IntPtr bmp);

//     [DllImport("gdi32.dll")]
//     private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest,
//                                       IntPtr hdcSrc, int xSrc, int ySrc, int Rop);

//     [DllImport("gdi32.dll")]
//     private static extern bool DeleteObject(IntPtr hObject);

//     [DllImport("gdi32.dll")]
//     private static extern bool DeleteDC(IntPtr hdc);

//     [DllImport("user32.dll")]
//     private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

//     [DllImport("user32.dll")]
//     private static extern int GetSystemMetrics(int nIndex);

//     private const int SRCCOPY = 0x00CC0020;
//     private const int SM_CXSCREEN = 0;  // 주 모니터 너비
//     private const int SM_CYSCREEN = 1;  // 주 모니터 높이

//     // 싱글톤 인스턴스
//     private static ScreenshotOCRManagerLegacy instance;

//     // OCR 결과 표시용 UI 오브젝트들
//     private GameObject ocrOverlayContainer;
//     private Coroutine destroyCoroutine;

//     // public Font customFont; // TMPro로 변경하면서 사용 안 함

//     // 싱글톤 인스턴스 접근
//     public static ScreenshotOCRManagerLegacy Instance
//     {
//         get
//         {
//             if (instance == null)
//             {
//                 instance = FindObjectOfType<ScreenshotOCRManagerLegacy>();
//             }
//             return instance;
//         }
//     }

//     private void Awake()
//     {
//         // 싱글톤 패턴 구현
//         if (instance == null)
//         {
//             instance = this;
//         }
//         else
//         {
//             return;
//         }
//     }

//     // MenuTrigger에서 호출할 public 함수
//     public void ExecuteFullScreenOCR()
//     {
//         StartCoroutine(ExecuteFullScreenOCRCoroutine());
//     }

//     private IEnumerator ExecuteFullScreenOCRCoroutine()
//     {
//         Debug.Log("[ScreenshotOCR] Starting full screen OCR...");

//         // 기존 오버레이가 있으면 제거
//         ClearOCROverlay();

//         // 전체화면 캡처
//         byte[] screenshotBytes = null;
//         List<GameObject> deactivatedCharObjects = new List<GameObject>();
//         List<GameObject> deactivatedUIObjects = new List<GameObject>();
//         Dictionary<GameObject, bool> uiWidgetVisibleStates = new Dictionary<GameObject, bool>();

//         // Char/UI Layer 제외 옵션 확인
//         bool shouldIncludeChar = true;
//         bool shouldIncludeUI = true;
//         try
//         {
//             shouldIncludeChar = SettingManager.Instance.settings.includeCharInScreenshot;
//             shouldIncludeUI = SettingManager.Instance.settings.includeUIInScreenshot;
//         }
//         catch
//         {
//             Debug.LogWarning("[ScreenshotOCR] Failed to get screenshot settings, using defaults (true)");
//         }

//         GameObject[] allObjects = FindObjectsOfType<GameObject>(true);

//         // Char Layer 필터링
//         if (!shouldIncludeChar)
//         {
//             int charLayer = LayerMask.NameToLayer("Char");
//             if (charLayer != -1)
//             {
//                 foreach (GameObject obj in allObjects)
//                 {
//                     if (obj.layer == charLayer && obj.activeSelf)
//                     {
//                         obj.SetActive(false);
//                         deactivatedCharObjects.Add(obj);
//                     }
//                 }
//                 Debug.Log($"[ScreenshotOCR] Char Layer 오브젝트 {deactivatedCharObjects.Count}개 임시 비활성화");
//             }
//         }

//         // UI Layer 필터링
//         if (!shouldIncludeUI)
//         {
//             int uiLayer = LayerMask.NameToLayer("UI");
//             if (uiLayer != -1)
//             {
//                 foreach (GameObject obj in allObjects)
//                 {
//                     if (obj.layer == uiLayer && obj.activeSelf)
//                     {
//                         // UIWidget이 있으면 IsVisible 상태 저장
//                         UIWidget widget = obj.GetComponent<UIWidget>();
//                         if (widget != null)
//                         {
//                             uiWidgetVisibleStates[obj] = widget.IsM_IsShowing;
//                         }
                        
//                         obj.SetActive(false);
//                         deactivatedUIObjects.Add(obj);
//                     }
//                 }
//                 Debug.Log($"[ScreenshotOCR] UI Layer 오브젝트 {deactivatedUIObjects.Count}개 임시 비활성화");
//             }
//         }

//         // 렌더링이 화면에 반영될 때까지 대기
//         if (deactivatedCharObjects.Count > 0 || deactivatedUIObjects.Count > 0)
//         {
//             yield return new WaitForEndOfFrame();
//             yield return new WaitForEndOfFrame();
//         }

//         // 전체화면 캡처
//         screenshotBytes = CaptureFullScreenToMemory();

//         // Char Layer 오브젝트 재활성화
//         if (deactivatedCharObjects.Count > 0)
//         {
//             foreach (GameObject obj in deactivatedCharObjects)
//             {
//                 if (obj != null)
//                 {
//                     obj.SetActive(true);
//                 }
//             }
//             Debug.Log($"[ScreenshotOCR] Char Layer 오브젝트 {deactivatedCharObjects.Count}개 재활성화");
//         }

//         // UI Layer 오브젝트 재활성화
//         if (deactivatedUIObjects.Count > 0)
//         {
//             foreach (GameObject obj in deactivatedUIObjects)
//             {
//                 if (obj != null)
//                 {
//                     // UIWidget이 있고 Close 중이었으면 복원하지 않음
//                     if (uiWidgetVisibleStates.TryGetValue(obj, out bool wasVisible))
//                     {
//                         if (wasVisible)
//                         {
//                             obj.SetActive(true);
//                         }
//                         else
//                         {
//                             Debug.Log($"[ScreenshotOCR] Skipping restore for closing widget: {obj.name}");
//                         }
//                     }
//                     else
//                     {
//                         // UIWidget이 없는 일반 UI 오브젝트는 그대로 복원
//                         obj.SetActive(true);
//                     }
//                 }
//             }
//             Debug.Log($"[ScreenshotOCR] UI Layer 오브젝트 {deactivatedUIObjects.Count}개 재활성화");
//         }

//         if (screenshotBytes == null || screenshotBytes.Length == 0)
//         {
//             Debug.LogError("[ScreenshotOCR] Failed to capture full screen");
//             yield break;
//         }

//         Debug.Log($"[ScreenshotOCR] Screenshot captured: {screenshotBytes.Length} bytes");

//         // PaddleOCR API 호출 (전체화면 OCR, targetLang 빈 문자열)
//         bool apiCallCompleted = false;
//         APIManager.Instance.CallPaddleOCR(screenshotBytes, "", false, (result) =>
//         {
//             if (result != null)
//             {
//                 Debug.Log("[ScreenshotOCR] OCR completed successfully");
//                 DisplayOCRResults(result);
//                 // 원문 박스(ocr_with_region_origin)도 함께 보고 싶다면 아래 주석 해제
//                 // DisplayOCROriginResults(result);
//             }
//             else
//             {
//                 Debug.LogError("[ScreenshotOCR] OCR failed");
//             }
//             apiCallCompleted = true;
//         });

//         // API 호출 완료 대기
//         while (!apiCallCompleted)
//         {
//             yield return null;
//         }
//     }

//     // 영역 OCR 실행 (ScreenshotManager의 설정된 영역 사용)
//     public void ExecuteAreaOCR()
//     {
//         StartCoroutine(ExecuteAreaOCRCoroutine());
//     }

//     // 전체화면 OCR + 번역 실행 (Test5)
//     public void ExecuteFullScreenOCRWithTranslate(string targetLang = "ko")
//     {
//         StartCoroutine(ExecuteFullScreenOCRWithTranslateCoroutine(targetLang));
//     }

//     // 영역 OCR + 번역 실행 (Test6)
//     public void ExecuteAreaOCRWithTranslate(string targetLang = "ko")
//     {
//         StartCoroutine(ExecuteAreaOCRWithTranslateCoroutine(targetLang));
//     }

//     private IEnumerator ExecuteAreaOCRCoroutine()
//     {
//         Debug.Log("[ScreenshotOCR] Starting area OCR...");

//         // ScreenshotManager 찾기
//         ScreenshotManager screenshotManager = FindObjectOfType<ScreenshotManager>();
//         if (screenshotManager == null)
//         {
//             Debug.LogError("[ScreenshotOCR] ScreenshotManager not found");
//             yield break;
//         }

//         // 영역이 설정되어 있는지 확인
//         if (!screenshotManager.IsScreenshotAreaSet())
//         {
//             Debug.LogError("[ScreenshotOCR] Screenshot area not set. Please set the area first.");
//             ShowNoResultUI();
//             yield break;
//         }

//         // 기존 오버레이가 있으면 제거
//         ClearOCROverlay();

//         // 영역 캡처를 위한 변수
//         byte[] areaBytes = null;
//         int captureX = 0, captureY = 0, captureWidth = 0, captureHeight = 0;

//         // ScreenshotManager의 CaptureScreenshotToMemory를 사용하여 캡처
//         bool captureCompleted = false;
//         yield return screenshotManager.CaptureScreenshotToMemoryWithInfo((bytes, x, y, w, h) =>
//         {
//             areaBytes = bytes;
//             captureX = x;
//             captureY = y;
//             captureWidth = w;
//             captureHeight = h;
//             captureCompleted = true;
//         });

//         while (!captureCompleted)
//         {
//             yield return null;
//         }

//         if (areaBytes == null || areaBytes.Length == 0)
//         {
//             Debug.LogError("[ScreenshotOCR] Failed to capture area");
//             yield break;
//         }

//         Debug.Log($"[ScreenshotOCR] Area captured: {areaBytes.Length} bytes, position: ({captureX}, {captureY}), size: {captureWidth}x{captureHeight}");

//         // PaddleOCR API 호출 (영역 OCR, targetLang 빈 문자열)
//         bool apiCallCompleted = false;
//         APIManager.Instance.CallPaddleOCR(areaBytes, "", false, (result) =>
//         {
//             if (result != null)
//             {
//                 Debug.Log("[ScreenshotOCR] Area OCR completed successfully");
                
//                 // OCR 결과 좌표를 영역 오프셋만큼 조정 (영역 내 상대좌표 → 전체 화면 절대좌표)
//                 AdjustOCRResultCoords(result, captureX, captureY);
                
//                 DisplayOCRResults(result, captureWidth, captureHeight);
//                 // 원문 박스(ocr_with_region_origin)도 함께 보고 싶다면 아래 주석 해제
//                 // DisplayOCROriginResults(result, captureWidth, captureHeight);
//             }
//             else
//             {
//                 Debug.LogError("[ScreenshotOCR] Area OCR failed");
//             }
//             apiCallCompleted = true;
//         });

//         // API 호출 완료 대기
//         while (!apiCallCompleted)
//         {
//             yield return null;
//         }
//     }

//     // OCR 결과 좌표를 영역 오프셋만큼 조정
//     private void AdjustOCRResultCoords(OCRResult result, int offsetX, int offsetY)
//     {
//         if (result == null || result.quad_boxes == null) return;

//         for (int i = 0; i < result.quad_boxes.Count; i++)
//         {
//             for (int j = 0; j < result.quad_boxes[i].Count; j++)
//             {
//                 if (result.quad_boxes[i][j].Count >= 2)
//                 {
//                     result.quad_boxes[i][j][0] += offsetX; // X 오프셋
//                     result.quad_boxes[i][j][1] += offsetY; // Y 오프셋
//                 }
//             }
//         }
        
//         Debug.Log($"[ScreenshotOCR] Adjusted {result.quad_boxes.Count} boxes with offset ({offsetX}, {offsetY})");
//     }

//     // OCRWithTranslate 결과 좌표를 영역 오프셋만큼 조정
//     private void AdjustOCRWithTranslateResultCoords(OCRWithTranslateResult result, int offsetX, int offsetY)
//     {
//         if (result == null || result.quad_boxes == null) return;

//         for (int i = 0; i < result.quad_boxes.Count; i++)
//         {
//             for (int j = 0; j < result.quad_boxes[i].Count; j++)
//             {
//                 if (result.quad_boxes[i][j].Count >= 2)
//                 {
//                     result.quad_boxes[i][j][0] += offsetX;
//                     result.quad_boxes[i][j][1] += offsetY;
//                 }
//             }
//         }
        
//         Debug.Log($"[ScreenshotOCR+Translate] Adjusted {result.quad_boxes.Count} boxes with offset ({offsetX}, {offsetY})");
//     }

//     // 전체화면 OCR + 번역 코루틴
//     private IEnumerator ExecuteFullScreenOCRWithTranslateCoroutine(string targetLang)
//     {
//         Debug.Log($"[ScreenshotOCR+Translate] Starting full screen OCR with translate (target: {targetLang})...");

//         ClearOCROverlay();

//         byte[] screenshotBytes = null;
//         List<GameObject> deactivatedCharObjects = new List<GameObject>();
//         List<GameObject> deactivatedUIObjects = new List<GameObject>();
//         Dictionary<GameObject, bool> uiWidgetVisibleStates = new Dictionary<GameObject, bool>();

//         bool shouldIncludeChar = true;
//         bool shouldIncludeUI = true;
//         try
//         {
//             shouldIncludeChar = SettingManager.Instance.settings.includeCharInScreenshot;
//             shouldIncludeUI = SettingManager.Instance.settings.includeUIInScreenshot;
//         }
//         catch
//         {
//             Debug.LogWarning("[ScreenshotOCR+Translate] Failed to get screenshot settings, using defaults (true)");
//         }

//         GameObject[] allObjects = FindObjectsOfType<GameObject>(true);

//         // Char Layer 필터링
//         if (!shouldIncludeChar)
//         {
//             int charLayer = LayerMask.NameToLayer("Char");
//             if (charLayer != -1)
//             {
//                 foreach (GameObject obj in allObjects)
//                 {
//                     if (obj.layer == charLayer && obj.activeSelf)
//                     {
//                         obj.SetActive(false);
//                         deactivatedCharObjects.Add(obj);
//                     }
//                 }
//                 Debug.Log($"[ScreenshotOCR+Translate] Char Layer 오브젝트 {deactivatedCharObjects.Count}개 임시 비활성화");
//             }
//         }

//         // UI Layer 필터링
//         if (!shouldIncludeUI)
//         {
//             int uiLayer = LayerMask.NameToLayer("UI");
//             if (uiLayer != -1)
//             {
//                 foreach (GameObject obj in allObjects)
//                 {
//                     if (obj.layer == uiLayer && obj.activeSelf)
//                     {
//                         UIWidget widget = obj.GetComponent<UIWidget>();
//                         if (widget != null)
//                         {
//                             uiWidgetVisibleStates[obj] = widget.IsM_IsShowing;
//                         }
//                         obj.SetActive(false);
//                         deactivatedUIObjects.Add(obj);
//                     }
//                 }
//                 Debug.Log($"[ScreenshotOCR+Translate] UI Layer 오브젝트 {deactivatedUIObjects.Count}개 임시 비활성화");
//             }
//         }

//         if (deactivatedCharObjects.Count > 0 || deactivatedUIObjects.Count > 0)
//         {
//             yield return new WaitForEndOfFrame();
//             yield return new WaitForEndOfFrame();
//         }

//         screenshotBytes = CaptureFullScreenToMemory();

//         // Char Layer 오브젝트 재활성화
//         if (deactivatedCharObjects.Count > 0)
//         {
//             foreach (GameObject obj in deactivatedCharObjects)
//             {
//                 if (obj != null) obj.SetActive(true);
//             }
//             Debug.Log($"[ScreenshotOCR+Translate] Char Layer 오브젝트 {deactivatedCharObjects.Count}개 재활성화");
//         }

//         // UI Layer 오브젝트 재활성화
//         if (deactivatedUIObjects.Count > 0)
//         {
//             foreach (GameObject obj in deactivatedUIObjects)
//             {
//                 if (obj != null)
//                 {
//                     if (uiWidgetVisibleStates.TryGetValue(obj, out bool wasVisible))
//                     {
//                         if (wasVisible) obj.SetActive(true);
//                     }
//                     else
//                     {
//                         obj.SetActive(true);
//                     }
//                 }
//             }
//             Debug.Log($"[ScreenshotOCR+Translate] UI Layer 오브젝트 {deactivatedUIObjects.Count}개 재활성화");
//         }

//         if (screenshotBytes == null || screenshotBytes.Length == 0)
//         {
//             Debug.LogError("[ScreenshotOCR+Translate] Failed to capture full screen");
//             yield break;
//         }

//         Debug.Log($"[ScreenshotOCR+Translate] Screenshot captured: {screenshotBytes.Length} bytes");

//         bool apiCallCompleted = false;
//         // APIManager.Instance.CallPaddleOCRWithTranslate(screenshotBytes, targetLang, (result) =>
//         // {
//         //     if (result != null)
//         //     {
//         //         Debug.Log("[ScreenshotOCR+Translate] OCR with translate completed successfully");
//         //         int captureWidth = GetSystemMetrics(SM_CXSCREEN);
//         //         int captureHeight = GetSystemMetrics(SM_CYSCREEN);
//         //         DisplayOCRResults(result, captureWidth, captureHeight);
//         //     }
//         //     else
//         //     {
//         //         Debug.LogError("[ScreenshotOCR+Translate] OCR with translate failed");
//         //         ShowNoResultUI();
//         //     }
//         //     apiCallCompleted = true;
//         // });

//         while (!apiCallCompleted)
//         {
//             yield return null;
//         }
//     }

//     // 영역 OCR + 번역 코루틴
//     private IEnumerator ExecuteAreaOCRWithTranslateCoroutine(string targetLang)
//     {
//         Debug.Log($"[ScreenshotOCR+Translate] Starting area OCR with translate (target: {targetLang})...");

//         ScreenshotManager screenshotManager = FindObjectOfType<ScreenshotManager>();
//         if (screenshotManager == null)
//         {
//             Debug.LogError("[ScreenshotOCR+Translate] ScreenshotManager not found");
//             yield break;
//         }

//         if (!screenshotManager.IsScreenshotAreaSet())
//         {
//             Debug.LogError("[ScreenshotOCR+Translate] Screenshot area not set. Please set the area first.");
//             ShowNoResultUI();
//             yield break;
//         }

//         ClearOCROverlay();

//         byte[] areaBytes = null;
//         int captureX = 0, captureY = 0, captureWidth = 0, captureHeight = 0;

//         bool captureCompleted = false;
//         yield return screenshotManager.CaptureScreenshotToMemoryWithInfo((bytes, x, y, w, h) =>
//         {
//             areaBytes = bytes;
//             captureX = x;
//             captureY = y;
//             captureWidth = w;
//             captureHeight = h;
//             captureCompleted = true;
//         });

//         while (!captureCompleted)
//         {
//             yield return null;
//         }

//         if (areaBytes == null || areaBytes.Length == 0)
//         {
//             Debug.LogError("[ScreenshotOCR+Translate] Failed to capture area");
//             yield break;
//         }

//         Debug.Log($"[ScreenshotOCR+Translate] Area captured: {areaBytes.Length} bytes, position: ({captureX}, {captureY}), size: {captureWidth}x{captureHeight}");

//         bool apiCallCompleted = false;
//         // APIManager.Instance.CallPaddleOCRWithTranslate(areaBytes, targetLang, (result) =>
//         // {
//         //     if (result != null)
//         //     {
//         //         Debug.Log("[ScreenshotOCR+Translate] Area OCR with translate completed successfully");
//         //         AdjustOCRResultCoords(result, captureX, captureY);
//         //         DisplayOCRResults(result, captureWidth, captureHeight);
//         //     }
//         //     else
//         //     {
//         //         Debug.LogError("[ScreenshotOCR+Translate] Area OCR with translate failed");
//         //         ShowNoResultUI();
//         //     }
//         //     apiCallCompleted = true;
//         // });

//         while (!apiCallCompleted)
//         {
//             yield return null;
//         }
//     }

//     // 영역 OCR → TTS 
//     public void ExecuteAreaOCRWithTTS()
//     {
//         StartCoroutine(ExecuteAreaOCRWithTTSCoroutine());
//     }

//     private IEnumerator ExecuteAreaOCRWithTTSCoroutine()
//     {
//         Debug.Log("[ScreenshotOCR→TTS] Starting area OCR with TTS...");

//         // ScreenshotManager 찾기
//         ScreenshotManager screenshotManager = FindObjectOfType<ScreenshotManager>();
//         if (screenshotManager == null)
//         {
//             Debug.LogError("[ScreenshotOCR→TTS] ScreenshotManager not found");
//             yield break;
//         }

//         // 영역이 설정되어 있는지 확인
//         if (!screenshotManager.IsScreenshotAreaSet())
//         {
//             Debug.LogError("[ScreenshotOCR→TTS] Screenshot area not set. Please set the area first.");
//             yield break;
//         }

//         // 영역 캡처
//         byte[] areaBytes = null;
//         int captureX = 0, captureY = 0, captureWidth = 0, captureHeight = 0;

//         bool captureCompleted = false;
//         yield return screenshotManager.CaptureScreenshotToMemoryWithInfo((bytes, x, y, w, h) =>
//         {
//             areaBytes = bytes;
//             captureX = x;
//             captureY = y;
//             captureWidth = w;
//             captureHeight = h;
//             captureCompleted = true;
//         });

//         while (!captureCompleted)
//         {
//             yield return null;
//         }

//         if (areaBytes == null || areaBytes.Length == 0)
//         {
//             Debug.LogError("[ScreenshotOCR→TTS] Failed to capture area");
//             yield break;
//         }

//         Debug.Log($"[ScreenshotOCR→TTS] Area captured: {areaBytes.Length} bytes");

//         // dev_voice 설정 활성화 (Test7용)
//         try
//         {
//             if (!SettingManager.Instance.settings.isDevSound)
//             {
//                 SettingManager.Instance.settings.isDevSound = true;
//                 Debug.Log("[ScreenshotOCR→TTS] dev_voice 활성화");
//             }
//         }
//         catch (Exception ex)
//         {
//             Debug.LogWarning($"[ScreenshotOCR→TTS] Failed to enable dev_voice: {ex.Message}");
//         }

//         // PaddleOCR API 호출 (일본어 지정)
//         bool apiCallCompleted = false;
//         APIManager.Instance.CallPaddleOCR(areaBytes, "ja", false, (result) =>
//         {
//             if (result != null && result.labels != null && result.labels.Count > 0)
//             {
//                 Debug.Log($"[ScreenshotOCR→TTS] OCR completed: {result.labels.Count} text regions found");
                
//                 // 캐릭터 이름 감지
//                 string detectedActor = null;
//                 string detectedActorText = null;  // actor로 감지된 원본 텍스트
//                 foreach (string label in result.labels)
//                 {
//                     string actor = STTDataActor.GetActorFromText(label);
//                     if (actor != null)
//                     {
//                         detectedActor = actor;
//                         detectedActorText = label;  // 원본 텍스트 저장
//                         Debug.Log($"[ScreenshotOCR→TTS] Detected actor: {detectedActor} from text '{label}'");
//                         break; // 첫 번째 매칭 사용
//                     }
//                 }
                
//                 // 기본값: arona
//                 if (detectedActor == null)
//                 {
//                     detectedActor = "arona";
//                     Debug.Log("[ScreenshotOCR→TTS] No actor detected, using default: arona");
//                 }

//                 // 텍스트-좌표 쌍 생성 (필터링: 길이 3 미만, 블랙리스트, actor 텍스트)
//                 List<(string text, float x, float y)> textItems = new List<(string, float, float)>();
//                 for (int i = 0; i < result.labels.Count; i++)
//                 {
//                     string text = result.labels[i];
                    
//                     // 길이 필터
//                     if (text.Length < 3)
//                     {
//                         Debug.Log($"[ScreenshotOCR→TTS] Filtered out (length < 3): '{text}'");
//                         continue;
//                     }
                    
//                     // 블랙리스트 필터
//                     if (STTDataActor.IsBlacklisted(text))
//                     {
//                         Debug.Log($"[ScreenshotOCR→TTS] Filtered out (blacklisted): '{text}'");
//                         continue;
//                     }
                    
//                     // actor 텍스트 필터 (이미 actor로 사용되었으면 제외)
//                     if (!string.IsNullOrEmpty(detectedActorText) && text.Trim() == detectedActorText.Trim())
//                     {
//                         Debug.Log($"[ScreenshotOCR→TTS] Filtered out (actor text): '{text}'");
//                         continue;
//                     }

//                     // quad_boxes에서 좌상단 좌표 추출
//                     float x = result.quad_boxes[i][0][0];
//                     float y = result.quad_boxes[i][0][1];
//                     textItems.Add((text, x, y));
//                 }

//                 if (textItems.Count == 0)
//                 {
//                     Debug.LogWarning("[ScreenshotOCR→TTS] No text with length >= 3 found");
//                     apiCallCompleted = true;
//                     return;
//                 }

//                 // Y,X 정렬 및 그룹핑
//                 // 1. Y 좌표 기준 1차 정렬
//                 textItems.Sort((a, b) => a.y.CompareTo(b.y));

//                 // 2. Y 임계값(30px)으로 줄 그룹핑
//                 const float yThreshold = 30f;
//                 List<List<(string text, float x, float y)>> lines = new List<List<(string, float, float)>>();
//                 List<(string text, float x, float y)> currentLine = new List<(string, float, float)>();

//                 foreach (var item in textItems)
//                 {
//                     if (currentLine.Count == 0)
//                     {
//                         // 첫 번째 아이템
//                         currentLine.Add(item);
//                         Debug.Log($"[ScreenshotOCR→TTS] New line started at Y={item.y}: '{item.text}'");
//                     }
//                     else
//                     {
//                         // 현재 줄의 평균 Y 계산
//                         float avgY = currentLine.Average(t => t.y);
                        
//                         if (Mathf.Abs(item.y - avgY) <= yThreshold)
//                         {
//                             // 같은 줄
//                             currentLine.Add(item);
//                             Debug.Log($"[ScreenshotOCR→TTS] Same line (Y diff={Mathf.Abs(item.y - avgY)}): '{item.text}'");
//                         }
//                         else
//                         {
//                             // 새 줄 시작
//                             lines.Add(currentLine);
//                             currentLine = new List<(string, float, float)> { item };
//                             Debug.Log($"[ScreenshotOCR→TTS] New line at Y={item.y} (prev avg Y={avgY}): '{item.text}'");
//                         }
//                     }
//                 }
                
//                 // 마지막 줄 추가
//                 if (currentLine.Count > 0)
//                 {
//                     lines.Add(currentLine);
//                 }

//                 // 3. 각 줄 내에서 X 좌표 정렬
//                 List<string> sortedTexts = new List<string>();
//                 foreach (var line in lines)
//                 {
//                     var sortedLine = line.OrderBy(item => item.x).ToList();
//                     foreach (var item in sortedLine)
//                     {
//                         sortedTexts.Add(item.text);
//                         Debug.Log($"[ScreenshotOCR→TTS] Added to final: '{item.text}' (X={item.x}, Y={item.y})");
//                     }
//                 }

//                 // 공백으로 연결
//                 string finalText = string.Join(" ", sortedTexts);
//                 Debug.Log($"[ScreenshotOCR→TTS] Final text: '{finalText}'");
//                 Debug.Log($"[ScreenshotOCR→TTS] Actor: {detectedActor}, Language: ja");

//                 // TTS 호출 (일본어 고정)
//                 string chatIdx = GameManager.Instance.chatIdxSuccess;

//                 Debug.Log("[ScreenshotOCR→TTS] Calling GetJpWavFromAPI");
//                 APIManager.Instance.GetJpWavFromAPI(finalText, chatIdx, detectedActor);
//             }
//             else
//             {
//                 Debug.LogError("[ScreenshotOCR→TTS] OCR failed or no text found");
//             }
//             apiCallCompleted = true;
//         });

//         while (!apiCallCompleted)
//         {
//             yield return null;
//         }

//         Debug.Log("[ScreenshotOCR→TTS] Completed");
//     }

//     // 영역 OCR → TTS (자동 언어 감지) (MenuTrigger Test8에서 호출)
//     public void ExecuteAreaOCRWithTTSAutoDetect()
//     {
//         StartCoroutine(ExecuteAreaOCRWithTTSAutoDetectCoroutine());
//     }

//     private IEnumerator ExecuteAreaOCRWithTTSAutoDetectCoroutine()
//     {
//         Debug.Log("[ScreenshotOCR→TTS-Auto] Starting area OCR with TTS (Auto Detect)...");

//         // ScreenshotManager 찾기
//         ScreenshotManager screenshotManager = FindObjectOfType<ScreenshotManager>();
//         if (screenshotManager == null)
//         {
//             Debug.LogError("[ScreenshotOCR→TTS-Auto] ScreenshotManager not found");
//             yield break;
//         }

//         // 영역이 설정되어 있는지 확인
//         if (!screenshotManager.IsScreenshotAreaSet())
//         {
//             Debug.LogError("[ScreenshotOCR→TTS-Auto] Screenshot area not set. Please set the area first.");
//             yield break;
//         }

//         // 영역 캡처
//         byte[] areaBytes = null;
//         int captureX = 0, captureY = 0, captureWidth = 0, captureHeight = 0;

//         bool captureCompleted = false;
//         yield return screenshotManager.CaptureScreenshotToMemoryWithInfo((bytes, x, y, w, h) =>
//         {
//             areaBytes = bytes;
//             captureX = x;
//             captureY = y;
//             captureWidth = w;
//             captureHeight = h;
//             captureCompleted = true;
//         });

//         while (!captureCompleted)
//         {
//             yield return null;
//         }

//         if (areaBytes == null || areaBytes.Length == 0)
//         {
//             Debug.LogError("[ScreenshotOCR→TTS-Auto] Failed to capture area");
//             yield break;
//         }

//         Debug.Log($"[ScreenshotOCR→TTS-Auto] Area captured: {areaBytes.Length} bytes");

//         // dev_voice 설정 활성화 (Test8용)
//         try
//         {
//             if (!SettingManager.Instance.settings.isDevSound)
//             {
//                 SettingManager.Instance.settings.isDevSound = true;
//                 Debug.Log("[ScreenshotOCR→TTS-Auto] dev_voice 활성화");
//             }
//         }
//         catch (Exception ex)
//         {
//             Debug.LogWarning($"[ScreenshotOCR→TTS-Auto] Failed to enable dev_voice: {ex.Message}");
//         }

//         // PaddleOCR API 호출 (자동 언어 감지)
//         bool apiCallCompleted = false;
//         APIManager.Instance.CallPaddleOCR(areaBytes, "", true, (result) =>
//         {
//             if (result != null && result.labels != null && result.labels.Count > 0)
//             {
//                 Debug.Log($"[ScreenshotOCR→TTS-Auto] OCR completed: {result.labels.Count} text regions found");
                
//                 // 캐릭터 이름 감지
//                 string detectedActor = null;
//                 string detectedActorText = null;  // actor로 감지된 원본 텍스트
//                 foreach (string label in result.labels)
//                 {
//                     string actor = STTDataActor.GetActorFromText(label);
//                     if (actor != null)
//                     {
//                         detectedActor = actor;
//                         detectedActorText = label;  // 원본 텍스트 저장
//                         Debug.Log($"[ScreenshotOCR→TTS-Auto] Detected actor: {detectedActor} from text '{label}'");
//                         break;
//                     }
//                 }
                
//                 if (detectedActor == null)
//                 {
//                     detectedActor = "arona";
//                     Debug.Log("[ScreenshotOCR→TTS-Auto] No actor detected, using default: arona");
//                 }

//                 // 텍스트-좌표 쌍 생성 (필터링: 길이 3 미만, 블랙리스트, actor 텍스트)
//                 List<(string text, float x, float y)> textItems = new List<(string, float, float)>();
//                 for (int i = 0; i < result.labels.Count; i++)
//                 {
//                     string text = result.labels[i];
                    
//                     // 길이 필터
//                     if (text.Length < 3)
//                     {
//                         Debug.Log($"[ScreenshotOCR→TTS-Auto] Filtered out (length < 3): '{text}'");
//                         continue;
//                     }
                    
//                     // 블랙리스트 필터
//                     if (STTDataActor.IsBlacklisted(text))
//                     {
//                         Debug.Log($"[ScreenshotOCR→TTS-Auto] Filtered out (blacklisted): '{text}'");
//                         continue;
//                     }
                    
//                     // actor 텍스트 필터 (이미 actor로 사용되었으면 제외)
//                     if (!string.IsNullOrEmpty(detectedActorText) && text.Trim() == detectedActorText.Trim())
//                     {
//                         Debug.Log($"[ScreenshotOCR→TTS-Auto] Filtered out (actor text): '{text}'");
//                         continue;
//                     }

//                     float x = result.quad_boxes[i][0][0];
//                     float y = result.quad_boxes[i][0][1];
//                     textItems.Add((text, x, y));
//                 }

//                 if (textItems.Count == 0)
//                 {
//                     Debug.LogWarning("[ScreenshotOCR→TTS-Auto] No text with length >= 3 found");
//                     apiCallCompleted = true;
//                     return;
//                 }

//                 // Y,X 정렬 및 그룹핑
//                 // 1. Y 좌표 기준 1차 정렬
//                 textItems.Sort((a, b) => a.y.CompareTo(b.y));

//                 // 2. Y 임계값(30px)으로 줄 그룹핑
//                 const float yThreshold = 30f;
//                 List<List<(string text, float x, float y)>> lines = new List<List<(string, float, float)>>();
//                 List<(string text, float x, float y)> currentLine = new List<(string, float, float)>();

//                 foreach (var item in textItems)
//                 {
//                     if (currentLine.Count == 0)
//                     {
//                         currentLine.Add(item);
//                         Debug.Log($"[ScreenshotOCR→TTS-Auto] New line started at Y={item.y}: '{item.text}'");
//                     }
//                     else
//                     {
//                         float avgY = currentLine.Average(t => t.y);
                        
//                         if (Mathf.Abs(item.y - avgY) <= yThreshold)
//                         {
//                             currentLine.Add(item);
//                             Debug.Log($"[ScreenshotOCR→TTS-Auto] Same line (Y diff={Mathf.Abs(item.y - avgY)}): '{item.text}'");
//                         }
//                         else
//                         {
//                             lines.Add(currentLine);
//                             currentLine = new List<(string, float, float)> { item };
//                             Debug.Log($"[ScreenshotOCR→TTS-Auto] New line at Y={item.y} (prev avg Y={avgY}): '{item.text}'");
//                         }
//                     }
//                 }
                
//                 if (currentLine.Count > 0)
//                 {
//                     lines.Add(currentLine);
//                 }

//                 // 3. 각 줄 내에서 X 좌표 정렬
//                 List<string> sortedTexts = new List<string>();
//                 foreach (var line in lines)
//                 {
//                     var sortedLine = line.OrderBy(item => item.x).ToList();
//                     foreach (var item in sortedLine)
//                     {
//                         sortedTexts.Add(item.text);
//                         Debug.Log($"[ScreenshotOCR→TTS-Auto] Added to final: '{item.text}' (X={item.x}, Y={item.y})");
//                     }
//                 }

//                 // 공백으로 연결
//                 string finalText = string.Join(" ", sortedTexts);
//                 Debug.Log($"[ScreenshotOCR→TTS-Auto] Final text: '{finalText}'");
//                 Debug.Log($"[ScreenshotOCR→TTS-Auto] Actor: {detectedActor}, Language: Auto");

//                 // TTS 호출 (자동 감지된 언어 기반)
//                 string chatIdx = GameManager.Instance.chatIdxSuccess;
                
//                 // sound_language 설정 가져오기
//                 string soundLang = "en";
//                 try
//                 {
//                     soundLang = SettingManager.Instance.settings.sound_language ?? "en";
//                 }
//                 catch
//                 {
//                     Debug.LogWarning("[ScreenshotOCR→TTS-Auto] Failed to get sound language, using default (en)");
//                 }

//                 if (soundLang == "ko" || soundLang == "en")
//                 {
//                     Debug.Log($"[ScreenshotOCR→TTS-Auto] Calling GetKoWavFromAPI with lang={soundLang}");
//                     APIManager.Instance.GetKoWavFromAPI(finalText, chatIdx, detectedActor);
//                 }
//                 else if (soundLang == "jp" || soundLang == "ja")
//                 {
//                     Debug.Log("[ScreenshotOCR→TTS-Auto] Calling GetJpWavFromAPI");
//                     APIManager.Instance.GetJpWavFromAPI(finalText, chatIdx, detectedActor);
//                 }
//                 else
//                 {
//                     Debug.LogWarning($"[ScreenshotOCR→TTS-Auto] Unknown sound language: {soundLang}, defaulting to Japanese");
//                     APIManager.Instance.GetJpWavFromAPI(finalText, chatIdx, detectedActor);
//                 }
//             }
//             else
//             {
//                 Debug.LogError("[ScreenshotOCR→TTS-Auto] OCR failed or no text found");
//             }
//             apiCallCompleted = true;
//         });

//         while (!apiCallCompleted)
//         {
//             yield return null;
//         }

//         Debug.Log("[ScreenshotOCR→TTS-Auto] Completed");
//     }

//     // 전체화면 캡처 (메모리로 직접)
//     private byte[] CaptureFullScreenToMemory()
//     {
//         // 주 모니터(현재 사용 중인 모니터)의 실제 해상도 가져오기
//         int width = GetSystemMetrics(SM_CXSCREEN);
//         int height = GetSystemMetrics(SM_CYSCREEN);
        
//         Debug.Log($"[ScreenshotOCR] Capturing primary monitor: {width}x{height}");

//         IntPtr desktopHwnd = GetDesktopWindow();
//         IntPtr desktopDC = GetWindowDC(desktopHwnd);
//         IntPtr memoryDC = CreateCompatibleDC(desktopDC);
//         IntPtr bitmap = CreateCompatibleBitmap(desktopDC, width, height);
//         IntPtr oldBitmap = SelectObject(memoryDC, bitmap);

//         // 주 모니터 전체화면 복사
//         BitBlt(memoryDC, 0, 0, width, height, desktopDC, 0, 0, SRCCOPY);

//         byte[] imageBytes = null;
//         using (System.Drawing.Bitmap bmp = System.Drawing.Bitmap.FromHbitmap(bitmap))
//         {
//             using (MemoryStream ms = new MemoryStream())
//             {
//                 bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
//                 imageBytes = ms.ToArray();
//             }
//         }

//         // Clean up
//         SelectObject(memoryDC, oldBitmap);
//         DeleteObject(bitmap);
//         DeleteDC(memoryDC);
//         ReleaseDC(desktopHwnd, desktopDC);

//         return imageBytes;
//     }

//     // OCR 결과를 화면에 표시 (전체 화면용)
//     private void DisplayOCRResults(OCRResult result)
//     {
//         int captureWidth = GetSystemMetrics(SM_CXSCREEN);
//         int captureHeight = GetSystemMetrics(SM_CYSCREEN);
//         DisplayOCRResults(result, captureWidth, captureHeight);
//     }

//     // 원문 OCR 결과를 화면에 표시 (전체 화면용)
//     private void DisplayOCROriginResults(OCRResult result)
//     {
//         int captureWidth = GetSystemMetrics(SM_CXSCREEN);
//         int captureHeight = GetSystemMetrics(SM_CYSCREEN);
//         DisplayOCROriginResults(result, captureWidth, captureHeight);
//     }

//     // OCR + 번역 결과를 화면에 표시 (전체 화면용)
//     private void DisplayOCRWithTranslateResults(OCRWithTranslateResult result)
//     {
//         int captureWidth = GetSystemMetrics(SM_CXSCREEN);
//         int captureHeight = GetSystemMetrics(SM_CYSCREEN);
//         DisplayOCRWithTranslateResults(result, captureWidth, captureHeight);
//     }

//     // OCR + 번역 결과를 화면에 표시 (영역 OCR용)
//     private void DisplayOCRWithTranslateResults(OCRWithTranslateResult result, int captureWidth, int captureHeight)
//     {
//         if (result == null || result.labels == null || result.labels.Count == 0)
//         {
//             Debug.LogWarning("[ScreenshotOCR+Translate] No OCR results to display");
//             ShowNoResultUI();
//             return;
//         }

//         Canvas canvas = FindObjectOfType<Canvas>();
//         if (canvas == null)
//         {
//             Debug.LogError("[ScreenshotOCR+Translate] Canvas not found");
//             return;
//         }

//         ocrOverlayContainer = new GameObject("OCROverlayContainer_Translated");
//         ocrOverlayContainer.transform.SetParent(canvas.transform, false);

//         RectTransform containerRect = ocrOverlayContainer.AddComponent<RectTransform>();
//         containerRect.anchorMin = Vector2.zero;
//         containerRect.anchorMax = Vector2.one;
//         containerRect.sizeDelta = Vector2.zero;
//         containerRect.anchoredPosition = Vector2.zero;

//         Debug.Log($"[ScreenshotOCR+Translate] Displaying {result.labels.Count} translated OCR results");

//         for (int i = 0; i < result.labels.Count; i++)
//         {
//             string translatedText = result.labels[i];
//             string originalText = (result.labels_origin != null && i < result.labels_origin.Count) 
//                 ? result.labels_origin[i] : "";
            
//             if (result.quad_boxes != null && i < result.quad_boxes.Count)
//             {
//                 CreateOCRTranslatedTextUI(translatedText, originalText, result.quad_boxes[i], canvas);
//             }
//         }

//         if (destroyCoroutine != null)
//         {
//             StopCoroutine(destroyCoroutine);
//         }
//         destroyCoroutine = StartCoroutine(DestroyOverlayAfterDelay(10f));
//     }

//     // 번역된 텍스트 UI 생성 (번역 텍스트만 표시, 배경색 다르게)
//     private void CreateOCRTranslatedTextUI(string translatedText, string originalText, List<List<float>> quadBox, Canvas canvas)
//     {
//         if (quadBox == null || quadBox.Count < 4) return;

//         float captureWidth = GetSystemMetrics(SM_CXSCREEN);
//         float captureHeight = GetSystemMetrics(SM_CYSCREEN);
//         float screenWidth = Screen.width;
//         float screenHeight = Screen.height;
//         float scaleX = screenWidth / captureWidth;
//         float scaleY = screenHeight / captureHeight;
        
//         List<Vector2> convertedPoints = new List<Vector2>();
        
//         foreach (var point in quadBox)
//         {
//             if (point.Count >= 2)
//             {
//                 float ocrX = point[0];
//                 float ocrY = point[1];
//                 float unityScreenX = ocrX * scaleX;
//                 float unityScreenY = screenHeight - (ocrY * scaleY);
//                 Vector2 unityScreenPos = new Vector2(unityScreenX, unityScreenY);
//                 Vector2 canvasLocalPos = WindowCollisionManager.Instance.ConvertWinposToUnityPos(unityScreenPos);
//                 convertedPoints.Add(canvasLocalPos);
//             }
//         }
        
//         if (convertedPoints.Count < 4) return;

//         float minX = convertedPoints.Min(p => p.x);
//         float minY = convertedPoints.Min(p => p.y);
//         float maxX = convertedPoints.Max(p => p.x);
//         float maxY = convertedPoints.Max(p => p.y);

//         float width = maxX - minX;
//         float height = maxY - minY;

//         if (width <= 0 || height <= 0) return;

//         GameObject textObj = new GameObject($"OCRTranslated_{translatedText.Substring(0, Math.Min(10, translatedText.Length))}");
//         textObj.transform.SetParent(ocrOverlayContainer.transform, false);

//         // 배경 패널 (번역 결과는 파란색 계열로 구분)
//         Image bgImage = textObj.AddComponent<Image>();
//         bgImage.color = new Color(0f, 0.1f, 0.3f, 0.7f); // 어두운 파란색

//         RectTransform rectTransform = textObj.GetComponent<RectTransform>();
//         rectTransform.anchorMin = new Vector2(0, 0);
//         rectTransform.anchorMax = new Vector2(0, 0);
//         rectTransform.pivot = new Vector2(0, 0);
//         rectTransform.localPosition = new Vector2(minX, minY);
//         rectTransform.sizeDelta = new Vector2(width, height);

//         // 번역된 텍스트
//         GameObject textChild = new GameObject("TranslatedText");
//         textChild.transform.SetParent(textObj.transform, false);
        
//         TextMeshProUGUI textComponent = textChild.AddComponent<TextMeshProUGUI>();
//         textComponent.text = translatedText;
//         textComponent.font = null;
//         textComponent.color = new Color(0.5f, 1f, 0.5f); // 연두색 (번역 텍스트)
//         textComponent.alignment = TextAlignmentOptions.MidlineLeft;

//         textComponent.enableAutoSizing = true;
//         textComponent.fontSizeMin = 8;
//         textComponent.fontSizeMax = 72;

//         float marginH = width > 4 ? 1f : 0f;
//         float marginV = height > 6 ? 2f : 0f;
//         textComponent.margin = new Vector4(marginH, marginV, marginH, marginV);

//         RectTransform textRect = textChild.GetComponent<RectTransform>();
//         textRect.anchorMin = Vector2.zero;
//         textRect.anchorMax = Vector2.one;
//         textRect.sizeDelta = Vector2.zero;
//         textRect.anchoredPosition = Vector2.zero;
//     }

//     // OCR 결과를 화면에 표시 (영역 OCR용 - 캡처 영역 크기 지정)
//     private void DisplayOCRResults(OCRResult result, int captureWidth, int captureHeight)
//     {
//         if (result == null || result.labels == null || result.labels.Count == 0)
//         {
//             Debug.LogWarning("[ScreenshotOCR] No OCR results to display");
//             ShowNoResultUI();
//             return;
//         }

//         // Canvas 찾기 또는 생성
//         Canvas canvas = FindObjectOfType<Canvas>();
//         if (canvas == null)
//         {
//             Debug.LogError("[ScreenshotOCR] Canvas not found");
//             return;
//         }

//         // 오버레이 컨테이너 생성
//         ocrOverlayContainer = new GameObject("OCROverlayContainer");
//         ocrOverlayContainer.transform.SetParent(canvas.transform, false);

//         RectTransform containerRect = ocrOverlayContainer.AddComponent<RectTransform>();
//         containerRect.anchorMin = Vector2.zero;
//         containerRect.anchorMax = Vector2.one;
//         containerRect.sizeDelta = Vector2.zero;
//         containerRect.anchoredPosition = Vector2.zero;

//         Debug.Log($"[ScreenshotOCR] Displaying {result.labels.Count} OCR results");

//         // 각 텍스트 영역에 대해 UI 생성
//         for (int i = 0; i < result.labels.Count; i++)
//         {
//             string text = result.labels[i];
            
//             // quad_boxes가 있는 경우 사용, 없으면 bboxes 사용
//             if (result.quad_boxes != null && i < result.quad_boxes.Count)
//             {
//                 CreateOCRTextUI(text, result.quad_boxes[i], canvas, captureWidth, captureHeight);
//             }
//         }

//         // 10초 후 자동 제거
//         if (destroyCoroutine != null)
//         {
//             StopCoroutine(destroyCoroutine);
//         }
//         destroyCoroutine = StartCoroutine(DestroyOverlayAfterDelay(10f));
//     }

//     // 원문 OCR 결과를 화면에 표시 (영역 OCR용 - 캡처 영역 크기 지정)
//     private void DisplayOCROriginResults(OCRResult result, int captureWidth, int captureHeight)
//     {
//         if (result == null || result.labels_origin == null || result.labels_origin.Count == 0)
//         {
//             Debug.LogWarning("[ScreenshotOCR] No origin OCR results to display");
//             ShowNoResultUI();
//             return;
//         }

//         List<List<List<float>>> originBoxes = (result.quad_boxes_origin != null && result.quad_boxes_origin.Count > 0)
//             ? result.quad_boxes_origin
//             : result.quad_boxes;

//         if (originBoxes == null || originBoxes.Count == 0)
//         {
//             Debug.LogWarning("[ScreenshotOCR] No origin quad_boxes to display");
//             ShowNoResultUI();
//             return;
//         }

//         OCRResult originOnly = new OCRResult
//         {
//             labels = result.labels_origin,
//             labels_origin = null,
//             quad_boxes = originBoxes
//         };

//         Debug.Log($"[ScreenshotOCR] Displaying {originOnly.labels.Count} origin OCR results");
//         DisplayOCRResults(originOnly, captureWidth, captureHeight);
//     }

//     // 개별 텍스트 UI 생성
//     private void CreateOCRTextUI(string text, List<List<float>> quadBox, Canvas canvas, int captureWidth, int captureHeight)
//     {
//         if (quadBox == null || quadBox.Count < 4) return;

//         // Unity 창의 렌더링 해상도
//         float screenWidth = Screen.width;
//         float screenHeight = Screen.height;
        
//         // 스케일 계산
//         float scaleX = screenWidth / captureWidth;
//         float scaleY = screenHeight / captureHeight;
        
//         // OCR 좌표를 Unity Screen 좌표로 변환 후 Canvas 로컬 좌표로 변환
//         List<Vector2> convertedPoints = new List<Vector2>();
        
//         foreach (var point in quadBox)
//         {
//             if (point.Count >= 2)
//             {
//                 // OCR 좌표 (캡처 이미지 기준, 좌상단 원점)
//                 float ocrX = point[0];
//                 float ocrY = point[1];
                
//                 // 1. OCR 좌표 → Unity Screen 좌표로 변환
//                 // - X: 스케일링
//                 // - Y: 스케일링 + Y축 반전 (좌상단→좌하단 원점)
//                 float unityScreenX = ocrX * scaleX;
//                 float unityScreenY = screenHeight - (ocrY * scaleY);
                
//                 Vector2 unityScreenPos = new Vector2(unityScreenX, unityScreenY);
                
//                 // 2. Unity Screen 좌표 → Canvas 로컬 좌표로 변환
//                 Vector2 canvasLocalPos = WindowCollisionManager.Instance.ConvertWinposToUnityPos(unityScreenPos);
//                 convertedPoints.Add(canvasLocalPos);
                
//                 // Debug.Log($"[OCR] OCR({ocrX}, {ocrY}) -> Screen({unityScreenX}, {unityScreenY}) -> Canvas({canvasLocalPos.x}, {canvasLocalPos.y})");
//             }
//         }
        
//         if (convertedPoints.Count < 4) return;

//         // 변환된 Canvas 좌표에서 바운딩 박스 계산
//         float minX = convertedPoints.Min(p => p.x);
//         float minY = convertedPoints.Min(p => p.y);
//         float maxX = convertedPoints.Max(p => p.x);
//         float maxY = convertedPoints.Max(p => p.y);

//         // 바운딩 박스 크기 계산
//         float width = maxX - minX;
//         float height = maxY - minY;

//         if (width <= 0 || height <= 0) return;

//         // UI 오브젝트 생성
//         GameObject textObj = new GameObject($"OCRText_{text.Substring(0, Math.Min(10, text.Length))}");
//         textObj.transform.SetParent(ocrOverlayContainer.transform, false);

//         // 배경 패널 추가 (반투명)
//         Image bgImage = textObj.AddComponent<Image>();
//         bgImage.color = new Color(0f, 0f, 0f, 0.5f); // 검은색 반투명

//         // RectTransform 설정 - 이미 Unity 로컬 좌표이므로 직접 사용
//         RectTransform rectTransform = textObj.GetComponent<RectTransform>();
//         rectTransform.anchorMin = new Vector2(0, 0);
//         rectTransform.anchorMax = new Vector2(0, 0);
//         rectTransform.pivot = new Vector2(0, 0);
//         rectTransform.localPosition = new Vector2(minX, minY); // localPosition 사용!
//         rectTransform.sizeDelta = new Vector2(width, height);

//         // 텍스트 컴포넌트 추가
//         GameObject textChild = new GameObject("Text");
//         textChild.transform.SetParent(textObj.transform, false);
        
//         TextMeshProUGUI textComponent = textChild.AddComponent<TextMeshProUGUI>();
//         textComponent.text = text;
//         // TMPro 기본 폰트 사용 (null이면 TMP Settings의 기본 폰트)
//         textComponent.font = null;
//         textComponent.color = Color.yellow; // 노란색 텍스트
//         textComponent.alignment = TextAlignmentOptions.MidlineLeft;

//         // BestFit (Auto Size) 설정
//         textComponent.enableAutoSizing = true;
//         textComponent.fontSizeMin = 8;
//         textComponent.fontSizeMax = 72;

//         // 여백 설정 (좌우 1px, 상하 2px) - 사각형이 충분히 클 때만
//         float marginH = width > 4 ? 1f : 0f;  // 좌우 각 1px (총 2px 필요)
//         float marginV = height > 6 ? 2f : 0f; // 상하 각 2px (총 4px 필요)
//         textComponent.margin = new Vector4(marginH, marginV, marginH, marginV); // left, top, right, bottom

//         RectTransform textRect = textChild.GetComponent<RectTransform>();
//         textRect.anchorMin = Vector2.zero;
//         textRect.anchorMax = Vector2.one;
//         textRect.sizeDelta = Vector2.zero;
//         textRect.anchoredPosition = Vector2.zero;
//     }

//     // 결과 없을 때 표시
//     private void ShowNoResultUI()
//     {
//         Canvas canvas = FindObjectOfType<Canvas>();
//         if (canvas == null) return;

//         ocrOverlayContainer = new GameObject("OCROverlayContainer_NoResult");
//         ocrOverlayContainer.transform.SetParent(canvas.transform, false);

//         // 배경 패널
//         Image image = ocrOverlayContainer.AddComponent<Image>();
//         image.color = new Color(1f, 0f, 0f, 0.3f); // 붉은색 투명

//         RectTransform rectTransform = ocrOverlayContainer.GetComponent<RectTransform>();
//         rectTransform.sizeDelta = new Vector2(600, 300);
//         rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
//         rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
//         rectTransform.pivot = new Vector2(0.5f, 0.5f);
//         rectTransform.anchoredPosition = Vector2.zero;

//         // 텍스트
//         GameObject textObj = new GameObject("NoResultText");
//         textObj.transform.SetParent(ocrOverlayContainer.transform, false);
//         TextMeshProUGUI tmpText = textObj.AddComponent<TextMeshProUGUI>();
//         tmpText.text = "No Text Detected";
//         tmpText.font = null; // TMPro 기본 폰트
//         tmpText.fontSize = 48;
//         tmpText.color = Color.white;
//         tmpText.alignment = TextAlignmentOptions.Center;

//         RectTransform textRect = textObj.GetComponent<RectTransform>();
//         textRect.sizeDelta = new Vector2(600, 300);
//         textRect.anchoredPosition = Vector2.zero;

//         // 5초 후 자동 제거
//         if (destroyCoroutine != null)
//         {
//             StopCoroutine(destroyCoroutine);
//         }
//         destroyCoroutine = StartCoroutine(DestroyOverlayAfterDelay(5f));
//     }

//     // 오버레이 제거 코루틴
//     private IEnumerator DestroyOverlayAfterDelay(float delay)
//     {
//         yield return new WaitForSeconds(delay);
//         ClearOCROverlay();
//         destroyCoroutine = null;
//     }

//     // OCR 오버레이 제거
//     private void ClearOCROverlay()
//     {
//         if (ocrOverlayContainer != null)
//         {
//             Destroy(ocrOverlayContainer);
//             ocrOverlayContainer = null;
//         }

//         if (destroyCoroutine != null)
//         {
//             StopCoroutine(destroyCoroutine);
//             destroyCoroutine = null;
//         }
//     }

//     private void OnDestroy()
//     {
//         ClearOCROverlay();
//     }

//     // === OCR + 자동 클릭 기능 ===

//     // 전체화면 OCR → 텍스트 매칭 → 자동 클릭
//     public void ExecuteFullScreenOCRAndClick(string targetText, bool exactMatch = true)
//     {
//         StartCoroutine(ExecuteFullScreenOCRAndClickCoroutine(targetText, exactMatch));
//     }

//     private IEnumerator ExecuteFullScreenOCRAndClickCoroutine(string targetText, bool exactMatch)
//     {
//         Debug.Log($"[ScreenshotOCR→Click] Starting full screen OCR to find and click '{targetText}' (exactMatch: {exactMatch})");

//         // 기존 오버레이 제거
//         ClearOCROverlay();

//         // ScreenshotManager 찾기
//         ScreenshotManager screenshotManager = FindObjectOfType<ScreenshotManager>();
//         if (screenshotManager == null)
//         {
//             Debug.LogError("[ScreenshotOCR] ScreenshotManager not found");
//             yield break;
//         }

//         bool shouldIncludeChar = false;
//         bool shouldIncludeUI = false;
//         try
//         {
//             shouldIncludeChar = SettingManager.Instance.settings.includeCharInScreenshot;
//             shouldIncludeUI = SettingManager.Instance.settings.includeUIInScreenshot;
//         }
//         catch (System.Exception ex)
//         {
//             Debug.LogWarning($"[ScreenshotOCR→Click] Failed to get settings: {ex.Message}");
//         }

//         List<GameObject> deactivatedCharObjects = new List<GameObject>();
//         List<GameObject> deactivatedUIObjects = new List<GameObject>();
//         Dictionary<GameObject, bool> uiWidgetVisibleStates = new Dictionary<GameObject, bool>();

//         GameObject[] allObjects = FindObjectsOfType<GameObject>();

//         // Char Layer 필터링
//         if (!shouldIncludeChar)
//         {
//             int charLayer = LayerMask.NameToLayer("Char");
//             if (charLayer != -1)
//             {
//                 foreach (GameObject obj in allObjects)
//                 {
//                     if (obj.layer == charLayer && obj.activeSelf)
//                     {
//                         obj.SetActive(false);
//                         deactivatedCharObjects.Add(obj);
//                     }
//                 }
//             }
//         }

//         // UI Layer 필터링
//         if (!shouldIncludeUI)
//         {
//             int uiLayer = LayerMask.NameToLayer("UI");
//             if (uiLayer != -1)
//             {
//                 foreach (GameObject obj in allObjects)
//                 {
//                     if (obj.layer == uiLayer && obj.activeSelf)
//                     {
//                         UIWidget widget = obj.GetComponent<UIWidget>();
//                         if (widget != null)
//                         {
//                             uiWidgetVisibleStates[obj] = widget.IsM_IsShowing;
//                         }
//                         obj.SetActive(false);
//                         deactivatedUIObjects.Add(obj);
//                     }
//                 }
//             }
//         }

//         yield return null; // 레이어 비활성화 반영 대기

//         // 전체화면 캡처
//         byte[] screenshotBytes = CaptureFullScreenToMemory();

//         // 레이어 복원
//         if (deactivatedCharObjects.Count > 0)
//         {
//             foreach (GameObject obj in deactivatedCharObjects)
//             {
//                 if (obj != null)
//                 {
//                     obj.SetActive(true);
//                 }
//             }
//         }

//         if (deactivatedUIObjects.Count > 0)
//         {
//             foreach (GameObject obj in deactivatedUIObjects)
//             {
//                 if (obj != null)
//                 {
//                     if (uiWidgetVisibleStates.TryGetValue(obj, out bool wasVisible))
//                     {
//                         if (wasVisible)
//                         {
//                             obj.SetActive(true);
//                         }
//                     }
//                     else
//                     {
//                         obj.SetActive(true);
//                     }
//                 }
//             }
//         }

//         if (screenshotBytes == null || screenshotBytes.Length == 0)
//         {
//             Debug.LogError("[ScreenshotOCR→Click] Failed to capture full screen");
//             yield break;
//         }

//         // OCR API 호출
//         bool apiCallCompleted = false;
//         int captureWidth = GetSystemMetrics(SM_CXSCREEN);
//         int captureHeight = GetSystemMetrics(SM_CYSCREEN);

//         APIManager.Instance.CallPaddleOCR(screenshotBytes, "", false, (result) =>
//         {
//             if (result != null)
//             {
//                 Debug.Log($"[ScreenshotOCR→Click] OCR completed, searching for '{targetText}'");
//                 FindTextAndClick(result, targetText, exactMatch, captureWidth, captureHeight);
//             }
//             else
//             {
//                 Debug.LogError("[ScreenshotOCR→Click] OCR failed");
//             }
//             apiCallCompleted = true;
//         });

//         while (!apiCallCompleted)
//         {
//             yield return null;
//         }
//     }

//     // 영역 OCR → 텍스트 매칭 → 자동 클릭
//     public void ExecuteAreaOCRAndClick(string targetText, bool exactMatch = true)
//     {
//         StartCoroutine(ExecuteAreaOCRAndClickCoroutine(targetText, exactMatch));
//     }

//     private IEnumerator ExecuteAreaOCRAndClickCoroutine(string targetText, bool exactMatch)
//     {
//         Debug.Log($"[ScreenshotOCR→Click] Starting area OCR to find and click '{targetText}' (exactMatch: {exactMatch})");

//         // ScreenshotManager 찾기
//         ScreenshotManager screenshotManager = FindObjectOfType<ScreenshotManager>();
//         if (screenshotManager == null)
//         {
//             Debug.LogError("[ScreenshotOCR] ScreenshotManager not found");
//             yield break;
//         }

//         ClearOCROverlay();

//         byte[] areaBytes = null;
//         int captureX = 0, captureY = 0, captureWidth = 0, captureHeight = 0;

//         bool captureCompleted = false;
//         yield return screenshotManager.CaptureScreenshotToMemoryWithInfo((bytes, x, y, w, h) =>
//         {
//             areaBytes = bytes;
//             captureX = x;
//             captureY = y;
//             captureWidth = w;
//             captureHeight = h;
//             captureCompleted = true;
//         });

//         while (!captureCompleted)
//         {
//             yield return null;
//         }

//         if (areaBytes == null || areaBytes.Length == 0)
//         {
//             Debug.LogError("[ScreenshotOCR→Click] Failed to capture area");
//             yield break;
//         }

//         // OCR API 호출
//         bool apiCallCompleted = false;
//         APIManager.Instance.CallPaddleOCR(areaBytes, "", false, (result) =>
//         {
//             if (result != null)
//             {
//                 Debug.Log($"[ScreenshotOCR→Click] Area OCR completed, searching for '{targetText}'");
                
//                 // 좌표 보정 (영역 내 상대좌표 → 전체 화면 절대좌표)
//                 AdjustOCRResultCoords(result, captureX, captureY);
                
//                 FindTextAndClick(result, targetText, exactMatch, captureWidth, captureHeight);
//             }
//             else
//             {
//                 Debug.LogError("[ScreenshotOCR→Click] Area OCR failed");
//             }
//             apiCallCompleted = true;
//         });

//         while (!apiCallCompleted)
//         {
//             yield return null;
//         }
//     }

//     // OCR 결과에서 텍스트를 찾아 클릭
//     private void FindTextAndClick(OCRResult result, string targetText, bool exactMatch, int captureWidth, int captureHeight)
//     {
//         if (result == null || result.labels == null || result.labels.Count == 0)
//         {
//             Debug.LogWarning($"[ScreenshotOCR→Click] No OCR results to search");
//             return;
//         }

//         // 텍스트 매칭
//         for (int i = 0; i < result.labels.Count; i++)
//         {
//             string label = result.labels[i];
//             bool isMatch = exactMatch ? (label == targetText) : label.Contains(targetText);

//             if (isMatch)
//             {
//                 Debug.Log($"[ScreenshotOCR→Click] Found matching text: '{label}' at index {i}");

//                 if (result.quad_boxes == null || i >= result.quad_boxes.Count)
//                 {
//                     Debug.LogError($"[ScreenshotOCR→Click] No quad_box data for matched text at index {i}");
//                     return;
//                 }

//                 // quad_box 중심 좌표 계산 (OCR 좌표계)
//                 List<List<float>> quadBox = result.quad_boxes[i];
//                 if (quadBox == null || quadBox.Count < 4)
//                 {
//                     Debug.LogError($"[ScreenshotOCR→Click] Invalid quad_box data");
//                     return;
//                 }

//                 float sumX = 0f, sumY = 0f;
//                 int pointCount = 0;
//                 foreach (var point in quadBox)
//                 {
//                     if (point.Count >= 2)
//                     {
//                         sumX += point[0];
//                         sumY += point[1];
//                         pointCount++;
//                     }
//                 }

//                 if (pointCount == 0)
//                 {
//                     Debug.LogError($"[ScreenshotOCR→Click] No valid points in quad_box");
//                     return;
//                 }

//                 float centerX_OCR = sumX / pointCount;
//                 float centerY_OCR = sumY / pointCount;

//                 Debug.Log($"[ScreenshotOCR→Click] Text box center in OCR coordinates: ({centerX_OCR}, {centerY_OCR})");

//                 // OCR 좌표 → Win32 화면 좌표 변환
//                 // OCR: 좌상단 기준, Y축 아래 방향
//                 // Win32: 좌상단 기준, Y축 아래 방향 (동일)
//                 // 단, 캡처 해상도와 실제 화면 해상도가 다를 경우 스케일링 필요
                
//                 int screenWidth = GetSystemMetrics(SM_CXSCREEN);
//                 int screenHeight = GetSystemMetrics(SM_CYSCREEN);

//                 // 캡처 크기가 실제 화면과 다를 경우 스케일링
//                 float scaleX = (float)screenWidth / captureWidth;
//                 float scaleY = (float)screenHeight / captureHeight;

//                 int clickX = (int)(centerX_OCR * scaleX);
//                 int clickY = (int)(centerY_OCR * scaleY);

//                 Debug.Log($"[ScreenshotOCR→Click] Click position (Win32 screen coords): ({clickX}, {clickY})");

//                 // ExecutorMouseAction을 통해 클릭 실행
//                 if (ExecutorMouseAction.Instance != null)
//                 {
//                     ExecutorMouseAction.Instance.ClickAtPosition(clickX, clickY);
//                 }
//                 else
//                 {
//                     Debug.LogError("[ScreenshotOCR→Click] ExecutorMouseAction instance not found");
//                 }

//                 return; // 첫 번째 매칭만 클릭
//             }
//         }

//         Debug.LogWarning($"[ScreenshotOCR→Click] No matching text found for '{targetText}' (exactMatch: {exactMatch})");
//     }
// }

// // OCR 결과 데이터 구조
// [System.Serializable]
// public class OCRResult
// {
//     public List<string> labels;
//     public List<string> labels_origin;  // 번역 전 원본 (없으면 null)
//     public List<List<List<float>>> quad_boxes; // quad_boxes: [[[x1,y1],[x2,y2],[x3,y3],[x4,y4]], ...]
//     public List<List<List<float>>> quad_boxes_origin; // 원문 박스 (ocr_with_region_origin)
// }

