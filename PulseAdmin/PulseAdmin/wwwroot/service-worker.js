// Service worker de PulseAdmin : rend le site installable (Chrome, Edge) et affiche les récaps envoyés en push.
// Rien n'est mis en cache : les pages sont privées et doivent toujours être fraîches.

self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', (event) => event.waitUntil(self.clients.claim()));

// Tout le traitement reste dans waitUntil : le navigateur peut arrêter le worker dès que la promesse est tenue.
self.addEventListener('push', (event) => event.waitUntil(showPush(event)));

async function showPush(event) {
    let payload = { title: 'PulseMetrics Admin', body: '', url: '/', tag: 'pulseadmin-digest' };
    try {
        if (event.data) {
            payload = { ...payload, ...event.data.json() };
        }
    } catch (err) {
        console.error('Notification illisible, texte par défaut :', err);
    }

    try {
        await self.registration.showNotification(payload.title, {
            body: payload.body,
            icon: '/icon-192.png',
            badge: '/badge-96.png',
            tag: payload.tag,
            renotify: true,
            data: { url: payload.url || '/' }
        });
    } catch (err) {
        // Un push qui n'affiche rien finit par faire retirer la permission par Chrome : toujours montrer quelque chose.
        console.error('showNotification en échec :', err);
        await self.registration.showNotification('PulseMetrics Admin', { body: 'Nouveau récap d\'usage.' });
    }
}

// Un clic ramène la fenêtre déjà ouverte (l'application installée) plutôt que d'en ouvrir une autre.
self.addEventListener('notificationclick', (event) => {
    event.notification.close();
    const target = new URL((event.notification.data && event.notification.data.url) || '/', self.location.origin).href;
    event.waitUntil((async () => {
        const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
        for (const client of windows) {
            if (new URL(client.url).origin === self.location.origin && 'focus' in client) {
                await client.focus();
                return client.navigate(target);
            }
        }
        return self.clients.openWindow(target);
    })());
});
