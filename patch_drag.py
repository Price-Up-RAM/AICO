import re
with open(e:\\UnityProject\\AICO\\Assets\\Scripts\\MR\\Input\\MRRayDragAdapter.cs, r, encoding=utf-8) as f:
    content = f.read()
pattern = r(private void HandleHoldStarted\(MRRayProvider provider\)\s*\{\s*if \(_dragging\) return;\s*ResolveRefs\(\);\s*if \(characterRoot == null\)\s*\{[^\}]+\}\s*if \(characterRoot\.CurrentCharacter == null\)\s*\{[^\}]+\}\s*_dragProvider = provider;\s*_dragging = true;)
replacement = r\g<1>\n // 캐릭터를 잡았을 때 ISDK 레이 인터랙터를 비활성화하여 UI가 동시에 잡히는 것을 방지\n var interactors = FindObjectsOfType<Oculus.Interaction.RayInteractor>(false);\n var disabledList = new System.Collections.Generic.List<Oculus.Interaction.RayInteractor>();\n foreach(var r in interactors)\n {\n if (r.enabled)\n {\n r.enabled = false;\n disabledList.Add(r);\n }\n }\n _disabledInteractors = disabledList.ToArray();
new_content = re.sub(pattern, replacement, content, count=1)
pattern2 = r(private void EndDrag\(string reason\)\s*\{\s*if \(!_dragging\) return;)
replacement2 = r\g<1>\n\n if (_disabledInteractors != null)\n {\n foreach (var r in _disabledInteractors)\n {\n if (r != null) r.enabled = true;\n }\n _disabledInteractors = null;\n }
new_content = re.sub(pattern2, replacement2, new_content, count=1)
pattern3 = r(private MRRayProvider _dragProvider;)
replacement3 = r\g<1>\n private Oculus.Interaction.RayInteractor[] _disabledInteractors;
new_content = re.sub(pattern3, replacement3, new_content, count=1)
with open(e:\\UnityProject\\AICO\\Assets\\Scripts\\MR\\Input\\MRRayDragAdapter.cs, w, encoding=utf-8) as f:
    f.write(new_content)
print(Patched MRRayDragAdapter.cs)
