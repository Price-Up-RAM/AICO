using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;

public class TransparentWindowXXX : MonoBehaviour
{
    // 외부 DLL(user32.dll)에서 MessageBox 함수를 가져옴. 이 함수는 메시지 박스를 표시합니다.
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    // 외부 DLL(user32.dll)에서 GetActiveWindow 함수를 가져옴. 이 함수는 현재 활성화된 창의 핸들을 반환합니다.
    [DllImport("user32.dll")]
    public static extern IntPtr GetActiveWindow();

    // 창의 여백 정보를 정의하는 구조체
    private struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    // 외부 DLL(Dwmapi.dll)에서 DwmExtendFrameIntoClientArea 함수를 가져옴. 이 함수는 윈도우의 클라이언트 영역에 프레임을 확장합니다.
    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS margins);

    // 외부 DLL(user32.dll)에서 SetWindowLong 함수를 가져옴. 이 함수는 윈도우의 속성 값을 설정합니다.
    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    // 외부 DLL(user32.dll)에서 SetWindowPos 함수를 가져옴. 이 함수는 윈도우의 위치와 크기를 설정합니다.
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    // 외부 DLL(user32.dll)에서 SetLayeredWindowAttributes 함수를 가져옴. 이 함수는 레이어드 윈도우의 속성을 설정합니다.
    [DllImport("user32.dll")]
    private static extern int SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    // 윈도우 스타일 속성 상수
    const int GWL_EXSTYLE = -20;
    const uint WS_EX_LAYERED = 0x00080000;
    const uint WS_EX_TRANSPARENT = 0x00000020;
    static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    const uint LWA_COLORKEY = 0x00000001;
    private IntPtr _hwnd;

    private int _displayIndex;

    // Start는 스크립트가 활성화될 때 한 번 호출됩니다.
    void Start()
    {
        _hwnd = GetActiveWindow(); // 현재 활성화된 창의 핸들을 가져옵니다.
        // Debug.Log("_hwnd : " + _hwnd);
        
        // 윈도우의 배경을 투명하게 설정
        MARGINS margins = new () {cxLeftWidth = -1 };
        DwmExtendFrameIntoClientArea(_hwnd, ref margins);

        #if !UNITY_EDITOR
        SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0, 0); // 윈도우를 항상 위에 위치시킵니다.
        SetWindowLong(_hwnd, GWL_EXSTYLE, new IntPtr(WS_EX_LAYERED)); // 윈도우를 레이어드 윈도우로 설정합니다.

        // (2, 1, 0) 색상을 투명 처리
        uint transparentColor = (2u) | (1u << 8) | (0u << 16); // RGB를 UINT로 변환
        SetLayeredWindowAttributes(_hwnd, transparentColor, 0, LWA_COLORKEY);
        #endif
        // ShowWindow(_hwnd, 0);
        // ShowWindow(_hwnd, 6);
        // ShowWindow(_hwnd, 1);
        // ShowWindow(_hwnd, 3);
        // ShowWindow(_hwnd, 5);
        // ShowWindow(_hwnd, 9);
        // ShowWindow(_hwnd, 10);
        // HideWindow();
        // ShowWindowManually();
    }

    // 메시지 박스를 표시하는 함수
    public void ShowMessageBox(string message)
    {
        var box = MessageBox(IntPtr.Zero, message, "알림", 0); // 메시지 박스를 띄우고 반환된 값을 로그에 출력
        Debug.Log(box);
    }

    // 창을 지정한 모니터로 이동시키는 함수
    public void MoveToMonitor(int monitorIndex)
    {
        // Display 정보를 담을 리스트 초기화
        List<DisplayInfo> displayInfos = new();
        Screen.GetDisplayLayout(displayInfos); // 현재 시스템의 모니터 레이아웃 정보를 가져옵니다.
        if(displayInfos.Count <= monitorIndex) // 지정한 인덱스가 모니터 수를 넘으면
        {
            monitorIndex = 0; // 기본 모니터(0번)로 설정
        }
        _displayIndex = monitorIndex; // 현재 인덱스를 업데이트
        DisplayInfo displayInfo = displayInfos[monitorIndex]; // 지정한 인덱스의 모니터 정보를 가져옴
        Screen.MoveMainWindowTo(displayInfo, Vector2Int.zero); // 메인 윈도우를 해당 모니터의 (0,0) 위치로 이동
    }

    // 다음 모니터로 창을 이동시키는 함수
    public void NextMonitor()
    {
        MoveToMonitor(_displayIndex + 1); // 현재 인덱스의 다음 모니터로 이동
    }

    // 응용 프로그램을 종료하는 함수
    public void Quit()
    {
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // Unity 에디터에서 실행 중일 경우, 플레이 상태를 중지
        #else
        Application.Quit(); // 빌드된 애플리케이션일 경우, 애플리케이션 종료
        #endif
    }
}
