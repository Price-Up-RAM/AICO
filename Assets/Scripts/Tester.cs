using System;
using System.Collections;
using System.Collections.Generic;
using Assistant;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityWeld.Binding;
using System.Diagnostics;
using System.IO;

public class Tester : MonoBehaviour 
{
    // Python 프로그램을 실행하고 로그를 Unity에서 출력
    public void StartPythonProcess(string serverType, string language)
    {
        string streamingAssetsPath = Application.streamingAssetsPath;
        string serverExePath = Path.Combine(streamingAssetsPath, "arguments.exe");

        // ProcessStartInfo 설정
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = serverExePath,
            Arguments = $"{serverType} {language}", // 변수 전달
            UseShellExecute = false,
            RedirectStandardOutput = true, // 표준 출력을 리다이렉트
            RedirectStandardError = true,  // 표준 에러를 리다이렉트
            CreateNoWindow = true // 콘솔 창 숨김
        };

        try
        {
            // 프로세스 실행
            Process process = new Process { StartInfo = startInfo };
            process.Start();

            // 표준 출력 및 에러 읽기
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            process.WaitForExit();

            // Unity 로그로 출력
            UnityEngine.Debug.Log("Python Output: " + output);
            if (!string.IsNullOrEmpty(error))
            {
                UnityEngine.Debug.LogError("Python Error: " + error);
            }
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError("Error running Python script: " + ex.Message);
        }
    }

    // 테스트용 함수
    void Start()
    {
        // 실제 변수 전달 예시
        string serverType = "cpu"; // 전달할 서버 타입 (예: "cpu" 또는 "gpu")
        string language = "en";    // 전달할 언어 (예: "ko", "en", "jp")
        
        UnityEngine.Debug.Log("StartPythonProcess Start");
        StartPythonProcess(serverType, language);
        UnityEngine.Debug.Log("StartPythonProcess Start");
    }
}
