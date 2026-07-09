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
        bool measured = EquipFitter.MeasureNatural(inst, out natural, out accCenter);

        // 2) 크기: RadiusRelative = 캡슐 월드지름 × sizeRatio (레거시 ContainUniform은 볼륨 길이)
        float rWorld = 0f;
        if (cap != null)
        {
            rWorld = cap.radius * EquipCapsuleMath.LossyAvg(socket.transform);
        }

        float scale = 1f;
        if (measured)
        {
            if (entry.fitMode == EquipEntryFit.RadiusRelative && rWorld > 1e-9f)
            {
                scale = EquipFitter.ComputeFitScale(2f * rWorld * entry.sizeRatio, natural);
            }
            else
            {
                if (cap != null)
                {
                    float volumeLength = EquipFitter.GetVolumeLength(cap) * EquipCapsuleMath.LossyAvg(socket.transform);
                    scale = EquipFitter.ComputeFitScale(volumeLength, natural);
                }
            }
        }
        scale = scale * entry.fitBias;

        // 3) placeholder 하위 부착 + 회전
        inst.transform.SetParent(placeholder.transform, false);
        inst.transform.localRotation = Quaternion.Euler(entry.rotationOffset);
        inst.transform.localScale = Vector3.one * scale;
        inst.transform.position = placeholder.transform.position;

        // 4) 접촉 규약: BottomAlign = 바운드 바닥(-up 접면)을 placeholder 점에 정렬 (파묻힘 방지)
        if (placeholder.contactAnchor == EquipContactAnchor.BottomAlign && measured)
        {
            Vector3 up = placeholder.transform.up;

            // 배치 후 월드 바운드로 바닥 정렬량 계산
            Bounds b;
            if (TryGetWorldBounds(inst, out b))
            {
                float extentAlongUp = b.extents.x * Mathf.Abs(up.x) + b.extents.y * Mathf.Abs(up.y) + b.extents.z * Mathf.Abs(up.z);
                float minAlongUp = Vector3.Dot(b.center, up) - extentAlongUp;
                float shift = Vector3.Dot(placeholder.transform.position, up) - minAlongUp;
                inst.transform.position = inst.transform.position + up * shift;
            }
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

    // inst를 socket 하위에 볼륨-핏 + 오프셋 적용해 배치한다.
    public static void Fit(GameObject inst, EquipSocket socket, float fitBias, Vector3 positionOffset, Vector3 rotationOffset)
    {
        if (inst == null || socket == null)
        {
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
