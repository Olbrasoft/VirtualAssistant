const CACHE_NAME = 'va-dictation-v23';

const SAME_ORIGIN_URLS = [
    '/remote.html',
    '/remote.js',
    '/manifest.json'
];

const CDN_URLS = [
    'https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/8.0.0/signalr.min.js'
];

const CACHEABLE_URLS = new Set([...SAME_ORIGIN_URLS, ...CDN_URLS]);

function isCacheable(request) {
    const url = new URL(request.url);
    return CACHEABLE_URLS.has(url.pathname) || CDN_URLS.some(cdn => request.url.startsWith(cdn));
}

self.addEventListener('install', event => {
    self.skipWaiting();
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

    const isSameOrigin = new URL(event.request.url).origin === self.location.origin;
    const fetchOptions = isSameOrigin ? { cache: 'no-cache' } : {};

    event.respondWith(
        fetch(event.request, fetchOptions)
            .then(response => {
                if (response.ok && response.status === 200 && !response.redirected && isCacheable(event.request)) {
                    const responseClone = response.clone();
                    const cachePromise = caches.open(CACHE_NAME)
                        .then(cache => cache.put(event.request, responseClone));
                    event.waitUntil(cachePromise);
                }
                return response;
            })
            .catch(() =>
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
                    .filter(name => name.startsWith('va-dictation-') && name !== CACHE_NAME)
                    .map(name => caches.delete(name))
            );
        }).then(() => self.clients.claim())
    );
});
