using UnityEngine;

// 악세서리 인스턴스를 소켓 볼륨에 맞춰 배치(스케일/회전/위치)하는 공유 로직.
// 런타임(EquipManager)과 에디터 라이브 미리보기가 동일하게 사용 → WYSIWYG 보장.
public static class EquipPlacement
{
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
