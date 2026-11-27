// Filtres : saisir une date passe la période sur « Dates… » ; choisir une période prédéfinie vide les dates.
document.addEventListener("change", (event) => {
    const form = event.target.closest("form.filters");
    if (!form) return;
    const period = form.elements["periode"];
    if (event.target.type === "date" && event.target.value) {
        period.value = "dates";
    } else if (event.target === period && period.value !== "dates") {
        form.elements["du"].value = "";
        form.elements["au"].value = "";
    }
});

// ---------------------------------------------------------------------------
// Application installée (Chrome, Edge) et notifications push.
// ---------------------------------------------------------------------------

const toast = (text) => {
    const el = document.getElementById("toast");
    if (!el) return;
    el.textContent = text;
    el.hidden = false;
    clearTimeout(toast.timer);
    toast.timer = setTimeout(() => { el.hidden = true; }, 4000);
};

// Le bouton « Installer l'app » n'apparaît que quand le navigateur propose l'installation.
let installPrompt = null;
window.addEventListener("beforeinstallprompt", (event) => {
    event.preventDefault();
    installPrompt = event;
    const button = document.getElementById("install-app");
    if (button) button.hidden = false;
});
window.addEventListener("appinstalled", () => {
    installPrompt = null;
    const button = document.getElementById("install-app");
    if (button) button.hidden = true;
    toast("PulseMetrics Admin est installé.");
});
document.getElementById("install-app")?.addEventListener("click", async () => {
    if (!installPrompt) return;
    installPrompt.prompt();
    await installPrompt.userChoice;
    installPrompt = null;
    document.getElementById("install-app").hidden = true;
});

const postJson = (url, body) => fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    credentials: "same-origin",
    redirect: "manual",
    body: JSON.stringify(body)
});

// Les clés VAPID circulent en base64url ; pushManager.subscribe attend des octets.
const keyBytes = (base64url) => {
    const padded = (base64url + "=".repeat((4 - base64url.length % 4) % 4)).replace(/-/g, "+").replace(/_/g, "/");
    return Uint8Array.from(atob(padded), (c) => c.charCodeAt(0));
};

const saveSubscription = (subscription) => {
    const json = subscription.toJSON();
    return postJson("/api/push/subscribe", { endpoint: subscription.endpoint, p256dh: json.keys.p256dh, auth: json.keys.auth });
};

async function setupPush(registration) {
    const toggle = document.getElementById("push-toggle");
    const test = document.getElementById("push-test");
    if (!toggle || !("PushManager" in window) || !("Notification" in window)) return;

    const times = toggle.dataset.times;
    let subscription = await registration.pushManager.getSubscription();

    const render = () => {
        const denied = Notification.permission === "denied";
        toggle.hidden = false;
        toggle.disabled = denied;
        toggle.classList.toggle("on", !!subscription);
        toggle.textContent = denied ? "Notifications bloquées" : subscription ? "🔔 Notifications actives" : "🔕 Activer les notifications";
        toggle.title = denied
            ? "Autorise les notifications pour ce site dans les réglages du navigateur."
            : subscription ? `Récap à ${times}. Cliquer pour désactiver sur cet appareil.` : `Recevoir le récap d'usage à ${times}.`;
        test.hidden = !subscription;
    };

    // Déjà abonné : on renvoie l'abonnement, que le serveur l'ait oublié ou que le navigateur ait changé ses clés.
    if (subscription) {
        saveSubscription(subscription).catch(() => { });
    }
    render();

    toggle.addEventListener("click", async () => {
        toggle.disabled = true;
        try {
            if (subscription) {
                const endpoint = subscription.endpoint;
                await subscription.unsubscribe();
                subscription = null;
                await postJson("/api/push/unsubscribe", { endpoint });
                toast("Notifications désactivées sur cet appareil.");
            } else {
                if (await Notification.requestPermission() !== "granted") {
                    toast("Notifications refusées par le navigateur.");
                    return;
                }
                subscription = await registration.pushManager.subscribe({
                    userVisibleOnly: true,
                    applicationServerKey: keyBytes(toggle.dataset.vapidKey)
                });
                const response = await saveSubscription(subscription);
                if (!response.ok) {
                    await subscription.unsubscribe();
                    subscription = null;
                    toast("Abonnement refusé par le serveur : reconnecte-toi.");
                    return;
                }
                toast(`Abonné : récap à ${times}.`);
            }
        } catch (err) {
            console.error(err);
            toast("Échec : " + err.message);
        } finally {
            toggle.disabled = false;
            render();
        }
    });

    test.addEventListener("click", async () => {
        if (!subscription) return;
        test.disabled = true;
        try {
            const response = await postJson("/api/push/test", { endpoint: subscription.endpoint });
            toast(response.ok ? "Notification de test envoyée." : `Le test a échoué (${response.status || "session expirée"}).`);
        } finally {
            test.disabled = false;
        }
    });
}

if ("serviceWorker" in navigator) {
    window.addEventListener("load", async () => {
        try {
            await navigator.serviceWorker.register("/service-worker.js", { scope: "/" });
            await setupPush(await navigator.serviceWorker.ready);
        } catch (err) {
            console.warn("Service worker indisponible :", err);
        }
    });
}
