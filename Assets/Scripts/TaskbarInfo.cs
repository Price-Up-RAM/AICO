using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class TaskbarInfo : MonoBehaviour
{
    // MR 포팅: Win32 P/Invoke는 Windows 스탠드얼론에서만 선언한다.
    // 그 외 플랫폼(Quest/Android)에서는 같은 시그니처의 스텁으로 대체해
    // 호출부를 수정하지 않고도 안전하게 no-op이 되게 한다.
#if UNITY_STANDALONE_WIN
    [DllImport("user32.dll")]
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
#else
    // 작업표시줄이 존재하지 않으므로 항상 "못 찾음"을 반환한다.
    private static IntPtr FindWindow(string lpClassName, string lpWindowName) => IntPtr.Zero;

    private static bool GetWindowRect(IntPtr hWnd, out RECT lpRect)
    {
        lpRect = default;
        return false;
    }
#endif

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public static Rect GetTaskbarRect()
    {
        IntPtr taskbarHandle = FindWindow("Shell_TrayWnd", null); // 작업 표시줄의 핸들을 찾음
        if (taskbarHandle != IntPtr.Zero)
        {
            RECT rect;
            if (GetWindowRect(taskbarHandle, out rect))
            {
                return new Rect(rect.Left, Screen.height - (rect.Bottom - rect.Top), rect.Right - rect.Left, rect.Bottom - rect.Top);
            }
        }
        return Rect.zero;
    }

    public Rect TaskbarRectangle;

    void Start()
    {
        TaskbarRectangle = GetTaskbarRect();
        Debug.Log("Taskbar Position: " + TaskbarRectangle);
    }
}
