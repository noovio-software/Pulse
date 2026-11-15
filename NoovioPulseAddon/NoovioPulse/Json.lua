-- NoovioPulse, Copyright (C) 2026 Florian Rampin. SPDX-License-Identifier: AGPL-3.0-only
-- Encodeur JSON minimal et Base64 : le client de jeu n'en fournit pas toujours (C_EncodingUtil est récent).

local _, ns = ...

local Json = {}
ns.Json = Json

local ArrayMarker = {}

--- Marque une table comme tableau JSON : une table vide serait sinon écrite « {} ».
function Json.Array(t)
    return setmetatable(t or {}, ArrayMarker)
end

local escapes = {
    ['"'] = '\\"',
    ['\\'] = '\\\\',
    ['\8'] = '\\b', -- retour arrière
    ['\12'] = '\\f', -- saut de page
    ['\n'] = '\\n',
    ['\r'] = '\\r',
    ['\t'] = '\\t',
}

local function EncodeString(s)
    -- Seuls les caractères de contrôle ASCII sont échappés : les octets UTF-8 (accents) passent tels quels.
    return '"' .. s:gsub('[%z\1-\31\127"\\]', function(c)
        return escapes[c] or string.format("\\u%04x", c:byte())
    end) .. '"'
end

local function EncodeNumber(n)
    if n ~= n or n == math.huge or n == -math.huge then
        return "0"
    end
    if n == math.floor(n) and math.abs(n) < 2 ^ 53 then
        return string.format("%d", n)
    end
    -- Quatre décimales suffisent aux pourcentages de la feuille ; les zéros de fin sont retirés.
    return (string.format("%.4f", n):gsub("0+$", ""):gsub("%.$", ""))
end

local Encode

local function IsArray(t)
    if getmetatable(t) == ArrayMarker then
        return true
    end
    return next(t) ~= nil and #t > 0 and next(t, #t) == nil
end

Encode = function(value, out)
    local kind = type(value)
    if kind == "nil" then
        out[#out + 1] = "null"
    elseif kind == "boolean" then
        out[#out + 1] = value and "true" or "false"
    elseif kind == "number" then
        out[#out + 1] = EncodeNumber(value)
    elseif kind == "string" then
        out[#out + 1] = EncodeString(value)
    elseif kind == "table" then
        if IsArray(value) then
            out[#out + 1] = "["
            for i = 1, #value do
                if i > 1 then
                    out[#out + 1] = ","
                end
                Encode(value[i], out)
            end
            out[#out + 1] = "]"
        else
            -- Clés triées : deux exports du même personnage donnent la même chaîne.
            local keys = {}
            for k in pairs(value) do
                keys[#keys + 1] = tostring(k)
            end
            table.sort(keys)
            out[#out + 1] = "{"
            for i, k in ipairs(keys) do
                if i > 1 then
                    out[#out + 1] = ","
                end
                out[#out + 1] = EncodeString(k)
                out[#out + 1] = ":"
                Encode(value[k], out)
            end
            out[#out + 1] = "}"
        end
    else
        out[#out + 1] = "null"
    end
end

function Json.Encode(value)
    local out = {}
    Encode(value, out)
    return table.concat(out)
end

local alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/"

--- Base64 standard (RFC 4648), avec remplissage.
function Json.Base64(data)
    local out = {}
    for i = 1, #data, 3 do
        local a, b, c = data:byte(i, i + 2)
        local n = a * 65536 + (b or 0) * 256 + (c or 0)
        local c1 = math.floor(n / 262144) % 64
        local c2 = math.floor(n / 4096) % 64
        local c3 = math.floor(n / 64) % 64
        local c4 = n % 64
        out[#out + 1] = alphabet:sub(c1 + 1, c1 + 1)
            .. alphabet:sub(c2 + 1, c2 + 1)
            .. (b and alphabet:sub(c3 + 1, c3 + 1) or "=")
            .. (c and alphabet:sub(c4 + 1, c4 + 1) or "=")
    end
    return table.concat(out)
end
