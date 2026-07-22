using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;

// 폰/XR용 QR 스캐너 — server_host가 표시한 "mlj:{server_id}" QR을 카메라로 읽어
// server_id 설정 + 서버 타입을 Server(10)로 전환한다. (디코딩: Assets/Plugins/ZXing/zxing.dll)
//
// 사용법(프로토): 아무 버튼 onClick에 StartScan() 연결, 미리보기를 원하면 previewImage(RawImage) 할당.
// 스캔 성공/중지 시 자동으로 카메라를 정리한다.
//
// Quest 3/3S(Horizon OS v74+): 패스스루 카메라가 WebCamTexture로 그대로 잡힌다 — 런타임 권한 요청 포함됨.
// TODO(Quest 빌드 셋업 시): AndroidManifest에 <uses-permission android:name="horizonos.permission.HEADSET_CAMERA"/>
//   선언 필요 (Meta XR SDK의 Update AndroidManifest 재생성 후에도 이 줄이 유지되는지 확인).
//   VL 대화용 캡처가 생기면 카메라 소유는 공용 매니저로 옮기고, 본 스캐너는 그 텍스처를 빌려
//   TryDecode만 수행할 것 (Android 카메라는 배타 점유라 이중 오픈 충돌).
public class ServerQrScanManager : MonoBehaviour
{
    [SerializeField] private RawImage previewImage;  // 카메라 미리보기 (선택 — 없어도 동작)
    [SerializeField] private float decodeInterval = 0.5f;  // 디코딩 시도 주기(초)

    private WebCamTexture camTexture;
    private Coroutine scanRoutine;

    // 싱글톤 (프로젝트 매니저 관례)
    private static ServerQrScanManager instance;
    public static ServerQrScanManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<ServerQrScanManager>();
            }
            return instance;
        }
    }

    // 스캔 시작 (버튼 onClick 연결용 — 성공 시 설정 자동 적용)
    public void StartScan()
    {
        StartScan(null);
    }

    // 스캔 시작 — onFound에 server_id 전달 (null이면 기본 동작: 설정 적용)
    public void StartScan(Action<string> onFound)
    {
        if (scanRoutine != null) return;  // 이미 스캔 중
        scanRoutine = StartCoroutine(ScanCoroutine(onFound));
    }

    // 스캔 중지 및 카메라 정리
    public void StopScan()
    {
        if (scanRoutine != null)
        {
            StopCoroutine(scanRoutine);
            scanRoutine = null;
        }
        if (camTexture != null)
        {
            camTexture.Stop();
            Destroy(camTexture);
            camTexture = null;
        }
        if (previewImage != null)
        {
            previewImage.texture = null;
            previewImage.gameObject.SetActive(false);
        }
    }

    private IEnumerator ScanCoroutine(Action<string> onFound)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // Android 카메라 권한 요청
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
        {
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera);
            float wait = 0f;
            while (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera) && wait < 10f)
            {
                wait += Time.deltaTime;
                yield return null;
            }
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
            {
                Debug.LogWarning("[ServerQrScan] 카메라 권한 거부됨");
                scanRoutine = null;
                yield break;
            }
        }

        // Quest(Horizon OS) 패스스루 카메라는 별도 권한 필요 (Quest 3/3S + OS v74+). 폰에서는 이 분기를 타지 않음
        if (SystemInfo.deviceModel.IndexOf("Quest", StringComparison.OrdinalIgnoreCase) >= 0
            || SystemInfo.deviceModel.IndexOf("Oculus", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            string headsetCameraPermission = "horizonos.permission.HEADSET_CAMERA";
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(headsetCameraPermission))
            {
                UnityEngine.Android.Permission.RequestUserPermission(headsetCameraPermission);
                float waitHeadset = 0f;
                while (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(headsetCameraPermission) && waitHeadset < 10f)
                {
                    waitHeadset += Time.deltaTime;
                    yield return null;
                }
                if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(headsetCameraPermission))
                {
                    Debug.LogWarning("[ServerQrScan] 헤드셋 카메라 권한 거부됨 (Quest 패스스루)");
                    NoticeBalloonManager.Instance?.ModifyNoticeBalloonText("Camera permission denied");
                    scanRoutine = null;
                    yield break;
                }
            }
        }
#endif
        if (WebCamTexture.devices.Length == 0)
        {
            Debug.LogWarning("[ServerQrScan] 사용 가능한 카메라 없음");
            NoticeBalloonManager.Instance?.ModifyNoticeBalloonText("No camera found");
            scanRoutine = null;
            yield break;
        }

        NoticeBalloonManager.Instance?.ModifyNoticeBalloonText("Scanning QR...");
        camTexture = new WebCamTexture(640, 480);
        camTexture.Play();
        if (previewImage != null)
        {
            previewImage.texture = camTexture;
            previewImage.gameObject.SetActive(true);
        }

        // 카메라 워밍업 대기
        float warmup = 0f;
        while (camTexture.width <= 16 && warmup < 5f)
        {
            warmup += Time.deltaTime;
            yield return null;
        }

        QRCodeReader reader = new QRCodeReader();
        var hints = new System.Collections.Generic.Dictionary<DecodeHintType, object>
        {
            { DecodeHintType.TRY_HARDER, true }  // 실카메라 저품질 프레임 인식률 향상
        };
        var interval = new WaitForSeconds(decodeInterval);
        while (true)
        {
            yield return interval;
            if (camTexture == null || !camTexture.isPlaying) continue;

            string decoded = TryDecode(reader, hints, camTexture);
            if (string.IsNullOrEmpty(decoded)) continue;
            if (!decoded.StartsWith(ServerQrDisplay.PayloadPrefix)) continue;  // 무관한 QR 무시

            string serverId = decoded.Substring(ServerQrDisplay.PayloadPrefix.Length);
            Debug.Log($"[ServerQrScan] server_id 인식: {serverId}");
            scanRoutine = null;
            StopScan();

            if (onFound != null)
            {
                onFound(serverId);
            }
            else
            {
                ApplyServerId(serverId);
            }
            yield break;
        }
    }

    // 프레임 1장 디코딩 시도 (실패 시 null)
    private string TryDecode(QRCodeReader reader, System.Collections.Generic.IDictionary<DecodeHintType, object> hints, WebCamTexture texture)
    {
        try
        {
            Color32[] pixels = texture.GetPixels32();
            int width = texture.width;
            int height = texture.height;

            // Color32(RGBA) → RGBA32 바이트 배열
            byte[] raw = new byte[pixels.Length * 4];
            for (int i = 0; i < pixels.Length; i++)
            {
                raw[i * 4] = pixels[i].r;
                raw[i * 4 + 1] = pixels[i].g;
                raw[i * 4 + 2] = pixels[i].b;
                raw[i * 4 + 3] = pixels[i].a;
            }
            var source = new RGBLuminanceSource(raw, width, height, RGBLuminanceSource.BitmapFormat.RGBA32);
            Result result = reader.decode(new BinaryBitmap(new HybridBinarizer(source)), hints);
            reader.reset();
            return result != null ? result.Text : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // 인식된 server_id를 설정에 반영하고 서버 타입을 Server로 전환
    private void ApplyServerId(string serverId)
    {
        if (SettingManager.Instance == null) return;
        SettingManager.Instance.SetServerID(serverId);
        SettingManager.Instance.SetServerTypeByValue(10);  // Server 타입 (idx 10)
        NoticeBalloonManager.Instance?.ModifyNoticeBalloonText($"Server ID set: {serverId}");
    }

    private void OnDisable()
    {
        // 스캔 중 비활성화 시 카메라/가드 상태 정리 (이미 죽은 코루틴에 StopCoroutine은 no-op이라 안전)
        StopScan();
    }

    private void OnDestroy()
    {
        StopScan();
    }
}
