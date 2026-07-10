using UnityEngine;

// 악세서리 인스턴스를 소켓 볼륨/placeholder에 맞춰 배치(스케일/회전/위치)하는 공유 로직.
// 런타임(EquipManager)과 에디터 라이브 미리보기가 동일하게 사용 → WYSIWYG 보장.
public static class EquipPlacement
{
    // placeholder에 악세서리 배치 (신규 경로). 크기=캡슐 반경 비례, 접촉=contactAnchor 규약.
    public static void FitToPlaceholder(GameObject inst, EquipSocket socket, EquipPlaceholder placeholder, EquipEntry entry)
    {
        if (inst == null || socket == null || placeholder == null || entry == null)
        {
            return;
        }

        CapsuleCollider cap = socket.SizingVolume as CapsuleCollider;

        // 1) 고유 크기 측정 (원점/identity/scale1)
        inst.transform.SetParent(null, false);
        inst.transform.position = Vector3.zero;
        inst.transform.rotation = Quaternion.identity;
        inst.transform.localScale = Vector3.one;

        float natural;
        Vector3 accCenter;
        Vector3 accExtents;
        bool measured = EquipFitter.MeasureNaturalFull(inst, out natural, out accCenter, out accExtents);

        // 2) 크기 기준 사다리: refDist(신모델, 부모-로컬 베이크 — 캐릭터가 커지면 같이 큼) → 캡슐 → 둘 다 없으면 거부
        float rWorld = 0f;
        if (placeholder.bakedRefDistLocal > 1e-12f)
        {
            rWorld = placeholder.bakedRefDistLocal * EquipMath.LossyAvg(socket.transform);
        }
        else
        {
            if (cap != null)
            {
                rWorld = cap.radius * EquipMath.LossyAvg(socket.transform);
            }
        }

        float scale = 1f;
        if (measured)
        {
            // 월드 목표 크기 계산 후, 부모(placeholder) lossyScale로 나눠 로컬 스케일로 환산
            // (나누지 않으면 루트 20000 캐릭터에서 2만 배 크기로 폭발)
            float parentLossy = EquipMath.LossyAvg(placeholder.transform);
            float worldTarget = 0f;

            if (entry.fitMode == EquipEntryFit.RadiusRelative && rWorld > 1e-9f)
            {
                worldTarget = 2f * rWorld * entry.sizeRatio;
            }
            else
            {
                if (cap != null)
                {
                    worldTarget = EquipFitter.GetVolumeLength(cap) * EquipMath.LossyAvg(socket.transform);
                }
                else
                {
                    if (rWorld > 1e-9f)
                    {
                        // ContainUniform 엔트리 + 캡슐 없는 신모델 소켓 → refDist 기반 자동 폴백
                        worldTarget = 2f * rWorld * entry.sizeRatio;
                    }
                }
            }

            if (worldTarget > 1e-9f)
            {
                scale = EquipFitter.ComputeFitScale(worldTarget, natural) / parentLossy;
            }
            else
            {
                // 크기 기준 전무(캡슐/refDist 모두 부재) → 폭발 방지 거부
                Debug.LogWarning($"[EquipPlacement] '{socket.slotId}/{placeholder.placeholderId}' 크기 기준 없음(캡슐·refDist 부재) — 장착 거부.");
                DestroySafe(inst);
                return;
            }
        }
        scale = scale * entry.fitBias;

        // 3) placeholder 하위 부착 + 회전/스케일
        inst.transform.SetParent(placeholder.transform, false);
        inst.transform.localRotation = Quaternion.Euler(entry.rotationOffset);
        inst.transform.localScale = Vector3.one * scale;

        // 4) 접촉 규약 — 결정적 계산.
        // Pivot = 모델 원점(0,0,0)을 placeholder 점에 그대로 (피벗 기준 저작 악세서리, 레거시 equip 감각).
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

        // 5) 아이템 고유 오프셋 (캡슐 radius 배수 단위 → 월드 환산, placeholder 프레임)
        if (rWorld > 1e-9f)
        {
            Vector3 offsetWorld =
                placeholder.transform.right * (entry.positionOffsetRadii.x * rWorld) +
                placeholder.transform.up * (entry.positionOffsetRadii.y * rWorld) +
                placeholder.transform.forward * (entry.positionOffsetRadii.z * rWorld);
            inst.transform.position = inst.transform.position + offsetWorld;
        }
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

    // 인스턴스의 렌더러 합산 월드 바운드
    private static bool TryGetWorldBounds(GameObject inst, out Bounds bounds)
    {
        bounds = new Bounds();
        Renderer[] rs = inst.GetComponentsInChildren<Renderer>();
        bool has = false;

        foreach (Renderer r in rs)
        {
            if (r == null)
            {
                continue;
            }

            if (has == false)
            {
                bounds = r.bounds;
                has = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }
        return has;
    }

    // inst를 socket 하위에 볼륨-핏 + 오프셋 적용해 배치한다 (레거시 직부착 — 캡슐 필수).
    public static void Fit(GameObject inst, EquipSocket socket, float fitBias, Vector3 positionOffset, Vector3 rotationOffset)
    {
        if (inst == null || socket == null)
        {
            return;
        }

        // 캡슐 없는 소켓의 직부착은 크기 기준이 없어 극단 스케일에서 폭발 — 거부 (placeholder 경로 사용)
        if (socket.SizingVolume == null)
        {
            Debug.LogWarning($"[EquipPlacement] '{socket.slotId}' 소켓 직부착은 캡슐이 필요합니다 — 장착 거부 (신모델 소켓은 placeholder 경로 사용).");
            DestroySafe(inst);
            return;
        }

        // 1) 고유 크기 측정: 원점/identity/scale1
        inst.transform.SetParent(null, false);
        inst.transform.position = Vector3.zero;
        inst.transform.rotation = Quaternion.identity;
        inst.transform.localScale = Vector3.one;

        float natural;
        Vector3 accCenter;
        bool measured = EquipFitter.MeasureNatural(inst, out natural, out accCenter);

        // 2) 볼륨-핏 스케일 계산 (콜라이더 있을 때만; 없으면 fitBias만)
        float scale = 1f;
        Collider volume = socket.SizingVolume;
        if (socket.fit == EquipFitMode.ContainUniform && volume != null && measured)
        {
            float volumeLength = EquipFitter.GetVolumeLength(volume);
            scale = EquipFitter.ComputeFitScale(volumeLength, natural);
        }
        scale = scale * fitBias;

        // 3) 소켓 하위 배치. 회전 = rotationOffset
        inst.transform.SetParent(socket.transform, false);
        inst.transform.localScale = Vector3.one * scale;
        inst.transform.localRotation = Quaternion.Euler(rotationOffset);

        // 4) 위치 정렬
        if (socket.pivot == EquipAnchorPivot.PlaceholderChild && socket.placeholderAnchor != null)
        {
            // placeholder 기준 + 오프셋
            inst.transform.position = socket.placeholderAnchor.position;
            inst.transform.rotation = socket.placeholderAnchor.rotation * Quaternion.Euler(rotationOffset);
            inst.transform.localScale = Vector3.one * scale;
            inst.transform.localPosition = inst.transform.localPosition + positionOffset;
        }
        else if (volume != null)
        {
            // 볼륨 center + 오프셋
            Vector3 volumeCenter = EquipFitter.GetVolumeCenter(volume);
            inst.transform.localPosition = volumeCenter + positionOffset;
        }
        else
        {
            // 소켓 원점 + 오프셋
            inst.transform.localPosition = positionOffset;
        }
    }
}
