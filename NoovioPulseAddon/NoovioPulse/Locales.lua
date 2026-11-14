-- NoovioPulse, Copyright (C) 2026 Florian Rampin. SPDX-License-Identifier: AGPL-3.0-only
-- Textes de l'interface : français par défaut, anglais pour les autres clients.

local _, ns = ...

local L = {
    title = "NoovioPulse",
    tagline = "Ton personnage et tes alts, analysés par Noovio",
    tools = "Les outils Noovio",
    choose = "Que veux-tu exporter ?",

    characterTitle = "Mon personnage",
    characterText = "Équipement, talents, gemmes et caractéristiques, tels qu'en jeu à l'instant. Comparé point par point au top 100 Mythique+ de ta spécialisation.",
    altsTitle = "Mes alts",
    altsText = "La Grande Chambre forte, les clés de la semaine, la clé en poche et le niveau d'objet de tous tes personnages, dans une seule grille.",
    altsCount = "%d personnage(s) enregistré(s) sur ce compte",
    altsCountOne = "1 personnage : connecte-toi sur tes alts pour les ajouter",
    pasteOn = "À coller sur",

    exportCharacter = "Analyse de %s",
    exportAlts = "Avancement de %d personnage(s)",
    step1 = "Ctrl+C : la chaîne est déjà sélectionnée.",
    step2 = "Ouvre cette adresse et colle-la (Ctrl+V) :",
    back = "Retour",
    select = "Tout sélectionner",
    close = "Fermer",
    exported = "%d pièces, %d talents",

    combat = "Export impossible en combat : réessaie une fois le combat terminé.",
    noSpec = "Choisis d'abord une spécialisation.",
    tooltip = "Clic : ouvrir NoovioPulse",
    tooltipDrag = "Glisser : déplacer le bouton",
    tooltipHide = "/noovio minimap : masquer le bouton",
    usage = "/noovio : ouvrir · /noovio perso : exporter le personnage · /noovio alts : exporter les alts · /noovio minimap : bouton de la minicarte · /noovio oublier : vider la liste des alts",
    minimapShown = "Bouton de la minicarte affiché.",
    minimapHidden = "Bouton de la minicarte masqué : /noovio minimap pour le réafficher.",
    altsReset = "Liste des alts vidée : chaque personnage y revient à sa prochaine connexion.",
}

if GetLocale() ~= "frFR" then
    L.tagline = "Your character and your alts, analysed by Noovio"
    L.tools = "Noovio tools"
    L.choose = "What do you want to export?"
    L.characterTitle = "My character"
    L.characterText = "Gear, talents, gems and stats, exactly as in game right now. Compared point by point with the Mythic+ top 100 of your spec."
    L.altsTitle = "My alts"
    L.altsText = "Great Vault, keys of the week, owned keystone and item level of all your characters, in a single grid."
    L.altsCount = "%d character(s) saved on this account"
    L.altsCountOne = "1 character: log in on your alts to add them"
    L.pasteOn = "Paste on"
    L.exportCharacter = "Analysis of %s"
    L.exportAlts = "Progress of %d character(s)"
    L.step1 = "Ctrl+C: the string is already selected."
    L.step2 = "Open this address and paste it (Ctrl+V):"
    L.back = "Back"
    L.select = "Select all"
    L.close = "Close"
    L.exported = "%d items, %d talents"
    L.combat = "Cannot export during combat: try again once combat ends."
    L.noSpec = "Pick a specialization first."
    L.tooltip = "Click: open NoovioPulse"
    L.tooltipDrag = "Drag: move the button"
    L.tooltipHide = "/noovio minimap: hide the button"
    L.usage = "/noovio: open · /noovio perso: export the character · /noovio alts: export the alts · /noovio minimap: minimap button · /noovio oublier: clear the alt list"
    L.minimapShown = "Minimap button shown."
    L.minimapHidden = "Minimap button hidden: /noovio minimap to show it again."
    L.altsReset = "Alt list cleared: each character comes back at its next login."
end

ns.L = L
