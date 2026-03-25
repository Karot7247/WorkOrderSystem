const DB_NAME = 'WorkOrderDB';
const DB_VERSION = 1;
const STORE_NAME = 'survey_draft';

// 1. 初始化並開啟資料庫
function openDb() {
    return new Promise((resolve, reject) => {
        const request = indexedDB.open(DB_NAME, DB_VERSION);

        // 如果是第一次建立，或版本升級，會執行這裡
        request.onupgradeneeded = (event) => {
            const db = event.target.result;
            // 建立一個 ObjectStore (類似資料表)，主鍵是 'id'
            if (!db.objectStoreNames.contains(STORE_NAME)) {
                db.createObjectStore(STORE_NAME, { keyPath: 'id' });
            }
        };

        request.onsuccess = (event) => {
            resolve(event.target.result);
        };

        request.onerror = (event) => {
            reject('IndexedDB error: ' + event.target.errorCode);
        };
    });
}

// 2. 儲存資料 (C# 呼叫此函數)
export async function saveSurveyData(data) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const transaction = db.transaction([STORE_NAME], 'readwrite');
        const store = transaction.objectStore(STORE_NAME);

        // 我們固定用 'current_draft' 這個 ID，這樣每次都會覆蓋舊的草稿
        // 達到「自動儲存」的效果
        data.id = 'current_draft';

        const request = store.put(data);

        request.onsuccess = () => resolve('Saved');
        request.onerror = (e) => reject(e);
    });
}

// 3. 讀取資料 (C# 呼叫此函數)
export async function loadSurveyData() {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const transaction = db.transaction([STORE_NAME], 'readonly');
        const store = transaction.objectStore(STORE_NAME);

        const request = store.get('current_draft');

        request.onsuccess = (event) => {
            // 如果有資料就回傳，沒資料就回傳 null
            resolve(event.target.result);
        };
        request.onerror = (e) => reject(e);
    });
} // <--- 修正點：loadSurveyData 在這裡結束

// 4. 清除資料 (獨立出來，放在最外層)
export async function clearSurveyData() {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const transaction = db.transaction([STORE_NAME], 'readwrite');
        const store = transaction.objectStore(STORE_NAME);
        const request = store.delete('current_draft'); // 清除草稿
        request.onsuccess = () => resolve('Cleared');
        request.onerror = (e) => reject(e);
    });
}