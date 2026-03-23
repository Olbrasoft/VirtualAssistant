const CACHE_NAME = 'va-dictation-v4';

const SAME_ORIGIN_URLS = [
    '/remote.html',
    '/remote.js',
    '/manifest.json'
];

const CDN_URLS = [
    'https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/8.0.0/signalr.min.js'
];

self.addEventListener('install', event => {
    event.waitUntil(
        caches.open(CACHE_NAME)
            .then(cache => {
                const sameOriginPromise = cache.addAll(SAME_ORIGIN_URLS);
                const cdnPromise = Promise.allSettled(
                    CDN_URLS.map(url => cache.add(url))
                );
                return sameOriginPromise.then(() => cdnPromise);
            })
    );
});

self.addEventListener('fetch', event => {
    if (event.request.method !== 'GET') {
        return;
    }

    event.respondWith(
        fetch(event.request).catch(() =>
            caches.match(event.request).then(cachedResponse => {
                if (cachedResponse) {
                    return cachedResponse;
                }
                if (event.request.mode === 'navigate') {
                    return caches.match('/remote.html');
                }
                return new Response('', {
                    status: 503,
                    statusText: 'Service Unavailable'
                });
            })
        )
    );
});

self.addEventListener('activate', event => {
    event.waitUntil(
        caches.keys().then(cacheNames => {
            return Promise.all(
                cacheNames
                    .filter(name => name !== CACHE_NAME)
                    .map(name => caches.delete(name))
            );
        })
    );
});
