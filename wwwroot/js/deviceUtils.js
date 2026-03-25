// wwwroot/js/deviceUtils.js
export function isMobile() {
    // 使用 User-Agent 偵測是否為手機
    // 我們排除了 iPad，因為 iPadOS 上的 Safari 通常能正常顯示 iframe PDF
    return /Mobi|Android|iPhone/i.test(navigator.userAgent);
}