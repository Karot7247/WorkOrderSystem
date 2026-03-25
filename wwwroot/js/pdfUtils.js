// 函式一：產生 PDF 並返回 Base64 資料
export function generatePdfAsBase64(elementId) {
    const element = document.getElementById(elementId);
    if (!element) {
        console.error("Element to export not found:", elementId);
        // 返回一個解析為 null 的 Promise，這樣 C# 才不會出錯
        return Promise.resolve(null);
    }

    // 找到所有需要操作的元素
    const elementsToHide = document.querySelectorAll('.no-print');
    const elementsToShow = document.querySelectorAll('.only-print');

    // 步驟 1: 套用列印專用樣式
    elementsToHide.forEach(el => el.style.display = 'none');
    elementsToShow.forEach(el => el.style.display = 'block');

    const options = {
        margin: 10,
        image: { type: 'jpeg', quality: 0.98 },
        html2canvas: { scale: 2, useCORS: true },
        jsPDF: { unit: 'mm', format: 'a4', orientation: 'portrait' }
    };

    // 步驟 2: 產生 PDF，並將後續操作串在 .then() 中
    // 這會回傳一個 Promise，Blazor 的 await 會等待它完成
    return html2pdf().from(element).set(options).output('datauristring').then(pdfBase64 => {

        // 步驟 3: PDF 資料產生完畢後，還原頁面樣式
        console.log("PDF data generated. Restoring element styles.");
        elementsToHide.forEach(el => el.style.display = ''); // 恢復預設
        elementsToShow.forEach(el => el.style.display = 'none'); // 再次隱藏

        // 步驟 4: 將 PDF 的 Base64 資料返回給 Blazor
        return pdfBase64;
    });
}

// 函式二：觸發瀏覽器下載 (此函式不變)
export function triggerBrowserDownload(base64Data, fileName) {
    const link = document.createElement('a');
    link.href = base64Data;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
}