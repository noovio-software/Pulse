// Petits utilitaires d'interop pour FrontPulse (presse-papiers, rafraîchissement des infobulles Wowhead).
window.frontpulse = {
    async copy(text) {
        try {
            await navigator.clipboard.writeText(text);
            return true;
        } catch {
            // Contexte non sécurisé (http hors localhost) : l'API Clipboard est absente.
            const area = document.createElement("textarea");
            area.value = text;
            area.setAttribute("readonly", "");
            area.style.position = "fixed";
            area.style.opacity = "0";
            document.body.appendChild(area);
            area.select();
            const ok = document.execCommand("copy");
            area.remove();
            return ok;
        }
    },
    refreshTooltips() {
        if (window.$WowheadPower && typeof window.$WowheadPower.refreshLinks === "function") {
            window.$WowheadPower.refreshLinks();
        }
    }
};
