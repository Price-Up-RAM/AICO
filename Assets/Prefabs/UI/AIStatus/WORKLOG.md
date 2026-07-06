# AIStatus — WORKLOG

## 지금 할 수 있는 것 (구현됨)
- `AIStatusView/Scripts/`
  - `AIStatusUi.cs` — 다크 팔레트 + UGUI 팩토리(MissionUi 계보) + 상태색(Ok/Warn/Bad) + `CreateGauge`/`SetGauge`/`CreateKvRow`.
  - `AIStatusData.cs` — `/status`(/full) 직렬화 계약(`AIStatusSnapshot` 등).
  - `AIStatusRow.cs` — GPU/fit 겸용 동적 행. `BindExisting` + `SetupGpu`/`SetupFit`(온도·verdict 색 매핑).
  - `AIStatusView.cs` — 이중모드 메인 패널(헤더 lite/full 토글·↻·×, 세로 스크롤 Body, 서버/GPU/시스템/벤치/fit 섹션).
  - `AIStatusClient.cs` — `ServerManager.GetBaseUrl` → `/status(/full)` 호출 → Newtonsoft 파싱 → `SetStatus`. graceful fallback + 데모용 직접호출(fallbackBaseUrl).
- `AIStatusView/Editor/AIStatusViewPrefabBuilder.cs` — `[MenuItem Tools/AIStatus/Build AIStatus Prefab]`.
- `Demo/AIStatusDemo.cs` — 시작 시 패널 열고 샘플 스냅샷 주입(서버 응답 시 덮어씀).
- `Demo/Editor/AIStatusDemoSceneBuilder.cs` — `[MenuItem Tools/AIStatus/Build Demo Scene]`. Camera/EventSystem/Canvas + 프리팹 + 데모 드라이버. 데모 클라이언트에 `127.0.0.1:5000` 직접호출 주입.

## 구조 메모
- UI는 코드로 빌드 → 정적 프리팹 베이크(에디터 편집 가능). 컨트롤러 이중모드(Build/BindExisting).
- GPU/Fit 리스트는 비활성 템플릿만 굽고 런타임 클론. 에디터(비플레이)에선 템플릿 미리보기만.
- lite/full 이중 응답: 벤치·fit 섹션은 스냅샷 `level=="full"`일 때만 노출.
- 데모 씬엔 ServerManager를 두지 않고, 클라이언트 `fallbackBaseUrl`로 로컬 서버에 직접 통신.

## 내가(사용자가) 해야 할 일
1. **에디터 리로드** — 스크립트 컴파일. 콘솔에 컴파일 에러 없는지 확인. (Auto Refresh off일 수 있음)
2. **Tools → AIStatus → Build AIStatus Prefab** — `AIStatusView/Prefabs/AIStatusView.prefab` 생성.
3. **Tools → AIStatus → Build Demo Scene** — `Demo/AIStatusDemo.unity` 생성. (2번 먼저)
4. **`Demo/AIStatusDemo.unity` 열고 Play** —
   - 로컬 파이썬 서버(`/status`)가 떠 있으면 실제 현황이 채워짐(← 통신 검증 포인트).
   - 없으면 샘플 스냅샷이 그대로 표시.
   - 헤더 `lite/full` 토글로 `/status ↔ /status/full` 전환(full일 때만 벤치·fit 노출), `↻` 재조회, `×` 닫기.

## 남은 것 / 열린 결정
- 게임 본체 연동: 씬에 `ServerManager`가 있으면 자동으로 baseUrl(127.0.0.1:5000 우선)로 호출. UIManager/열기 버튼 등록은 별도.
- `/status/full` 벤치는 서버에 토큰 생성 부하를 줌 → 자동 폴링/자동 full 기본 off 유지 권장.
- 온도/사용률 등 일부 필드는 서버가 `null`을 줄 수 있음(파서는 0 처리).
