# 장비 원본 에셋 전달 요청 (AICO ← MY-Little-Jarvis-3D)

**작성**: 2026-08-09, SampleSceneKAI Function(인벤토리/상점/장비) 이식 작업 중
**받는 분**: MY-Little-Jarvis-3D(원본 풀 프로젝트) 관리자
**요지**: 카탈로그(8월판)는 AICO에 동기화 완료. 그런데 신규 장비 8종의 **3D 프리팹이
오버레이 리포(MY-Little-Jarvis-3D-Script)에 추적되지 않아** AICO에서도 결측입니다.
현재 상점·인벤토리에 아이콘/이름/가격까지 정상 표시되지만, **착용 시 prefab이 null이라
EquipManager가 조용히 거부**합니다. 원본 프로젝트에서 아래 프리팹을 전달해 주시면
카탈로그 수정 없이 즉시 착용까지 동작합니다.

## 결측 프리팹 8종

카탈로그가 기대하는 GUID입니다. **GUID가 보존되어야 자동 연결**되므로 반드시
`.meta`를 포함해 전달해 주세요.

| key | 표시명 | 기대 prefab GUID | 루트 fileID | 아이콘(출처 추정) |
|---|---|---|---|---|
| bag_bear | 곰모양 가방 | `05f62330e26c13b42837b317ad81dd6d` | 7117280948445998936 | SM_Chr_Attach_Bag_Bear_01 (Synty) |
| bag_pug | 퍼그모양 가방 | `863b56d2cf70d874ebc8414c807a9451` | 7110058803885176568 | SM_Chr_Attach_Bag_Pug_01 (Synty) |
| bag_bee | 벌모양 가방 | `1584760a89056db40b35e3a724a67aa9` | 7850950579395786722 | SM_Chr_Attach_Bag_Bee_01 (Synty) |
| bag_bird | 새 모양 가방 | `4c33fc8f1c6ba9a41b2c6427750b2bb5` | 4216674876371085428 | SM_Chr_Attach_Bag_Bird_01 (Synty) |
| wing_fairy | 요정 날개 | `edabf95878324824ab5332bc5be93f03` | 4431458837526703948 | SM_Chr_Attach_FairyWings_01 |
| wing_butterfly | 나비 날개 | `31447cd5fdb0d674595c970391ecedba` | 648708502543545363 | FantasyKingdom_FairyWings |
| hat_crown | 왕관 모자 | `e4079ecb4dda24549b06f585114cd4e7` | 5658099891815096197 | SM_Chr_Attach_Crown_01 (Synty) |
| hat_magician | 마법사 모자 | `13b59c9b88c91eb4495b783d31d28c18` | 540002192673604984 | SM_Chr_Attach_Hat_Magician_01 (Synty) |

- 참조처: `Assets/Prefabs/Assist/ItemSystem/Resources/ItemEquipCatalog.asset`의 `prefab` 필드.
- 핏 데이터(sizeRatio/rotationOffset 등)는 `EquipCatalog.asset`에 이미 들어와 있습니다.
- 베이크된 아이콘 PNG는 `Assets/Model/Sprite/Equip/Baked/`에 이미 있습니다.

## 전달 방법 (권장)

원본 프로젝트에서 프리팹 8개 선택 → 우클릭 → **Export Package... (Include dependencies 체크)**
→ 생성된 `.unitypackage`를 전달. 이 방식이 메시/머티리얼/텍스처와 GUID를 함께 보존합니다.
AICO에서는 임포트만 하면 됩니다 (추가 배선 불필요).

※ 출처가 Synty(POLYGON 계열)/FantasyKingdom 등 유료 에셋팩이면 라이선스상 리포 커밋 가능
여부도 함께 알려주세요. 커밋 불가면 로컬 임포트 전용으로 관리하겠습니다.

## (부수) 같이 주시면 좋은 결측 2건 — 필수 아님

| 용도 | 기대 GUID | 비고 |
|---|---|---|
| 골드 아이콘 스프라이트 | `08557bbe25c2e413ea57256cd4f78fdd` | `ItemCurrencyCatalog.asset`의 currency_gold.icon. 없으면 아이콘만 비어 보임 |
| 재화 획득 사운드 | `d277e116012604f73833af4d67a01fa0` | CurrencyRewardManager 획득 효과음. 없으면 무음 |
