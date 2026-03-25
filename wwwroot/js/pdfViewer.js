// wwwroot/js/pdfViewer.js

// 設定 PDF.js Worker 的路徑 (從 CDN 載入)
// 確保版本號 (2.16.105) 與 App.razor 中引入的 pdf.js 版本一致
pdfjsLib.GlobalWorkerOptions.workerSrc = 'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/2.16.105/pdf.worker.min.js';

let currentPdfDoc = null;
let currentCanvas = null;

// 將 Base64 字串轉換為 Uint8Array
function base64ToUint8Array(base64) {
    var binary_string = window.atob(base64);
    var len = binary_string.length;
    var bytes = new Uint8Array(len);
    for (var i = 0; i < len; i++) {
        bytes[i] = binary_string.charCodeAt(i);
    }
    return bytes;
}

// 供 Blazor 呼叫：載入 PDF 文件
export function loadPdf(base64Data, canvasElementId) {
    // 將 base64 轉為二進制陣列
    const pdfData = base64ToUint8Array(base64Data);

    // 取得 Canvas 元素
    currentCanvas = document.getElementById(canvasElementId);
    if (!currentCanvas) {
        console.error(`Canvas element with ID '${canvasElementId}' not found.`);
        return Promise.reject("Canvas element not found");
    }

    // 異步載入 PDF
    var loadingTask = pdfjsLib.getDocument({ data: pdfData });
    return loadingTask.promise.then(function (pdfDoc_) {
        console.log('PDF loaded');
        currentPdfDoc = pdfDoc_;

        // 返回總頁數給 Blazor
        return { totalPages: pdfDoc_.numPages };
    }).catch(function (reason) {
        console.error('Error loading PDF: ' + reason);
        return Promise.reject(reason);
    });
}

// 供 Blazor 呼叫：渲染指定頁面
export function renderPage(pageNum) {
    if (!currentPdfDoc || !currentCanvas) {
        console.error("PDF doc or canvas is not initialized.");
        return Promise.reject("PDF not loaded");
    }

    if (pageNum < 1 || pageNum > currentPdfDoc.numPages) {
        console.error("Invalid page number: " + pageNum);
        return Promise.reject("Invalid page number");
    }

    // 取得頁面
    return currentPdfDoc.getPage(pageNum).then(function (page) {
        console.log('Page loaded: ' + pageNum);

        // --- 這裡是高畫質渲染的關鍵 ---

        // 1. 偵測設備像素比 (例如 Retina 螢幕可能是 2 或 3)
        const devicePixelRatio = window.devicePixelRatio || 1;

        // 2. 設定一個基礎縮放比例 (例如您原本的 1.5)
        const baseScale = 1.5;

        // 3. 取得以此基礎比例計算的 viewport (用來計算尺寸)
        var viewport = page.getViewport({ scale: baseScale });

        // 4. 取得 Canvas 2D 上下文
        var context = currentCanvas.getContext('2d');

        // 5. 【重要】放大 Canvas 的 *畫布* 尺寸，乘以 devicePixelRatio
        //    這會讓畫布的像素量變成 2x 或 3x，以匹配螢幕物理像素
        currentCanvas.height = Math.floor(viewport.height * devicePixelRatio);
        currentCanvas.width = Math.floor(viewport.width * devicePixelRatio);

        // 6. 【重要】使用 CSS 來 *縮小* Canvas 的 *顯示* 尺寸，讓它保持視覺上的 1.5 倍大小
        //    這樣瀏覽器就不會自己放大低解析度圖片了
        currentCanvas.style.height = Math.floor(viewport.height) + "px";
        currentCanvas.style.width = Math.floor(viewport.width) + "px";

        // 7. 告訴 PDF.js 在這個 *高解析度* 畫布上渲染
        //    我們需要一個新的 transform 來匹配放大的畫布
        var renderContext = {
            canvasContext: context,
            viewport: viewport,
            // 關鍵：告訴 PDF.js 畫布已經被 devicePixelRatio 縮放過了
            transform: [devicePixelRatio, 0, 0, devicePixelRatio, 0, 0]
        };

        var renderTask = page.render(renderContext);
        return renderTask.promise.then(function () {
            console.log('Page ' + pageNum + ' rendered at ' + devicePixelRatio + 'x');
            return true; // 返回成功
        });
    });
}

// 供 Blazor 呼叫：清理資源
export function cleanup() {
    currentPdfDoc = null;
    currentCanvas = null;
    console.log("PDF viewer cleaned up.");
}