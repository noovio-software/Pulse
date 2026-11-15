-- NoovioPulse, Copyright (C) 2026 Florian Rampin. SPDX-License-Identifier: AGPL-3.0-only
--
-- Fenêtre de l'addon, aux couleurs des sites Noovio : un bandeau, deux cartes illustrées (mon personnage, mes alts),
-- puis l'écran de copie de la chaîne avec l'adresse du site où la coller. Commande /noovio et menu des addons.

local _, ns = ...

local L = ns.L
local Media = "Interface\\AddOns\\NoovioPulse\\Media\\"
local Font = STANDARD_TEXT_FONT or "Fonts\\FRIZQT__.TTF"

local Width, Height = 760, 580

local Colors = {
    background = { 0.043, 0.055, 0.078, 1 },
    card = { 0.075, 0.09, 0.13, 1 },
    line = { 0.165, 0.212, 0.31, 1 },
    text = { 0.9, 0.92, 0.95 },
    dim = { 0.6, 0.65, 0.74 },
    faint = { 0.4, 0.44, 0.54 },
    violet = { 0.486, 0.227, 0.929 },
    green = { 0.302, 1, 0.533 },
}

-- Le site où coller la chaîne : celui du rôle joué pour l'analyse, AltPulse pour les alts.
local Sites = {
    TANK = { name = "FrontPulse", url = "https://frontpulse.noovio.fr/importer", hex = "f59e0b" },
    HEALER = { name = "LifePulse", url = "https://lifepulse.noovio.fr/importer", hex = "4fa8f7" },
    DAMAGER = { name = "BurstPulse", url = "https://burstpulse.noovio.fr/importer", hex = "a78bfa" },
    ALTS = { name = "AltPulse", url = "https://altpulse.noovio.fr", hex = "4dff88" },
}

local window

local function Print(message)
    print("|cff7c3aedNoovioPulse|r " .. message)
end

local function Text(parent, size, color, layer)
    local text = parent:CreateFontString(nil, layer or "OVERLAY")
    text:SetFont(Font, size, "")
    text:SetTextColor(color[1], color[2], color[3])
    text:SetJustifyH("LEFT")
    return text
end

local function Solid(parent, color, layer)
    local texture = parent:CreateTexture(nil, layer or "BACKGROUND")
    texture:SetColorTexture(color[1], color[2], color[3], color[4] or 1)
    return texture
end

--- Fond opaque couvrant tout <paramref name="parent"/> : une texture sans ancrage n'a pas de taille et n'est pas dessinée.
local function Fill(parent, color)
    local texture = Solid(parent, color)
    texture:SetAllPoints()
    return texture
end

--- Cadre d'un pixel autour de <paramref name="frame"/> ; renvoie une fonction pour en changer la couleur (survol).
local function Border(frame, color)
    local edges = {}
    for i = 1, 4 do
        edges[i] = Solid(frame, color, "BORDER")
    end
    edges[1]:SetPoint("TOPLEFT"); edges[1]:SetPoint("TOPRIGHT"); edges[1]:SetHeight(1)
    edges[2]:SetPoint("BOTTOMLEFT"); edges[2]:SetPoint("BOTTOMRIGHT"); edges[2]:SetHeight(1)
    edges[3]:SetPoint("TOPLEFT"); edges[3]:SetPoint("BOTTOMLEFT"); edges[3]:SetWidth(1)
    edges[4]:SetPoint("TOPRIGHT"); edges[4]:SetPoint("BOTTOMRIGHT"); edges[4]:SetWidth(1)
    return function(r, g, b, a)
        for _, edge in ipairs(edges) do
            edge:SetColorTexture(r, g, b, a or 1)
        end
    end
end

local function Button(parent, label, width)
    local button = CreateFrame("Button", nil, parent, "UIPanelButtonTemplate")
    button:SetSize(width or 130, 26)
    button:SetText(label)
    return button
end

--- Zone de texte en lecture seule : toute saisie remet le texte d'origine, sélectionné, prêt pour Ctrl+C.
local function ReadOnly(edit, getText)
    edit:SetAutoFocus(false)
    edit:SetFont(Font, 12, "")
    edit:SetTextColor(Colors.text[1], Colors.text[2], Colors.text[3])
    edit:SetScript("OnEscapePressed", function(self) self:ClearFocus() end)
    edit:SetScript("OnTextChanged", function(self, userInput)
        if userInput and getText() then
            self:SetText(getText())
            self:HighlightText()
        end
    end)
    edit:SetScript("OnMouseUp", function(self)
        self:SetFocus()
        self:HighlightText()
    end)
end

-- --- Cartes de choix --------------------------------------------------------------------------

local function Card(parent, art, accent, title, description)
    local card = CreateFrame("Button", nil, parent)
    card:SetSize(340, 358)
    Fill(card, Colors.card)
    local setBorder = Border(card, Colors.line)

    local image = card:CreateTexture(nil, "ARTWORK")
    image:SetTexture(Media .. art)
    image:SetPoint("TOPLEFT", 1, -1)
    image:SetPoint("TOPRIGHT", -1, -1)
    image:SetHeight(169)
    image:SetVertexColor(0.85, 0.85, 0.85)

    local stripe = Solid(card, { accent[1], accent[2], accent[3], 1 }, "ARTWORK")
    stripe:SetPoint("TOPLEFT", image, "BOTTOMLEFT")
    stripe:SetPoint("TOPRIGHT", image, "BOTTOMRIGHT")
    stripe:SetHeight(2)

    local glow = Solid(card, { accent[1], accent[2], accent[3], 0.08 }, "BACKGROUND")
    glow:SetAllPoints()
    glow:Hide()

    card.title = Text(card, 18, Colors.text)
    card.title:SetPoint("TOPLEFT", image, "BOTTOMLEFT", 16, -14)
    card.title:SetText(title)

    card.description = Text(card, 12, Colors.dim)
    card.description:SetPoint("TOPLEFT", card.title, "BOTTOMLEFT", 0, -8)
    card.description:SetPoint("RIGHT", card, "RIGHT", -16, 0)
    card.description:SetSpacing(3)
    card.description:SetText(description)

    card.footer = Text(card, 11, Colors.faint)
    card.footer:SetPoint("BOTTOMLEFT", 16, 14)
    card.footer:SetPoint("RIGHT", card, "RIGHT", -16, 0)

    card:SetScript("OnEnter", function()
        setBorder(accent[1], accent[2], accent[3], 1)
        image:SetVertexColor(1, 1, 1)
        glow:Show()
    end)
    card:SetScript("OnLeave", function()
        setBorder(Colors.line[1], Colors.line[2], Colors.line[3], 1)
        image:SetVertexColor(0.85, 0.85, 0.85)
        glow:Hide()
    end)
    card:SetScript("OnMouseDown", function() image:SetPoint("TOPLEFT", 2, -2) end)
    card:SetScript("OnMouseUp", function() image:SetPoint("TOPLEFT", 1, -1) end)
    return card
end

-- --- Fenêtre --------------------------------------------------------------------------------------

local function CreateWindow()
    local f = CreateFrame("Frame", "NoovioPulseFrame", UIParent)
    f:SetSize(Width, Height)
    f:SetPoint("CENTER")
    -- Au-dessus de tout : les cadres d'unité, auras et barres d'autres addons montent souvent en HIGH ou DIALOG.
    f:SetFrameStrata("FULLSCREEN_DIALOG")
    f:SetFrameLevel(100)
    f:SetToplevel(true)
    f:SetMovable(true)
    f:EnableMouse(true)
    f:RegisterForDrag("LeftButton")
    f:SetScript("OnDragStart", f.StartMoving)
    f:SetScript("OnDragStop", f.StopMovingOrSizing)
    f:SetClampedToScreen(true)
    tinsert(UISpecialFrames, f:GetName()) -- Échap ferme la fenêtre.

    Fill(f, Colors.background)
    Border(f, Colors.line)

    -- Bandeau : logo, nom, accroche, et les sites Noovio.
    local header = f:CreateTexture(nil, "ARTWORK")
    header:SetTexture(Media .. "Header")
    header:SetPoint("TOPLEFT", 1, -1)
    header:SetPoint("TOPRIGHT", -1, -1)
    header:SetHeight(96)

    local accent = Solid(f, { Colors.violet[1], Colors.violet[2], Colors.violet[3], 0.9 }, "ARTWORK")
    accent:SetPoint("TOPLEFT", header, "BOTTOMLEFT")
    accent:SetPoint("TOPRIGHT", header, "BOTTOMRIGHT")
    accent:SetHeight(1)

    local logo = f:CreateTexture(nil, "OVERLAY")
    logo:SetTexture(Media .. "Icon")
    logo:SetSize(56, 56)
    logo:SetPoint("TOPLEFT", 22, -20)
    if logo.SetMask then
        logo:SetMask("Interface\\CharacterFrame\\TempPortraitAlphaMask")
    end

    local title = Text(f, 26, Colors.text)
    title:SetPoint("TOPLEFT", logo, "TOPRIGHT", 14, -4)
    title:SetText("|cffc4b5fdNoovio|rPulse")

    local tagline = Text(f, 13, Colors.dim)
    tagline:SetPoint("TOPLEFT", title, "BOTTOMLEFT", 0, -6)
    tagline:SetText(L.tagline)

    local tools = Text(f, 11, Colors.faint)
    tools:SetPoint("TOPRIGHT", -44, -30)
    tools:SetJustifyH("RIGHT")
    tools:SetText(L.tools .. "\n|cfff59e0bFrontPulse|r · |cff4fa8f7LifePulse|r · |cffa78bfaBurstPulse|r · |cff4dff88AltPulse|r\n|cffc4b5fdnoovio.fr|r")
    tools:SetSpacing(3)

    local close = CreateFrame("Button", nil, f, "UIPanelCloseButton")
    close:SetPoint("TOPRIGHT", -4, -4)

    -- Écran 1 : choix.
    local home = CreateFrame("Frame", nil, f)
    home:SetPoint("TOPLEFT", 0, -98)
    home:SetPoint("BOTTOMRIGHT")
    f.home = home

    local choose = Text(home, 14, Colors.text)
    choose:SetPoint("TOPLEFT", 28, -20)
    choose:SetText(L.choose)

    f.characterCard = Card(home, "CardCharacter", Colors.violet, L.characterTitle, L.characterText)
    f.characterCard:SetPoint("TOPLEFT", 28, -50)
    f.characterCard:SetScript("OnClick", function() ns.ShowExport("character") end)

    f.altsCard = Card(home, "CardAlts", Colors.green, L.altsTitle, L.altsText)
    f.altsCard:SetPoint("TOPRIGHT", -28, -50)
    f.altsCard:SetScript("OnClick", function() ns.ShowExport("alts") end)

    -- Écran 2 : copie.
    local export = CreateFrame("Frame", nil, f)
    export:SetPoint("TOPLEFT", 0, -98)
    export:SetPoint("BOTTOMRIGHT")
    export:Hide()
    f.export = export

    export.heading = Text(export, 18, Colors.text)
    export.heading:SetPoint("TOPLEFT", 28, -22)

    export.status = Text(export, 12, Colors.faint)
    export.status:SetPoint("LEFT", export.heading, "RIGHT", 12, -1)

    local step1 = Text(export, 12, Colors.dim)
    step1:SetPoint("TOPLEFT", export.heading, "BOTTOMLEFT", 0, -14)
    step1:SetText("|cffc4b5fd1.|r  " .. L.step1)

    local box = CreateFrame("Frame", nil, export)
    box:SetPoint("TOPLEFT", step1, "BOTTOMLEFT", 0, -8)
    box:SetPoint("RIGHT", -28, 0)
    box:SetHeight(200)
    Fill(box, { 0.024, 0.031, 0.047, 1 })
    Border(box, Colors.line)

    local scroll = CreateFrame("ScrollFrame", nil, box, "UIPanelScrollFrameTemplate")
    scroll:SetPoint("TOPLEFT", 10, -10)
    scroll:SetPoint("BOTTOMRIGHT", -30, 10)

    local edit = CreateFrame("EditBox", nil, scroll)
    edit:SetMultiLine(true)
    edit:SetMaxLetters(0)
    edit:SetWidth(Width - 56 - 40)
    ReadOnly(edit, function() return f.text end)
    scroll:SetScrollChild(edit)
    f.edit = edit

    local step2 = Text(export, 12, Colors.dim)
    step2:SetPoint("TOPLEFT", box, "BOTTOMLEFT", 0, -18)
    step2:SetText("|cffc4b5fd2.|r  " .. L.step2)

    local urlBox = CreateFrame("Frame", nil, export)
    urlBox:SetPoint("TOPLEFT", step2, "BOTTOMLEFT", 0, -8)
    urlBox:SetSize(420, 32)
    Fill(urlBox, { 0.024, 0.031, 0.047, 1 })
    export.setUrlBorder = Border(urlBox, Colors.line)

    local url = CreateFrame("EditBox", nil, urlBox)
    url:SetPoint("TOPLEFT", 12, 0)
    url:SetPoint("BOTTOMRIGHT", -12, 0)
    ReadOnly(url, function() return f.url end)
    f.urlEdit = url

    local back = Button(export, "‹ " .. L.back, 120)
    back:SetPoint("BOTTOMLEFT", 28, 22)
    back:SetScript("OnClick", function() ns.ShowHome() end)

    local selectAll = Button(export, L.select, 150)
    selectAll:SetPoint("BOTTOMRIGHT", -28, 22)
    selectAll:SetScript("OnClick", function()
        edit:SetFocus()
        edit:HighlightText()
    end)

    return f
end

local function Window()
    window = window or CreateWindow()
    return window
end

local function SiteLine(site)
    return L.pasteOn .. "  |cff" .. site.hex .. site.url:gsub("^https://", "") .. "|r"
end

--- Écran de choix, avec le site du rôle joué et le nombre d'alts connus.
function ns.ShowHome()
    local f = Window()
    local site = Sites[ns.Export.Role() or ""] or Sites.DAMAGER
    f.characterCard.footer:SetText(SiteLine(site))
    local count = ns.Alts.Count()
    f.altsCard.footer:SetText((count > 1 and string.format(L.altsCount, count) or L.altsCountOne) .. "\n" .. SiteLine(Sites.ALTS))
    f.export:Hide()
    f.home:Show()
    f:Show()
end

--- Écran de copie : <paramref name="kind"/> vaut character (analyse du personnage) ou alts (avancement AltPulse).
function ns.ShowExport(kind)
    local data, problem, heading, status, site
    if kind == "alts" then
        data = ns.Alts.Collect()
        heading = string.format(L.exportAlts, #data.characters)
        site = Sites.ALTS
    else
        data, problem = ns.Export.Collect()
        if not data then
            Print(problem)
            return
        end
        heading = string.format(L.exportCharacter, data.character.name)
        status = string.format(L.exported, #data.gear, #data.talents.nodes)
        site = Sites[data.character.role or ""] or Sites.DAMAGER
    end

    local f = Window()
    f.text = ns.Export.Encode(data)
    f.url = site.url
    f.export.heading:SetText(heading)
    f.export.status:SetText(status or "")
    f.edit:SetText(f.text)
    f.urlEdit:SetText(f.url)
    f.urlEdit:SetTextColor(tonumber(site.hex:sub(1, 2), 16) / 255, tonumber(site.hex:sub(3, 4), 16) / 255, tonumber(site.hex:sub(5, 6), 16) / 255)
    f.home:Hide()
    f.export:Show()
    f:Show()
    f.edit:SetFocus()
    f.edit:HighlightText()
end

SLASH_NOOVIOPULSE1 = "/noovio"
SLASH_NOOVIOPULSE2 = "/noovipulse"
SlashCmdList.NOOVIOPULSE = function(message)
    local command = (message or ""):lower():match("^%s*(%S*)")
    if command == "help" or command == "aide" then
        Print(L.usage)
    elseif command == "minimap" then
        ns.ToggleMinimap()
    elseif command == "perso" or command == "personnage" or command == "character" then
        ns.ShowExport("character")
    elseif command == "alts" then
        ns.ShowExport("alts")
    elseif command == "oublier" or command == "forget" then
        ns.Alts.Reset()
        Print(L.altsReset)
    else
        ns.ShowHome()
    end
end

-- Menu des addons (sous la minicarte) : déclaré dans le .toc.
function NoovioPulse_OnAddonCompartmentClick()
    ns.ShowHome()
end

function NoovioPulse_OnAddonCompartmentEnter(_, button)
    GameTooltip:SetOwner(button or UIParent, "ANCHOR_LEFT")
    GameTooltip:SetText(L.title)
    GameTooltip:AddLine(L.tooltip, 1, 1, 1)
    GameTooltip:Show()
end

function NoovioPulse_OnAddonCompartmentLeave()
    GameTooltip:Hide()
end
