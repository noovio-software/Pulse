-- NoovioPulse, Copyright (C) 2026 Florian Rampin. SPDX-License-Identifier: AGPL-3.0-only
--
-- Lit le personnage connecté (équipement, talents, caractéristiques) et en fait une chaîne à coller sur un site Pulse.
-- Format : « NP1:<mode>:<base64> ». Mode D : JSON compressé (deflate) par le client ; mode J : JSON tel quel,
-- quand le client ne sait pas compresser. Le schéma est décrit dans le README de l'addon.

local _, ns = ...

local Json = ns.Json
local Export = {}
ns.Export = Export

Export.FormatVersion = 1

-- Emplacements analysés, avec le nom de l'API Blizzard (chemise et tabard ignorés).
local Slots = {
    { 1, "HEAD" }, { 2, "NECK" }, { 3, "SHOULDER" }, { 15, "BACK" }, { 5, "CHEST" }, { 9, "WRIST" },
    { 10, "HANDS" }, { 6, "WAIST" }, { 7, "LEGS" }, { 8, "FEET" }, { 11, "FINGER_1" }, { 12, "FINGER_2" },
    { 13, "TRINKET_1" }, { 14, "TRINKET_2" }, { 16, "MAIN_HAND" }, { 17, "OFF_HAND" },
}

local Regions = { [1] = "us", [2] = "kr", [3] = "eu", [4] = "tw", [5] = "cn" }

-- Indices de combat (valeurs de repli si la constante globale manque).
local CR = {
    CritMelee = CR_CRIT_MELEE or 9,
    CritRanged = CR_CRIT_RANGED or 10,
    CritSpell = CR_CRIT_SPELL or 11,
    Speed = CR_SPEED or 14,
    Lifesteal = CR_LIFESTEAL or 17,
    HasteMelee = CR_HASTE_MELEE or 18,
    HasteRanged = CR_HASTE_RANGED or 19,
    HasteSpell = CR_HASTE_SPELL or 20,
    Avoidance = CR_AVOIDANCE or 21,
    Mastery = CR_MASTERY or 26,
    VersatilityDone = CR_VERSATILITY_DAMAGE_DONE or 29,
    VersatilityTaken = CR_VERSATILITY_DAMAGE_TAKEN or 31,
}

-- « Unique-Equipped: Embellished (2) » selon la langue du client.
local EmbellishedWords = { "embellish", "enjoliv", "verzier", "embellec", "embelez", "abbelli", "украш", "장식", "裝飾", "装饰" }

local function Pass(ok, ...)
    if ok then
        return ...
    end
    return nil
end

--- Appel protégé : une API absente ou en erreur donne nil au lieu de casser l'export. Toutes les valeurs de retour
--- sont transmises (GetSpecializationInfo donne le rôle en 5e position).
local function Try(fn, ...)
    if type(fn) ~= "function" then
        return nil
    end
    return Pass(pcall(fn, ...))
end

local function Number(value)
    return type(value) == "number" and value or 0
end

--- Retire les codes de couleur, textures et atlas du texte d'infobulle.
local function Clean(text)
    if type(text) ~= "string" then
        return nil
    end
    text = text:gsub("|c%x%x%x%x%x%x%x%x", ""):gsub("|r", ""):gsub("|T.-|t", ""):gsub("|A.-|a", ""):gsub("|n", " ")
    text = text:gsub("^%s+", ""):gsub("%s+$", "")
    return text ~= "" and text or nil
end

-- Partagés avec le suivi des alts et la fenêtre.
ns.Try = Try
ns.Clean = Clean

local function StartsWith(text, prefix)
    return type(prefix) == "string" and prefix ~= "" and text:sub(1, #prefix) == prefix
end

-- Ligne « Niveau d'amélioration : Héros 2/6 » de l'infobulle, motif tiré du texte du jeu (donc dans la langue du client).
local UpgradePattern = (function()
    local format = ITEM_UPGRADE_TOOLTIP_FORMAT_STRING or ITEM_UPGRADE_TOOLTIP_FORMAT
    if type(format) ~= "string" then
        return nil
    end
    local pattern = format:gsub("([%(%)%.%+%-%*%?%[%]%^%$])", "%%%1")
    pattern = pattern:gsub("%%s", "(.-)"):gsub("%%d", "(%%d+)")
    return "^" .. pattern
end)()

--- Piste d'amélioration d'une pièce : nom de la piste, rang, rang max et, quand le jeu le dit, niveau d'objet au rang max.
--- <paramref name="location"/> (porté ou sac) permet de demander le niveau max au jeu ; sinon le site l'estime.
local function Upgrade(lines, location)
    local upgrade
    for _, line in ipairs(lines or {}) do
        local text = Clean(line.leftText)
        if text then
            local track, rank, maxRank
            if UpgradePattern then
                track, rank, maxRank = text:match(UpgradePattern)
            end
            if not rank then
                track, rank, maxRank = text:match(":%s*(.-)%s+(%d+)/(%d+)%s*$")
            end
            if rank and tonumber(maxRank) and tonumber(maxRank) > 1 then
                upgrade = { track = Clean(track), rank = tonumber(rank), max_rank = tonumber(maxRank) }
                break
            end
        end
    end

    local info = location and Try(C_ItemUpgrade and C_ItemUpgrade.GetItemUpgradeItemInfo, location)
    if info and Number(info.maxUpgrade) > 0 then
        upgrade = upgrade or {}
        upgrade.rank = upgrade.rank or info.currUpgrade
        upgrade.max_rank = upgrade.max_rank or info.maxUpgrade
        local levels = info.upgradeLevelInfos
        local last = type(levels) == "table" and levels[#levels]
        local maxIlvl = Number(info.maxItemLevel) > 0 and info.maxItemLevel or (last and last.itemLevel)
        upgrade.max_ilvl = Number(maxIlvl) > 0 and maxIlvl or nil
    end
    return upgrade
end

Export.Upgrade = Upgrade

--- Champs d'un lien d'objet : id, enchantement, 4 gemmes, …, nombre de bonus puis les bonus.
local function ParseLink(link)
    local itemString = link and link:match("item:([%-%d:]*)")
    if not itemString then
        return nil
    end
    -- Les champs vides donnent nil : les positions sont gardées grâce au compteur.
    local parsed = { itemId = 0, enchantId = nil, gems = {}, bonus = Json.Array() }
    local raw = {}
    local i = 0
    for value in (itemString .. ":"):gmatch("([^:]*):") do
        i = i + 1
        raw[i] = tonumber(value)
    end
    parsed.itemId = raw[1] or 0
    parsed.enchantId = raw[2]
    for g = 1, 4 do
        parsed.gems[g] = raw[2 + g]
    end
    local bonusCount = raw[13] or 0
    for b = 1, bonusCount do
        local id = raw[13 + b]
        if id then
            parsed.bonus[#parsed.bonus + 1] = id
        end
    end
    return parsed
end

Export.ParseLink = ParseLink

local function IsEmbellished(text)
    local lower = text:lower()
    for _, word in ipairs(EmbellishedWords) do
        if lower:find(word, 1, true) then
            return true
        end
    end
    return false
end

--- Indices secondaires portés par la pièce (hors gemmes et enchantements), et nombre de châsses de base.
local function ItemStats(link)
    local stats = {}
    local sockets = 0
    local all = Try(C_Item and C_Item.GetItemStats, link) or Try(GetItemStats, link) or {}
    for key, value in pairs(all) do
        if key:find("EMPTY_SOCKET", 1, true) then
            sockets = sockets + Number(value)
        elseif key:find("HASTE", 1, true) or key:find("CRIT", 1, true) or key:find("MASTERY", 1, true) or key:find("VERSATILITY", 1, true) then
            stats[(key:gsub("^ITEM_MOD_", ""):gsub("_SHORT$", ""))] = Number(value)
        end
    end
    return stats, sockets
end

Export.ItemStats = ItemStats

local function ReadGear(slotId, slotName)
    local link = GetInventoryItemLink("player", slotId)
    local parsed = ParseLink(link)
    if not parsed then
        return nil
    end

    local location = ItemLocation and ItemLocation.CreateFromEquipmentSlot and ItemLocation:CreateFromEquipmentSlot(slotId)
    local ilvl = location and Try(C_Item.GetCurrentItemLevel, location)
        or Try(C_Item and C_Item.GetDetailedItemLevelInfo, link)
        or 0

    local piece = {
        slot = slotName,
        id = parsed.itemId,
        name = Clean((Try(C_Item and C_Item.GetItemInfo, link))) or Clean(link:match("%[(.-)%]")) or ("#" .. parsed.itemId),
        ilvl = math.floor(Number(ilvl) + 0.5),
        bonus = parsed.bonus,
        enchant_id = parsed.enchantId,
        gems = Json.Array(),
        effects = Json.Array(),
        embellished = false,
        set_id = select(16, Try(C_Item and C_Item.GetItemInfo, link)), -- pièce de set (tier) : ne pas la remplacer à la légère
    }

    local stats, baseSockets = ItemStats(link)
    piece.stats = stats

    -- Infobulle : texte de l'enchantement, des gemmes et des effets, tels qu'affichés en jeu.
    local enchantType = Enum and Enum.TooltipDataLineType and Enum.TooltipDataLineType.ItemEnchantmentPermanent
    local gemType = Enum and Enum.TooltipDataLineType and Enum.TooltipDataLineType.GemSocket
    local useSpellName, useSpellId = Try(C_Item and C_Item.GetItemSpell, link)
    local tooltip = Try(C_TooltipInfo and C_TooltipInfo.GetInventoryItem, "player", slotId)
    local socketTexts = {}
    for _, line in ipairs(tooltip and tooltip.lines or {}) do
        local text = Clean(line.leftText)
        if text then
            if enchantType and line.type == enchantType then
                piece.enchant = text
            elseif gemType and line.type == gemType then
                socketTexts[#socketTexts + 1] = { text = text, filled = line.gemIcon ~= nil, socket = line.socketType }
            elseif StartsWith(text, ITEM_SPELL_TRIGGER_ONUSE) then
                piece.effects[#piece.effects + 1] = {
                    trigger = "use",
                    spell = useSpellId or 0,
                    name = Clean(useSpellName),
                    text = Clean(text:sub(#ITEM_SPELL_TRIGGER_ONUSE + 1)),
                }
            elseif StartsWith(text, ITEM_SPELL_TRIGGER_ONEQUIP) then
                piece.effects[#piece.effects + 1] = {
                    trigger = "equip",
                    spell = 0,
                    text = Clean(text:sub(#ITEM_SPELL_TRIGGER_ONEQUIP + 1)),
                }
            elseif IsEmbellished(text) then
                piece.embellished = true
            end
        end
    end
    piece.upgrade = Upgrade(tooltip and tooltip.lines, location)

    -- Châsses : autant que de lignes de châsse de l'infobulle (ou que la pièce en a d'origine), remplies dans l'ordre du lien.
    local filledTexts = {}
    for _, s in ipairs(socketTexts) do
        if s.filled then
            filledTexts[#filledTexts + 1] = s.text
        end
    end
    local gemCount = 0
    for g = 1, 4 do
        if parsed.gems[g] then
            gemCount = g
        end
    end
    local socketCount = math.max(#socketTexts, baseSockets, gemCount)
    local nextFilled = 1
    for g = 1, socketCount do
        local gemId = parsed.gems[g]
        local socket = { type = socketTexts[g] and socketTexts[g].socket or nil }
        if gemId then
            socket.id = gemId
            socket.name = Clean((Try(C_Item and C_Item.GetItemGem, link, g)))
            socket.text = filledTexts[nextFilled]
            nextFilled = nextFilled + 1
        end
        piece.gems[#piece.gems + 1] = socket
    end

    return piece
end

local function ReadGearSet()
    local gear = Json.Array()
    for _, slot in ipairs(Slots) do
        local piece = ReadGear(slot[1], slot[2])
        if piece then
            gear[#gear + 1] = piece
        end
    end
    return gear
end

--- Talents de la configuration active : [nœud, entrée, sort, rang, offert (0/1)] par nœud choisi.
local function ReadTalents()
    local talents = { nodes = Json.Array() }
    local configId = Try(C_ClassTalents and C_ClassTalents.GetActiveConfigID)
    if not configId then
        return talents
    end

    talents.code = Try(C_Traits.GenerateImportString, configId)

    local heroSubTree = Try(C_ClassTalents.GetActiveHeroTalentSpec)
    if heroSubTree then
        local info = Try(C_Traits.GetSubTreeInfo, configId, heroSubTree)
        talents.hero_id = heroSubTree
        talents.hero = info and info.name or nil
    end

    local subTreeSelection = Enum and Enum.TraitNodeType and Enum.TraitNodeType.SubTreeSelection
    local config = Try(C_Traits.GetConfigInfo, configId)
    for _, treeId in ipairs(config and config.treeIDs or {}) do
        for _, nodeId in ipairs(Try(C_Traits.GetTreeNodes, treeId) or {}) do
            local node = Try(C_Traits.GetNodeInfo, configId, nodeId)
            local active = node and node.ID and node.ID ~= 0 and Number(node.activeRank) > 0 and node.activeEntry
            -- Nœuds de l'arbre héroïque non joué, et nœud de choix de l'arbre héroïque : rien à comparer.
            if active and node.subTreeActive == false then
                active = false
            end
            if active and subTreeSelection and node.type == subTreeSelection then
                active = false
            end
            if active then
                local entryId = node.activeEntry.entryID
                local entry = Try(C_Traits.GetEntryInfo, configId, entryId)
                local definition = entry and entry.definitionID and Try(C_Traits.GetDefinitionInfo, entry.definitionID)
                local spellId = definition and (definition.spellID or definition.overriddenSpellID) or 0
                local granted = Number(node.ranksPurchased) == 0 and 1 or 0
                talents.nodes[#talents.nodes + 1] = Json.Array({ nodeId, entryId, spellId, node.activeRank, granted })
            end
        end
    end
    return talents
end

local function Bonus(rating)
    return Number(Try(GetCombatRatingBonus, rating))
end

local function ReadStats()
    local spellCrit = 0
    for school = 2, 7 do
        spellCrit = math.max(spellCrit, Number(Try(GetSpellCritChance, school)))
    end
    return {
        haste = {
            rating = Number(Try(GetCombatRating, CR.HasteMelee)),
            melee = Number(Try(GetMeleeHaste) or Try(GetHaste)),
            ranged = Number(Try(GetRangedHaste) or Try(GetHaste)),
            spell = Number(Try(UnitSpellHaste, "player") or Try(GetHaste)),
        },
        crit = {
            rating = Number(Try(GetCombatRating, CR.CritMelee)),
            melee = Number(Try(GetCritChance)),
            ranged = Number(Try(GetRangedCritChance)),
            spell = spellCrit,
        },
        mastery = {
            rating = Number(Try(GetCombatRating, CR.Mastery)),
            percent = Number(Try(GetMasteryEffect)),
        },
        versatility = {
            rating = Number(Try(GetCombatRating, CR.VersatilityDone)),
            done = Bonus(CR.VersatilityDone) + Number(Try(GetVersatilityBonus, CR.VersatilityDone)),
            taken = Bonus(CR.VersatilityTaken) + Number(Try(GetVersatilityBonus, CR.VersatilityTaken)),
        },
    }
end

local function Stat(index)
    local _, effective = Try(UnitStat, "player", index)
    return Number(effective)
end

local function ReadSheet()
    local _, armor = Try(UnitArmor, "player")
    local manaType = Enum and Enum.PowerType and Enum.PowerType.Mana or 0
    return {
        health = Number(Try(UnitHealthMax, "player")),
        strength = Stat(1),
        agility = Stat(2),
        stamina = Stat(3),
        intellect = Stat(4),
        mana = Number(Try(UnitPowerMax, "player", manaType)),
        armor = Number(armor),
        dodge = Number(Try(GetDodgeChance)),
        parry = Number(Try(GetParryChance)),
        block = Number(Try(GetBlockChance)),
        leech = Number(Try(GetLifesteal) or Bonus(CR.Lifesteal)),
        avoidance = Number(Try(GetAvoidance) or Bonus(CR.Avoidance)),
        speed = Number(Try(GetSpeed) or Bonus(CR.Speed)),
    }
end

--- Rôle de la spécialisation active : TANK, HEALER ou DAMAGER (choisit le site où coller la chaîne).
function Export.Role()
    local specIndex = Try(GetSpecialization)
    local _, _, _, _, role = Try(GetSpecializationInfo, specIndex or 0)
    return role
end

--- Région Blizzard du client : us, eu, kr, tw, cn.
function Export.Region()
    return Regions[Try(GetCurrentRegion) or 0]
end

local function ReadCharacter()
    local specIndex = Try(GetSpecialization)
    local specId, specName, _, _, role = Try(GetSpecializationInfo, specIndex or 0)
    local _, _, classId = UnitClass("player")
    local _, averageEquipped = Try(GetAverageItemLevel)
    return {
        name = UnitName("player"),
        realm = Try(GetRealmName),
        realm_normalized = Try(GetNormalizedRealmName),
        region = Export.Region(),
        class = classId,
        race = (UnitRace("player")),
        faction = (UnitFactionGroup("player")),
        level = UnitLevel("player"),
        spec = specId,
        spec_name = specName,
        role = role,
        ilvl = Number(averageEquipped),
    }
end

--- En-tête commun aux exports : version du format, de l'addon et du client, date. <paramref name="kind"/> : character ou alts.
function Export.Envelope(kind)
    local version, build = Try(GetBuildInfo)
    return {
        v = Export.FormatVersion,
        kind = kind,
        addon = Try(C_AddOns and C_AddOns.GetAddOnMetadata, "NoovioPulse", "Version"),
        game = version and (version .. "." .. tostring(build)) or nil,
        locale = Try(GetLocale),
        exported_at = Try(GetServerTime) or time(),
    }
end

--- Données brutes de l'export du personnage (table Lua), ou nil et un message.
function Export.Collect()
    if InCombatLockdown() then
        return nil, ns.L.combat
    end
    local character = ReadCharacter()
    if not character.spec then
        return nil, ns.L.noSpec
    end
    local data = Export.Envelope("character")
    data.character = character
    data.gear = ReadGearSet()
    data.talents = ReadTalents()
    data.stats = ReadStats()
    data.sheet = ReadSheet()
    -- Sacs et coffre : un échec n'empêche pas l'analyse de l'équipement porté.
    data.bags = ns.Inventory and Try(ns.Inventory.ReadBags) or nil
    data.vault = ns.Inventory and Try(ns.Inventory.ReadVault) or nil
    return data
end

--- Chaîne à coller sur le site, à partir des données de <see cref="Export.Collect"/>.
function Export.Encode(data)
    local json = Json.Encode(data)
    local deflate = Enum and Enum.CompressionMethod and Enum.CompressionMethod.Deflate
    if deflate and C_EncodingUtil and C_EncodingUtil.CompressString then
        local compressed = Try(C_EncodingUtil.CompressString, json, deflate)
        if type(compressed) == "string" and compressed ~= "" then
            return "NP1:D:" .. Json.Base64(compressed)
        end
    end
    return "NP1:J:" .. Json.Base64(json)
end
