# CharacterDetail Integration Plan

## Goal
Connect the `CharacterDetail` prefab to runtime UI flow while keeping data binding flexible for later character metadata expansion.

## Controller Scope
Create `CharacterDetailController.cs` and attach it to the root `CharacterDetail` prefab.

The controller owns:
- Show/hide lifecycle.
- Prompt area expand/collapse.
- Prompt language dropdown handling.
- Copy/reset prompt buttons.
- Affection value and segmented affection bar display.
- Character portrait, name, source, form, status tags, feature tags.
- Basic counters such as conversation count and costume count.
- Hide button callback.

## Public API
- `Show(ChangeCharInfo charInfo, ChangeCharClothesInfo clothesInfo = null)`
- `Hide()`
- `SetPromptExpanded(bool expanded)`
- `TogglePromptExpanded()`
- `RefreshStaticInfo()`
- `RefreshPrompt()`
- `RefreshStats()`

## Data Sources
Available now:
- `ChangeCharInfo.name`
- `ChangeCharInfo.clothesList`
- `ChangeCharClothesInfo.spriteAddress`
- `ChangeCharClothesInfo.isLocal`
- `ChangeCharClothesInfo.isSelectable`
- `ChangeCharClothesInfo.charAttr_charcode`
- `ChangeCharClothesInfo.charAttr_type`
- `ApiGeminiCharacterDataManager.GetCharacterPrompt(charName, lang)`
- `MemoryManager.GetAllConversationMemory(targetNickname)`

Not available yet:
- Character source such as `원신`, `트릭컬`, `블루아카이브`, `오리지널`.
- Detailed feature tag list.
- Affection save data.
- Voice selection persistence.

Until the missing data model exists, the controller exposes serialized defaults/fallbacks and helper methods so later data can be injected without prefab restructuring.

## UIManager Integration
Add a `characterDetail` managed UI field.

Methods:
- `ShowCharacterDetail(ChangeCharInfo charInfo, ChangeCharClothesInfo clothesInfo = null)`
- `CloseCharacterDetail()`
- `ToggleCharacterDetail(ChangeCharInfo charInfo, ChangeCharClothesInfo clothesInfo = null)`

Position comes from:
- `UIPositionManager.GetMenuPosition("characterDetail")`

## CharChange Long Press
Add long-press behavior to:
- `ChangeCharCardController`
- `ChangeCharListSlotController`

Behavior:
- Pointer down starts a 2 second timer.
- Pointer up/exit cancels if the threshold was not reached.
- If threshold is reached, show `CharacterDetail` through `UIManager`.
- After long press fires, suppress the normal character-change click once.

## Prefab Wiring
Attach `CharacterDetailController` to root `CharacterDetail`.

Add a top hide button to the prefab root and wire it through serialized field/manual Inspector assignment.
For first implementation, the controller also tries to auto-bind common child names in `Awake` to reduce setup friction.
