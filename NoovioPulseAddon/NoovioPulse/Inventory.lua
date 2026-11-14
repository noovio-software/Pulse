-- NoovioPulse, Copyright (C) 2026 Florian Rampin. SPDX-License-Identifier: AGPL-3.0-only
--
-- Sacs et Grande Chambre forte, pour l'export du personnage : l'équipement qui attend dans les sacs (une amélioration
-- oubliée ?), les consommables (flacon, potions, nourriture, huile, rune) et les récompenses du coffre de la semaine.

local _, ns = ...

local Json = ns.Json
local Inventory = {}
ns.Inventory = Inventory

local MaxBagGear = 60

-- Emplacement d'équipement (INVTYPE_*) → type d'emplacement de l'API Blizzard, comme pour l'équipement porté.
local SlotOfEquipLoc = {
    INVTYPE_HEAD = "HEAD", INVTYPE_NECK = "NECK", INVTYPE_SHOULDER = "SHOULDER", INVTYPE_CLOAK = "BACK",
    INVTYPE_CHEST = "CHEST", INVTYPE_ROBE = "CHEST", INVTYPE_WRIST = "WRIST", INVTYPE_HAND = "HANDS",
    INVTYPE_WAIST = "WAIST", INVTYPE_LEGS = "LEGS", INVTYPE_FEET = "FEET", INVTYPE_FINGER = "FINGER_1",
    INVTYPE_TRINKET = "TRINKET_1", INVTYPE_WEAPON = "MAIN_HAND", INVTYPE_2HWEAPON = "MAIN_HAND",
    INVTYPE_WEAPONMAINHAND = "MAIN_HAND", INVTYPE_RANGED = "MAIN_HAND", INVTYPE_RANGEDRIGHT = "MAIN_HAND",
    INVTYPE_WEAPONOFFHAND = "OFF_HAND", INVTYPE_SHIELD = "OFF_HAND", INVTYPE_HOLDABLE = "OFF_HAND",
}

local ConsumableClass = 0
-- Sous-classes de consommables gardées : potion, élixir, flacon, nourriture, amélioration d'objet (huiles, pierres), autre (runes).
local Kinds = { [1] = "potion", [2] = "elixir", [3] = "flask", [5] = "food", [6] = "enhancement", [8] = "other" }

local function Try(...)
    return ns.Try(...)
end

local function Number(value)
    return type(value) == "number" and value or 0
end

--- Ligne « Utiliser : » de l'objet : sert au site à distinguer potion de soins, de mana ou de combat.
local function UseText(itemId)
    local _, spellId = Try(C_Item and C_Item.GetItemSpell, itemId)
    local description = spellId and Try(C_Spell and C_Spell.GetSpellDescription, spellId)
    return ns.Clean(description)
end

local function Quality(link)
    return Try(C_TradeSkillUI and C_TradeSkillUI.GetItemCraftedQualityByItemInfo, link)
        or Try(C_TradeSkillUI and C_TradeSkillUI.GetItemReagentQualityByItemInfo, link)
end

--- Pièce d'équipement d'un sac : mêmes champs que l'équipement porté, sans l'infobulle (le site n'en a pas besoin ici).
local function BagGear(bag, slot, info, equipLoc)
    local parsed = ns.Export.ParseLink(info.hyperlink)
    if not parsed then
        return nil
    end
    local location = ItemLocation and ItemLocation.CreateFromBagAndSlot and ItemLocation:CreateFromBagAndSlot(bag, slot)
    local ilvl = location and Try(C_Item.GetCurrentItemLevel, location)
        or Try(C_Item and C_Item.GetDetailedItemLevelInfo, info.hyperlink)
        or 0
    local itemInfo = { Try(C_Item and C_Item.GetItemInfo, info.hyperlink) }
    local stats = ns.Export.ItemStats(info.hyperlink)
    return {
        slot = SlotOfEquipLoc[equipLoc],
        id = parsed.itemId,
        name = ns.Clean(itemInfo[1]) or ns.Clean(info.hyperlink:match("%[(.-)%]")) or ("#" .. parsed.itemId),
        ilvl = math.floor(Number(ilvl) + 0.5),
        bonus = parsed.bonus,
        enchant_id = parsed.enchantId,
        gems = Json.Array(),
        effects = Json.Array(),
        stats = stats,
        set_id = itemInfo[16],
        upgrade = ns.Export.Upgrade((Try(C_TooltipInfo and C_TooltipInfo.GetBagItem, bag, slot) or {}).lines, location),
        usable = Try(C_PlayerInfo and C_PlayerInfo.CanUseItem, parsed.itemId) ~= false,
        bound = info.isBound and true or false,
    }
end

--- Contenu des sacs : équipement mettable et consommables utiles en donjon (regroupés par objet, quantité totale).
function Inventory.ReadBags()
    local gear, consumables = Json.Array(), {}
    local currentExpansion = Try(GetExpansionLevel)
    for bag = 0, 5 do
        for slot = 1, Number(Try(C_Container and C_Container.GetContainerNumSlots, bag)) do
            local info = Try(C_Container.GetContainerItemInfo, bag, slot)
            if info and info.itemID and info.hyperlink then
                local _, _, _, equipLoc, _, classId, subclassId = Try(C_Item.GetItemInfoInstant, info.itemID)
                if equipLoc and SlotOfEquipLoc[equipLoc] and #gear < MaxBagGear then
                    gear[#gear + 1] = BagGear(bag, slot, info, equipLoc)
                elseif classId == ConsumableClass and Kinds[subclassId] then
                    local entry = consumables[info.itemID]
                    if entry then
                        entry.count = entry.count + Number(info.stackCount)
                    else
                        local itemInfo = { Try(C_Item and C_Item.GetItemInfo, info.hyperlink) }
                        local expansion = itemInfo[15]
                        consumables[info.itemID] = {
                            id = info.itemID,
                            name = ns.Clean(itemInfo[1]) or ns.Clean(info.hyperlink:match("%[(.-)%]")),
                            kind = Kinds[subclassId],
                            count = Number(info.stackCount),
                            quality = Quality(info.hyperlink),
                            min_level = itemInfo[5],
                            current = expansion and currentExpansion and expansion >= currentExpansion or nil,
                            use = UseText(info.itemID),
                        }
                    end
                end
            end
        end
    end

    local list = Json.Array()
    for _, entry in pairs(consumables) do
        list[#list + 1] = entry
    end
    table.sort(list, function(a, b) return a.id < b.id end)
    return { gear = gear, consumables = list }
end

local function VaultItem(link)
    local parsed = ns.Export.ParseLink(link)
    if not parsed then
        return nil
    end
    local _, _, _, equipLoc = Try(C_Item.GetItemInfoInstant, parsed.itemId)
    local itemInfo = { Try(C_Item and C_Item.GetItemInfo, link) }
    return {
        slot = equipLoc and SlotOfEquipLoc[equipLoc],
        id = parsed.itemId,
        name = ns.Clean(itemInfo[1]) or ns.Clean(link:match("%[(.-)%]")) or ("#" .. parsed.itemId),
        ilvl = math.floor(Number(Try(C_Item and C_Item.GetDetailedItemLevelInfo, link)) + 0.5),
        bonus = parsed.bonus,
        gems = Json.Array(),
        effects = Json.Array(),
        stats = ns.Export.ItemStats(link),
        set_id = itemInfo[16],
        upgrade = ns.Export.Upgrade((Try(C_TooltipInfo and C_TooltipInfo.GetHyperlink, link) or {}).lines),
    }
end

--- Grande Chambre forte : progression de chaque case, niveau d'objet promis et, quand le coffre est prêt, les objets proposés.
function Inventory.ReadVault()
    local vault = {
        has_rewards = Try(C_WeeklyRewards and C_WeeklyRewards.HasAvailableRewards) and true or false,
        can_claim = Try(C_WeeklyRewards and C_WeeklyRewards.CanClaimRewards) and true or false,
        activities = Json.Array(),
    }
    for _, activity in ipairs(Try(C_WeeklyRewards and C_WeeklyRewards.GetActivities) or {}) do
        local example = activity.id and Try(C_WeeklyRewards.GetExampleRewardItemHyperlinks, activity.id)
        local entry = {
            type = activity.type,
            index = activity.index,
            threshold = activity.threshold,
            progress = activity.progress,
            level = activity.level,
            example_ilvl = example and math.floor(Number(Try(C_Item.GetDetailedItemLevelInfo, example)) + 0.5) or nil,
            items = Json.Array(),
        }
        for _, reward in ipairs(activity.rewards or {}) do
            local link = reward.itemDBID and Try(C_WeeklyRewards.GetItemHyperlink, reward.itemDBID)
            local item = link and VaultItem(link)
            if item then
                entry.items[#entry.items + 1] = item
            end
        end
        vault.activities[#vault.activities + 1] = entry
    end
    return vault
end
