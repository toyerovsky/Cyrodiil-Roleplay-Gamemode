-- HitBoxProbe: shows the weapon hit boxes (UVHitBoxComponent) around the player and logs when the player's own is
-- switched on and off, where it is then and what it already overlaps, to answer why a standing battle axe power
-- attack makes no impact on a tree while a crouched one and every combo swing do.
--
--   Num6  probe on / off
--
-- While on, every hit box within RADIUS of the player is drawn (red while it can hit, white while it cannot) and the
-- player's own weapon box is logged on every change. The game's impacts are logged by OblivionMp as [LocalWeaponHit].
-- Num Lock on. Everything goes to UE4SS.log, prefixed [HitBoxProbe].

local RADIUS = 800.0       -- cm around the player whose hit boxes are drawn
local POLL_MS = 16         -- how often the boxes are read: a power attack's hit window lasts a few tenths of a second

local enabled = false
local shown = {}           -- address -> component made visible
local last = {}            -- address -> last logged state string

local function log(fmt, ...)
    print(string.format("[HitBoxProbe] " .. fmt .. "\n", ...))
end

local function valid(object)
    return object ~= nil and object.IsValid ~= nil and object:IsValid()
end

local function playerPawn()
    local controller = FindFirstOf("PlayerController")
    if not valid(controller) then return nil end
    local pawn = controller.Pawn
    if not valid(pawn) then return nil end
    return pawn
end

local function vec(v)
    return string.format("(%.0f, %.0f, %.0f)", v.X, v.Y, v.Z)
end

local function distance(a, b)
    local dx, dy, dz = a.X - b.X, a.Y - b.Y, a.Z - b.Z
    return math.sqrt(dx * dx + dy * dy + dz * dz)
end

local function try(action, fallback)
    local ok, value = pcall(action)
    if ok then return value end
    return fallback
end

-- The weapon actor hangs off the pawn through a few attachments (weapon actor, hand socket, child actor)
local function belongsTo(component, pawn)
    local actor = try(function() return component:GetOwner() end)
    local target = pawn:GetAddress()
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
    local ok = pcall(function() component:GetOverlappingComponents(out) end)
    if not ok then return "?" end
    local names = {}
    local function add(element)
        if valid(element) then
            local owner = try(function() return element:GetOwner():GetName() end, "?")
            table.insert(names, string.format("%s %s", owner, element:GetName()))
        end
    end
    if type(out) == "table" then
        for _, element in ipairs(out) do add(element) end
    else
        out:ForEach(function(_, element) add(element:get()) end)
    end
    if #names == 0 then return "nothing" end
    return table.concat(names, "; ")
end

-- Red while the box can hit, white while it cannot. A colour change only shows once the render state is rebuilt,
-- which setting the extent does.
local function paint(component, active)
    pcall(function()
        component.ShapeColor = active and { R = 255, G = 0, B = 0, A = 255 } or { R = 255, G = 255, B = 255, A = 255 }
    end)
    pcall(function() component.LineThickness = 2.0 end)
    pcall(function() component:SetBoxExtent(component.BoxExtent, false) end)
end

local function show(component)
    local address = component:GetAddress()
    if shown[address] then return end
    shown[address] = component
    pcall(function() component.bDrawOnlyIfSelected = false end)
    pcall(function() component:SetHiddenInGame(false, false) end)
    pcall(function() component:SetVisibility(true, false) end)
    paint(component, false)
end

local function hideAll()
    for _, component in pairs(shown) do
        if valid(component) then
            pcall(function() component:SetHiddenInGame(true, false) end)
        end
    end
    shown = {}
    last = {}
end

local function stateOf(component)
    local collision = try(function() return component:GetCollisionEnabled() end, -1)
    local overlaps = try(function() return component:GetGenerateOverlapEvents() end, "?")
    local flags = try(function() return component.TriggerFlags end, "?")
    local attacker = try(function() return valid(component.AttackerActor) end, "?")
    return collision, string.format("collision %s, overlap events %s, trigger flags %s, attacker set %s",
        tostring(collision), tostring(overlaps), tostring(flags), tostring(attacker))
end

-- FindAllOf walks the whole object array: the boxes are looked up a couple of times a second, read every poll
local boxes = {}
local polls = 0
local REFRESH_POLLS = 30

local function poll()
    local pawn = playerPawn()
    if not pawn then return end
    local origin = pawn:K2_GetActorLocation()

    if polls % REFRESH_POLLS == 0 then
        boxes = FindAllOf("VHitBoxComponent") or {}
    end
    polls = polls + 1

    for _, component in ipairs(boxes) do
        if valid(component) then
            local at = try(function() return component:K2_GetComponentLocation() end)
            if at and distance(at, origin) <= RADIUS then
                show(component)
                local collision, state = stateOf(component)
                local address = component:GetAddress()
                if last[address] ~= state then
                    local active = collision ~= 0 and collision ~= -1
                    paint(component, active)
                    if belongsTo(component, pawn) then
                        local extent = try(function() return vec(component:GetScaledBoxExtent()) end, "?")
                        log("%s %s: %s. Box at %s, half size %s, %.0f cm from the player at %s. Overlapping: %s.",
                            active and "ON " or "off", component:GetOwner():GetName(), state, vec(at), extent,
                            distance(at, origin), vec(origin), overlapping(component))
                    end
                    last[address] = state
                end
            end
        end
    end
end

RegisterKeyBind(Key.NUM_SIX, function()
    enabled = not enabled
    if not enabled then
        ExecuteInGameThread(hideAll)
    end
    log("probe %s", enabled and "on" or "off")
end)

LoopAsync(POLL_MS, function()
    if enabled then
        ExecuteInGameThread(function()
            local ok, err = pcall(poll)
            if not ok then log("poll failed: %s", tostring(err)) end
        end)
    end
    return false
end)

log("loaded. Num6 shows the weapon hit boxes and logs the player's.")
