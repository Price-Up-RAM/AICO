using UnityEditor;
using UnityEngine;
using System.IO;

public class ForceServerSettings
{
    [MenuItem("Tools/MR/강제로 arona654 서버 설정하기")]
    public static void ForceSet()
    {
        string dir = Path.Combine(Application.persistentDataPath, "config");
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        
        string file = Path.Combine(dir, "settings.json");
        
        if (File.Exists(file))
        {
            string json = File.ReadAllText(file);
            // 아주 단순무식하게 텍스트 교체 (temp -> arona654, server_type_idx 강제 10)
            json = System.Text.RegularExpressions.Regex.Replace(json, "\"server_id\":\\s*\".*?\"", "\"server_id\": \"arona614sd\"");
            json = System.Text.RegularExpressions.Regex.Replace(json, "\"server_type_idx\":\\s*\\d+", "\"server_type_idx\": 10");
            File.WriteAllText(file, json);
            Debug.Log("[MR] settings.json 파일을 arona654 (Type 10)로 강제 덮어씌웠습니다! 이제 플레이 해보세요.");
        }
        else
        {
            Debug.LogError("[MR] settings.json 파일이 아직 없습니다. 게임을 한 번 실행했다가 끈 뒤에 다시 눌러주세요.");
        }
    }
}
