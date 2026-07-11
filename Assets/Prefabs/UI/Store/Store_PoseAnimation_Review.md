# 포즈 상품 검토서 — 애니메이션 제어 & 스냅샷 아이콘

> 목적: 상점 "포즈" 탭 상품(`pose_greeting` / `pose_dance` / `pose_sit`, `Editor/StoreTools.cs:68,140`)은
> 현재 "아이템 소유"까지만 구현되어 있다. 이 문서는 상품 완성에 필요한 두 축 —
> **1장: 구매한 포즈를 캐릭터에서 실제 재생/장착하는 방법**, **2장: 포즈 아이콘을 자동 생성하는 방법** —
> 을 코드베이스 조사 결과에 근거해 검토한다. 조사에서 확인되지 않은 사항은 "검증 필요"로 명시한다.

---

## 1장. 블렌드 애니메이션 판매/제어 검토

### 1.1 현황 (발견한 사실)

**컨트롤러 구조**
- 아로나 POC 프리팹(`Assets/Prefabs/Char_toon/arona_6_clean_POC.prefab:15874`)의 `m_Controller`는
  `Assets/Animation/mixamo/Blend_Animation_Controller.controller`로 해석된다(guid를 .meta로 확인).
- 이 컨트롤러의 파라미터(컨트롤러 YAML 1042~1146행): bool `isWalk/isRun/isPick/isDance/isPat/isListen`,
  trigger `doRandomMotion1~4/doSelect/doBlendStand/doHide/doShow`, float `BlendStand/BlendIdle/BlendPick/BlendDance`.
  레이어는 'Base Layer' 단일.
- 상태: `idle`(825행, BlendTree on BlendIdle 0~4), `Stand Blend`(1485행), `Dance`(1309행 →
  509행 BlendTree, **threshold 0~30 = 댄스 클립 31개**), `Pick`(1431행), `Walk`(1710행), `Pat`(1013행),
  `Listen`(1609행), `Hide`(959행), `Show`(1636행), `doRandomMotion1~4`, `doSelect`. 클립 참조 총 59개.
- **컨트롤러 파편화**: `BlendDance`가 존재하는 컨트롤러는 4개뿐(Blend_Animation_Controller와
  `_Mika`/`_Plana`/`_Diana` 변형). `Animation/Aris_Animation_Controller.controller`는
  isDance/BlendDance가 없고, `Animation/Humanoid/Arona_Animation_Controller.controller`는
  `doBlend`/`Blend` float 방식, Char2D 계열은 Aris 유사. **포즈 시스템은 "공유 애니메이터 파라미터"를
  가정할 수 없다.**

**런타임 구동 경로**
- 댄스 구동: `Assets/Scripts/AnimationManager.cs:41-61` `Dance()` =
  `_animator.Play("idle",0,0)` → `SetFloat("BlendDance", Random.Range(0,31))` + `SetBool("isDance",true)`.
  클립 수 31이 `AnimationManager.cs:19`에 **하드코딩**되어 BlendTree threshold(0~30)와 수동 동기화 상태.
- Idle/Talk/Listen/Hide/Show 등도 `AnimationManager.cs:21-176`에서 SetBool/SetTrigger 이름 문자열 방식.
  Animator는 매 호출 `CharManager.Instance.GetCurrentCharacter().GetComponent<Animator>()`로 획득
  (`CharManager.cs:338`).
- 에이전트 액션 경로: `ApiAgentFunctionManager.cs:453-465` (`character_dance`/`character_walk_left/right`)
  → `ApiAgentFunctionAction.cs:20-35` → `AnimationManager.Instance.Dance()` /
  `PhysicsManager.SetWalkLeft/RightState()` (`PhysicsManager.cs:132-179`).
- 기타 콜사이트: `ClickHandler.cs:64-75,94,215`, `DragHandler.cs:131-188`, `SubClickHandler.cs:65-148`,
  `PortraitClickHandler.cs:136` — 전부 이름 문자열 SetFloat/SetBool/SetTrigger 패턴.
- 2d_general 블렌드 랜덤화: `AnimationBlendController.cs:24-25,93` — 상태별 클립 수를 JSON에서 주입
  (`CharManager.cs:727-733` `InjectBlendCounts`).

**포즈 시스템 설계에 결정적인 제약/자산**
- **컨트롤러는 런타임에 교체된다**: 의상/스킨 변경 시 `CharManager.cs:701-725`가
  `animator.runtimeAnimatorController`를 재할당(`ChangeCharManager.cs:161`, Addressables 경유
  `CharManager.cs:572,717`, 그 외 `ChangeCharCardController.cs:273` 등). 컨트롤러 위에 얹는 어떤
  메커니즘이든 이 교체를 견뎌야 한다.
- **Playables 선례가 이미 코드베이스에 있다**: `Assets/Scripts/animationplayermanager.cs` —
  PlayableGraph + AnimationClipPlayable로 랜덤 포즈를 프리즈(`EnsureGraph` 318-339행,
  `ApplyRandomPoseAndFreeze` 352-371행, `ReleasePlayer` 383-402행 = 그래프 파괴로 컨트롤러 복귀).
  `HasState(0, StringToHash(name))` 프로빙(515행)과 컨트롤러 교체 감지(GetInstanceID, 260-282행)도 시연됨.
- `AnimatorOverrideController`는 프로젝트 전체에서 **사용처 0** (grep 결과 RuntimeAnimatorController 로드만 존재).
- 캐릭터별 영속 설정 저장소: `SettingCharManager.cs` — `persistentDataPath/config/settings_char.json`
  (115행), `CharCodeSetting`(57-62행: char_size/affection/voiceId)이 char_code 키로 저장됨 →
  `equipped_pose` 문자열 필드를 넣기에 자연스러운 위치.
- 상점 측 준비 상태: 포즈 키 3종은 이미 StoreCatalog/InventoryCatalog에 등록됨(`StoreTools.cs:68,140`).
  StoreCatalog(`Scripts/StoreCatalog.cs`)와 EquipCatalog(`Prefabs/Assist/EquipSystem/Scripts/EquipCatalog.cs:30-113`)는
  동일 패턴(SO + List + lazy Dictionary + OnValidate) — 포즈용 카탈로그도 같은 틀로 복제 가능.

### 1.2 접근안 비교표

| 방식 | 개요 | 장점 | 단점 |
|---|---|---|---|
| (a) AnimatorOverrideController + placeholder 상태 | 각 컨트롤러에 'Pose' 상태(placeholder 클립) + doPose 트리거를 1회 추가, 장착 시 컨트롤러를 Override로 감싸 클립 교체 후 트리거 | 이후 새 포즈는 컨트롤러 수정 불필요, idle 복귀 트랜지션이 컨트롤러에 authored, 기존 트리거 호출 스타일과 일치 | "1회 추가"가 실제로는 파라미터 세트가 제각각인 **~55개 컨트롤러 YAML 편집**; CharManager의 컨트롤러 재할당(701-725행)마다 래퍼가 파괴되어 재구축 필요; GetInstanceID 변화가 animationplayermanager 캐시(260-282행)를 교란; 프로젝트 내 선례 0 |
| (b) BlendTree float 확장 (BlendDance 패턴) | 'Pose' 상태에 1D BlendTree로 전 포즈 클립을 패킹, `SetFloat("BlendPose", index)`로 구동 — Dance 흐름과 동일 | 가장 관용적(검증된 Dance 흐름과 동일), 런타임 코드 최소, 랜덤화 인프라(AnimationBlendController) 존재 | **새 포즈마다 공유 .controller 편집**(상점이 피하려는 아이템별 저작 비용); index↔카탈로그 key 동기화 취약(Dance도 31 하드코딩, AnimationManager.cs:19); 컨트롤러 변형 4개+ 전부에 클립 참조 증식; 비Blend 컨트롤러 캐릭터는 조용히 무동작 |
| (c) CrossFade/Play로 포즈별 상태 | 포즈마다 AnimatorState를 저작, `HasState` 가드 후 `CrossFade(key, 0.2f)` | 트랜지션/속도 저작 가능, HasState로 우아한 성능 저하, 파라미터 가정 없음 | 확장성 최악: 포즈 x 컨트롤러 패밀리마다 편집; **상태 추가는 Editor 전용 API라 런타임/DLC 추가 불가**; 상점 상품이 애니메이션 그래프 저작에 종속 |
| (d) **Playables one-shot (컨트롤러 편집 0)** | PlayableGraph + AnimationPlayableOutput을 Animator 위에 만들어 카탈로그의 AnimationClip을 weight 1로 재생(컨트롤러 마스킹), 종료 시 그래프 파괴로 컨트롤러 복귀 — animationplayermanager의 그래프 라이프사이클(318-339, 383-402행) 재사용 | **컨트롤러 편집이 영원히 0**: 새 포즈 = 클립 1개 + 카탈로그 엔트리 1개; ~55개 컨트롤러 전부에 균일 동작 + 런타임 컨트롤러 교체 생존; 검증된 코드가 이미 존재; 추후 Addressables 배포도 가능 | idle로의 블렌드 복귀가 공짜가 아님(부드럽게 하려면 AnimationMixerPlayable weight 페이드 필요); AnimationPlayerManager(밈 모드)와 **그래프 소유권 조율 필수**(한 Animator에 그래프 2개 금지); 릭 호환은 캐릭터 타입별(mixamo 휴머노이드 클립은 Char2D 스프라이트에 미동작 → rigType 플래그/스킵 필요) |

### 1.3 권장안: (d) Playables one-shot — EquipSystem을 미러링한 PoseSystem

**이유**
1. **아이템별 저작 비용 제거**: (b)/(c)는 새 상점 포즈마다 4개+ 컨트롤러 변형의 YAML 수동 편집이
   필요하다. 상점은 "상품 추가 = 데이터 추가"여야 하는데, 이를 만족하는 것은 (d)뿐이다.
2. **런타임 컨트롤러 교체 생존**: (a)의 Override 래퍼는 `CharManager.cs:701-725`의 재할당마다 파괴되고
   instance-ID 기반 캐시를 교란한다. (d)의 그래프는 컨트롤러 "위"에 앉으므로 교체와 독립적이다.
3. **검증된 코드 재사용**: 그래프 생성/클립 재생/그래프 파괴(컨트롤러 복귀)의 전 과정이
   `animationplayermanager.cs:318-402`에 이미 출하되어 있다.
4. 블렌드 복귀가 프로토타입에서 문제로 확인되면, 단일 AnimationClipPlayable을 2-input
   AnimationMixerPlayable(컨트롤러 레이어 + 클립)로 승급해 weight 페이드 — 여전히 컨트롤러 편집 0.

**검증 필요(불확실)**: ① 스냅 복귀의 체감 품질(프로토타입에서 판단), ② Char2D 캐릭터에서의 스킵 처리
(mixamo 휴머노이드 클립은 스프라이트 릭에 미동작 — 사실로 확인됐고, 대응은 rigType 플래그 설계 몫),
③ AnimationPlayerManager와의 소유권 규약(공유 owner 체크 또는 `ForceReset()` 경유) 세부.

### 1.4 상점과의 결합 방식 (문자열 key 규약, 약결합 유지)

- **key 규약 `pose_*` 유지**: 상점/인벤토리/포즈 카탈로그가 같은 문자열 key 공간을 공유한다
  (기존 InventoryCatalog/EquipCatalog/StoreCatalog 규약 그대로, keys는 `StoreTools.cs:68`에 이미 존재).
- **의존 방향 단방향**: Store(또는 인벤토리 "사용/장착" 핸들러) → `PoseManager.Instance.EquipPose(charCode, key)`.
  key가 `pose_`로 시작할 때만 위임하고, PoseSystem은 Store를 모른다. Store 코드에 애니메이션 지식 0.
- 장착 상태는 `SettingCharManager.CharCodeSetting`(57-62행)에 `equipped_pose` 문자열로 캐릭터별 저장 —
  char_size/voiceId와 같은 저장 방식이라 컨트롤러 종류와 무관하게 전 캐릭터에 동작.
- (선택) 에이전트 함수 `character_pose`를 `character_dance` 옆(`ApiAgentFunctionManager.cs:453`)에
  추가하면 AI가 장착 포즈를 발동할 수 있다 — 후속.

### 1.5 구현 시 작업 목록

1. **PoseCatalog SO** — `Assets/Prefabs/Assist/PoseSystem/Scripts/PoseCatalog.cs`,
   `EquipCatalog.cs` 준-복제(key `pose_*` → AnimationClip + rigType 플래그 + loop/duration).
   에셋은 Resources에 베이크(EquipCatalog/StoreCatalog와 동일하게 문자열 key만으로 해석).
2. **PoseManager** — 싱글톤 MonoBehaviour(AnimationManager 스타일).
   `EquipPose(charCode, poseKey)` / `PlayEquipped(target=null)` / `StopPose()`.
   재생은 `animationplayermanager.cs:318-402`의 그래프 생성→클립 재생→그래프 파괴 코드를 이식,
   클립 종료 시 그래프 파괴로 컨트롤러 idle 복귀. 재생 전 AnimationPlayerManager와의
   이중 그래프 방지(ForceReset 또는 공유 소유권 체크).
3. **영속화** — `SettingCharManager.CharCodeSetting`에 `equipped_pose` 추가(settings_char.json).
4. **상점 결합** — 인벤토리/상점의 사용·장착 경로에서 `pose_` 접두사 키만 PoseManager로 위임(단방향).
5. **검증 스파이크** — 아로나(3D 휴머노이드) 1종으로 재생/복귀 확인 → 필요 시 MixerPlayable 페이드 승급
   → Char2D 스킵 동작 확인.
6. (선택) 에이전트 함수 `character_pose` 추가.

---

## 2장. 포즈 스냅샷 아이콘 검토

### 2.1 현황 (발견한 사실)

**대상/렌더링 환경**
- 아로나 POC(`Assets/Prefabs/Char_toon/arona_6_clean_POC.prefab`, 550,687바이트 YAML):
  SkinnedMeshRenderer 33 + MeshRenderer 7 + Animator 1(휴머노이드 아바타). 다수 SMR은 대체 의상
  (chipao/idol/pareo/sister/sportswear/swimsuit).
- 셰이더는 **Toony Colors Pro가 아니라 com.unity.toonshader(Unity Toon Shader) 0.14.1-preview**
  (`Packages/manifest.json:20`) — POC 렌더러가 참조하는 머티리얼 guid는 39개(유니크)로, 그중 33개가
  `Assets/Char/arona_sfm_fbx_ai/ToonShader/*.mat`, 나머지는 얼굴 등 별도 폴더(예:
  `Assets/Char/arona_face/ToonShader/Arona_Original_FaceEmo.mat`) — 모두 UnityToon 계열.
  파이프라인 URP 17.3.0 (`manifest.json:17`).

**재사용 가능한 기존 코드 (파이프라인 전 단계가 이미 존재)**
- 캐릭터 인스턴스화 + 카메라 프레이밍: `InventorySystemTools.cs:219-241`
  (InstantiatePrefab + UnpackPrefabInstance + StripAppComponents + FrameCamera).
  `FrameCamera`(329-371행)는 Renderer 병합 bounds로 -Z 카메라 배치(dist = radius/sin(fov/2)*1.15).
- 컴포넌트 스트립: `InventorySystemTools.cs:374-415` — EquipSocket/EquipMarker 외 전 MonoBehaviour를
  최대 4패스로 제거. POC 위 실제 MonoBehaviour: MagicaCloth(+CapsuleCollider), DragHandler,
  ClickHandler, FallingObject, CharAttributes, AnimationController, menutrigger, WheelHandler,
  EmotionFaceAronaNewController, EquipSocket. **Animator는 MonoBehaviour가 아니라 스트립 후에도 남아**
  휴머노이드 포즈 샘플링이 가능. 단 MagicaCloth 제거로 머리카락/치마는 바인드 포즈.
- 포즈 프리즈: `animationplayermanager.cs:352-371` `ApplyRandomPoseAndFreeze` —
  PlayableGraph + AnimationClipPlayable, `SetSpeed(0)` → `SetTime(t)` → `graph.Evaluate(0f)`.
  PlayableGraph 평가는 에디트 모드에서도 동작하므로 에디터 베이크에 그대로 재사용 가능.
- 스냅샷 배관: RT+ReadPixels 리사이즈(`FaceTextureChanger.cs:120-128`), ReadPixels+EncodeToPNG
  (`ApiAgentFunctionScreenshotAction.cs:35-38`), **PNG → WriteAllBytes → ImportAsset →
  TextureImporter(Sprite, alphaIsTransparency, no mips) → Sprite 로드 전체 레시피**
  (`Prefabs/UI/Jukebox/JukeboxView/Editor/JukeboxDownloadButtonInject.cs:165-199`).
  PreviewRenderUtility/AssetPreview 사용처는 프로젝트 코드에 0.
- 오프스크린 렌더 자원: `Assets/Scripts/PortraitCamera.cs`(RT 할당 리그, 레이어 로직은 없음) +
  TagManager 레이어 6 **'PortraitModel'**(코드 참조 0곳 — 아이콘 베이크 격리용으로 자유롭게 사용 가능.
  단 씬/프리팹의 컬링마스크 사용 여부는 베이크 구현 시 확인).
- 카탈로그 슬롯: `InventoryCatalog.cs:11` `public Sprite icon`(null이면 이름 텍스트 폴백,
  `InventorySlotView.cs:82-100`). `InventorySystemTools.CreateCatalog`(70-90행)가 SerializedObject로
  guid 기반 아이콘 배정을 이미 수행 — 베이커가 쓸 배정 메커니즘 그대로.

**소재/용량/배치 제약**
- 포즈 전용 클립은 아직 없지만, POC의 컨트롤러에 연결된 mixamo 휴머노이드 클립 40+개
  (Dance_loop 31, Stand_loop 5, Stand 8 등; muscle 커브 확인: `Animation/mixamo/Stand_loop/Standard Idle.anim`)의
  프레임에서 오늘 바로 베이크 가능.
- 기존 수제 아이콘 기준: `Assets/Model/Sprite/*.png` 256x256 RGBA, 상세 아이콘 ~35-55KB.
- 용량 산정: 출하 용량은 PNG가 아니라 텍스처 압축이 결정 — 256² DXT5/BC7 no mips = 64KB/개
  (10개 ≈ 0.64MB), 128² = 16KB/개(10개 ≈ 0.16MB). **수제 아트도 같은 해상도면 같은 바이트** —
  베이크의 이득은 용량이 아니라 노동력/일관성/재베이크 가능성.
- batchmode 호환: 프로젝트 표준 배치 커맨드(InventorySystem WORKLOG.md:91)는 `-nographics`를 넘기지
  않으므로 batchmode에도 GPU 디바이스가 있어 Camera.Render→RT→ReadPixels→EncodeToPNG 전부 동작.
  InstantiatePrefab/ImportAsset도 같은 파일이 배치에서 이미 사용 중(`InventorySystemTools.cs:33-42`).

### 2.2 접근안 비교표

| 방식 | 개요 | 장점 | 단점 |
|---|---|---|---|
| (a) **에디터 베이크** (Tools 메뉴 → PNG → Sprite → 카탈로그) | 임시 씬(또는 PortraitModel 레이어)에 POC 인스턴스화 → 스트립(Animator 유지) → Playables 포즈 프리즈 → FrameCamera(알파 0 SolidColor) → 256² ARGB32 RT 렌더 → PNG 저장 → Sprite 임포트 → 카탈로그 배정 | **전 단계가 이 프로젝트에서 검증된 코드로 존재**; batchmode 동작; 의상/포즈 변경 시 원클릭 재베이크; 런타임 비용 0; UI는 이미 Sprite 표시 준비됨; 스타일 일관성 결정적 | 10개 기준 ~0.16-0.64MB 텍스처 출하(수제와 동일 — 용량 이득 없음); **toon 셰이더 불투명 패스의 알파 채널은 첫 베이크에서 검증 필요**(보장 안 되면 2-pass 매트/알파 후보정); MagicaCloth 머리카락 바인드 포즈; 라이팅 리그 결정 필요(디렉셔널 1개로 충분할 가능성, InventorySystemTools.cs:213 참조) |
| (b) 런타임 RT 라이브 프리뷰 | PortraitCamera 패턴 확장: PortraitModel 레이어의 숨김 리그가 포즈 프리즈 후 RT로 렌더 → RawImage | 출하 바이트 0; 현재 의상/MagicaCloth 상태 실시간 반영 | 10슬롯 그리드에 RT 10개(256²에서 VRAM ~2.5MB) 또는 사실상 (a)의 런타임판인 캡처 스킴; 메인 씬에 상시 리그(정적 프리팹 UI 방법론 위배); 복제 캐릭터는 33-SMR 스킨드 메시 메모리 2배; batchmode 검증 불가(Play 모드 전용 = 사용자 몫) |
| (c) AssetPreview / PreviewRenderUtility | `AssetPreview.GetAssetPreview` 또는 PreviewRenderUtility + 수동 포즈 샘플링 | AssetPreview는 한 줄; PreviewRenderUtility는 격리 씬 + 카메라/라이트 제어 | AssetPreview: 128² 고정·바인드 포즈만·투명 배경 없음·batchmode 캐시 불안정 → **포즈 아이콘에 사용 불가**. PreviewRenderUtility: 프로젝트 내 선례 0, URP+toonshader 재현 미검증, 프레이밍/스트립/포즈 코드는 어차피 (a)와 동일하게 작성해야 해서 절감 0 |
| (d) 수제 스프라이트 (현상 유지) | `Assets/Model/Sprite/` 방식으로 256² PNG 수작업 + guid 배정 | 신규 코드 0; 아트 디렉션 완전 제어; 파이프라인 이미 동작 | 출하 바이트는 베이크와 동일(용량 이점 없음); 포즈/의상 변경마다 수작업; 작가/배치 간 스타일 표류; 포즈 잡은 3D 툰 캐릭터를 손으로 일관되게 그리기 자체가 어려움 |

### 2.3 권장안: (a) 에디터 베이크 (Tools/Store 메뉴 + batch 진입점)

**이유** — 근거가 이례적으로 강하다: 파이프라인의 모든 단계가 이 저장소에 동작하는 코드로 존재한다
(인스턴스화/스트립/프레이밍 = InventorySystemTools.cs 219-241·329-371·374-415, 포즈 프리즈 =
animationplayermanager.cs 352-371, RT→PNG = FaceTextureChanger/ApiAgentFunctionScreenshotAction,
PNG→Sprite 임포트 = JukeboxDownloadButtonInject.cs 188-199, 카탈로그 배정 = CreateCatalog 70-90).
소재도 mixamo 클립 40+개가 POC 컨트롤러에 이미 연결되어 있어 신규 클립이 선결 조건이 아니다.
batchmode도 표준 커맨드가 -nographics를 생략하므로 안전하다.

**기대치 조정**: 베이크는 **용량을 아끼지 않는다**(256² 아이콘은 수제와 같은 ~64KB/개).
이득은 노동력 제거·완벽한 일관성·재베이크 가능성이다. 바이트가 중요하면 128²(10개 ≈ 0.16MB)로 베이크.

**첫 베이크에서 검증 필요(불확실) — 단일 아이콘 스파이크로 확인**
1. **알파 채널**: ARGB32 RT를 (0,0,0,0)으로 클리어하고 Unity Toon Shader 불투명 패스가 캐릭터 픽셀을
   alpha=1로 남기는지 검사(URP 불투명 셰이더는 보장하지 않음). 실패 시 흰/검 2-pass 매트 또는 알파
   후보정. 베이크 카메라에서 URP 포스트프로세싱은 OFF.
2. **MagicaCloth**: 스트립 후 머리카락/치마가 바인드 포즈로 렌더됨 — 스틸에는 수용 가능하나
   클리핑 여부 확인.

(b)는 "아이콘이 현재 의상을 반영해야 한다"가 요구사항이 되기 전까지 보류, (c)는 배제
(AssetPreview는 포즈 제어 불가, PreviewRenderUtility는 코드 절감 0에 URP/toon 불확실성만 추가).

### 2.4 상점과의 결합 방식 (문자열 key 규약, 약결합 유지)

- 산출물은 `pose_<key>.png` → Sprite로 임포트되어 **InventoryCatalog의 `icon` 필드에 배정**
  (`InventoryCatalog.cs:11`). Store는 이미 `Catalog.Get(key)`의 icon/displayName을 우선 표시하므로
  **상점 코드 변경 0** — 아이콘이 채워지는 순간 카드/슬롯에 자동 반영(없으면 기존 이름 텍스트 폴백).
- 베이크 대상 목록은 `pose_*` key 목록에서 도출(카탈로그 데이터 주도) — 새 포즈 상품 추가 시
  카탈로그 등록 + 재베이크만으로 완결.
- 베이커는 Editor 폴더의 Tools 메뉴 + batch 진입점으로만 존재 — 런타임 시스템과 상호 참조 없음.

### 2.5 구현 시 작업 목록

1. **단일 아이콘 스파이크** — 클립 1개·프레임 1개로 알파 채널/MagicaCloth 클리핑/라이팅을 먼저 검증.
2. **베이크 툴** — `Tools/Store/Bake Pose Icons` 메뉴 + batch 진입점(프로젝트 관례).
   파이프라인: 임시 씬(EditorSceneManager.NewScene) 또는 PortraitModel 레이어 → POC InstantiatePrefab
   → Unpack → StripAppComponents(화이트리스트 패턴 복제, Animator 유지) → 엔트리별 클립+시각으로
   Playables 프리즈 → FrameCamera(SolidColor, 알파 0) → 256² ARGB32 RT 렌더 → ReadPixels →
   EncodeToPNG → `Icons/pose_<key>.png` 저장 → TextureImporter Sprite 임포트 → SerializedObject로
   카탈로그 icon 배정.
3. **포즈-클립 매핑 데이터** — 포즈 key별 (클립, 샘플 시각, 카메라 보정) 정의(1장의 PoseCatalog에
   합류시키면 아이콘과 재생이 같은 소스를 공유).
4. **해상도 확정** — 기본 256²(개당 64KB), 용량 우선이면 128².
5. WORKLOG/디자인 문서에 베이크 절차와 재베이크 시점(의상/포즈 변경 시) 기록.

---

## 결론

포즈 상품의 완성 경로는 **① 아이콘 베이크 → ② 구매/장착 → ③ 애니메이션 제어**로 이어진다:
에디터 베이크 툴(2장 (a))이 `pose_*` 아이콘을 InventoryCatalog에 채우면 상점 카드가 즉시 상품답게
보이고, 구매는 기존 Store 흐름 그대로 소유를 만들며, 장착/재생은 Playables one-shot 기반
PoseSystem(1장 (d))이 컨트롤러 편집 0으로 담당한다. 두 축 모두 이 저장소에서 이미 검증된 코드를
재사용하고, 상점과는 문자열 key 규약(`pose_*`) + 단방향 호출만으로 결합해 기존 약결합 원칙을
유지한다. 남은 불확실성은 두 개의 스파이크(포즈 재생 복귀 품질, 베이크 알파 채널)로 초기에 해소한다.
