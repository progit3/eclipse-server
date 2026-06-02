ent-ProtoCore = proto core
    .desc = A stationary imperial energy vault used for emergency station power reserves.
ent-ProtoCoreConsole = proto core control console
    .desc = A hardened terminal wired into the emergency control loop of a proto core.
ent-ProtoCoreActivationKey = activation key
    .desc = A secured imperial data key that grants access to emergency proto core protocols.
ent-AshHackingDevice = ash hacking device
    .desc = A compact breaching apparatus built to force external authorization into a proto core.
ent-PinpointerProtoCoreActivationKey = activation key tracker
    .desc = A handheld tracker locked onto the activation key's emergency beacon.

ash-legion-title = Ash Legion
ash-legion-description = The Ash Legion targets the station's built-in proto core instead of bringing a nuclear device.
ash-legion-welcome =
    You are the Ash Legion.
    Infiltrate {$station}, seize the activation key, connect the ash hacking device to the proto core console, and start the core meltdown procedure.
ash-legion-briefing = Seize the activation key, breach the proto core console, begin meltdown, and hold the core until the countdown completes.

roles-antag-ash-legion-name = Ash Legion operative
roles-antag-ash-legion-medic-name = Ash Legion medic
roles-antag-ash-legion-commander-name = Ash Legion commander
roles-antag-ash-legion-objective = Use the activation key and ash hacking device to start the proto core meltdown procedure.

proto-core-announcement-sender = Proto Core Emergency System
proto-core-announcement-unauthorized-connection = Attention. Unauthorized connection to the proto core detected. External authorization attempt registered.
proto-core-announcement-meltdown-started = Proto core meltdown procedure initiated. Time until critical state: {$time}.
proto-core-announcement-stabilized = Stabilization complete. The proto core has returned to emergency energy accumulation mode.
proto-core-announcement-critical = The proto core has entered a critical state. Automatic emergency shuttle call initiated.
proto-core-announcement-shuttle-called = Emergency shuttle automatically called due to proto core critical state.

proto-core-console-key-accepted = The activation key authorizes the proto core emergency channel.
proto-core-console-hack-start = The ash hacking device begins forcing a connection.
proto-core-console-device-already-connected = A breaching device is already locked into the proto core loop.
proto-core-console-device-disconnected = The breaching device is torn out of the proto core loop.
ash-hacking-device-locked = The device is locked into the proto core loop. Disconnect it through the control console.

proto-core-verb-start = Start meltdown procedure
proto-core-verb-stabilize = Stabilize proto core
proto-core-verb-physical-override = Force device disconnect

proto-core-state-idle = idle
proto-core-state-hacked = externally connected
proto-core-state-meltdown = melting down
proto-core-state-critical = critical
proto-core-state-stabilized = stabilized
proto-core-examine-status = Status: {$state}. Remaining time: {$time}.
proto-core-console-examine = Authorization: {$authorized}. Breaching device: {$device}.

ash-legion-roundend-critical = The proto core reached a critical state.
ash-legion-roundend-stabilized = The proto core was stabilized.
ash-legion-roundend-started = The Ash Legion activated the proto core meltdown procedure.
steal-target-groups-proto-core-activation-key = activation key
