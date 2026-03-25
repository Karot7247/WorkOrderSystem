// wwwroot/js/kanban.js (包含視圖記憶功能)

// 確保不會重複宣告
window.kanban = window.kanban || {};

// 內部變數
let draggedTaskId = null;
let dotnetReference = null;

// --- 初始化 ---
window.kanban.init = function (dotNetRef) {
    if (dotnetReference) {
        console.log("Kanban JS already initialized. Skipping.");
        return;
    }

    dotnetReference = dotNetRef;
    console.log("🚀 Kanban JS Initialized. Ready.");

    // 1. 綁定 dragstart
    document.addEventListener('dragstart', (e) => {
        const card = e.target.closest('.kanban-card');
        if (card && card.hasAttribute('data-task-id')) {
            draggedTaskId = card.getAttribute('data-task-id');
            e.dataTransfer.effectAllowed = 'move';
            e.dataTransfer.setData('text/plain', draggedTaskId);

            console.log(`✊ Drag Start | ID: ${draggedTaskId}`);

            // 視覺效果
            setTimeout(() => card.style.opacity = '0.5', 0);
        }
    });

    // 2. 綁定 dragend
    document.addEventListener('dragend', (e) => {
        const card = e.target.closest('.kanban-card');
        if (card) card.style.opacity = '1';
    });
};

// --- 視圖記憶功能 (新增) ---
window.kanban.saveView = function (viewName) {
    localStorage.setItem('manager_dashboard_view', viewName);
};

window.kanban.loadView = function () {
    return localStorage.getItem('manager_dashboard_view');
};

// --- 拖曳事件處理 ---
window.kanban.handleDragEnter = function (laneId) {
    const lane = document.getElementById(laneId);
    if (lane) lane.classList.add('drag-over');
};

window.kanban.handleDragLeave = function (laneId) {
    const lane = document.getElementById(laneId);
    if (lane) lane.classList.remove('drag-over');
};

window.kanban.handleDrop = function (laneId, targetStatus) {
    const lane = document.getElementById(laneId);
    if (lane) lane.classList.remove('drag-over');

    if (!draggedTaskId || !dotnetReference) {
        console.error("❌ Drop failed: No Task ID found.");
        return;
    }

    console.log(`⬇️ Drop | ID: ${draggedTaskId} -> Status: ${targetStatus}`);

    // 呼叫 C#
    dotnetReference.invokeMethodAsync('ReceiveDrop', parseInt(draggedTaskId), targetStatus);

    draggedTaskId = null;
};