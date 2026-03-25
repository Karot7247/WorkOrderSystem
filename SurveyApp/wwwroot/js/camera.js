let currentStream = null;

// 1. 取得裝置清單
export async function getVideoDevices() {
    if (!navigator.mediaDevices || !navigator.mediaDevices.enumerateDevices) {
        return [];
    }
    const devices = await navigator.mediaDevices.enumerateDevices();
    return devices
        .filter(d => d.kind === 'videoinput')
        .map(d => ({ deviceId: d.deviceId, label: d.label || `Camera ${d.deviceId.slice(0, 5)}...` }));
}

// 2. 啟動相機
export async function startCamera(videoElement, deviceId = null) {
    if (navigator.mediaDevices && navigator.mediaDevices.getUserMedia) {
        try {
            stopCamera();

            const constraints = {
                video: {
                    deviceId: deviceId ? { exact: deviceId } : undefined,
                    facingMode: deviceId ? undefined : { ideal: "environment" },
                    // 雖然請求 1920，但為了支援變焦，手機可能會給更高解析度
                    width: { ideal: 1920 },
                    height: { ideal: 1080 }
                }
            };

            const stream = await navigator.mediaDevices.getUserMedia(constraints);
            currentStream = stream;
            videoElement.srcObject = stream;

            await new Promise(resolve => videoElement.onloadedmetadata = resolve);

            const track = stream.getVideoTracks()[0];
            const capabilities = track.getCapabilities ? track.getCapabilities() : {};

            return {
                width: videoElement.videoWidth,
                height: videoElement.videoHeight,
                zoomSupported: 'zoom' in capabilities,
                minZoom: capabilities.zoom ? capabilities.zoom.min : 1,
                maxZoom: capabilities.zoom ? capabilities.zoom.max : 1,
                stepZoom: capabilities.zoom ? capabilities.zoom.step : 0.1
            };

        } catch (err) {
            console.error("Error accessing camera: ", err);
            alert("無法啟動相機: " + err.message);
            return null;
        }
    }
}

// 3. 設定變焦
export async function setZoom(zoomLevel) {
    if (currentStream) {
        const track = currentStream.getVideoTracks()[0];
        const capabilities = track.getCapabilities ? track.getCapabilities() : {};

        if ('zoom' in capabilities) {
            try {
                await track.applyConstraints({
                    advanced: [{ zoom: zoomLevel }]
                });
            } catch (err) {
                console.error("Zoom error:", err);
            }
        }
    }
}

// 4. 切換補光燈
export async function toggleTorch(enable) {
    if (currentStream) {
        const track = currentStream.getVideoTracks()[0];
        const capabilities = track.getCapabilities ? track.getCapabilities() : {};

        if ('torch' in capabilities) {
            try {
                await track.applyConstraints({
                    advanced: [{ torch: enable }]
                });
                return true;
            } catch (err) {
                console.error("Torch error:", err);
                return false;
            }
        }
    }
    return false;
}

// 5. 停止相機
export function stopCamera() {
    const video = document.querySelector('video');

    if (currentStream) {
        currentStream.getTracks().forEach(track => {
            try { track.applyConstraints({ advanced: [{ torch: false }] }); } catch { }
            track.stop();
        });
        currentStream = null;
    }

    if (video && video.srcObject) {
        const tracks = video.srcObject.getTracks();
        tracks.forEach(track => track.stop());
        video.srcObject = null;
    }

    if (document.exitFullscreen) document.exitFullscreen().catch(e => { });
    if (document.webkitExitFullscreen) document.webkitExitFullscreen();
}

// 6. 拍照 (★★★ 關鍵修正：強制縮小圖片以解決卡頓 ★★★)
export function takePicture(dotNetHelper, videoElement) {
    if (!videoElement.videoWidth) return;

    const canvas = document.createElement("canvas");

    // 設定最大寬度為 1920 (HD)，這能保證檔案大小與傳輸速度跟原版一樣快
    const MAX_WIDTH = 1920;
    let width = videoElement.videoWidth;
    let height = videoElement.videoHeight;

    // 如果相機給的解析度太大 (例如 4000px)，我們就縮小它
    if (width > MAX_WIDTH) {
        height *= MAX_WIDTH / width;
        width = MAX_WIDTH;
    }

    canvas.width = width;
    canvas.height = height;

    const ctx = canvas.getContext("2d");
    // 將巨大的視訊流縮小繪製到 1920 的畫布上
    ctx.drawImage(videoElement, 0, 0, width, height);

    // 輸出 JPG 0.8
    const dataUrl = canvas.toDataURL("image/jpeg", 0.8);
    dotNetHelper.invokeMethodAsync('ReceivePhoto', dataUrl);
}

// 7. 全螢幕
export function enterFullScreen() {
    const elem = document.documentElement;
    if (elem.requestFullscreen) {
        elem.requestFullscreen().catch(e => console.log("Fullscreen blocked:", e));
    } else if (elem.webkitRequestFullscreen) {
        elem.webkitRequestFullscreen();
    }
    setTimeout(() => { window.scrollTo(0, 1); }, 100);
}

// 8. 定位
export function getCurrentLocation() {
    return new Promise((resolve, reject) => {
        if (!navigator.geolocation) {
            reject("Geolocation not supported");
        } else {
            navigator.geolocation.getCurrentPosition(
                p => resolve({ latitude: p.coords.latitude, longitude: p.coords.longitude }),
                e => reject(e.message),
                { enableHighAccuracy: true, timeout: 10000, maximumAge: 0 }
            );
        }
    });
}

// 9. 浮水印
export function addWatermark(base64Image, text1, text2) {
    return new Promise((resolve, reject) => {
        const img = new Image();
        img.onload = () => {
            const canvas = document.createElement('canvas');
            canvas.width = img.width;
            canvas.height = img.height;
            const ctx = canvas.getContext('2d');

            ctx.drawImage(img, 0, 0);

            const fontSize = Math.max(20, canvas.width / 30);
            ctx.font = `bold ${fontSize}px Arial`;
            ctx.fillStyle = "white";
            ctx.shadowColor = "black";
            ctx.shadowBlur = 4;
            ctx.lineWidth = 2;
            ctx.textAlign = "right";

            const padding = fontSize;
            const lineHeight = fontSize * 1.3;

            ctx.strokeText(text1, canvas.width - padding, canvas.height - padding - lineHeight);
            ctx.fillText(text1, canvas.width - padding, canvas.height - padding - lineHeight);

            ctx.strokeText(text2, canvas.width - padding, canvas.height - padding);
            ctx.fillText(text2, canvas.width - padding, canvas.height - padding);

            resolve(canvas.toDataURL("image/jpeg", 0.8));
        };
        img.onerror = reject;
        img.src = base64Image;
    });
}