using System;
using System.Runtime.InteropServices;
using System.IO;
using UnityEngine;
// MR 포팅: System.Drawing은 Windows 스탠드얼론에서만 사용 가능하다 (Android/IL2CPP 미지원).
#if UNITY_STANDALONE_WIN
using System.Drawing;
#endif

public class Win32ScreenCapture : MonoBehaviour
{
#if UNITY_STANDALONE_WIN
    // P/Invoke functions from gdi32.dll and user32.dll
    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowDC(IntPtr hWnd);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr bmp);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest,
                                      IntPtr hdcSrc, int xSrc, int ySrc, int Rop);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    // Constants
    private const int SRCCOPY = 0x00CC0020;
#endif

    public void CaptureDesktop(string filePath)
    {
#if !UNITY_STANDALONE_WIN
        // MR 포팅: 캡처할 데스크톱 화면이 존재하지 않는다.
        Debug.LogWarning("[Win32ScreenCapture] 데스크톱 캡처는 Windows 전용입니다. 건너뜁니다.");
        return;
#else
        // Get the desktop window and its DC
        IntPtr desktopHwnd = GetDesktopWindow();
        IntPtr desktopDC = GetWindowDC(desktopHwnd);
        IntPtr memoryDC = CreateCompatibleDC(desktopDC);

        // Get screen dimensions
        int screenWidth = Screen.width;
        int screenHeight = Screen.height;

        // Create a compatible bitmap
        IntPtr bitmap = CreateCompatibleBitmap(desktopDC, screenWidth, screenHeight);
        IntPtr oldBitmap = SelectObject(memoryDC, bitmap);

        // Copy the screen content to the memory DC
        BitBlt(memoryDC, 0, 0, screenWidth, screenHeight, desktopDC, 0, 0, SRCCOPY);

        // Create a Bitmap object from the handle
        using (Bitmap bmp = Bitmap.FromHbitmap(bitmap))
        {
            // Save the Bitmap as PNG
            bmp.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
        }

        // Clean up
        SelectObject(memoryDC, oldBitmap);
        DeleteObject(bitmap);
        DeleteDC(memoryDC);
        ReleaseDC(desktopHwnd, desktopDC);
#endif
    }
}
