// Petits utilitaires d'interop pour AltPulse (langue, scroll vers une colonne, rafraîchissement des infobulles Wowhead).
window.altpulse = {
    // Langue du prérendu serveur, reprise par le client WebAssembly au démarrage.
    culture() {
        return document.documentElement.lang || "fr";
    },
    scrollToColumn(key) {
        const el = document.querySelector(`[data-character-key="${CSS.escape(key)}"]`);
        if (el) {
            el.scrollIntoView({ behavior: "smooth", block: "nearest", inline: "center" });
        }
    },
    isSmallScreen() {
        return window.matchMedia("(max-width: 720px)").matches;
    },
    refreshTooltips() {
        if (window.$WowheadPower && typeof window.$WowheadPower.refreshLinks === "function") {
            window.$WowheadPower.refreshLinks();
        }
    }
};
