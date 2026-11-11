// Petits utilitaires d'interop des applications Pulse (presse-papiers, rafraîchissement des infobulles Wowhead).
window.pulse = {
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

// Menu des spécialisations (rôles qui en comptent trop pour une rangée d'icônes) : se referme au choix
// d'une spécialisation, à un clic en dehors et sur Échap.
document.addEventListener("click", event => {
    for (const menu of document.querySelectorAll("details.spec-menu[open]")) {
        if (!menu.contains(event.target) || event.target.closest(".spec-menu-link")) {
            menu.removeAttribute("open");
        }
    }
});
document.addEventListener("keydown", event => {
    if (event.key === "Escape") {
        document.querySelectorAll("details.spec-menu[open]").forEach(menu => menu.removeAttribute("open"));
    }
});
