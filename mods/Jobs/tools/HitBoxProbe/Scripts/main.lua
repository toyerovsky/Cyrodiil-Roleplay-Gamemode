-- HitBoxProbe: shows the weapon hit boxes (UVHitBoxComponent) around the player and logs when the player's own is
-- switched on and off, where it is then and what it already overlaps, to answer why a standing battle axe power
-- attack makes no impact on a tree while a crouched one and every combo swing do.
--
--   Num6  probe on / off, logging only
--   Num7  probe on / off, logging and drawing: a hit box within RADIUS of the player shows while it can hit
--
-- While on, the player's own weapon box is logged on every change. The game's impacts are logged by OblivionMp as [LocalWeaponHit].
-- Num Lock on. Everything goes to UE4SS.log, prefixed [HitBoxProbe].
--
-- Safety: no game object is kept from one poll to the next (a weapon's components die on sheathing, and touching a
-- dead one takes UE4SS down), every call into the game is guarded, and the probe turns itself off after a few failed
-- polls in a row.

local RADIUS = 800.0       -- cm around the player whose hit boxes are drawn
local POLL_MS = 100        -- a power attack's hit window lasts a few tenths of a second
local MAX_FAILURES = 5

local enabled = false
local draw = false          -- Num7: also make the boxes visible; Num6 only logs
local last = {}            -- address -> last state string
local drawn = {}           -- address -> true while this probe made it visible
local failures = 0

local function log(fmt, ...)
    print(string.format("[HitBoxProbe] " .. fmt .. "\n", ...))
end

local function try(action, fallback)
    local ok, value = pcall(action)
    if ok and value ~= nil then return value end
    return fallback
end

local function valid(object)
    return object ~= nil and try(function() return object:IsValid() end, false)
end

local function nameOf(object)
    if not valid(object) then return "<none>" end
    return try(function() return object:GetName() end, "?")
end

-- The first live controller with a pawn: FindFirstOf can hand back the class default object
local function playerPawn()
    for _, controller in ipairs(FindAllOf("PlayerController") or {}) do
        local full = try(function() return controller:GetFullName() end, "")
        if valid(controller) and full ~= "" and not full:find("Default__", 1, true) then
            local pawn = try(function() return controller.Pawn end)
            if valid(pawn) then return pawn end
        end
    end
    return nil
end

local function vec(v)
    if not v then return "?" end
    return string.format("(%.0f, %.0f, %.0f)", v.X, v.Y, v.Z)
end

local function distance(a, b)
    local dx, dy, dz = a.X - b.X, a.Y - b.Y, a.Z - b.Z
    return math.sqrt(dx * dx + dy * dy + dz * dz)
end

-- The weapon actor hangs off the pawn through a few attachments (weapon actor, hand socket, child actor)
local function belongsTo(component, pawn)
    local target = pawn:GetAddress()
    local actor = try(function() return component:GetOwner() end)
    for _ = 1, 6 do
        if not valid(actor) then return false end
        if actor:GetAddress() == target then return true end
        local parent = try(function() return actor:GetAttachParentActor() end)
        if not valid(parent) then parent = try(function() return actor.Owner end) end
        actor = parent
    end
    return false
end

-- A TArray out parameter comes back as a plain Lua table in this UE4SS build, a TArray in others
local function overlapping(component)
    local out = {}
    if not pcall(function() component:GetOverlappingComponents(out) end) then return "?" end
    local names = {}
    local function add(element)
        if valid(element) then
            table.insert(names, nameOf(try(function() return element:GetOwner() end)) .. " " .. nameOf(element))
        end
    end
    if type(out) == "table" then
        for _, element in ipairs(out) do add(element) end
    else
        pcall(function() out:ForEach(function(_, element) add(element:get()) end) end)
    end
    if #names == 0 then return "nothing" end
    return table.concat(names, "; ")
end

local function setDrawn(component, visible)
    pcall(function() component:SetHiddenInGame(not visible, false) end)
    pcall(function() component:SetVisibility(visible, false) end)
end

-- The actors in one of the box's TArray<TWeakObjectPtr<AActor>> lists: the ones it is touching (TriggeringActors)
-- or holding (TrappedActors). A box makes no new impact on an actor it already lists, and every foliage instance of
-- an area, trees and grass alike, belongs to one InstancedFoliageActor.
local function actorList(component, property)
    local names = {}
    local ok = pcall(function()
        component[property]:ForEach(function(_, element)
            local weak = element:get()
            local actor = try(function() return weak:Get() end, weak)
            table.insert(names, nameOf(actor))
        end)
    end)
    if not ok then return "?" end
    if #names == 0 then return "none" end
    return table.concat(names, ", ")
end

local function stateOf(component)
    local collision = try(function() return component:GetCollisionEnabled() end, -1)
    local overlaps = try(function() return component:GetGenerateOverlapEvents() end, "?")
    local flags = try(function() return component.TriggerFlags end, "?")
    local attacker = valid(try(function() return component.AttackerActor end))
    return collision, string.format("collision %s, overlap events %s, trigger flags %s, attacker set %s, touching [%s], holding [%s]",
        tostring(collision), tostring(overlaps), tostring(flags), tostring(attacker),
        actorList(component, "TriggeringActors"), actorList(component, "TrappedActors"))
end

-- FindAllOf also returns the class default object and the Blueprint templates (Default__..., ..._GEN_VARIABLE),
-- which are in no world: a function called on one can crash the game where pcall cannot catch it. Only a component
-- an actor owns, by name and flags, is touched any further.
local RF_TEMPLATES = 0x00000010 | 0x00000020 -- RF_ClassDefaultObject | RF_ArchetypeObject

local function isLive(component)
    if not valid(component) then return false end
    local full = try(function() return component:GetFullName() end, "")
    if full == "" or full:find("Default__", 1, true) or full:find("_GEN_VARIABLE", 1, true) then return false end
    if try(function() return component:HasAnyFlags(RF_TEMPLATES) end, false) then return false end
    return valid(try(function() return component:GetOwner() end))
end

local tracing = false -- the first poll after switching on logs every box before touching it

local function poll()
    local pawn = playerPawn()
    if not pawn then return end
    local origin = pawn:K2_GetActorLocation()

    local found = FindAllOf("VHitBoxComponent") or {}
    if tracing then log("first poll: %d hit boxes found", #found) end
    for _, component in ipairs(found) do
        if tracing then log("checking %s", try(function() return component:GetFullName() end, "?")) end
        if isLive(component) then
            local at = try(function() return component:K2_GetComponentLocation() end)
            if at and distance(at, origin) <= RADIUS then
                local address = component:GetAddress()
                local collision, state = stateOf(component)
                if last[address] ~= state then
                    last[address] = state
                    local active = collision ~= 0 and collision ~= -1
                    if draw then
                        setDrawn(component, active)
                        drawn[address] = active or nil
                    end
                    if belongsTo(component, pawn) then
                        log("%s %s: %s. Box at %s, half size %s, %.0f cm from the player at %s. Overlapping: %s.",
                            active and "ON " or "off", nameOf(try(function() return component:GetOwner() end)), state,
                            vec(at), vec(try(function() return component:GetScaledBoxExtent() end)),
                            distance(at, origin), vec(origin), overlapping(component))
                    end
                end
            end
        end
    end
    if tracing then log("first poll done") end
    tracing = false
end

-- Hides what this probe made visible: looked up again, never kept
local function hideAll()
    for _, component in ipairs(FindAllOf("VHitBoxComponent") or {}) do
        if isLive(component) and drawn[component:GetAddress()] then
            setDrawn(component, false)
        end
    end
    drawn = {}
    last = {}
end

local function toggle(withDrawing)
    if enabled then
        enabled = false
        ExecuteInGameThread(function() pcall(hideAll) end)
        log("probe off")
        return
    end
    enabled, draw, failures, last, tracing = true, withDrawing, 0, {}, true
    log("probe on, %s", withDrawing and "logging and drawing" or "logging only")
end

RegisterKeyBind(Key.NUM_SIX, function() toggle(false) end)
RegisterKeyBind(Key.NUM_SEVEN, function() toggle(true) end)

LoopAsync(POLL_MS, function()
    if enabled then
        ExecuteInGameThread(function()
            if not enabled then return end
            local ok, err = pcall(poll)
            if ok then
                failures = 0
                return
            end
            failures = failures + 1
            log("poll failed (%d of %d): %s", failures, MAX_FAILURES, tostring(err))
            if failures >= MAX_FAILURES then
                enabled = false
                pcall(hideAll)
                log("probe off after %d failed polls in a row", MAX_FAILURES)
            end
        end)
    end
    return false
end)

log("loaded. Num6 logs the player's weapon hit box, Num7 also draws the boxes near the player while they can hit.")
