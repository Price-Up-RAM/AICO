using UnityEngine;

public class TransParentWindowAndroidXXXX : MonoBehaviour
{
    // // 앱이 시작될 때 호출되도록 Start에서 투명 배경과 항상 위 설정을 적용
    // void Start()
    // {
    //     SetTransparentBackground(); // 배경 투명하게 설정
    //     SetAlwaysOnTop(); // 항상 위에 표시되도록 설정
    // }

    // // 안드로이드에서 배경을 투명하게 설정하는 함수
    // public void SetTransparentBackground()
    // {
    //     #if UNITY_ANDROID
    //     // 안드로이드에서 투명 배경을 설정하는 코드
    //     using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
    //     {
    //         using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
    //         {
    //             // WindowManager 객체를 가져옵니다.
    //             AndroidJavaObject window = currentActivity.Call<AndroidJavaObject>("getWindow");
    //             // 투명 배경 설정 (WindowManager.LayoutParams.FLAG_TRANSLUCENT_BACKGROUND)
    //             window.Call("setFlags", 0x80000000, 0x80000000);
    //         }
    //     }
    //     #endif
    // }

    // // 안드로이드에서 앱을 화면에 항상 위로 설정하는 함수
    // public void SetAlwaysOnTop()
    // {
    //     #if UNITY_ANDROID
    //     using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
    //     {
    //         using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
    //         {
    //             // 항상 위에 표시될 수 있도록 설정
    //             AndroidJavaObject window = currentActivity.Call<AndroidJavaObject>("getWindow");
    //             window.Call("addFlags", 0x00000080); // FLAG_KEEP_SCREEN_ON (화면 꺼짐 방지)
    //         }
    //     }
    //     #endif
    // }
}
