using UnityEngine;
using UnityEngine.UI;

// Server 패널에서 server_id를 QR로 표시 — 폰/XR이 스캔해 같은 id로 접속하는 용도
// payload 형식: "mlj:{server_id}" (ServerQrScanManager와 약속된 접두어)
public class ServerQrDisplay : MonoBehaviour
{
    public const string PayloadPrefix = "mlj:";

    [SerializeField] private Image qrImage;  // QR을 그릴 대상 (스프라이트는 런타임 생성)

    private string lastRenderedId;  // 마지막으로 그린 id (변경 감지용)
    private Texture2D qrTexture;
    private Sprite qrSprite;

    private void OnEnable()
    {
        Refresh();
    }

    private void Update()
    {
        // 패널이 열린 동안 id 입력 변화를 따라감 (문자열 비교뿐이라 부담 없음)
        string currentId = SettingManager.Instance != null ? SettingManager.Instance.settings?.server_id : null;
        if (currentId != lastRenderedId)
        {
            Refresh();
        }
    }

    // 현재 server_id로 QR 재생성
    public void Refresh()
    {
        string serverId = SettingManager.Instance != null ? SettingManager.Instance.settings?.server_id : null;
        lastRenderedId = serverId;

        if (qrImage == null) return;
        if (string.IsNullOrEmpty(serverId))
        {
            qrImage.enabled = false;
            return;
        }

        bool[,] modules = QrCodeEncoder.Encode(PayloadPrefix + serverId);
        if (modules == null)
        {
            qrImage.enabled = false;
            return;
        }

        int size = modules.GetLength(0);
        int quiet = 2;  // 스캔 안정용 최소 여백 (quiet zone)
        int texSize = size + quiet * 2;

        if (qrTexture == null || qrTexture.width != texSize)
        {
            if (qrTexture != null) Destroy(qrTexture);
            qrTexture = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
            qrTexture.filterMode = FilterMode.Point;  // 확대 시 픽셀 또렷하게
        }

        Color32 dark = new Color32(0, 0, 0, 255);
        Color32 light = new Color32(255, 255, 255, 255);
        Color32[] pixels = new Color32[texSize * texSize];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = light;
        for (int r = 0; r < size; r++)
        {
            for (int c = 0; c < size; c++)
            {
                if (!modules[r, c]) continue;
                // 텍스처 y축은 아래→위이므로 행을 뒤집어 기록
                int y = texSize - 1 - (quiet + r);
                pixels[y * texSize + (quiet + c)] = dark;
            }
        }
        qrTexture.SetPixels32(pixels);
        qrTexture.Apply();

        if (qrSprite != null) Destroy(qrSprite);
        qrSprite = Sprite.Create(qrTexture, new Rect(0, 0, texSize, texSize), new Vector2(0.5f, 0.5f), 100f);
        qrImage.sprite = qrSprite;
        qrImage.enabled = true;
    }

    private void OnDestroy()
    {
        if (qrSprite != null) Destroy(qrSprite);
        if (qrTexture != null) Destroy(qrTexture);
    }
}
