# 캐릭터 데이터 추가 가이드 (`character_database.json`)

이 문서는 `character_database.json` 파일에 새로운 캐릭터 데이터를 추가할 때 규칙과 방법을 설명합니다. 앱 실행 시 사용되는 캐릭터와 파생 프리팹 목록을 정리하는 데 사용됩니다.

## 1. 기본 구조
각 캐릭터는 `characters` 배열 내에 하나의 JSON Object 형태로 추가됩니다.

```json
{
  "id": 3,
  "name": "Aris",
  "clothesList": [
    ...
  ]
}
```

## 2. 프로퍼티 설명

- `id`: 캐릭터의 고유 식별자입니다. 반드시 기존 데이터의 마지막 `id` 순서를 따라 **순차적으로 1씩 증가**시켜야 하며 중복되지 않도록 합니다.
- `name`: 캐릭터의 이름입니다. 보통 첫 문자를 대문자로 표기합니다. (예: `Aris`, `Shun Kid` 등)
- `clothesList`: 캐릭터가 속한 의상 또는 버전(프리팹)의 배열입니다. 이 배열의 첫 번째 자리에 대개 'Basic' 의상을 배치합니다.

---

## 3. 의상 데이터 (`clothesList`) 작성 규칙

의상 객체는 프리팹 원본 이름(예: `aris`, `mika_swimsuit`)을 기준으로 나머지 속성들을 파생시켜 작성합니다.

### A. 기본(Basic) 의상
프리팹 이름에 별다른 접미사가 붙지 않은 오리지널 형태입니다.
- **`prefabAddress`**: 프리팹 원본 이름 (예: `aris`, `mika`)
- **`name`**: `"Basic"`
- **`text`**: `"Basic"`
- **`spriteAddress`**: `{prefabAddress}`에 `_sprite`가 붙은 형태 (예: `aris_sprite`)

### B. 파생(Variant) 의상
수영복, 메이드복, 전투복 등 프리팹 이름에 접미사가 붙은 형태입니다.
- **`prefabAddress`**: 프리팹 이름_접미사 (예: `aris_battle`, `yuzu_maid`, `mika_3d`)
- **`name`**: 언더바(`_`) 뒷부분의 단어들을 대문자로 시작하게 변환한 이름 (예: `Battle`, `Maid`, `3D`, `Battle 2`)
- **`text`**: `name`과 동일한 값
- **`spriteAddress`**: `{prefabAddress}` 뒤에 `_sprite`가 붙은 형태 (예: `aris_battle_sprite`, `mari_3d_idol_sprite`)

---

## 4. 추가 예시

예를 들어, 프리팹 리스트가 `yuzu`, `yuzu_maid`, `yuzu_battle`, `yuzu_battle2` 일 경우 다음과 같이 작성합니다.

```json
{
  "id": 19,
  "name": "Yuzu",
  "clothesList": [
    {
      "name": "Basic",
      "text": "Basic",
      "spriteAddress": "yuzu_sprite",
      "prefabAddress": "yuzu"
    },
    {
      "name": "Maid",
      "text": "Maid",
      "spriteAddress": "yuzu_maid_sprite",
      "prefabAddress": "yuzu_maid"
    },
    {
      "name": "Battle",
      "text": "Battle",
      "spriteAddress": "yuzu_battle_sprite",
      "prefabAddress": "yuzu_battle"
    },
    {
      "name": "Battle 2",
      "text": "Battle 2",
      "spriteAddress": "yuzu_battle2_sprite",
      "prefabAddress": "yuzu_battle2"
    }
  ]
}
```

## 5. ⚠️ 주의사항

1. JSON 형식을 엄격하게 맞추어야 합니다 (특히 따옴표 `"`와 각 객체/배열 끝의 쉼표 `,`).
2. 동일 캐릭터의 파생의상 목록이 중복되지 않게 구성해야 합니다.
3. 데이터 변경 후에는 반드시 JSON 유효성(Validity)을 확인하여 런타임에 Parsing Error가 나지 않도록 대비하세요.
