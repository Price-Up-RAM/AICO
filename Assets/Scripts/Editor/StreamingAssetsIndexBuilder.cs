using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;


public class StreamingAssetsIndexBuilder : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        GenerateIndex();
    }

    [MenuItem("Tools/MR/Generate StreamingAssets Index")]
    public static void GenerateIndexMenu()
    {
        GenerateIndex();
        AssetDatabase.Refresh();
    }

    private static void GenerateIndex()
    {
        string streamingAssetsPath = Application.streamingAssetsPath;
        if (!Directory.Exists(streamingAssetsPath))
        {
            Debug.LogWarning("[StreamingAssetsIndexBuilder] StreamingAssets folder does not exist.");
            return;
        }

        string indexPath = Path.Combine(streamingAssetsPath, "_index.json");
        StreamingAssetsIndexData indexData = new StreamingAssetsIndexData
        {
            version = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
        };

        // 1차 구현 복사 대상 폴더 (core)
        // whisper, Sound 등 대용량 데이터는 현재 복사 대상에서 제외
        string[] targetFolders = { "Config", "prompt" }; 
        
        foreach (string folder in targetFolders)
        {
            string folderPath = Path.Combine(streamingAssetsPath, folder);
            if (Directory.Exists(folderPath))
            {
                // 하위 디렉터리 포함 모든 파일 검색
                string[] files = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories);
                foreach (string file in files)
                {
                    // 메타 파일 제외
                    if (file.EndsWith(".meta")) continue;

                    // StreamingAssets 폴더 기준 상대 경로로 변환하고 슬래시로 통일
                    string relativePath = file.Substring(streamingAssetsPath.Length + 1).Replace("\\", "/");
                    indexData.files.Add(relativePath);
                }
            }
        }

        string json = JsonUtility.ToJson(indexData, true);
        File.WriteAllText(indexPath, json);

        Debug.Log($"[StreamingAssetsIndexBuilder] _index.json generated with {indexData.files.Count} files. Version: {indexData.version}");
    }
}
