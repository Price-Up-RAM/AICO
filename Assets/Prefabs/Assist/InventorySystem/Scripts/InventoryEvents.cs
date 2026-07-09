using System;

// InventorySystem 전역 이벤트 허브 (정적). 매니저 → 뷰 단방향 알림용.
public static class InventoryEvents
{
    // 활성 소유자(캐릭터) 변경 시 발동. 인자 = charcode
    public static Action<string> OnActiveOwnerChanged;

    // 스토어 내용 변경 시 발동. 인자 = 변경된 스토어의 ownerId ("MAIN" 또는 charcode).
    // 장착 토글 시에도 해당 charcode로 발동 (장착 하이라이트 갱신용)
    public static Action<string> OnStoreChanged;
}
