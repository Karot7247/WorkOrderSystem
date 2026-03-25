let canvas = null;
let panzoomInstance = null;
let currentTool = 'pencil';
// 繪圖變數
let isMouseDown = false;
let origX = 0;
let origY = 0;
let activeShape = null;
let undoStack = [];
let redoStack = [];

// 限制 Canvas 解析度 (避免手機記憶體不足)
const MAX_CANVAS_DIMENSION = 2500;

export function initialize(canvasId, imageUrl, isDrawingMode) {
    // 延遲執行，確保 DOM 已經長出來了
    setTimeout(() => {
        setupCanvas(canvasId, imageUrl, isDrawingMode);
    }, 100);
}

function setupCanvas(canvasId, imageUrl, isDrawingMode) {
    if (canvas) { canvas.dispose(); }

    canvas = new fabric.Canvas(canvasId, {
        isDrawingMode: false,
        selection: false,
        allowTouchScrolling: true, // 允許 Fabric 傳遞觸控事件
        containerClass: 'canvas-container',
        enableRetinaScaling: true,
        renderOnAddRemove: true
    });

    // 綁定繪圖事件
    canvas.on('path:created', (e) => {
        e.path.set({ selectable: false, evented: false });
        undoStack.push(e.path);
        redoStack = [];
    });
    canvas.on('mouse:down', onMouseDown);
    canvas.on('mouse:move', onMouseMove);
    canvas.on('mouse:up', onMouseUp);

    setImage(imageUrl, isDrawingMode);
}

export function setImage(imageUrl, isDrawingMode) {
    if (!canvas) return;
    resetUndoRedoStacks();
    canvas.clear();

    fabric.Image.fromURL(imageUrl, function (img) {

        // --- 1. 計算高解析度 Canvas 尺寸 ---
        let targetWidth = img.width;
        let targetHeight = img.height;
        let canvasScale = 1;

        // 如果圖太大，等比縮小到限制尺寸 (保持清晰度但省記憶體)
        if (img.width > MAX_CANVAS_DIMENSION || img.height > MAX_CANVAS_DIMENSION) {
            canvasScale = Math.min(MAX_CANVAS_DIMENSION / img.width, MAX_CANVAS_DIMENSION / img.height);
            targetWidth = img.width * canvasScale;
            targetHeight = img.height * canvasScale;
        }

        // --- 2. 設定 Canvas 實體尺寸 ---
        canvas.setDimensions({ width: targetWidth, height: targetHeight });

        // --- 3. 設定圖片為背景 (居中填滿 Canvas) ---
        canvas.setBackgroundImage(img, canvas.renderAll.bind(canvas), {
            originX: 'left',
            originY: 'top',
            scaleX: canvasScale,
            scaleY: canvasScale
        });

        // --- 4. 抓取並清理 Wrapper (重要！) ---
        const fabricWrapper = canvas.getElement().parentElement;

        // 強制用 JS 清除干擾，確保它是乾淨的絕對定位元素
        fabricWrapper.style.margin = '0px';
        fabricWrapper.style.padding = '0px';
        fabricWrapper.style.border = 'none';
        fabricWrapper.style.position = 'absolute';
        fabricWrapper.style.transformOrigin = '0 0'; // 物理基準點
        fabricWrapper.style.left = '0px';
        fabricWrapper.style.top = '0px';

        // --- 5. 初始化 Panzoom ---
        initPanzoom(fabricWrapper);

        // --- 6. 設定模式 ---
        toggleDrawing(isDrawingMode);

    }, { crossOrigin: 'anonymous' });
}

function initPanzoom(element) {
    const container = document.getElementById('panzoom-container');

    if (panzoomInstance) {
        panzoomInstance.dispose();
    }

    // 初始化 Panzoom
    panzoomInstance = Panzoom(element, {
        maxScale: 10,
        minScale: 0.1,
        cursor: 'default',
        excludeClass: 'is-drawing-active',
        noBind: false,
        touchAction: 'none', // 禁止瀏覽器預設縮放
        origin: '0 0',       // 確保跟 CSS 一樣是左上角

        // 解決手機點擊時可能產生的位移
        click: false
    });

    // 綁定滾輪
    container.addEventListener('wheel', panzoomInstance.zoomWithWheel);

    // ★ 初始化後，立刻執行一次「計算並置中」
    setTimeout(() => {
        zoomToFit(false); // false = 無動畫瞬間完成
    }, 50);
}

// 🔥 [核心函式]：強制計算置中座標
// 不管現在在哪裡，直接算出這一刻該在哪裡，然後跳過去
function zoomToFit(animate = true) {
    if (!panzoomInstance || !canvas) return;

    const container = document.getElementById('panzoom-container');

    // 1. 取得當前容器 (螢幕) 大小
    // 使用 getBoundingClientRect 最準，可以包含小數點
    const containerRect = container.getBoundingClientRect();
    const cW = containerRect.width;
    const cH = containerRect.height;

    // 2. 取得 Canvas (圖片) 大小
    const contentW = canvas.width;
    const contentH = canvas.height;

    // 3. 計算「完美塞入」的縮放比例 (Contain)
    const scaleX = cW / contentW;
    const scaleY = cH / contentH;
    // 取最小值，並留 5% 邊距，確保不會貼邊
    const targetScale = Math.min(scaleX, scaleY) * 0.95;

    // 4. 計算「置中」的左上角座標
    // 公式： ( 螢幕寬 - (圖片寬 * 縮放) ) / 2
    // 這算出來的是：圖片左邊邊緣距離螢幕左邊邊緣的距離
    const targetX = (cW - (contentW * targetScale)) / 2;
    const targetY = (cH - (contentH * targetScale)) / 2;

    // 5. 執行指令
    if (animate) {
        panzoomInstance.zoom(targetScale, { animate: true });
        panzoomInstance.pan(targetX, targetY, { animate: true });
    } else {
        // 無動畫 (瞬間設定)
        panzoomInstance.zoom(targetScale, { animate: false });
        panzoomInstance.pan(targetX, targetY, { animate: false });
    }
}

export function resetZoom() {
    zoomToFit(true);
}

// --- 按鈕縮放 (以螢幕中心為焦點) ---
export function zoomIn() {
    if (panzoomInstance) {
        const container = document.getElementById('panzoom-container');
        const rect = container.getBoundingClientRect();
        // 螢幕中心
        const cx = rect.width / 2;
        const cy = rect.height / 2;
        panzoomInstance.zoomIn({ animate: true, focal: { x: cx, y: cy } });
    }
}

export function zoomOut() {
    if (panzoomInstance) {
        const container = document.getElementById('panzoom-container');
        const rect = container.getBoundingClientRect();
        const cx = rect.width / 2;
        const cy = rect.height / 2;
        panzoomInstance.zoomOut({ animate: true, focal: { x: cx, y: cy } });
    }
}

// --- 下載 ---
export function downloadImage(fileName) {
    if (!canvas) return;
    // 因為我們在 setImage 裡可能縮小了 Canvas (為了效能)，下載時要放大回去
    // 這裡簡單用 1 / (Canvas寬 / 原圖寬) 來算
    // 但因為我们在 setImage 沒有存 originalImageWidth 在全域，這裡改用簡單方式：
    // 直接輸出高品質原圖大小 (multiplier = 1 表示輸出 Canvas 當前大小)
    // 如果需要原圖解析度，需要將 setImage 裡的 canvasScale 存到全域變數

    // 簡單修復：重新取得 scale
    const bgImage = canvas.backgroundImage;
    let multiplier = 1;
    if (bgImage) {
        // 原圖寬 / Canvas寬
        multiplier = 1 / bgImage.scaleX;
    }

    const dataURL = canvas.toDataURL({
        format: 'jpeg',
        quality: 0.85,
        multiplier: multiplier
    });
    const link = document.createElement('a');
    link.download = fileName;
    link.href = dataURL;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
}

// --- 繪圖工具 (保持不變，但加上防呆) ---
export function toggleDrawing(isDrawing) {
    if (!canvas || !panzoomInstance) return;
    const upperCanvas = canvas.upperCanvasEl;

    if (isDrawing) {
        panzoomInstance.setOptions({ disablePan: true, disableZoom: true, cursor: 'crosshair' });
        if (upperCanvas) upperCanvas.classList.add('is-drawing-active');

        if (!canvas.freeDrawingBrush) canvas.freeDrawingBrush = new fabric.PencilBrush(canvas);

        // 動態調整筆刷大小 (讓它在不同解析度下看起來差不多粗)
        const baseWidth = 5;
        const scaleFactor = canvas.width / 1000;
        canvas.freeDrawingBrush.width = baseWidth * scaleFactor;

        canvas.freeDrawingBrush.color = canvas.currentBrushColor || "#FF0000";
        canvas.isDrawingMode = (currentTool === 'pencil');
    } else {
        panzoomInstance.setOptions({ disablePan: false, disableZoom: false, cursor: 'grab' });
        if (upperCanvas) upperCanvas.classList.remove('is-drawing-active');
        canvas.isDrawingMode = false;
    }
}

export function setTool(toolName) {
    currentTool = toolName;
    if (canvas && canvas.upperCanvasEl.classList.contains('is-drawing-active')) {
        canvas.isDrawingMode = (toolName === 'pencil');
    }
}

export function setBrushColor(color) {
    if (canvas && canvas.freeDrawingBrush) {
        canvas.freeDrawingBrush.color = color;
        canvas.currentBrushColor = color;
    }
}

function onMouseDown(o) {
    if (!canvas || !canvas.upperCanvasEl.classList.contains('is-drawing-active') || currentTool === 'pencil') return;
    isMouseDown = true;
    const pointer = canvas.getPointer(o.e);
    origX = pointer.x;
    origY = pointer.y;
    const color = canvas.freeDrawingBrush ? canvas.freeDrawingBrush.color : "#FF0000";
    const scaleFactor = canvas.width / 1000;
    const strokeW = 5 * scaleFactor;

    if (currentTool === 'rect') {
        activeShape = new fabric.Rect({
            left: origX, top: origY, width: 0, height: 0,
            stroke: color, strokeWidth: strokeW, fill: 'transparent', strokeUniform: true
        });
    } else if (currentTool === 'circle') {
        activeShape = new fabric.Ellipse({
            left: origX, top: origY, rx: 0, ry: 0,
            stroke: color, strokeWidth: strokeW, fill: 'transparent', strokeUniform: true
        });
    }
    if (activeShape) {
        activeShape.set({ selectable: false, evented: false });
        canvas.add(activeShape);
    }
}

function onMouseMove(o) {
    if (!isMouseDown || !activeShape) return;
    const pointer = canvas.getPointer(o.e);
    if (currentTool === 'rect') {
        if (pointer.x < origX) activeShape.set({ left: pointer.x });
        if (pointer.y < origY) activeShape.set({ top: pointer.y });
        activeShape.set({ width: Math.abs(origX - pointer.x), height: Math.abs(origY - pointer.y) });
    } else if (currentTool === 'circle') {
        const rx = Math.abs(origX - pointer.x) / 2;
        const ry = Math.abs(origY - pointer.y) / 2;
        const newLeft = (pointer.x < origX) ? pointer.x : origX;
        const newTop = (pointer.y < origY) ? pointer.y : origY;
        activeShape.set({ rx: rx, ry: ry, left: newLeft, top: newTop });
    }
    canvas.requestRenderAll();
}

function onMouseUp(o) {
    if (isMouseDown && activeShape) {
        activeShape.set({ selectable: false, evented: false });
        undoStack.push(activeShape);
        redoStack = [];
        activeShape = null;
    }
    isMouseDown = false;
}

export function undo() { if (canvas && undoStack.length > 0) { const obj = undoStack.pop(); redoStack.push(obj); canvas.remove(obj); canvas.renderAll(); } }
export function redo() { if (canvas && redoStack.length > 0) { const obj = redoStack.pop(); undoStack.push(obj); canvas.add(obj); canvas.renderAll(); } }
function resetUndoRedoStacks() { undoStack = []; redoStack = []; }
export function clearCanvas() { if (canvas) { const bg = canvas.backgroundImage; canvas.clear(); canvas.setBackgroundImage(bg, canvas.renderAll.bind(canvas)); resetUndoRedoStacks(); } }
export function destroy() { if (canvas) { canvas.dispose(); canvas = null; } if (panzoomInstance) { panzoomInstance.destroy(); panzoomInstance = null; } undoStack = null; redoStack = null; activeShape = null; }