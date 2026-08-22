using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class StreamingAssetsIndexData
{
    public string version;
    public List<string> files = new List<string>();
}

[DefaultExecutionOrder(-100)]
public class MRStreamingAssetsInstaller : MonoBehaviour
{
    private string installRoot;
    
    // UI나 다른 스크립트에서 완료 여부를 확인하기 위함
    public static bool IsInstalled { get; private set; } = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        GameObject go = new GameObject("MRStreamingAssetsInstaller");
        go.AddComponent<MRStreamingAssetsInstaller>();
        DontDestroyOnLoad(go);
#endif
    }

    private void Awake()
    {
        installRoot = MRDataPath.Root; // Android에서는 persistentDataPath/StreamingAssets, PC에서는 원본

#if UNITY_ANDROID && !UNITY_EDITOR
        StartCoroutine(InstallRoutine());
#else
        // 데스크톱 환경에서는 복사 불필요
        IsInstalled = true;
        Debug.Log("[MRStreamingAssetsInstaller] Desktop environment, skipping installation.");
#endif
    }

    private IEnumerator InstallRoutine()
    {
        Debug.Log("[MRStreamingAssetsInstaller] Starting installation check...");

        string apkIndexPath = Path.Combine(Application.streamingAssetsPath, "_index.json");
        string localIndexPath = Path.Combine(installRoot, "_index.json");

        // 1. APK의 _index.json 읽기
        using (UnityWebRequest req = UnityWebRequest.Get(apkIndexPath))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[MRStreamingAssetsInstaller] Failed to load _index.json from APK: {req.error}");
                IsInstalled = true; // 실패해도 앱이 완전히 멈추지 않도록 일단 true로 처리
                yield break;
            }

            string apkIndexJson = req.downloadHandler.text;
            StreamingAssetsIndexData apkIndex = JsonUtility.FromJson<StreamingAssetsIndexData>(apkIndexJson);

            // 2. 로컬 _index.json 확인 및 버전 비교
            if (File.Exists(localIndexPath))
            {
                string localIndexJson = File.ReadAllText(localIndexPath);
                StreamingAssetsIndexData localIndex = JsonUtility.FromJson<StreamingAssetsIndexData>(localIndexJson);

                if (localIndex != null && localIndex.version == apkIndex.version)
                {
                    Debug.Log("[MRStreamingAssetsInstaller] Version matches, skipping installation.");
                    IsInstalled = true;
                    yield break;
                }
            }

            // 3. 파일 순회하며 복사
            if (!Directory.Exists(installRoot))
            {
                Directory.CreateDirectory(installRoot);
            }

            int totalFiles = apkIndex.files.Count;
            int currentFile = 0;

            foreach (string file in apkIndex.files)
            {
                string sourcePath = Path.Combine(Application.streamingAssetsPath, file);
                string targetPath = Path.Combine(installRoot, file);

                string targetDir = Path.GetDirectoryName(targetPath);
                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                using (UnityWebRequest fileReq = UnityWebRequest.Get(sourcePath))
                {
                    yield return fileReq.SendWebRequest();

                    if (fileReq.result == UnityWebRequest.Result.Success)
                    {
                        File.WriteAllBytes(targetPath, fileReq.downloadHandler.data);
                        currentFile++;
                        Debug.Log($"[MRStreamingAssetsInstaller] Copied ({currentFile}/{totalFiles}): {file}");
                    }
                    else
                    {
                        Debug.LogError($"[MRStreamingAssetsInstaller] Failed to copy {file}: {fileReq.error}");
                    }
                }
            }

            // 4. 완료 후 _index.json 을 복사본 쪽에 기록
            File.WriteAllText(localIndexPath, apkIndexJson);
            Debug.Log("[MRStreamingAssetsInstaller] Installation completed successfully.");
            
            IsInstalled = true;
        }
    }
}
