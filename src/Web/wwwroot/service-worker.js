const CACHE_NAME = 'supportagent-ui-v2'; // Changed version string to clear old caches

// Group all core static layout shell links that must load when offline
const ASSETS_TO_CACHE = [
    '/',
    '/weather',
    '/counter',
    'manifest.json',
    'favicon.png',
    'app.css'
];

self.addEventListener('install', (event) => {
    event.waitUntil(
        caches.open(CACHE_NAME).then((cache) => {
            return cache.addAll(ASSETS_TO_CACHE);
        })
    );
});

// Clear old cache versions automatically on activation
self.addEventListener('activate', (event) => {
    event.waitUntil(
        caches.keys().then((keys) => Promise.all(
            keys.map((key) => {
                if (key !== CACHE_NAME) return caches.delete(key);
            })
        ))
    );
});

self.addEventListener('fetch', (event) => {
    // Bypass API calls and Blazor's framework engine dependencies completely
    if (event.request.url.includes('/api/')
        || event.request.url.includes('/_framework/')) {
        return;
    }

    // Route Navigation helper: Handles refreshing on pages like /login or /weather-wasm
    if (event.request.mode === 'navigate') {
        event.respondWith(
            fetch(event.request).catch(() => {
                // If network fails (completely offline), fallback to the main cache frame index
                return caches.match('/');
            })
        );
        return;
    }

    // Standard static assets fallback
    event.respondWith(
        caches.match(event.request).then((cachedResponse) => {
            return cachedResponse || fetch(event.request);
        })
    );
});
