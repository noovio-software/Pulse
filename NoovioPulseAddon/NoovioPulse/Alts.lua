-- NoovioPulse, Copyright (C) 2026 Florian Rampin. SPDX-License-Identifier: AGPL-3.0-only
--
-- Avancement de chaque personnage du compte, pour AltPulse : Grande Chambre forte, clés de la semaine, clé en poche,
-- niveau d'objet, cote M+. Chaque personnage est relu à la connexion et quand ces données changent, puis gardé dans
-- NoovioPulseDB.characters (variables sauvegardées du compte) : l'export contient tous les personnages déjà connectés.

local _, ns = ...

local Json = ns.Json
local Alts = {}
ns.Alts = Alts

local Week = 7 * 24 * 3600

local function Try(...)
    return ns.Try(...)
end

local function Number(value)
    return type(value) == "number" and value or 0
end

local function Store()
    NoovioPulseDB = NoovioPulseDB or {}
    NoovioPulseDB.characters = NoovioPulseDB.characters or {}
    return NoovioPulseDB.characters
end

local function MapName(mapId)
    return mapId and (Try(C_ChallengeMode and C_ChallengeMode.GetMapUIInfo, mapId)) or nil
end

--- Début de la semaine en cours (dernière réinitialisation), en secondes : rattache le coffre et les clés à leur semaine.
local function WeekStart(now)
    local untilReset = Try(C_DateAndTime and C_DateAndTime.GetSecondsUntilWeeklyReset)
    return untilReset and (now + untilReset - Week) or nil
end

--- Cases de la Grande Chambre forte : type (1 donjons, 3 raid, 6 monde et gouffres…), seuil, progression, niveau.
local function ReadVault()
    local vault = {}
    for _, activity in ipairs(Try(C_WeeklyRewards and C_WeeklyRewards.GetActivities) or {}) do
        vault[#vault + 1] = {
            type = activity.type,
            index = activity.index,
            threshold = activity.threshold,
            progress = activity.progress,
            level = activity.level,
        }
    end
    return vault
end

--- Clés terminées cette semaine (dans le temps ou non), avec le nom du donjon dans la langue du client.
local function ReadRuns()
    local runs = {}
    for _, run in ipairs(Try(C_MythicPlus and C_MythicPlus.GetRunHistory, false, true) or {}) do
        if run.thisWeek ~= false then
            runs[#runs + 1] = {
                map = run.mapChallengeModeID,
                map_name = MapName(run.mapChallengeModeID),
                level = run.level,
                completed = run.completed and true or false,
            }
        end
    end
    table.sort(runs, function(a, b) return Number(a.level) > Number(b.level) end)
    return runs
end

local function ReadKeystone()
    local map = Try(C_MythicPlus and C_MythicPlus.GetOwnedKeystoneChallengeMapID)
    local level = Try(C_MythicPlus and C_MythicPlus.GetOwnedKeystoneLevel)
    if not map or not level or level <= 0 then
        return nil
    end
    return { map = map, map_name = MapName(map), level = level }
end

local function RatingColor(score)
    local color = score > 0 and Try(C_ChallengeMode and C_ChallengeMode.GetDungeonScoreRarityColor, score)
    local hex = color and color.GenerateHexColor and Try(color.GenerateHexColor, color)
    return type(hex) == "string" and #hex >= 8 and ("#" .. hex:sub(3, 8)) or nil
end

local function Key(region, realm, name)
    return string.lower((region or "?") .. "/" .. (realm or "?") .. "/" .. (name or "?"))
end

--- Relit le personnage connecté et met à jour sa fiche dans la liste du compte.
function Alts.Snapshot()
    local name, realm = UnitName("player"), Try(GetRealmName)
    local level = UnitLevel("player") or 0
    if not name or not realm or level < 10 then
        return
    end

    local now = Try(GetServerTime) or time()
    local specIndex = Try(GetSpecialization)
    local specId, specName, _, _, role = Try(GetSpecializationInfo, specIndex or 0)
    local _, _, classId = UnitClass("player")
    local overall, equipped = Try(GetAverageItemLevel)
    local score = Number(Try(C_ChallengeMode and C_ChallengeMode.GetOverallDungeonScore))
    local region = ns.Export.Region()

    Store()[Key(region, realm, name)] = {
        name = name,
        realm = realm,
        region = region,
        class = classId,
        race = (UnitRace("player")),
        faction = (UnitFactionGroup("player")),
        level = level,
        spec = specId,
        spec_name = specName,
        role = role,
        ilvl = { equipped = Number(equipped), overall = Number(overall) },
        rating = score,
        rating_color = RatingColor(score),
        keystone = ReadKeystone(),
        week_start = WeekStart(now),
        has_rewards = Try(C_WeeklyRewards and C_WeeklyRewards.HasAvailableRewards) and true or false,
        vault = ReadVault(),
        runs = ReadRuns(),
        saved_at = now,
    }
end

--- Nombre de personnages connus du compte.
function Alts.Count()
    local count = 0
    for _ in pairs(Store()) do
        count = count + 1
    end
    return count
end

--- Oublie tous les personnages (ils reviennent à leur prochaine connexion).
function Alts.Reset()
    NoovioPulseDB.characters = {}
    Alts.Snapshot()
end

local function CopyArray(source)
    local array = Json.Array()
    for i, value in ipairs(source or {}) do
        array[i] = value
    end
    return array
end

--- Export AltPulse : tous les personnages, le plus récent en tête. Les tableaux sont re-marqués :
--- les variables sauvegardées perdent la marque, et un tableau vide serait sinon écrit « {} ».
function Alts.Collect()
    if not InCombatLockdown() then
        Alts.Snapshot()
    end

    local characters = Json.Array()
    for _, saved in pairs(Store()) do
        local copy = {}
        for k, v in pairs(saved) do
            copy[k] = v
        end
        copy.vault = CopyArray(saved.vault)
        copy.runs = CopyArray(saved.runs)
        characters[#characters + 1] = copy
    end
    table.sort(characters, function(a, b) return Number(a.saved_at) > Number(b.saved_at) end)

    local data = ns.Export.Envelope("alts")
    data.characters = characters
    return data
end

-- Relecture : à la connexion (le temps que le jeu reçoive coffre et clés), puis à chaque changement, regroupés.
local pending = false
local function Schedule(delay)
    if pending then
        return
    end
    pending = true
    C_Timer.After(delay or 2, function()
        pending = false
        Try(Alts.Snapshot)
    end)
end

local events = CreateFrame("Frame")
events:RegisterEvent("PLAYER_ENTERING_WORLD")
events:RegisterEvent("WEEKLY_REWARDS_UPDATE")
events:RegisterEvent("CHALLENGE_MODE_COMPLETED")
events:RegisterEvent("CHALLENGE_MODE_MAPS_UPDATE")
events:RegisterEvent("PLAYER_EQUIPMENT_CHANGED")
events:RegisterEvent("BAG_UPDATE_DELAYED")
events:RegisterEvent("PLAYER_LOGOUT")
events:SetScript("OnEvent", function(_, event)
    if event == "PLAYER_LOGOUT" then
        Try(Alts.Snapshot)
    elseif event == "PLAYER_ENTERING_WORLD" then
        Try(C_MythicPlus and C_MythicPlus.RequestMapInfo)
        Try(C_MythicPlus and C_MythicPlus.RequestRewards)
        Schedule(5)
    else
        Schedule(2)
    end
end)
