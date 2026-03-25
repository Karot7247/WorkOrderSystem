namespace WorkOrderSystem.Models.Line
{
    // ✨ 請全選覆蓋，確保包含所有狀態 ✨
    public enum ConversationState
    {
        None = 0,

        // --- 舊的報修流程狀態 (必須保留) ---
        AwaitingCustomerName,
        ConfirmingCustomerName,
        AwaitingContactPerson,
        ConfirmingContactPerson,
        AwaitingContactPhone,
        ConfirmingContactPhone,
        AwaitingDescription,
        ConfirmingDescription,
        AwaitingPhotoUploadChoice,
        AwaitingPhotos,
        AwaitingFinalConfirmation,

        // --- ✨ 新增：派工系統 (查報價) 專用狀態 ✨ ---
        // 請確認這三行一定要有！
        Quotation_SelectingYear,
        Quotation_SelectingScope,
        Quotation_AwaitingKeyword, // ✨ 新增這行：等待輸入關鍵字
        Quotation_ViewingResults,
            
    }
}