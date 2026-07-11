using UnityEngine;

// 악세서리 인스턴스를 placeholder(부착점)에 맞춰 배치(스케일/회전/위치)하는 공유 로직.
// 런타임(EquipManager)과 에디터 라이브 미리보기·고스트가 동일하게 사용 → WYSIWYG 보장.
public static class EquipPlacement
{
    // placeholder에 악세서리 배치. 크기 = refDist 비례, 접촉 = contactAnchor 규약.
    // 반환 false = 배치 거부(크기 기준 없음 등) — 인스턴스는 내부에서 파괴됨.
    // (Play 모드의 Destroy는 프레임 말 지연이라 호출측의 == null 검사가 통하지 않으므로 반드시 반환값으로 판정)
    public static bool FitToPlaceholder(GameObject inst, EquipSocket socket, EquipPlaceholder placeholder, EquipEntry entry)
    {
        if (inst == null || socket == null || placeholder == null || entry == null)
        {
            return false;
        }

        // 1) 고유 크기 측정 (원점/identity/scale1)
        inst.transform.SetParent(null, false);
        inst.transform.position = Vector3.zero;
        inst.transform.rotation = Quaternion.identity;
        inst.transform.localScale = Vector3.one;

        float natural;
        Vector3 accCenter;
        Vector3 accExtents;
        bool measured = EquipFitter.MeasureNaturalFull(inst, out natural, out accCenter, out accExtents);

        // 2) 크기 기준: refDist 단독 (부모-로컬 베이크 — 캐릭터가 커지면 lossy를 타고 같이 큼). 미베이크(0)면 거부.
        float rWorld = 0f;
        if (placeholder.bakedRefDistLocal > 1e-12f)
        {
            rWorld = placeholder.bakedRefDistLocal * EquipMath.LossyAvg(socket.transform);
        }

        float scale = 1f;
        if (measured)
        {
            // 월드 목표 크기 계산 후, 부모(placeholder) lossyScale로 나눠 로컬 스케일로 환산
            // (나누지 않으면 루트 20000 캐릭터에서 2만 배 크기로 폭발)
            float parentLossy = EquipMath.LossyAvg(placeholder.transform);

            if (rWorld > 1e-9f)
            {
                float worldTarget = 2f * rWorld * entry.sizeRatio;
                scale = EquipFitter.ComputeFitScale(worldTarget, natural) / parentLossy;
            }
            else
            {
                // 크기 기준 없음(refDist 미베이크) → 폭발 방지 거부
                Debug.LogWarning($"[EquipPlacement] '{socket.slotId}/{placeholder.placeholderId}' refDist 미베이크 — 장착 거부.");
                DestroySafe(inst);
                return false;
            }
        }
        scale = scale * entry.fitBias;

        // 3) placeholder 하위 부착 + 회전/스케일
        inst.transform.SetParent(placeholder.transform, false);
        inst.transform.localRotation = Quaternion.Euler(entry.rotationOffset);
        inst.transform.localScale = Vector3.one * scale;

        // 4) 접촉 규약 — 결정적 계산.
        // Pivot = 모델 원점(0,0,0)을 placeholder 점에 그대로 (피벗 기준 저작 악세서리, 기본).
        // Center/BottomAlign = identity에서 측정한 center/extents를 현재 회전·스케일로 환산해 바운드 정렬
        // (Renderer.bounds는 이동 직후 같은 프레임에 stale할 수 있으므로 읽지 않는다).
        if (measured && placeholder.contactAnchor != EquipContactAnchor.Pivot)
        {
            float worldScale = scale * EquipMath.LossyAvg(placeholder.transform);
            Quaternion worldRot = inst.transform.rotation;

            Vector3 worldCenterOffset = worldRot * (accCenter * worldScale);
            inst.transform.position = placeholder.transform.position - worldCenterOffset;

            // BottomAlign = 회전된 AABB의 up 방향 반치수만큼 올림 (파묻힘 방지)
            if (placeholder.contactAnchor == EquipContactAnchor.BottomAlign)
            {
                Vector3 up = placeholder.transform.up;
                float extentAlongUp =
                    Mathf.Abs(Vector3.Dot(up, worldRot * Vector3.right)) * accExtents.x +
                    Mathf.Abs(Vector3.Dot(up, worldRot * Vector3.up)) * accExtents.y +
                    Mathf.Abs(Vector3.Dot(up, worldRot * Vector3.forward)) * accExtents.z;
                inst.transform.position = inst.transform.position + up * (extentAlongUp * worldScale);
            }
        }
        else
        {
            inst.transform.position = placeholder.transform.position;
        }

        // 5) 아이템 고유 오프셋 (rWorld=refDist 월드환산의 배수 단위 → 월드 환산, placeholder 프레임)
        if (rWorld > 1e-9f)
        {
            Vector3 offsetWorld =
                placeholder.transform.right * (entry.positionOffsetRadii.x * rWorld) +
                placeholder.transform.up * (entry.positionOffsetRadii.y * rWorld) +
                placeholder.transform.forward * (entry.positionOffsetRadii.z * rWorld);
            inst.transform.position = inst.transform.position + offsetWorld;
        }

        return true;
    }

    // 장착 거부 시 인스턴스 파괴 (플레이=지연, 에딧=즉시)
    private static void DestroySafe(GameObject go)
    {
        if (go == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Object.Destroy(go);
        }
        else
        {
            Object.DestroyImmediate(go);
        }
    }
}
