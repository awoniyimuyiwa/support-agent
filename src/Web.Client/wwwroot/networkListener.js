/* ==========================================================================
   Blazor Sync Ecosystem: Browser Native Connection Status Listener Hook
   ========================================================================== */

let activeOnlineListener = null;
let activeOfflineListener = null;

export function registerNetworkListener(dotNetHelper) {
    // Unhook older references if they exist to prevent memory leak cascades
    unregisterNetworkListener();

    activeOnlineListener = () => dotNetHelper.invokeMethodAsync('OnConnectionStatusChanged', true);
    activeOfflineListener = () => dotNetHelper.invokeMethodAsync('OnConnectionStatusChanged', false);

    window.addEventListener('online', activeOnlineListener);
    window.addEventListener('offline', activeOfflineListener);

    return navigator.onLine;
}

export function unregisterNetworkListener() {
    if (activeOnlineListener) {
        window.removeEventListener('online', activeOnlineListener);
        activeOnlineListener = null;
    }
    if (activeOfflineListener) {
        window.removeEventListener('offline', activeOfflineListener);
        activeOfflineListener = null;
    }
}
