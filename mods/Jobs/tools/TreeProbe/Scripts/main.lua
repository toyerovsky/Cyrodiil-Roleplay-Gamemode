-- TreeProbe: answers "what is a vanilla tree in Oblivion Remastered, and can one instance be hidden at runtime?"
-- before the Jobs mod's native foliage control is built on the answer. Single player is enough, no relay needed.
--
--   Num1 list every instanced mesh near the player (class, mesh, instance, distance) and time the scan
--   Num2 hide the nearest instance whose mesh path contains "tree" (RemoveInstance, as the SDK does)
--   Num3 put back everything Num2 hid (AddInstance)
--   Num4 play the woodcutting montage (claymore power attack) on the player, third and first person
--   Num5 play the mining montage (warhammer power attack) on the player, third and first person
--
-- Num Lock on. F7 is taken by the game. Everything goes to UE4SS.log, prefixed [TreeProbe].

local RADIUS = 1500.0      -- cm around the player for Num1 and Num2
local MONTAGES = {
    chop = "/Game/Art/Animation/Humanoid/ThirdPerson/TwoHand/Combat/Attacks/Claymore/AM_Humanoid_Claymore_Attack_Power.AM_Humanoid_Claymore_Attack_Power",
    mine = "/Game/Art/Animation/Humanoid/ThirdPerson/TwoHand/Combat/Attacks/Warhammer/AM_Humanoid_Warhammer_Attack_Power.AM_Humanoid_Warhammer_Attack_Power",
    chopFirstPerson = "/Game/Art/Animation/Humanoid/FirstPerson/TwoHand/Combat/Attacks/Claymore/AM_Humanoid_Claymore_Attack_Power_FP.AM_Humanoid_Claymore_Attack_Power_FP",
    mineFirstPerson = "/Game/Art/Animation/Humanoid/FirstPerson/TwoHand/Combat/Attacks/Warhammer/AM_Humanoid_Warhammer_Attack_Power_FP.AM_Humanoid_Warhammer_Attack_Power_FP",
}

local hidden = {} -- { component, original transform }

-- A TArray return value comes back as a plain Lua table in this UE4SS build, a TArray in others
local function eachIndex(indices, action)
    if type(indices) == "table" then
        for _, index in ipairs(indices) do action(index) end
    else
        indices:ForEach(function(_, element) action(element:get()) end)
    end
end

local function log(fmt, ...)
    print(string.format("[TreeProbe] " .. fmt .. "\n", ...))
end

local function playerPawn()
    local controller = FindFirstOf("PlayerController")
    if not controller or not controller:IsValid() then return nil end
    local pawn = controller.Pawn
    if not pawn or not pawn:IsValid() then return nil end
    return pawn
end

local function distance(a, b)
    local dx, dy, dz = a.X - b.X, a.Y - b.Y, a.Z - b.Z
    return math.sqrt(dx * dx + dy * dy + dz * dz)
end

local function meshPath(component)
    local mesh = component.StaticMesh
    if mesh and mesh:IsValid() then return mesh:GetFullName() end
    return "<no mesh>"
end

-- GetInstanceTransform has an out parameter; UE4SS fills the table passed for it
local function instanceTransform(component, index)
    local out = {}
    local ok, err = pcall(function() component:GetInstanceTransform(index, out, true) end)
    if not ok then
        log("GetInstanceTransform(%d) failed: %s", index, tostring(err))
        return nil
    end
    return out
end

local function translationOf(transform)
    local t = transform and (transform.Translation or transform.translation)
    if not t then return nil end
    return { X = t.X, Y = t.Y, Z = t.Z }
end

-- Every instance within RADIUS of the pawn: { component, index, location, distance, class, mesh }
local function scan(pawn)
    local origin = pawn:K2_GetActorLocation()
    local center = { X = origin.X, Y = origin.Y, Z = origin.Z }

    local started = os.clock()
    local components = FindAllOf("InstancedStaticMeshComponent") or {}
    local findMs = (os.clock() - started) * 1000.0

    local hits = {}
    for _, component in ipairs(components) do
        if component:IsValid() then
            local ok, indices = pcall(function() return component:GetInstancesOverlappingSphere(center, RADIUS, true) end)
            if ok and indices then
                eachIndex(indices, function(index)
                    local location = translationOf(instanceTransform(component, index))
                    if location then
                        table.insert(hits, {
                            component = component,
                            index = index,
                            location = location,
                            distance = distance(location, center),
                            class = component:GetClass():GetFName():ToString(),
                            mesh = meshPath(component),
                        })
                    end
                end)
            elseif not ok then
                log("GetInstancesOverlappingSphere failed on %s: %s", component:GetFullName(), tostring(indices))
            end
        end
    end
    table.sort(hits, function(a, b) return a.distance < b.distance end)
    return hits, #components, findMs, center
end

local function listNearby()
    local pawn = playerPawn()
    if not pawn then return log("no player pawn yet") end

    local hits, componentCount, findMs, center = scan(pawn)
    log("player at (%.0f, %.0f, %.0f), pawn class %s", center.X, center.Y, center.Z, pawn:GetClass():GetFName():ToString())
    log("FindAllOf(InstancedStaticMeshComponent): %d components in %.1f ms; %d instances within %.0f cm",
        componentCount, findMs, #hits, RADIUS)
    for i, hit in ipairs(hits) do
        if i > 40 then break end
        log("  %6.0f cm  #%-5d %-45s %s  owner=%s", hit.distance, hit.index, hit.class, hit.mesh,
            hit.component:GetOwner() and hit.component:GetOwner():GetFullName() or "?")
    end
end

local function hideNearestTree()
    local pawn = playerPawn()
    if not pawn then return log("no player pawn yet") end

    for _, hit in ipairs((scan(pawn))) do
        if string.find(string.lower(hit.mesh), "tree", 1, true) then
            local original = instanceTransform(hit.component, hit.index)
            local ok, removed = pcall(function() return hit.component:RemoveInstance(hit.index) end)
            log("RemoveInstance #%d of %s at %.0f cm in %s: %s", hit.index, hit.mesh, hit.distance,
                hit.component:GetFullName(), ok and tostring(removed) or tostring(removed))
            if ok and removed then table.insert(hidden, { component = hit.component, original = original }) end
            return
        end
    end
    log("no instance with 'tree' in its mesh path within %.0f cm", RADIUS)
end

local function restoreAll()
    for _, entry in ipairs(hidden) do
        if entry.component:IsValid() then
            local ok, index = pcall(function() return entry.component:AddInstance(entry.original, true) end)
            log("AddInstance: %s", tostring(index))
        else
            log("restore: component gone (streamed out?)")
        end
    end
    hidden = {}
end

local function loadMontage(path)
    local montage = StaticFindObject(path)
    if not montage or not montage:IsValid() then
        LoadAsset(path)
        montage = StaticFindObject(path)
    end
    if montage and montage:IsValid() then return montage end
    log("montage not found: %s", path)
    return nil
end

local function playOn(mesh, path, view)
    local montage = loadMontage(path)
    local anim = montage and mesh and mesh:IsValid() and mesh:GetAnimInstance()
    if not anim or not anim:IsValid() then return log("%s: no montage or no anim instance", view) end
    local length = anim:Montage_Play(montage, 1.0, 0, 0.0, true)
    log("%s Montage_Play %s on %s: length %.2f s", view, montage:GetFName():ToString(), anim:GetFullName(), length or -1)
end

-- Third person is the pawn's Mesh; first person view draws FirstPersonSkeletalMeshComponent with its own _FP montages
local function playMontage(path, firstPersonPath)
    local pawn = playerPawn()
    if not pawn then return log("no player pawn yet") end
    playOn(pawn.Mesh, path, "third person")
    playOn(pawn.FirstPersonSkeletalMeshComponent, firstPersonPath, "first person")
end

local function onGameThread(action)
    return function() ExecuteInGameThread(function()
        local ok, err = pcall(action)
        if not ok then log("error: %s", tostring(err)) end
    end) end
end

RegisterKeyBind(Key.NUM_ONE, onGameThread(listNearby))
RegisterKeyBind(Key.NUM_TWO, onGameThread(hideNearestTree))
RegisterKeyBind(Key.NUM_THREE, onGameThread(restoreAll))
RegisterKeyBind(Key.NUM_FOUR, onGameThread(function() playMontage(MONTAGES.chop, MONTAGES.chopFirstPerson) end))
RegisterKeyBind(Key.NUM_FIVE, onGameThread(function() playMontage(MONTAGES.mine, MONTAGES.mineFirstPerson) end))

log("loaded: Num1 list, Num2 hide nearest tree, Num3 restore, Num4 chop montage, Num5 mine montage")
