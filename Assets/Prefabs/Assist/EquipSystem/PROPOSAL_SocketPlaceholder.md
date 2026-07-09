# EquipSystem — 소켓(볼륨)+플레이스홀더(표면점) 최종 설계

> 2026-07-09. 워크플로우 산출물: 사용자 모델 강철맨 설계 → 사용자 모델 그릴 + 현행 점-소켓 비판(양방향) → 종합.
> 네이밍 확정: socket(캡슐형 부위 볼륨) → placeholder(캡슐 테두리/근처의 부착점).

코드 스팟체크 완료 — 두 비판 문서가 인용한 코드 사실(`EquipPlacement.cs:53`의 `volumeCenter + positionOffset`, `EquipSlotStamper.cs:721-751`의 Donor 전용 앵커 복제와 "VolumeCenter로 강등", `EquipCatalog`의 `targetSlotId` 단일 주소)이 실제 코드와 일치함을 확인했다. 종합에 들어간다.

# 소켓+플레이스홀더 최종 설계

기준 코드: `d:\unity\AICO\Assets\Prefabs\Assist\EquipSystem\` (Scripts/Editor 전체 검증됨)

## 1. 판정: 사용자 모델 채택 범위 (부분 채택 — head 우선, 전 부위 확장은 단계화)

**채택 핵심** — 사용자 모델의 본질 주장 "캡슐 전개 + 테두리 placeholder + 방향·반경배율 기억 = 크로스 캐릭터 표면 정규화"는 세 문서 모두에서 살아남았다. 결정적 근거는 현행 모델 비판의 격차 2: 현행 전파의 오프셋 정규화 축이 **전신 키**(`rootDirFromBone = Δ/height`)라서, 이 로스터의 명시 특성인 두신비 극단(치비 머리 40% vs 일반 15%)에서 **체계적으로** 틀어진다. 캡슐 반경 정규화는 이 1차 오차항을 정확히 소거한다. 손익 회계(그릴 공격 6)도 두신비 경계 전파 + 부착점 2개 이상에서 이득 성립을 확인했다.

**채택 항목**: ① `EquipPlaceholder`(axisT/dirLocal/radiusScale 무차원 인코딩) ② head 소켓의 볼륨 승격 ③ 표면 스냅 에디터 ④ placeholder 단위 장착/해제(모자+헤어핀+천사링 동시 착용) ⑤ RadiusRelative 크기 핏 ⑥ head/overhead 통합.

**수정 후 채택 (그릴의 유효 공격 3건 반영)**:
- **공격 2 (positionOffset 스케일 종속)**: 유효. 강철맨이 "전부 무차원" 선언하면서 오프셋 경로는 원시 로컬 단위로 남겼다 — 본 lossy 1/35/20000 로스터에서 카탈로그 공용 오프셋이 깨진다. → 신 경로의 오프셋은 **캡슐 radius 배수**로 재정의(2절).
- **공격 3 (접촉면 규약 부재)**: 유효. 바운드 중심을 표면점에 놓으면 모자 절반이 파묻히고, 오프셋으로 때우면 파묻힘 깊이가 캐릭터마다 달라진다. → **contactAnchor 규약**(BottomAlign 기본) 신설(2절).
- **공격 1 (캡슐 표면 ≠ 시각 표면)**: 유효. "무료 이식"은 두개골 기준에서만 참. → 서사를 **"방향 이식 무료 + 반경(헤어 두께)은 캐릭터당 1회 보정 가능성"**으로 정직화하고, head 캡슐 기준 표면을 정책 결정 사항으로 명시(8절 질문 2, 추천: 헤어 포함 실루엣). 반경 보정은 `EquipPlaceholderStamp`의 손보정 보호가 흡수하는 **예상된 워크플로우**로 격상.

**보류 (과설계 경고 유효 → Phase로 이연)**:
- **torso(chest/back) 통합**: 그릴 공격 4 유효. 몸통 단면은 타원이라 캡슐 근사가 나쁘고(front 표면점이 뜨거나 파고듦), chest/back은 현행도 별개 소켓이라 동시 장착이 이미 되고 사다리 비용도 별칭 공유로 사실상 1회. 이득이 def 1개 절약 수준. → **Phase C 보류, 점-소켓 유지.**
- **EquipVolumeAutoFitter**: 그릴 공격 5 유효. 단일 결합 SMR + head 지배 스킨 헤어(준구형 균일 부풀림)는 잔차 지표가 침묵 통과시키는 가장 흔한 오염 모드. 수동 캡슐 피팅 1회는 현행 5점 조정보다 싸다. → **Phase B로 이연**, 헤어 의심 정점 비율 리포트를 전제 조건으로.
- **배치 EquipMigrationWindow**: 대상이 head/overhead로 줄어 별칭 매핑 + 소규모 처리로 충분. → 축소(6절).

**기각된 공격 (사유 명시)**:
- *회전 규약이 모자/핀/리본에 다 되는가* → 기각. 구형 소켓에서 법선 ≡ dirLocal이므로 SurfaceAligned 프레임은 캐릭터 불변, 극점 퇴화 폴백도 정의됨.
- *chibi에서 "같은 방향 = 같은 자리"가 깨진다* → 기각(부메랑). 현행 키-정규화야말로 두신비 경계에서 체계적으로 틀리는 방식이고, 방향 오차는 2차 항 + 스탬프 보호가 흡수.
- *20000 스케일 수치 불안정* → 기각. "월드 계산 → 마지막 로컬 환산"은 `SetCapsuleByWorldLength`로 이미 검증된 전략.
- *스냅은 과설계* → 기각. Encode→radiusScale=1→Decode 한 줄이고, 품질 주장이 아니라 저작 조작감.
- *소켓 프레임 루트 정렬 전제 의심* → 기각. `EquipSlotStamper.StampTemplateToInstance`의 `rootRot * Euler(rootFrameEuler)` 실코드 확인.
- *MagicaCloth 헤어가 흔들려 표면이 무의미* → 차등 공격 아님(점-소켓과 공통 조건). 단 기대치는 질문 4로 확인.

## 2. 최종 데이터 모델

### EquipSocket (수정 — 필드 추가만, 기존 필드 전부 보존)
```csharp
public class EquipSocket : MonoBehaviour
{
    public string slotId;                          // 유지
    // fit/pivot/placeholderAnchor 유지 (레거시 경로 무손상)
    public EquipPlaceholder FindPlaceholder(string placeholderId);  // 신규
    public CapsuleCollider Capsule { get; }                         // 신규 (SizingVolume 캐스팅)
}
```
의미 승격: "부착점" → "부위 볼륨". 같은 캡슐이 사이징 볼륨과 표면 볼륨을 겸한다(현행 비판의 방어 3 수용).

### EquipPlaceholder (신규 — 핵심)
```csharp
public enum EquipPlaceholderOrientation { SurfaceAligned, SocketFrame }
public enum EquipContactAnchor { BottomAlign, Center }   // 그릴 공격 3 반영

public class EquipPlaceholder : MonoBehaviour
{
    public string placeholderId;          // "top", "side_l", "halo"
    // 이식 좌표 (원본, 전부 무차원)
    public float axisT;                   // 캡슐 축 위 [-1..1], 구 퇴화 시 0
    public Vector3 dirLocal;              // 소켓 로컬 표면 방향 단위벡터
    public float radiusScale;             // 1=표면, >1=부유(천사링), 0=중심
    // 회전/접촉 규약
    public EquipPlaceholderOrientation orientation;   // 모자/핀=SurfaceAligned, 링=SocketFrame
    public Vector3 rotationOffsetEuler;
    public EquipContactAnchor contactAnchor = EquipContactAnchor.BottomAlign;
    // Transform은 파생 캐시: ApplyToTransform() / CaptureFromTransform() (EquipCapsuleMath 사용)
}
```
BottomAlign 정의: 핏 완료된 악세서리 바운드의 −법선(placeholder −up) 방향 접면을 표면점에 정렬. 파묻힘 깊이가 캐릭터 불변이 된다.

### EquipCapsuleMath (신규 — 순수 정적, 유닛테스트 가능)
세그먼트 산출(halfLen = max(0, h/2−r), 구는 자동 퇴화) / Encode(점→axisT·dirLocal·radiusScale) / Decode / 스냅(Encode 후 radiusScale=1 Decode).

### EquipEntry (카탈로그 — additive 확장, 기존 에셋 무손상)
```csharp
public string targetPlaceholderId;    // 신규. 비면 레거시 경로 (하위호환)
public EquipEntryFit fitMode;         // RadiusRelative(신규 기본) / ContainUniform(레거시)
public float sizeRatio = 1f;          // 최장변 = 캡슐 월드지름 × sizeRatio
public Vector3 positionOffsetRadii;   // 신규: placeholder 로컬, "캡슐 radius 배수" 단위 — 무차원
// 기존 positionOffset/rotationOffset/fitBias 유지 (레거시 경로 전용)
```

## 3. 부위 목록 결론 (5슬롯 → 4슬롯: head 통합, torso 보류)

| 슬롯 | 결론 | 근거 |
|---|---|---|
| `head` | 볼륨 소켓 승격 + 표준 placeholder 시드 `top`(모자, rs=1) / `side_l`·`side_r`(핀) / `halo`(rs≈1.6, SocketFrame) | 신모델 본진. 두신비 경계 전파의 이득이 여기 집중 |
| `overhead` | **폐지 → head placeholder로 흡수**. 레거시 매핑 `"overhead"→("head","top")` | 본이 같고(둘 다 Head) 위치만 달랐음 — placeholder가 정확히 그 차이. 동시 장착도 placeholder 단위 장착으로 오히려 개선(기능 후퇴 없음 — 현행 비판 방어 6 조건 충족) |
| `chest` / `back` | **점-소켓 유지** (torso 통합은 Phase C 재검토) | 캡슐 근사 나쁨(타원 단면) + 이미 동시 장착 가능 + 사다리 비용 공유 — 통합 이득 미달(그릴 공격 4) |
| `origin` | 유지, ContainUniform 볼륨-핏 지속 | 해부학 표면 없음 — 점/볼륨-핏이 정답인 슬롯(현행 비판 방어 1). 추후 `PH_ground` 확장 여지만 열어둠 |

`EquipSlotTemplate.StandardSlotIds = { "head", "chest", "back", "origin" }`. 데모 카탈로그의 비표준 `head1`은 마이그레이션 시 `("head","side_l")`로 재기입(6절).

## 4. 에디터 도구 델타

- **`EquipPlaceholderEditor` (신규)**: `EquipSocketEditor` 패턴 복제. `Handles.FreeMoveHandle` 드래그 + 인스펙터 토글 "표면 스냅"(기본 on: Encode→rs=1→Decode로 캡슐 표면을 미끄러짐 / off: 자유 radiusScale — 천사링 높이용). 드래그 종료 시 `Undo` + `CaptureFromTransform()`. 인스펙터 수치 직접 편집 시 `OnValidate`→`ApplyToTransform()` 양방향 동기화. 시각화: 표면 접원 `DrawWireDisc`, 축 최근접점→placeholder 선분, rs>1 점선. **라이브 미리보기(카탈로그 키 선택 + HideAndDontSave + 재핏 루프)를 그대로 복제** — 기존 WYSIWYG 자산 계승.
- **`EquipSocketAuthorWindow` (수정)**: head 행에 placeholder 목록 표시 + [Placeholder 추가] + 표준 시드 버튼. 슬롯 행은 4종.
- **`EquipSocketEditor` (수정)**: [Placeholder 추가] / [Placeholder 재배치](캡슐 수정 후 순정 스탬프 placeholder만 `ApplyToTransform()` 재실행) 버튼.
- **자동 피팅: Phase B**. 도입 시 지배-웨이트 캡슐 피팅(강철맨 3-2절 알고리즘)에 **"헤어 의심 정점 비율" 리포트를 필수 추가**(물리 본 자손 웨이트 보유/헤어 머티리얼 비율) — 잔차 단독으로는 준구형 헤어 부풀림을 못 잡는다(그릴 공격 5). 결과는 항상 스탬프 스냅샷 동반 = 손보정 보호 지배.

## 5. 런타임/전파 델타

**Equip 경로** (`EquipManager` 수정):
```
Equip(target, key) → 소켓 탐색(레거시 slotId 별칭 매핑 경유) 
→ targetPlaceholderId 있음 → FindPlaceholder → placeholder 하위 부착
                     없음(id 지정인데 미존재) → 경고 + 볼륨 center 폴백
                     id 빈 문자열 → 기존 Fit 경로 그대로 (레거시 무손상)
→ ClearEquipped를 placeholder 단위로 (동시 장착 획득)
→ EquipPlacement.FitToPlaceholder(...)
```
`Unequip(target, slotId)` 유지 + `Unequip(target, slotId, placeholderId)` 오버로드.

**`EquipPlacement.FitToPlaceholder` (신규, 기존 `Fit` 보존)**: `scale = ComputeFitScale(2·radius_world·sizeRatio, natural)·fitBias` (RadiusRelative), 배치 = placeholder pose + contactAnchor 정렬 + `positionOffsetRadii × radius_world` 가산. radiusScale 부유는 placeholder Transform에 이미 구워져 있어 런타임 수학 0 유지. `EquipFitter`/`MeasureNatural`/`ComputeFitScale` 무수정 재사용. 에디터 미리보기도 동일 함수 호출.

**전파** (`EquipSlotStamper` 수정, Phase B):
- `EquipSlotDef`에 `List<EquipPlaceholderDef>` additive 추가(placeholderId/axisT/dirLocal/radiusScale/orientation/rotationOffsetEuler/contactAnchor — **본 정보 없음**). 기존 템플릿 에셋은 placeholder 0개로 유효.
- Capture: 소켓 geometry 후 자식 placeholder 순회, placeholderId 기준 merge.
- Stamp: 기존 흐름 끝에 `PH_{id}` GO 확보(AcquireSocketGo 패턴 재사용) → 필드 복사 → `ApplyToTransform()` → `EquipPlaceholderStamp.TakeSnapshot()`.
- **`EquipPlaceholderStamp` (신규)**: 비교 대상이 무차원 인코딩 필드(`Angle(dirLocal)>3°`, `|Δrs|>0.05`, `|ΔaxisT|>0.05`)라 `EquipSocketStamp.IsHandTuned`의 "잣대=캡슐높이" 곡예가 원천 소멸 — 실측된 개선. 캐릭터별 헤어 두께 반경 보정이 이 보호로 영구 고정된다(공격 1의 잔존 변수를 시스템이 흡수하는 지점).
- 드라이런 리포트는 `slotId = "head/top"` 문자열 규약만 확장. Donor 복제는 기존 `CopySocketFields` 앵커 로직을 placeholder 자식 복제로 일반화.
- **본 해석 사다리/`EquipPhysicsBoneFilter`/`EquipPropagationWindow` 셸/배치 IO: 무수정 재사용.** 본 해석은 소켓 1개에만 풀고 placeholder N개는 수학으로 따라온다 — 단, 이 "무료"는 방향에 한정(반경은 보정 가능성 있음).

## 6. 마이그레이션 (현행 점-소켓 / 기존 카탈로그 처리)

배치 창 대신 **소규모 도구 + 별칭 매핑**으로 축소 (torso 보류로 범위가 head/overhead뿐):

1. **레거시 slotId 별칭 매핑** (`EquipManager` 진입부): `{ "overhead"→("head","top") }` — 기존 카탈로그 에셋이 무수정 동작. chest/back은 그대로라 매핑 불요.
2. **head 소켓**: 캡슐을 손으로 "머리 볼륨"으로 확장(에디터 핸들, 자동 피팅은 Phase B). 기존 부착 중심점을 Encode → `PH_side_l` 생성.
3. **overhead 소켓**: 월드 위치를 head 캡슐 기준 Encode → rs≈1이면 `PH_top` 스냅, rs>1.2면 `PH_halo` 분류 제안. 소켓 GO 삭제는 **스탬프 순정일 때만**, 손보정이면 KEEP 공존(별칭 매핑이 있어 동작 무지장). `Tools/EquipSystem/Migrate Head Sockets`로 제공, `ProcessPrefabTarget` 재사용 + 드라이런.
4. **`placeholderAnchor`(PlaceholderChild) 소켓**: 자식 앵커를 `EquipPlaceholder("legacy_anchor")`로 승격, enum/필드는 deprecate 표기 후 존치.
5. **카탈로그 엔트리(현 1~4종, 데모 상태)**: 손으로 재기입 — 비표준 `head1` → `targetSlotId="head", targetPlaceholderId="side_l"`, overhead류 → `("head","top"|"halo")`. `targetPlaceholderId` 빈 엔트리는 영원히 레거시 경로로 동작하므로 강제 아님.
6. 골든 캐릭터 완료 후 `CaptureTemplate` 1회 재실행 → 템플릿에 placeholder def 반영.

## 7. 단계 계획

**Phase A — 최소 전환 (단일 캐릭터, 레거시 무손상)**
- 신규: `EquipPlaceholder`, `EquipCapsuleMath`, `EquipPlaceholderEditor`(스냅+미리보기). 수정: `EquipSocket`, `EquipCatalog`, `EquipPlacement.FitToPlaceholder`, `EquipManager`(별칭 매핑+placeholder 장착). 골든 1체 head 캡슐 수동 피팅 + top/side_l/halo 저작 + 카탈로그 재기입.
- **검증**: ① `targetPlaceholderId` 빈 카탈로그로 기존 데모가 바이트 동일 동작(레거시 회귀 0) ② 골든에서 모자+헤어핀+천사링 동시 장착 육안 ③ `EquipCapsuleMath` Encode↔Decode 왕복/스냅/구 퇴화 에딧모드 유닛테스트 ④ batchmode 1회 컴파일 확인(CLAUDE.md 절차).

**Phase B — 크로스 캐릭터 전파 + 보호**
- `EquipSlotDef.placeholders`, Capture/Stamp 확장, `EquipPlaceholderStamp`, 리포트 "head/top" 표기, Donor placeholder 복제. `EquipVolumeAutoFitter`(헤어 비율 리포트 필수 동반, [자동 피팅] 버튼 + 전파 창 체크박스).
- **검증**: ① 골든 캡처 → 두신비 경계 립(MMD 치비 + AI 일반) 3체 스탬프 드라이런 → top/halo 위치 육안, 반경 보정 필요 횟수 기록(예측: 방향 0회, 반경 캐릭터당 0~1회) ② 손보정 후 재스탬프 시 KEEP 확인 ③ 자동 피팅 vs 수동 캡슐 radius 편차와 헤어 비율 리포트 대조.

**Phase C — 확장 (조건부)**
- torso 통합 재검토(head 실전 데이터로 chest/back 손보정 빈도가 실측 고통일 때만), `PH_ground` 등 origin 확장, 배치 마이그레이션 창(로스터 전체 전환 시).
- **검증**: 전파 드라이런 리포트의 슬롯별 손보정률 비교.

**전 단계 공통**: vibe 코딩규칙(삼항 금지, 중괄호 if-else, `//` 주석, 지연초기화 싱글톤), 작업 종료 시 "닫기→batchmode→재오픈" 1회, `WORKLOG.md` 갱신.

## 8. 사용자 결정 질문 (각 추천 기본값)

1. **악세서리 로드맵 규모** — 캐릭터당 부착점을 몇 개까지 늘릴 계획인가? 특히 `ApiAgentFunctionManager`의 액션 레지스트리에 "AI가 악세서리를 장착해주는" 액션을 넣을 계획이 있는가? (손익 분기: head 부착점 2개↑ + 두신비 경계 전파에서 이득) **추천: 있음으로 가정하고 Phase A 진행** — AICO의 AI-어시스턴트 정체성상 "아이코가 스스로 모자를 쓴다"는 자연스러운 확장이고, 이 경우 카탈로그 성장으로 채택 근거가 강해진다.
2. **head 캡슐의 기준 표면** — 두개골(헤어 제외)인가 보이는 실루엣(헤어 포함)인가? **추천: 헤어 포함 실루엣.** 헤어핀·모자 모두 시각 표면에 놓이는 물건이고, Phase A가 수동 피팅이라 저작자가 실루엣에 맞춰 드래그하는 것이 자연스러우며, 캐릭터별 잔차는 placeholder 스탬프 보호가 흡수한다. (Phase B 자동 피팅 도입 시 이 정책을 피팅 대상 정점 선택에 반영.)
3. **torso 통합 시기** — chest/back을 언제 볼륨 소켓으로 합칠 것인가? **추천: Phase C 보류(기본값: 안 함).** 캡슐 근사 품질이 나쁘고 동시 장착·사다리 비용 이득이 이미 확보돼 있다.
4. **물리 헤어 기대치** — 헤어핀이 MagicaCloth로 흔들리는 머리카락을 따라가지 않고 머리 캡슐(두개골 프레임)에 고정되는 것을 수용하는가? **추천: 수용.** 미수용이면 placeholder 설계가 아니라 부착 철학 자체(클로스 버텍스 추종)를 재검토해야 하며 비용이 자릿수로 다르다.
5. **자동 피팅 투입 시점** — Phase B에 넣을지, head 신모델 실전 검증 후로 더 미룰지. **추천: Phase B에 넣되 "제안 전용"**(스탬프 보호 지배 + 헤어 비율 리포트 필수). 수동 피팅이 항상 최종 권위.