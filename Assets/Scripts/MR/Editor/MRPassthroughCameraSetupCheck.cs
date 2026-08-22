// Passthrough Camera Access 프로젝트 설정 점검 (Tools → MR → 11)
//
// 왜 별도 도구가 필요한가 — Kickoff Guide §4-56
// --------------------------------------------
// Meta의 Project Setup Tool은 **씬에 PassthroughCameraAccess가 있어야** 검사를 시작한다.
// PassthroughCameraAccessProjectSetup.cs가 이렇게 되어 있다:
//
//     if (Object.FindAnyObjectByType<PassthroughCameraAccess>(FindObjectsInactive.Include) == null)
//         return true;    // ← 컴포넌트가 없으면 "통과"로 처리한다
//
// 즉 설정이 전부 꺼져 있어도 경고가 안 뜬다. "경고가 없다"와 "검사했고 통과했다"는 다른 사실이다.
// 이 도구는 컴포넌트 유무와 무관하게 항상 검사한다.
//
// 왜 Editor 스크립트인가
// --------------------
// OVRProjectConfig는 Oculus.VR.Editor 어셈블리에 있어 런타임 스크립트에서 읽을 수 없다.
// AndroidManifest.xml도 빌드 산출물이 아니라 Assets 아래 소스 파일이라 에디터에서 읽는다.
// requestPassthroughCameraAccessPermissionOnStartup은 OVRManager에서 internal 필드라
// 직접 접근이 안 되므로 SerializedObject로 읽는다(OVRManager.cs:1269).
//
// 이 도구가 하지 않는 것
// -------------------
// 고치지 않는다. 보고만 한다. 수정은 Meta의 Project Setup Tool이 하는 일이고,
// 매니페스트는 Meta → Tools → Update AndroidManifest.xml 이 생성한다
// (OVRManifestPreprocessor.cs:1031 — isPassthroughCameraAccessEnabled가 켜져 있을 때만 쓴다).
// 두 벌의 수정 경로를 만들면 §4-47처럼 서로 어긋난다.
//
// 번호가 10이 아니라 11인 이유: 10은 폐기된 MRSystemMenuBuilder가 쓰던 자리라
// 옛 문서와 헷갈리지 않도록 비워 둔다.

using System.IO;
using System.Text;
using Meta.XR;
using UnityEditor;
using UnityEngine;

namespace AICO.MR.EditorTools
{
    public static class MRPassthroughCameraSetupCheck
    {
        private const string MenuRoot = "Tools/MR/";
        private const string ProjectConfigPath = "Assets/Oculus/OculusProjectConfig.asset";
        private const string ManifestPath = "Assets/Plugins/Android/AndroidManifest.xml";

        // OVRProjectConfig.DeviceType (OVRProjectConfig.cs:42). PCA는 Quest3/3S만 지원한다.
        private static readonly string[] DeviceTypeNames =
        {
            "(0 미사용)", "Quest", "Quest2", "QuestPro", "Quest3", "Quest3S"
        };

        [MenuItem(MenuRoot + "11. Passthrough Camera Access 설정 점검 (4-C)", false, 109)]
        public static void Run()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[MRPCA-설정] Passthrough Camera Access 선행 조건 점검 — 설계서 §3 / §11");
            sb.AppendLine("항목                                          지금 값        필요 값       판정");
            sb.AppendLine("-------------------------------------------------------------------------------");

            int fail = 0;

            fail += CheckProjectConfig(sb);
            fail += CheckManifest(sb);
            fail += CheckOvrManager(sb);
            fail += CheckSceneComponent(sb);

            CheckTargetDevices(sb);   // 경고만 — 판정에 넣지 않는다

            sb.AppendLine("-------------------------------------------------------------------------------");
            if (fail == 0)
            {
                sb.AppendLine("✅ 선행 조건 충족. 이제 Play 해서 MRPassthroughCameraProbe 로그를 볼 것.");
            }
            else
            {
                sb.AppendLine($"❌ {fail}건 미충족. 위의 '할 일'을 순서대로 처리한 뒤 다시 실행할 것.");
                sb.AppendLine("   순서가 중요하다: 컴포넌트 추가 → 설정 켜기 → 매니페스트 재생성 (§4-56).");
            }

            Debug.Log(sb.ToString());
        }

        // -----------------------------------------------------
        // OculusProjectConfig.asset — 타입 참조 없이 SerializedObject로 읽는다
        // -----------------------------------------------------
        private static int CheckProjectConfig(StringBuilder sb)
        {
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(ProjectConfigPath);
            if (asset == null)
            {
                sb.AppendLine($"❌ {ProjectConfigPath} 를 찾을 수 없다. 경로가 바뀌었는지 확인할 것.");
                return 1;
            }

            SerializedObject so = new SerializedObject(asset);
            int fail = 0;

            SerializedProperty pca = so.FindProperty("isPassthroughCameraAccessEnabled");
            if (pca == null)
            {
                sb.AppendLine("⚠ isPassthroughCameraAccessEnabled 프로퍼티가 없다 — SDK 버전이 바뀐 것일 수 있다.");
            }
            else
            {
                bool ok = pca.boolValue;
                sb.AppendLine(Row("isPassthroughCameraAccessEnabled", Bool01(ok), "1", ok));
                if (!ok)
                {
                    sb.AppendLine("     할 일: Edit → Project Settings → Meta XR → " +
                                  "Passthrough Camera Access 체크. 이걸 켜야 매니페스트에 권한이 생긴다.");
                    fail++;
                }
            }

            SerializedProperty pt = so.FindProperty("_insightPassthroughSupport");
            if (pt != null)
            {
                // FeatureSupport: 0 None / 1 Supported / 2 Required
                bool ok = pt.intValue >= 1;
                sb.AppendLine(Row("_insightPassthroughSupport", FeatureSupportName(pt.intValue),
                                  "Supported 이상", ok));
                if (!ok)
                {
                    sb.AppendLine("     할 일: Passthrough Support 를 Supported 이상으로 올릴 것.");
                    fail++;
                }
            }

            return fail;
        }

        // -----------------------------------------------------
        // AndroidManifest.xml — 손으로 편집하지 말고 재생성시킬 것
        // -----------------------------------------------------
        private static int CheckManifest(StringBuilder sb)
        {
            string permission = OVRPermissionsRequester.PassthroughCameraAccessPermission;

            if (!File.Exists(ManifestPath))
            {
                sb.AppendLine(Row("매니페스트 파일", "없음", "존재", false));
                sb.AppendLine("     할 일: Meta → Tools → Android Manifest Tool → " +
                              "'Generate New Store-Compatible AndroidManifest.xml'.");
                return 1;
            }

            bool has = File.ReadAllText(ManifestPath).Contains(permission);
            sb.AppendLine(Row("매니페스트 HEADSET_CAMERA 권한", Present(has), "있음", has));

            if (!has)
            {
                sb.AppendLine("     할 일: isPassthroughCameraAccessEnabled 를 먼저 켠 뒤 " +
                              "Meta → Tools → Android Manifest Tool → " +
                              "'Update AndroidManifest.xml for Store Compatibility'.");
                sb.AppendLine("     (Project Setup Tool의 Required 수정도 같은 일을 한다 — RegenerateAndroidManifest 태그)");
                sb.AppendLine("     확인: 실행 후 파일을 열어 " + permission + " 가 실제로 들어갔는지 눈으로 볼 것 (§4-51).");
                return 1;
            }

            return 0;
        }

        // -----------------------------------------------------
        // OVRManager — internal 필드라 SerializedObject로 읽는다
        // -----------------------------------------------------
        private static int CheckOvrManager(StringBuilder sb)
        {
            OVRManager manager = Object.FindFirstObjectByType<OVRManager>(FindObjectsInactive.Include);
            if (manager == null)
            {
                sb.AppendLine(Row("씬의 OVRManager", "없음", "1개", false));
                return 1;
            }

            SerializedObject so = new SerializedObject(manager);
            int fail = 0;

            SerializedProperty passthrough = so.FindProperty("isInsightPassthroughEnabled");
            if (passthrough != null)
            {
                bool ok = passthrough.boolValue;
                sb.AppendLine(Row("OVRManager.isInsightPassthroughEnabled", Bool01(ok), "1", ok));
                if (!ok)
                {
                    fail++;
                }
            }

            SerializedProperty request = so.FindProperty("requestPassthroughCameraAccessPermissionOnStartup");
            if (request == null)
            {
                sb.AppendLine("⚠ requestPassthroughCameraAccessPermissionOnStartup 프로퍼티가 없다 — SDK 버전 확인.");
                return fail;
            }

            bool granted = request.boolValue;
            sb.AppendLine(Row("OVRManager.request...OnStartup", Bool01(granted), "1 또는 직접 요청", granted));
            if (!granted)
            {
                sb.AppendLine("     위치: OVRManager 인스펙터 → 'Permission Requests On Startup' 폴드아웃 →");
                sb.AppendLine("           Passthrough Camera Access. ('Quest Features'가 아니다 — 거긴 capability 쪽이다)");
                sb.AppendLine("     ⚠ 안 보이면: 인스펙터 맨 위 [ Quest | Editor + Link ] 토글이 'Editor + Link'로 되어 있는 것이다.");
                sb.AppendLine("       DrawPermissionRequestsSection이 _activeTargetPlatform != Quest 이면 통째로 return 한다");
                sb.AppendLine("       (OVRManagerEditor.cs:859). Quest로 바꾸면 나타난다. 빌드 타겟과는 무관하고 EditorPrefs 값이다.");
                sb.AppendLine("     주의: 이 필드는 internal 이라 우리 코드에서 대입할 수 없다(OVRManager.cs:1269).");
                sb.AppendLine("     대안(권장): 끈 채로 두고 Permission.RequestUserPermission 을 필요한 순간에 직접 부른다.");
                sb.AppendLine("       SDK 툴팁도 'It is recommended to manage runtime permissions yourself'라고 적고 있고,");
                sb.AppendLine("       Project Setup Tool에서도 이 항목만 Required가 아니라 Optional이다.");
                sb.AppendLine("       ※ 대안을 택했다면 이 ❌는 무시해도 된다 — 요청 코드가 있는지만 확인할 것.");
                fail++;
            }

            return fail;
        }

        // -----------------------------------------------------
        // 씬의 PassthroughCameraAccess — 이게 있어야 Meta의 설정 툴이 깨어난다 (§4-56)
        // -----------------------------------------------------
        private static int CheckSceneComponent(StringBuilder sb)
        {
            PassthroughCameraAccess[] found = Object.FindObjectsByType<PassthroughCameraAccess>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            bool ok = found.Length > 0;
            sb.AppendLine(Row("씬의 PassthroughCameraAccess", $"{found.Length}개", "1개 이상", ok));

            if (!ok)
            {
                sb.AppendLine("     할 일: Meta → Tools → Building Blocks → 'Passthrough Camera Access' 추가.");
                sb.AppendLine("     결과: 루트에 [BuildingBlock] Passthrough Camera Access 가 생긴다. " +
                              "의존 블록 Camera Rig 는 이미 씬에 있다.");
                sb.AppendLine("     ⚠ 이걸 먼저 해야 Meta Project Setup Tool 이 나머지를 검사한다 (§4-56).");
                return 1;
            }

            foreach (PassthroughCameraAccess p in found)
            {
                string state = "정상";
                if (!p.gameObject.activeInHierarchy)
                {
                    state = "⚠ GameObject 비활성";
                }
                else if (!p.enabled)
                {
                    state = "⚠ 컴포넌트 disabled";
                }

                sb.AppendLine($"     · {p.gameObject.name} | 눈={p.CameraPosition}" +
                              $" | 요청해상도 {p.RequestedResolution.x}x{p.RequestedResolution.y} | {state}");
            }

            return 0;
        }

        // -----------------------------------------------------
        // targetDeviceTypes — PCA 미지원 기기가 섞여 있으면 IsSupported 분기가 필요하다 (리스크 #1)
        // -----------------------------------------------------
        private static void CheckTargetDevices(StringBuilder sb)
        {
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(ProjectConfigPath);
            if (asset == null)
            {
                return;
            }

            SerializedProperty devices = new SerializedObject(asset).FindProperty("targetDeviceTypes");
            if (devices == null || !devices.isArray)
            {
                return;
            }

            StringBuilder names = new StringBuilder();
            bool hasUnsupported = false;

            for (int i = 0; i < devices.arraySize; i++)
            {
                int v = devices.GetArrayElementAtIndex(i).intValue;

                string name = $"({v})";
                if (v >= 0 && v < DeviceTypeNames.Length)
                {
                    name = DeviceTypeNames[v];
                }

                if (i > 0)
                {
                    names.Append(", ");
                }
                names.Append(name);

                // Quest3(4) / Quest3S(5) 외에는 PCA를 지원하지 않는다.
                if (v != 4 && v != 5)
                {
                    hasUnsupported = true;
                }
            }

            // 판정 카운트에 넣지 않으므로 ❌가 아니라 ⚠로 표시한다.
            // 설정 오류가 아니라 "코드에서 분기하라"는 항목이라 ❌로 찍으면 할 일로 오독된다.
            if (hasUnsupported)
            {
                sb.AppendLine($"⚠ {"targetDeviceTypes",-42} {names,-14} {"코드에서 분기",-14}");
            }
            else
            {
                sb.AppendLine(Row("targetDeviceTypes", names.ToString(), "Quest3/Quest3S", true));
            }

            if (hasUnsupported)
            {
                sb.AppendLine("     ⚠ PCA 미지원 기기가 대상에 포함돼 있다. 기기를 빼는 게 아니라 " +
                              "코드에서 PassthroughCameraAccess.IsSupported 로 분기하고 " +
                              "기능 자체를 숨길 것 (설계서 §9 리스크 #1).");
            }
        }

        // -----------------------------------------------------
        // 표 형식 헬퍼
        // -----------------------------------------------------
        private static string Row(string item, string now, string need, bool ok)
        {
            string mark = "❌";
            if (ok)
            {
                mark = "✅";
            }
            return $"{mark} {item,-42} {now,-14} {need,-14}";
        }

        private static string Bool01(bool v)
        {
            if (v)
            {
                return "1";
            }
            return "0";
        }

        private static string Present(bool v)
        {
            if (v)
            {
                return "있음";
            }
            return "없음";
        }

        private static string FeatureSupportName(int v)
        {
            if (v == 0)
            {
                return "None";
            }
            if (v == 1)
            {
                return "Supported";
            }
            if (v == 2)
            {
                return "Required";
            }
            return $"({v})";
        }
    }
}
