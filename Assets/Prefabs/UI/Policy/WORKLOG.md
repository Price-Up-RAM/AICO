# Policy UI WORKLOG

약관/정책 열람 패널. 좌측 문서 탭 + 우측 스크롤 본문(스크롤바) 구조의 다크 테마 UGUI/TMP 패널.
법무 방향 문서는 `Docs/LEGAL_TERMS_PLAN.md` (repo 루트) 참조.
**문서 조항별 근거와 배포 전 최종 수정 체크리스트는 이 폴더의 `README.md` 참조.**

## 현재 할 수 있는 일

- 문서 4종 x 3언어(ko/en/jp) 열람: 이용약관 / 개인정보처리방침 / AI 고지 / AI 운영정책.
  전문은 `PolicyView/Documents/{key}_{lang}.txt` TextAsset으로 프리팹에 직렬화(번들)된다.
- 표시 언어: Show() 시 `SettingManager.settings.ui_language`(ko/jp/en)로 시작, 읽기 실패 시 **en** 폴백.
  헤더 × 왼쪽 언어 버튼(KO/JP/EN 표기) 클릭 시 SettingManager 등록 순서(ko→jp→en)로 순환.
  코드용 `PolicyView.SetLanguageOverride("ko"|"en"|"jp")`도 제공(다음 Show에서 리셋).
- 탭 라벨은 각 문서의 첫 `# ` 행에서 파싱 → 언어 전환 시 탭·본문이 함께 바뀐다.
- **본문 구조 = Content 아래 Title/Body 2노드 고정** (샘플 매핑 방식):
  - 프리팹(에디터)에는 스타일 샘플 `TitleSample`/`BodySample`이 보이는 상태로 구워진다.
    폰트 크기·행간(Line Spacing)·좌우 오프셋·상단 위치를 이 샘플에서 조정하면 된다.
  - 런타임 Show 시 샘플을 숨기고, 샘플의 복제본 `Title`/`Body`에 파일 내용을 매핑한다.
    문서 길이에 맞춰 Content 높이를 계산하고 스크롤은 맨 위로 리셋.
  - Body는 `richText=on` — 파일의 `## ` 절 제목을 크기/색 태그로 강조한다.
    따라서 **문서 txt 안에 `<` 문자를 직접 쓰지 말 것** (태그로 해석됨).
  - Title/Body 사이 간격·하단 여백은 PolicyView 인스펙터(`titleBodyGap`, `contentBottomPadding`).

## 빌드 절차 (Tools 메뉴)

1. `Tools/Policy/1. Build PolicyView Prefab` — 코드 빌드 → 정적 프리팹 베이크
   (`PolicyView/Prefabs/PolicyView.prefab`). Documents의 TextAsset 참조를 직렬화.
2. `Tools/Policy/2. Apply SUIT-Bold Font` — 프리팹 전체 TMP를 SUIT-Bold로 교체 +
   SUIT-Bold 폴백에 NotoSansJP 보장(멱등).
3. `Tools/Policy/3. Build Demo Scene` — `PolicyView/Demo/PolicyViewDemo.unity` 생성.
- `Tools/Policy/Build All (Prefab+Font+Demo)` = 위 3개 일괄. batchmode에서는
  `-executeMethod PolicyBatch.BuildAll`.

## How to use (데모)

`PolicyView/Demo/PolicyViewDemo.unity`를 열고 Play:
- 좌측 탭 클릭 → 문서 전환, 우측 휠/드래그/스크롤바 → 본문 스크롤
- 헤더 언어 버튼(× 왼쪽) → ko→jp→en 순환, 키 1/2/3 → ko/en/jp 직접 지정
- 헤더 드래그 → 패널 이동, × 버튼 → 패널 닫기
- 데모 씬엔 SettingManager가 없어 최초 언어는 en 폴백이 정상 (본편에선 ui_language를 따름)

## 문서 관리 규칙

- 원문(정본)은 ko. en/jp는 ko 개정 후 재번역한다. 세 파일의 `# `/`## ` 행 개수는 항상 일치시킬 것
  (탭 라벨 파싱과 검수 스크립트가 이 구조에 의존).
- 본문은 LanguageData에 등록하지 않는다(장문·기능결합 문자열 등록 금지 원칙). 언어별 전문 파일이 정본.
- 플레이스홀더: `[연락처 이메일]`, `[YYYY-MM-DD]`(시행일), 배포 전 실제 값으로 치환 필요.
- 문서는 초안(v0.1)이며 법률 자문을 거친 확정본이 아니다.

## 남은 것

- [ ] UIManager/UIPositionManager 연동 (필드 + Show/Close/Toggle + menuName "policy" 위치 케이스).
      본편에서 여는 진입점(설정 메뉴 등)이 정해지면 연결한다. 현재는 데모 씬으로만 검증.
- [ ] 플레이스홀더(연락처/시행일) 실값 치환, 문서 확정(법률 검토).
- [ ] (선택) 첫 실행 시 동의 플로우(약관 동의 체크) — AI기본법 고지 구현과 함께 설계.
- [ ] (선택) Third-Party Notices 문서 — 배포 전에는 필요(오픈소스 라이선스 의무). 현 패널 범위에서는 제외 결정(2026-07-22).
