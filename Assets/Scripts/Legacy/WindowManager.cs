using System;
using System.Runtime.InteropServices;
using UnityEngine;
using Diagnostics = System.Diagnostics;

public class WindowManager : MonoBehaviour
{
    // 싱글톤 (Lazy getter)
    public static WindowManager instance;
    public static WindowManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<WindowManager>();
                if (instance == null)
                {
                    var go = new GameObject("WindowManager");
                    instance = go.AddComponent<WindowManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int X,
        int Y,
        int cx,
        int cy,
        uint uFlags
    );

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint TOPMOST_FLAGS = SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE;

    // 인스턴스 캐시
    private IntPtr _cachedUnityHwnd = IntPtr.Zero;

    // 외부 호출은 이것만
    public static void SetWindowAlwaysOnTop(bool topMost)
    {
#if !UNITY_ANDROID && !UNITY_EDITOR
        Instance.SetWindowAlwaysOnTopInternal(topMost);
#endif
    }

    private void SetWindowAlwaysOnTopInternal(bool topMost)
    {
        // 1) 1차 시도
        IntPtr hWnd = ResolveUnityWindowHandle();
        if (hWnd == IntPtr.Zero)
        {
            UnityEngine.Debug.LogWarning("Unity window handle not found. Skip SetWindowPos.");
            return;
        }

        if (TrySetTopMost(hWnd, topMost))
        {
            return;
        }

        // 2) 실패하면 캐시 무효화 후 1회 재탐색 + 재시도
        _cachedUnityHwnd = IntPtr.Zero;

        hWnd = ResolveUnityWindowHandle();
        if (hWnd == IntPtr.Zero)
        {
            UnityEngine.Debug.LogWarning("Unity window handle not found after retry. Skip SetWindowPos.");
            return;
        }

        if (!TrySetTopMost(hWnd, topMost))
        {
            UnityEngine.Debug.LogWarning("SetWindowPos failed after retry.");
        }
    }

    private bool TrySetTopMost(IntPtr hWnd, bool topMost)
    {
        bool ok;

        if (topMost)
        {
            ok = SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, TOPMOST_FLAGS);
            if (ok)
            {
                UnityEngine.Debug.Log("Window set to always on top.");
            }
            return ok;
        }

        ok = SetWindowPos(hWnd, HWND_NOTOPMOST, 0, 0, 0, 0, TOPMOST_FLAGS);
        if (ok)
        {
            UnityEngine.Debug.Log("Window no longer set to always on top.");
        }
        return ok;
    }

    // static 제거: 인스턴스 메서드로 처리
    private IntPtr ResolveUnityWindowHandle()
    {
        // 1) 캐시가 있으면 프로세스 소유 검증 후 사용
        if (_cachedUnityHwnd != IntPtr.Zero)
        {
            if (IsOwnedByCurrentProcess(_cachedUnityHwnd))
            {
                return _cachedUnityHwnd;
            }

            _cachedUnityHwnd = IntPtr.Zero;
        }

        // 2) 현재 프로세스 메인 윈도우 핸들 우선
        try
        {
            var p = Diagnostics.Process.GetCurrentProcess();
            if (p != null && p.MainWindowHandle != IntPtr.Zero && IsOwnedByCurrentProcess(p.MainWindowHandle))
            {
                _cachedUnityHwnd = p.MainWindowHandle;
                return _cachedUnityHwnd;
            }
        }
        catch
        {
            // ignore
        }

        // 3) 보조: Foreground 창이 우리 프로세스 소유일 때만 채택
        IntPtr fg = GetForegroundWindow();
        if (fg != IntPtr.Zero && IsOwnedByCurrentProcess(fg))
        {
            _cachedUnityHwnd = fg;
            return _cachedUnityHwnd;
        }

        return IntPtr.Zero;
    }

    private bool IsOwnedByCurrentProcess(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
        {
            return false;
        }

        uint pid;
        GetWindowThreadProcessId(hWnd, out pid);

        try
        {
            return pid == (uint)Diagnostics.Process.GetCurrentProcess().Id;
        }
        catch
        {
            return false;
        }
    }
}
