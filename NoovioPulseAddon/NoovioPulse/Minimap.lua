-- NoovioPulse, Copyright (C) 2026 Florian Rampin. SPDX-License-Identifier: AGPL-3.0-only
-- Bouton de la minicarte : clic pour exporter, glisser pour le déplacer autour de la carte.
-- Position et visibilité gardées dans NoovioPulseDB (compte). Sans bibliothèque : même habillage que les boutons du jeu.

local _, ns = ...

local L = ns.L
local Icon = "Interface\\AddOns\\NoovioPulse\\Media\\Icon"
local button

local function Settings()
    NoovioPulseDB = NoovioPulseDB or {}
    NoovioPulseDB.minimap = NoovioPulseDB.minimap or {}
    local settings = NoovioPulseDB.minimap
    if type(settings.angle) ~= "number" then
        settings.angle = 225 -- en bas à gauche, à l'écart des boutons du jeu
    end
    return settings
end

--- Place le bouton sur le bord de la minicarte : un cercle, ou le carré d'une minicarte carrée (ElvUI…).
local function UpdatePosition()
    local angle = math.rad(Settings().angle)
    local x, y = math.cos(angle), math.sin(angle)
    local shape = GetMinimapShape and GetMinimapShape() or "ROUND"
    if shape == "SQUARE" then
        local edge = math.max(math.abs(x), math.abs(y))
        x, y = x / edge, y / edge
    end
    local radiusX = Minimap:GetWidth() / 2 + 5
    local radiusY = Minimap:GetHeight() / 2 + 5
    button:ClearAllPoints()
    button:SetPoint("CENTER", Minimap, "CENTER", x * radiusX, y * radiusY)
end

--- Pendant le glisser : l'angle suit le curseur autour du centre de la minicarte.
local function FollowCursor()
    local centerX, centerY = Minimap:GetCenter()
    local cursorX, cursorY = GetCursorPosition()
    local scale = Minimap:GetEffectiveScale()
    local atan2 = math.atan2 or math.atan
    Settings().angle = math.deg(atan2(cursorY / scale - centerY, cursorX / scale - centerX)) % 360
    UpdatePosition()
end

local function ShowTooltip(owner)
    GameTooltip:SetOwner(owner, "ANCHOR_LEFT")
    GameTooltip:SetText(L.title)
    GameTooltip:AddLine(L.tooltip, 1, 1, 1)
    GameTooltip:AddLine(L.tooltipDrag, 0.7, 0.7, 0.7)
    GameTooltip:AddLine(L.tooltipHide, 0.7, 0.7, 0.7)
    GameTooltip:Show()
end

local function CreateButton()
    local b = CreateFrame("Button", "NoovioPulseMinimapButton", Minimap)
    b:SetSize(31, 31)
    b:SetFrameStrata("MEDIUM")
    b:SetFrameLevel(8)
    b:RegisterForClicks("LeftButtonUp")
    b:RegisterForDrag("LeftButton")
    b:SetHighlightTexture("Interface\\Minimap\\UI-Minimap-ZoomButton-Highlight")

    local background = b:CreateTexture(nil, "BACKGROUND")
    background:SetSize(24, 24)
    background:SetTexture("Interface\\Minimap\\UI-Minimap-Background")
    background:SetPoint("TOPLEFT", 4, -4)

    local icon = b:CreateTexture(nil, "ARTWORK")
    icon:SetSize(22, 22)
    icon:SetTexture(Icon)
    icon:SetPoint("TOPLEFT", 4.5, -4.5)
    if icon.SetMask then
        icon:SetMask("Interface\\CharacterFrame\\TempPortraitAlphaMask") -- icône ronde, dans l'anneau
    end
    b.icon = icon

    local border = b:CreateTexture(nil, "OVERLAY")
    border:SetSize(53, 53)
    border:SetTexture("Interface\\Minimap\\MiniMap-TrackingBorder")
    border:SetPoint("TOPLEFT")

    -- L'icône s'enfonce d'un pixel au clic, comme les boutons du jeu.
    b:SetScript("OnMouseDown", function(self) self.icon:SetPoint("TOPLEFT", 5.5, -5.5) end)
    b:SetScript("OnMouseUp", function(self) self.icon:SetPoint("TOPLEFT", 4.5, -4.5) end)
    b:SetScript("OnClick", function() ns.ShowHome() end)
    b:SetScript("OnEnter", ShowTooltip)
    b:SetScript("OnLeave", function() GameTooltip:Hide() end)
    b:SetScript("OnDragStart", function(self)
        GameTooltip:Hide()
        self:SetScript("OnUpdate", FollowCursor)
    end)
    b:SetScript("OnDragStop", function(self)
        self:SetScript("OnUpdate", nil)
        self.icon:SetPoint("TOPLEFT", 4.5, -4.5)
    end)

    return b
end

local function Refresh()
    if Settings().hide then
        if button then
            button:Hide()
        end
        return
    end
    button = button or CreateButton()
    UpdatePosition()
    button:Show()
end

--- /noovio minimap : affiche ou masque le bouton.
function ns.ToggleMinimap()
    local settings = Settings()
    settings.hide = not settings.hide
    Refresh()
    print("|cff7c3aedNoovioPulse|r " .. (settings.hide and L.minimapHidden or L.minimapShown))
end

-- Les variables sauvegardées ne sont lues qu'au chargement de l'addon.
local loader = CreateFrame("Frame")
loader:RegisterEvent("PLAYER_LOGIN")
loader:SetScript("OnEvent", function(self)
    self:UnregisterAllEvents()
    Refresh()
end)
