import re
with open(r"e:\UnityProject\AICO\Assets\Scripts\CharManager.cs", "r", encoding="utf-8") as f:
    content = f.read()
pattern = r"(foreach \(var charInfo in _characterDatabaseData\.characters\)\s*\{\s*if \(charInfo == null \|\| charInfo\.clothesList == null\) continue;\s*foreach \(var clothes in charInfo\.clothesList\)\s*\{\s*)(if \(clothes != null && !string\.IsNullOrEmpty\(clothes\.charAttr_charcode\) && clothes\.charAttr_charcode\.ToLower\(\) == characterId\)\s*\{\s*return charInfo;\s*\})"
replacement = r"""\g<1>if (clothes == null) continue;
                if (!string.IsNullOrEmpty(clothes.charAttr_charcode) && clothes.charAttr_charcode.ToLower() == characterId)
                {
                    return charInfo;
                }
                if (!string.IsNullOrEmpty(clothes.name) && clothes.name.ToLower() == characterId)
                {
                    return charInfo;
                }
                if (!string.IsNullOrEmpty(clothes.prefabAddress) && clothes.prefabAddress.ToLower() == characterId)
                {
                    return charInfo;
                }"""
new_content = re.sub(pattern, replacement, content, count=1)
with open(r"e:\UnityProject\AICO\Assets\Scripts\CharManager.cs", "w", encoding="utf-8") as f:
    f.write(new_content)
print("Patched CharManager")
