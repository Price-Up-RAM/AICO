# Skills UI

스킬 통합 카탈로그 패널(`SkillView`). 서버 `/skills/list`의 server/unity/custom 스킬을
한 화면에서 보고, custom 스킬은 생성·수정·삭제한다.

위치
- 프리팹 : `SkillView/Prefabs/SkillView.prefab` (정적 프리팹, 에디터에서 편집 가능)
- 컨트롤러 : `SkillView/Scripts/SkillView.cs`
- 서버/로컬 연동 : `SkillView/Scripts/SkillCatalogClient.cs`
- 베이크 도구 : `SkillView/Editor/SkillViewPrefabBuilder.cs`

---

## 현재 할 수 있는 일

- **통합 카탈로그 표시** — `GET /skills/list?lang=`의 server/unity/custom을 드롭다운으로 선택
- **source별 동작 분기**
  - `server` / `unity` : 읽기 전용. 입력창에 description + parameters 표시, 이름·저장·삭제 비활성
  - `custom` : 본문(body) 편집 가능
- **custom 본문 load/save** — 로컬 `persistentDataPath/skills/{key}.md`가 본문의 원천
  (서버 list는 body를 안 주므로). `ApiAgentFunctionSkillManager` 재사용
- **custom 신규 생성** — 헤더 `＋`. key는 이름에서 자동 생성(`^[A-Za-z0-9_-]{1,64}$`, 중복 시 suffix)
- **custom 저장** — 로컬 저장(기존 frontmatter 보존) + 서버 `POST /skills/custom` 동기화
- **custom 삭제** — 2단계 확인(첫 클릭 "삭제 확인?"). 로컬 삭제 + 서버 `DELETE /skills/custom/<key>`
- **언어 전환** — 언어 드롭다운 → `lang`으로 카탈로그 재요청
- **source / category 읽기전용 배지** 표시
- **서버 미연결 시** 로컬 custom만으로 동작 (graceful fallback)

UI 구성: ①헤더(이름 입력 + ＋신규 + ×닫기) ②셀렉터(스킬 드롭다운 + ⟳ + 언어) ③배지 ④CRUD(삭제/저장/되돌리기) ⑤본문 입력

---

## 하고 있는 일 / 남은 것

- [ ] **UIManager / UIPositionManager 연계 미적용** — 아직 씬에서 메뉴로 열고 닫는 연동은 안 됨
- [ ] description 편집 UI 없음(본문만 편집), description 다국어 입력 미지원
- [ ] 서버에 단일 스킬 **body GET 엔드포인트 없음** → custom 본문은 로컬이 원천(서버 간 본문 동기화는 POST 방향만)
- [ ] Play 모드 실서버 연동 동작 미검증 (드롭다운 펼침/POST/DELETE 실제 호출 확인 필요)

---

## How to use

### 1. 프리팹 다시 굽기 (UI 코드 변경 시)
`SkillView.cs`의 `Build()`를 바꿨다면 정적 프리팹을 재생성한다.
- Unity 에디터: 메뉴 **`Tools → Skills → Build SkillView Prefab`**
- 또는 batchmode:
  ```
  Unity.exe -batchmode -quit -projectPath <프로젝트> \
    -executeMethod SkillViewPrefabBuilder.BuildPrefab -logFile -
  ```

### 2. 씬에 올리기
- `SkillView.prefab`을 Canvas 하위에 배치. 루트에 `SkillView` + `SkillCatalogClient`가 이미 붙어 있다.
- 씬에 **`ServerManager`**(baseUrl 제공)와 **`ApiAgentFunctionSkillManager`**(로컬 custom 파일)가 있으면 자동 연동.
  - 둘 다 없어도 SkillView는 단독(샘플 데이터)으로 뜬다.
- 활성화되면 `SkillCatalogClient.OnEnable`이 카탈로그를 자동 로드한다.

### 3. 코드에서 제어
```csharp
skillView.Show();   // 열기 + 갱신
skillView.Hide();   // 닫기
// 데이터 직접 주입(서버 없이 테스트):
skillView.SetSkills(entries);
```

### 4. 서버 API 계약 (Unity가 호출)
| Method | Path | 용도 |
|---|---|---|
| GET | `/skills/list?lang=` | 통합 카탈로그 (드롭다운) |
| POST | `/skills/custom` | custom 생성/수정 (upsert) |
| DELETE | `/skills/custom/<key>` | custom 삭제 |

POST body: `{ key, name, body, lang, require_image, overwrite }` — `source`는 서버가 항상 `custom` 강제.
경계 변환: API는 `require_image`, 서버 내부 저장은 `require_vl`.
