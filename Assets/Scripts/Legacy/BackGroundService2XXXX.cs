using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BackGroundService2 : MonoBehaviour
{
    private AndroidJavaObject unityActivity;
    private AndroidJavaObject context;

    void Start()
    {
        // Android Activity 및 Context 초기화
        AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        unityActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        context = new AndroidJavaClass("com.example.mylittlejarvisandroidplugin.MainService");
    }

    public void StartBackgroundService()
    {
        #if UNITY_ANDROID && !UNITY_EDITOR  // 안드로이드
        context.CallStatic("createNotification");
        Debug.Log("서비스 시작");
        #endif
    }

    public void StopBackgroundService()
    {
        #if UNITY_ANDROID && !UNITY_EDITOR  // 안드로이드
        context.CallStatic("stopService");
        Debug.Log("서비스 정지");
        #endif
    }
}
