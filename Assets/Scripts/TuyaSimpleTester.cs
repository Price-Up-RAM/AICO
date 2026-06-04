using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System;

public class TuyaSimpleTester : MonoBehaviour
{
    [Header("Tuya Credentials")]
    public string accessId = "cpg7uhytu7u3fp379y4d";
    public string accessSecret = "79be081d854941068de5aedf25367a79";
    public string deviceId = "ebd8d9ee889859825e4mi0";
    
    [Header("Region Settings")]
    public string regionUrl = "https://openapi.tuyaus.com"; // Western America 기준

    [Header("Test Switch")]
    public bool isPlugOn; // 인스펙터에서 이 체크박스를 클릭하면 작동합니다.

    private string cachedToken = "";
    private long tokenExpireTime = 0;

    // 인스펙터에서 값이 바뀔 때마다 호출되는 함수
    private void OnValidate()
    {
        // 유니티 시스템상 에디터 수정 중 코루틴 실행을 위해 처리
        if (Application.isPlaying)
        {
            StopAllCoroutines();
            StartCoroutine(ControlRoutine(isPlugOn));
        }
        else
        {
            Debug.Log("실시간 테스트를 보려면 'Play' 버튼을 누른 후 체크박스를 클릭하세요.");
        }
    }

    IEnumerator ControlRoutine(bool turnOn)
    {
        // 1. 토큰 체크 및 갱신
        if (string.IsNullOrEmpty(cachedToken) || GetCurrentTimestamp() > tokenExpireTime)
        {
            yield return StartCoroutine(GetToken());
        }

        if (string.IsNullOrEmpty(cachedToken)) yield break;

        // 2. 명령 전송
        yield return StartCoroutine(SendDeviceCommand(turnOn));
    }

    IEnumerator GetToken()
    {
        string t = GetCurrentTimestamp().ToString();
        string url = "/v1.0/token?grant_type=1";
        string sign = CalculateSign(accessId, accessSecret, "", t, "GET", url);

        using (UnityWebRequest request = UnityWebRequest.Get(regionUrl + url))
        {
            request.SetRequestHeader("client_id", accessId);
            request.SetRequestHeader("sign", sign);
            request.SetRequestHeader("t", t);
            request.SetRequestHeader("sign_method", "HMAC-SHA256");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<TuyaTokenResponse>(request.downloadHandler.text);
                if (response.success)
                {
                    cachedToken = response.result.access_token;
                    tokenExpireTime = GetCurrentTimestamp() + (response.result.expire_time * 1000) - 60000;
                    Debug.Log("Tuya Token 갱신 성공!");
                }
            }
        }
    }

    IEnumerator SendDeviceCommand(bool turnOn)
    {
        string t = GetCurrentTimestamp().ToString();
        string url = $"/v1.0/devices/{deviceId}/commands";
        
        // 주의: 기기에 따라 'switch_1' 또는 'switch'일 수 있습니다.
        string body = "{\"commands\":[{\"code\":\"switch_1\",\"value\":" + turnOn.ToString().ToLower() + "}]}";
        string sign = CalculateSign(accessId, accessSecret, cachedToken, t, "POST", url, body);

        using (UnityWebRequest request = new UnityWebRequest(regionUrl + url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(body);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("client_id", accessId);
            request.SetRequestHeader("sign", sign);
            request.SetRequestHeader("t", t);
            request.SetRequestHeader("sign_method", "HMAC-SHA256");
            request.SetRequestHeader("access_token", cachedToken);

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
                Debug.Log($"플러그 상태 변경 완료: {turnOn} | {request.downloadHandler.text}");
            else
                Debug.LogError($"에러 발생: {request.error}");
        }
    }

    // --- 보안 서명 생성 헬퍼 함수 ---
    string CalculateSign(string id, string secret, string token, string t, string method, string url, string body = "")
    {
        string contentHash = "";
        if (!string.IsNullOrEmpty(body))
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(body));
                contentHash = BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }
        else
        {
            contentHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
        }

        string stringToSign = method + "\n" + contentHash + "\n" + "" + "\n" + url;
        string message = id + token + t + stringToSign;
        
        byte[] keyByte = Encoding.UTF8.GetBytes(secret);
        byte[] messageBytes = Encoding.UTF8.GetBytes(message);
        using (var hmacsha256 = new HMACSHA256(keyByte))
        {
            byte[] hashmessage = hmacsha256.ComputeHash(messageBytes);
            return BitConverter.ToString(hashmessage).Replace("-", "").ToUpper();
        }
    }

    long GetCurrentTimestamp() => DateTimeOffset.Now.ToUnixTimeMilliseconds();

    // JSON 파싱용 클래스
    [Serializable] public class TuyaTokenResponse { public bool success; public TokenResult result; }
    [Serializable] public class TokenResult { public string access_token; public int expire_time; }
}