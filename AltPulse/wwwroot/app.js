// Petits utilitaires d'interop pour AltPulse (scroll vers une colonne, rafraîchissement des infobulles Wowhead).
window.altpulse = {
    scrollToColumn(key) {
        const el = document.querySelector(`[data-character-key="${CSS.escape(key)}"]`);
        if (el) {
            el.scrollIntoView({ behavior: "smooth", block: "nearest", inline: "center" });
        }
    },
    refreshTooltips() {
        if (window.$WowheadPower && typeof window.$WowheadPower.refreshLinks === "function") {
            window.$WowheadPower.refreshLinks();
        }
    }
};
