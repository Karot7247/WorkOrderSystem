// wwwroot/js/signature.js (最終完整版)

let signaturePad;
let canvasElement;
let resizeTimer;

// 將 resize 邏輯獨立出來，並加入延遲以提升效能
function resizeCanvas() {
    if (!canvasElement || !signaturePad) return;

    // 使用一個計時器來避免過於頻繁地觸發 resize，例如在拖動視窗邊緣時
    clearTimeout(resizeTimer);
    resizeTimer = setTimeout(() => {
        const ratio = Math.max(window.devicePixelRatio || 1, 1);
        canvasElement.width = canvasElement.offsetWidth * ratio;
        canvasElement.height = canvasElement.offsetHeight * ratio;
        canvasElement.getContext("2d").scale(ratio, ratio);
        signaturePad.clear(); // 重新設定大小後清除畫布
    }, 250);
}

// --- Signature Pad Functions ---
export function initById(canvasId) {
    canvasElement = document.getElementById(canvasId);
    if (!canvasElement) {
        console.error(`Signature pad canvas element with id '${canvasId}' not found!`);
        return;
    }

    signaturePad = new SignaturePad(canvasElement, {
        backgroundColor: 'rgb(255, 255, 255)'
    });

    // 監聽視窗大小變化事件 (包含螢幕旋轉)
    window.addEventListener("resize", resizeCanvas);
    // 立即執行一次，確保初始大小正確
    resizeCanvas();
}

export function clear() {
    if (signaturePad) {
        signaturePad.clear();
    }
}

export function save() {
    if (signaturePad && !signaturePad.isEmpty()) {
        // 回傳 Base64 格式的 PNG 圖片字串
        return signaturePad.toDataURL('image/png');
    }
    return null;
}

export function dispose() {
    // 移除監聽，避免記憶體洩漏
    window.removeEventListener("resize", resizeCanvas);
    if (signaturePad) {
        signaturePad.off();
    }
    signaturePad = null;
    canvasElement = null;
}

// --- Bootstrap Modal Functions ---
export function showModal(modalId) {
    const modalElement = document.getElementById(modalId);
    if (modalElement) {
        const modal = new bootstrap.Modal(modalElement);
        modal.show();
    }
}

export function hideModal(modalId) {
    const modalElement = document.getElementById(modalId);
    if (modalElement) {
        const modal = bootstrap.Modal.getInstance(modalElement);
        if (modal) {
            modal.hide();
        }
    }
}