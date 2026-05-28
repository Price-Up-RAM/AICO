#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

public class DLCMenuTools
{
    [MenuItem("DLC/Clear All Caches (강제 초기화)")]
    public static void ClearAllCaches()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("[DLC] 에디터 Play 모드 실행 중에는 캐시가 파일 사용 중이라 삭제에 실패합니다! Play 모드를 끄고 다시 실행해 주세요.");
            return;
        }

        // 1. 유니티 AssetBundle 캐시 삭제 (데이터가 실제로 있는지 체크)
        long totalSize = 0;
        var cachePaths = new System.Collections.Generic.List<string>();
        Caching.GetAllCachePaths(cachePaths);
        
        foreach (string path in cachePaths)
        {
            Cache cache = Caching.GetCacheByPath(path);
            if (cache.valid)
            {
                totalSize += cache.spaceOccupied;
            }
        }

        if (totalSize > 0)
        {
            bool success = Caching.ClearCache();
            if (success)
                Debug.Log($"[DLC] Unity AssetBundle 캐시 완전 삭제 완료 (✓) (삭제된 용량: {totalSize / 1024.0 / 1024.0:F2} MB)");
            else
                Debug.LogWarning("[DLC] Unity AssetBundle 캐시 삭제 실패. 권한 문제나 사용 중인 파일이 있을 수 있습니다.");
        }
        else
        {
            // 혹시 모르니 빈 상태라도 한 번 Clear 호출
            Caching.ClearCache(); 
            Debug.Log("[DLC] Unity AssetBundle 캐시가 비어있습니다. (삭제할 내용 없음)");
        }

        // 2. Addressables 런타임 캐시 폴더 삭제 (카탈로그 해시 등)
        string catalogPath = Application.persistentDataPath + "/com.unity.addressables";
        if (Directory.Exists(catalogPath))
        {
            Directory.Delete(catalogPath, true);
            Debug.Log($"[DLC] Addressables 로컬 카탈로그/해시 정보 무력화 완료 (✓) : {catalogPath}");
        }
        else
        {
            // 이 로그가 매번 거슬린다면 지워도 무방합니다.
            Debug.Log("[DLC] Addressables 원격 카탈로그 캐시 폴더가 없습니다. (이미 비워짐 혹은 사용되지 않음)");
        }
        
        Debug.Log("[DLC] 🚀 [캐시 정리 완료] 이제 다시 Play 모드에 들어가서 다운로드 과정을 테스트할 수 있습니다.");
    }
}
#endif
