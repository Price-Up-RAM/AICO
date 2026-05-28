using System;
using UnityEngine;
// using UnityEngine.InputSystem;

public class BackgroundManager : MonoBehaviour
{

//     bool permissionGranted = false;

//    void Start()
//    {
//        #if UNITY_ANDROID && !UNITY_EDITOR
//        Application.runInBackground = true;
//        RequestPermission(); 
//        InputSystem.EnableDevice(StepCounter.current);
//        #endif
//    }

//    async void RequestPermission()
//    {
//        #if UNITY_ANDROID
//             AndroidRuntimePermissions.Permission result = await AndroidRuntimePermissions.RequestPermissionAsync("android.permission.ACTIVITY_RECOGNITION");
//             if (result == AndroidRuntimePermissions.Permission.Granted)
//             {
//                 permissionGranted = true;
//                 InitializeCounter();
//             }
//             else
//             {
//             }
//        #endif
//    }

//     void OnApplicationPause(bool pause)
//     {
//         Debug.Log("permissionGranted : " + permissionGranted.ToString());
//         if (!pause && permissionGranted)
//         {
//             // Reinitialize the step counter when the app is resumed
//             InitializeCounter();
//         }
//     }

//     void InitializeCounter()
//     {
//         InputSystem.EnableDevice(StepCounter.current);
//         // stepOffset = StepCounter.current.stepCounter.ReadValue();
//     }
}
