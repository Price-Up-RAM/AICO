# -*- coding: utf-8 -*-
# Kickoff Guide 전파 (§7-1 F) — Phase 5 현황 / §6 도구표 / §4 새 함정 5개
# 이 파일은 LF다.
import sys, os

P = os.path.expanduser("~/mnt/UnityProject--AICO/Assets/Scripts/Plan/MR_Phase_Kickoff_Guide.md")
ok = True
data = open(P, "rb").read()
assert data.count(b"\r\n") == 0, "개행이 CRLF다 — 스크립트 수정 필요"


def patch(old, new, label):
    global data, ok
    o = old.encode("utf-8")
    n = new.encode("utf-8")
    c = data.count(o)
    if c != 1:
        print("FAIL %s : 앵커 %d회 매치" % (label, c))
        ok = False
        return
    data = data.replace(o, n)
    print("OK   %s" % label)


# ===== 1. Phase 5 현황 행 =====
patch(
"""| 5 | StreamingAssets 이관 · 음성 대화 | ⬜ 대기 | `MR_StreamingAssets_Migration_Plan.md` |""",
"""| 5 | StreamingAssets 이관 · 음성 대화 · **라우터/스킬 배선** | 🟢 **진행 중 (2026-08-25)**. 음성→라우터 전환·TTS ref_id·주크박스 4종 완료. 남은 것은 alarm/UI창/todo/inventory 22종 | **`MR_Phase5_Voice_Router_Plan.md`**(설계·실측), **`MR_Phase5_Tool_Matrix.md`**(툴 61종 대조표), `MR_StreamingAssets_Migration_Plan.md` |""",
"1. Phase 5 현황 행")

# ===== 2. §6 도구표 =====
patch(
"""| ~~`MRSystemMenuBuilder`~~ | — | **폐기 (2026-08-19)**. 시스템 메뉴가 ContextMenu로 바뀌어 생성할 패널이 없어졌다 (`MR_Phase4A_SystemMenu_Design.md`) |""",
"""| ~~`MRSystemMenuBuilder`~~ | — | **폐기 (2026-08-19)**. 시스템 메뉴가 ContextMenu로 바뀌어 생성할 패널이 없어졌다 (`MR_Phase4A_SystemMenu_Design.md`) |
| `MRQuerySender` | `Scripts/MR/Editor/` | (신규 2026-08-25) `Tools → MR → 질문 보내기 창`. Play 중에 에디터에서 라우터로 직접 질의를 보낸다. **Editor + Link에서는 `TouchScreenKeyboard`가 뜨지 않고**(Android 런타임 전용) STT는 한 언어만 인식해서 정확한 문장을 넣을 방법이 없었다. STT·InputField를 모두 우회한다 |
| `MRKeyboardBinderSetup` | `Scripts/MR/Editor/` | (신규 2026-08-25) `Tools → MR → 키보드 바인더 부착` / `키보드 상태 리포트`. `MRTMPVirtualKeyboardBinder`가 씬에 **0개**여서 실기 키보드가 뜨지 않던 것을 부착·진단한다 |
| `MRJukeboxKoreanTags` | `Scripts/MR/Editor/` | (신규 2026-08-25) `Tools → MR → 주크박스 한글 태그 부여`. 곡 이름이 전부 영문(`campfire`/`Lofi1`/`rain`)인데 STT는 한 언어만 인식해서 한국어로 곡을 고를 수 없었다. `PlayByTag`가 태그 부분 일치라 한글 태그를 넣으면 기존 경로가 그대로 먹는다 |
| `ForceServerSettings` | `Scripts/MR/Editor/` | (기존) `Tools → MR → 강제로 arona655 서버 설정하기`. `persistentDataPath/config/settings.json`의 `server_id`를 덮어쓴다. **코드 기본값을 고쳐도 저장 파일이 있으면 안 바뀐다**(§4-62 계열) — 서버가 바뀌면 이걸 쓸 것 |""",
"2. §6 도구표")

# ===== 3. §4 새 함정 5개 =====
patch(
"""---

## 5. Phase 현황""",
"""### 4-60. 조회 부작용으로 생긴 빈 엔트리가 기본값 시딩을 영구히 무력화한다 ⭐⭐

`SettingCharManager.GetCharCodeSetting`은 **조회만으로** 없는 키에 빈 엔트리를 만든다.
`SaveToFile`은 `RebuildListsFromDict()`로 **딕셔너리 전체**를 파일에 쓴다.
그래서 무관한 저장(캐릭터 변경·크기·친밀도) 한 번이면 그 빈 엔트리가 파일에 굳고,
`File.Exists` 기반 시딩은 그 뒤로 영원히 안 돈다.

2026-08-25에 `aico.voiceId=''`가 정확히 이렇게 만들어져 **TTS `ref_id`가 조용히 누락**됐다.
서버는 `ref_id` 없는 요청에 500을 반환했다(16건 실측).

게다가 **"엔트리가 없다"와 "엔트리는 있는데 값이 비었다"를 사후 조회로는 구분할 수 없다** —
조회하는 순간 엔트리가 생겨 버리기 때문이다. 진단 로그는 **생성되는 그 순간**에 찍어야 한다.

> **일반화 ①**: 게터가 없는 키를 만들어 반환하면 **null 체크가 무력화되고 결손이 정상값으로 위장된다.**
> "조회만으로는 저장 안 한다"는 주석이 붙어 있어도, 전체 덤프 방식의 저장이 하나라도 있으면 굳는다.
> **읽기 함수가 상태를 바꾸는지** 먼저 확인할 것.
> **일반화 ②**: 기본값 시딩·마이그레이션의 가드는 **컨테이너 존재 여부(파일/딕셔너리)가 아니라
> 실제로 채우려는 값이 비었는지**로 건다. 전자는 부분 결손을 영원히 못 잡는다.
> 같은 함정을 서버 주소에서도 밟았다 — `SettingManager.cs`의 `server_id = "arona655"`는
> 기본값 블록이라, 낡은 `settings.json`이 있는 환경은 계속 옛 주소로 갔다.

### 4-61. 한 저장소 안에서도 파일마다 개행이 다르다 ⭐

이 프로젝트는 대부분 **CRLF**인데 `Assets/Scripts/MR/`는 **CRLF 10개 / LF 38개**로 섞여 있고
`MR/Editor/`는 전부 **LF**다. 2026-08-25에 패치 스크립트가 CRLF를 가정해
`MRSceneStripper.cs`에서 앵커 0회 매치로 실패했다.

더 나쁜 건 **같은 스크립트의 다른 앵커(개행이 없는 한 줄)는 매치에 성공해서**,
LF 파일에 CRLF 줄을 섞어 넣은 채 절반만 적용됐다는 점이다.

> **일반화**: 파일을 프로그램으로 편집할 때는 **파일마다 개행을 판별**해서 쓴다.
> 전역 가정은 "일부만 적용"이라는 최악의 상태를 만든다. BOM도 같다 —
> `utf-8-sig`로 쓰면 없던 BOM이 생겨 diff가 전체 파일로 부풀고 Unity가 재임포트한다.
> **편집 후에는 CRLF/LF 개수와 BOM 유무를 원본과 대조**하는 검증을 한 줄 넣을 것.
> 그 검증이 실제로 사고를 하나 더 잡았다 — 파이썬에서
> `open(p,'wb').write(open(p,'rb').read())`는 `'wb'`가 먼저 평가돼 **파일을 0바이트로 잘라먹는다.**

### 4-62. `.meta`는 CRLF다 — GUID 변수에 `\\r`이 붙어 grep이 조용히 0을 반환한다 ⭐⭐

```bash
g=$(grep -m1 '^guid:' "$f.meta" | cut -d' ' -f2 | tr -d '\\r')   # tr 필수
```

`tr -d '\\r'` 없이 `AlarmManager`를 찾았다가 **"MR 씬에 없다"는 틀린 결론**을 냈다(2026-08-25).
그대로 믿었으면 "AlarmManager를 씬에 추가하세요"라는 틀린 안내를 만들었을 것이다.
교차 확인(`grep -c "<guid>"`을 `guid: ` 접두사 **없이** 다시 실행)으로 잡았다.

§4-55가 "카운트 0은 없다가 아니다"라면, 이건 **그 카운트 자체가 거짓말인 경우**다.

> 덧붙여 `Assets` 전체 재귀 grep은 45초 타임아웃에 걸린다(§4-10).
> `find`로 파일 목록을 먼저 뽑아 `grep -l -F -e ... $(cat 목록)`으로 한 번에 돌리면 즉시 끝난다.

### 4-63. `Resume()`류의 "이어하기" API는 시작 상태에서 조용히 무시된다 ⭐⭐

`MRJukebox.Resume()`은 `_isPaused`도 아니고 `_currentIndex`도 -1인 초기 상태에서
두 분기 모두 빗나가 **아무 일도 안 하고 예외도 안 낸다.**

```csharp
public void Resume() {
    if (_isPaused && ...)                        { UnPause; }   // _isPaused=false → 실패
    else if (!isPlaying && _currentIndex >= 0)   { PlayTrack; } // -1 >= 0 → 실패
}                                                                // 아무것도 안 하고 끝
```

2026-08-25에 `jukebox_play`를 이 위에 얹고 **무조건 성공을 반환**해서,
캐릭터가 "재생을 시작했습니다"라고 말하는데 소리는 안 나는 상태가 됐다.
`CurrentTrackName`도 `_currentIndex < 0`이면 null이라 로그의 곡 이름이 비어 있었다 —
그게 유일한 단서였다.

> **일반화**: 재생·열기·시작 계열 API를 감쌀 때는 **호출 직후 상태를 다시 읽어 성공을 판정한다.**
> "예외가 안 났다"는 성공의 근거가 아니다. §4-58의 액션 버전이다.
> 그리고 **`Toggle` 계열을 단방향 명령에 매핑하지 말 것** —
> `InventorySystemManager.ToggleEquip(key)`는 미장착이면 오히려 장착한다.
> `inventory_unequip`을 거기 매핑하면 해제 요청이 장착으로 뒤집힌다.

### 4-64. 클래스가 있다 ≠ 씬에 있다 ≠ `Instance`가 자가생성한다 ⭐⭐⭐

`ApiAgentFunctionSfx` / `ApiAgentFunctionAction` / `ApiAgentFunctionChatMode` 세 컴포넌트는
**프로젝트 어느 씬에도, 프리팹 864개 어디에도 없다**(2026-08-25 GUID 실측).
그런데 `ApiAgentFunctionManager.ExecuteAction`은 null 체크 없이 `.Instance.메서드()`를 부른다.
`Instance` 게터가 `FindObjectOfType`만 하고 못 찾으면 **null을 반환**하기 때문에
`play_sfx` / `set_chat_mode` / `toggle_chat_mode` / `get_chat_mode`는 **호출 즉시 NRE**다.

같은 파일의 `character_dance`가 멀쩡히 동작해서 더 헷갈렸다 —
`ApiVlRouterManager.ExecuteRouterFunction`의 switch가 `default:`(→`ExecuteAction`) **이전에**
가로채 `AnimationManager.Instance.Dance()`를 직접 부르기 때문이다. **우연히 살아난 것이다.**

> **일반화**: 싱글톤을 쓰기 전에 **`Instance` 게터가 자가생성하는지** 반드시 확인한다.
> 이 프로젝트만 해도 세 패턴이 섞여 있다 —
> 자가생성(`CurrencyManager`, `MissionList`, `ApiAgentFunctionTodoAction`),
> `Awake`에서만 세팅(`JarvisTodoStore`, `MRJukebox`),
> `FindObjectOfType`만 하고 null 반환(`ApiAgentFunction*` 3종, `InventorySystemManager`).
> **비활성 GameObject에 붙어 있으면 `Awake`가 안 돌아 싱글톤이 아예 성립하지 않는다** —
> `AlarmManager`가 그 상태라 `Resources.FindObjectsOfTypeAll` + `scene.IsValid()` 헬퍼를 써야 한다.

---

## 5. Phase 현황""",
"3. §4 새 함정 5개")

if ok:
    open(P, "wb").write(data)
    print("저장 완료 (%d bytes)" % len(data))
sys.exit(0 if ok else 1)
