# CharacterDetail UI Prefab Plan

## Goal
Create a wide character detail UI prefab only, without binding scripts or runtime behavior.

## Target Output
- `Assets/Prefabs/UI/CharacterDetail/CharacterDetail.prefab`
- A 1000 x 600 root panel suitable for filling roughly half of the main canvas.
- No outer scrollbar. Only the prompt input area may scroll internally through TMP input field behavior.

## Layout
1. Root panel
   - Rounded dark background image.
   - Horizontal layout split into portrait column and information column.
2. Portrait column
   - Large character portrait placeholder.
   - Status tag row for `사용가능`, `다운로드필요`.
   - Affection container with `호감도 0/100` and `친밀`.
3. Information column
   - Name TextMeshPro title.
   - Source and form fields.
   - Available feature tags as rounded text pills.
   - Voice TMP dropdown with hardcoded options `남자1~15`, `여자1~25`.
   - Prompt area with copy/reset buttons in the upper right and multiline TMP input field.
   - Affinity(인연도) TMP dropdown and direct input field, initially inactive.
   - Conversation count, costume count, and default alarm voice buttons.

## Notes
- Components are named clearly so future scripts can find and bind them.
- Status and feature tags are individual GameObjects with rounded Image + TMP text so they can later be mapped and toggled active/inactive.
- No custom runtime script will be added in this pass.
