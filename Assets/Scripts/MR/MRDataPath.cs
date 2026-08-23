using System.IO;
using UnityEngine;

public static class MRDataPath
{
    /// <summary>
    /// StreamingAssets 대체 루트. 데스크톱은 원본 경로, Android는 복사본 경로.
    /// </summary>
    public static string Root
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        get => Path.Combine(Application.persistentDataPath, "StreamingAssets");
#else
        get => Application.streamingAssetsPath;
#endif
    }

    /// <summary>
    /// 쓰기 전용 경로. StreamingAssets는 읽기 전용이므로 쓰기는 항상 여기로.
    /// </summary>
    public static string WritableRoot => Path.Combine(Application.persistentDataPath, "UserData");
}
