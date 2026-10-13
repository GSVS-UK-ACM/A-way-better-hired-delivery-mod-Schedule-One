[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $MelonLoaderRoot,

    [Parameter(Mandatory = $true)]
    [string] $AssemblyPath
)

$ErrorActionPreference = "Stop"
$script:PassCount = 0
$cecilPath = Join-Path $MelonLoaderRoot "net6\Mono.Cecil.dll"

if (-not (Test-Path -LiteralPath $AssemblyPath -PathType Leaf)) {
    throw "Missing Vehicle Handlers assembly: $AssemblyPath"
}

if (-not (Test-Path -LiteralPath $cecilPath -PathType Leaf)) {
    throw "Missing Mono.Cecil reference: $cecilPath"
}

[void] [System.Reflection.Assembly]::LoadFrom($cecilPath)
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($AssemblyPath)

try {
    function Get-RequiredType([string] $FullName) {
        $type = $assembly.MainModule.GetTypes() |
            Where-Object { $_.FullName -ceq $FullName } |
            Select-Object -First 1

        if (-not $type) {
            throw "Missing managed type: $FullName"
        }

        return $type
    }

    function Get-RequiredMethod(
        [string] $TypeName,
        [string] $MethodName,
        [string[]] $ParameterTypes = @()
    ) {
        $type = Get-RequiredType $TypeName
        $matches = @($type.Methods | Where-Object {
            if ($_.Name -cne $MethodName -or $_.Parameters.Count -ne $ParameterTypes.Count) {
                return $false
            }

            for ($index = 0; $index -lt $ParameterTypes.Count; $index++) {
                if ($_.Parameters[$index].ParameterType.FullName -cne $ParameterTypes[$index]) {
                    return $false
                }
            }

            return $true
        })

        if ($matches.Count -ne 1) {
            throw "Expected one method $TypeName::$MethodName($($ParameterTypes -join ', ')); found $($matches.Count)."
        }

        return $matches[0]
    }

    function Assert-Condition([bool] $Condition, [string] $Message) {
        if (-not $Condition) {
            throw $Message
        }

        Write-Output "PASS $Message"
        $script:PassCount++
    }

    function Assert-Call(
        [Mono.Cecil.MethodDefinition] $Method,
        [string] $CalledMethodPattern,
        [string] $Label
    ) {
        $matches = @($Method.Body.Instructions | Where-Object {
            $_.Operand -and $_.Operand.ToString() -like $CalledMethodPattern
        })

        Assert-Condition ($matches.Count -gt 0) $Label
    }

    function Assert-HiddenFromIl2Cpp(
        [Mono.Cecil.MethodDefinition] $Method,
        [string] $Label
    ) {
        $hidden = @($Method.CustomAttributes | Where-Object {
            $_.AttributeType.FullName -ceq "Il2CppInterop.Runtime.Attributes.HideFromIl2CppAttribute"
        })

        Assert-Condition ($hidden.Count -eq 1) $Label
    }

    $handlerType = Get-RequiredType "VehicleHandlers.Employees.HandlerEmployee"
    Assert-Condition ($handlerType.IsPublic) "HandlerEmployee is public for IL2CPP registration"
    Assert-Condition ($handlerType.BaseType.FullName -ceq "UnityEngine.MonoBehaviour") "HandlerEmployee uses the supported MonoBehaviour injection boundary"

    $nativeConstructor = Get-RequiredMethod "VehicleHandlers.Employees.HandlerEmployee" ".ctor" @("System.IntPtr")
    Assert-Condition ($nativeConstructor.IsPublic) "HandlerEmployee exposes the native-pointer constructor"

    $ensureAttached = Get-RequiredMethod "VehicleHandlers.Employees.HandlerEmployee" "EnsureAttached" @("Il2CppScheduleOne.Employees.Employee")
    Assert-Condition ($ensureAttached.IsPublic -and $ensureAttached.IsStatic) "HandlerEmployee exposes idempotent attachment"
    Assert-Call $ensureAttached "*GetComponent*VehicleHandlers.Employees.HandlerEmployee*" "HandlerEmployee attachment reuses an existing marker"
    Assert-Call $ensureAttached "*UnityEngine.GameObject::AddComponent*VehicleHandlers.Employees.HandlerEmployee*" "HandlerEmployee attachment adds the injected marker to the vanilla employee object"

    $awake = Get-RequiredMethod "VehicleHandlers.Employees.HandlerEmployee" "Awake"
    Assert-Condition ($awake.IsPublic) "HandlerEmployee exposes the Unity Awake message"
    Assert-Call $awake "*GetComponent*Il2CppScheduleOne.Employees.Employee*" "HandlerEmployee binds to the real game Employee component"
    Assert-Call $awake "*Il2CppScheduleOne.Employees.Employee::set_Type*" "HandlerEmployee Awake applies the Handler role"

    $baseWorkAvailable = Get-RequiredMethod "VehicleHandlers.Employees.HandlerEmployee" "get_IsBaseWorkAvailable"
    Assert-HiddenFromIl2Cpp $baseWorkAvailable "Managed-only work eligibility is hidden from IL2CPP method injection"
    Assert-Call $baseWorkAvailable "*Il2CppScheduleOne.Employees.Employee::get_Fired*" "Vehicle Handler work eligibility rejects fired employees"
    Assert-Call $baseWorkAvailable "*Il2CppScheduleOne.Employees.Employee::get_PaidForToday*" "Vehicle Handler work eligibility rejects unpaid employees"
    Assert-Call $baseWorkAvailable "*Il2CppScheduleOne.Employees.Employee::get_AssignedProperty*" "Vehicle Handler work eligibility requires an assigned property"

    $bindIdentity = Get-RequiredMethod "VehicleHandlers.Employees.HandlerEmployee" "BindIdentity" @("System.String")
    Assert-Condition (-not $bindIdentity.IsPublic) "Handler identity binding remains internal to the lifecycle bridge"
    Assert-HiddenFromIl2Cpp $bindIdentity "Managed-only identity binding is hidden from IL2CPP method injection"

    $notifyUnavailable = Get-RequiredMethod "VehicleHandlers.Employees.HandlerEmployee" "NotifyUnavailable" @(
        "VehicleHandlers.Contracts.HandlerUnavailableReason"
    )
    Assert-HiddenFromIl2Cpp $notifyUnavailable "Managed-only lifecycle signaling is hidden from IL2CPP method injection"
    Assert-Call $notifyUnavailable "*Il2CppScheduleOne.NPCs.NPC::get_GUID*" "Lifecycle cleanup can recover the game-owned Handler GUID"
    Assert-Call $notifyUnavailable "*VehicleHandlers.Contracts.HandlerId::TryParse*" "Lifecycle cleanup validates the stable Handler identity"
    Assert-Call $notifyUnavailable "*VehicleHandlers.Employees.HandlerLifecycleBridge::NotifyUnavailable*" "Lifecycle cleanup forwards through the frozen sink boundary"

    $reportWorkBlocked = Get-RequiredMethod "VehicleHandlers.Employees.HandlerEmployee" "ReportVanillaWorkBlocked"
    Assert-HiddenFromIl2Cpp $reportWorkBlocked "Managed-only work diagnostics are hidden from IL2CPP method injection"
    Assert-Call $reportWorkBlocked "*MelonLoader.MelonLogger::Msg*" "Vanilla-work suppression emits a one-shot runtime diagnostic"

    $identityType = Get-RequiredType "VehicleHandlers.Employees.VehicleHandlerIdentity"
    $choiceLabelField = $identityType.Fields | Where-Object { $_.Name -ceq "ChoiceLabel" } | Select-Object -First 1
    $choiceTextField = $identityType.Fields | Where-Object { $_.Name -ceq "ChoiceText" } | Select-Object -First 1
    $employeeIdPrefixField = $identityType.Fields | Where-Object { $_.Name -ceq "EmployeeIdPrefix" } | Select-Object -First 1
    $customTypeField = $identityType.Fields | Where-Object { $_.Name -ceq "CustomEmployeeType" } | Select-Object -First 1
    Assert-Condition ($choiceLabelField.IsLiteral -and $choiceLabelField.Constant -ceq "VehicleHandler") "Vehicle Handler uses a distinct fixer choice label"
    Assert-Condition ($choiceTextField.IsLiteral -and $choiceTextField.Constant -ceq "Vehicle Handler (moving owned vehicles)") "Vehicle Handler exposes distinct player-facing choice text"
    Assert-Condition ($employeeIdPrefixField.IsLiteral -and $employeeIdPrefixField.Constant -ceq "vehiclehandler_") "Vehicle Handler uses a save-safe persistent employee ID marker"
    Assert-Condition ($customTypeField.IsLiteral -and [int] $customTypeField.Constant -eq 4) "Vehicle Handler uses the verified unused network discriminator"

    $appearancePostfix = Get-RequiredMethod "VehicleHandlers.Employees.HandlerAppearancePatch" "Postfix" @("Il2CppScheduleOne.Employees.Employee")
    Assert-Condition ($appearancePostfix.IsPrivate -and $appearancePostfix.IsStatic) "Handler appearance is isolated in a static Harmony postfix"
    Assert-Call $appearancePostfix "*GetComponent*VehicleHandlers.Employees.HandlerEmployee*" "Handler appearance excludes unmarked vanilla Handlers"
    Assert-Call $appearancePostfix "*VehicleHandlers.Employees.HandlerAppearance::TryApply*" "Handler appearance applies the role-specific overlay"

    $attachment = Get-RequiredMethod "VehicleHandlers.Employees.HandlerAttachment" "TryAttach" @(
        "Il2CppScheduleOne.Employees.Employee", "System.String", "System.String"
    )
    Assert-Call $attachment "*VehicleHandlers.Employees.HandlerEmployee::BindIdentity*" "Marker attachment binds the game-owned Handler GUID"
    Assert-Call $attachment "*VehicleHandlers.Employees.HandlerAppearance::TryApply*" "Marker attachment applies the Vehicle Handler appearance after initialization"

    $serverPrefix = Get-RequiredMethod "VehicleHandlers.Employees.HandlerCreateEmployeeServerPatch" "Prefix" @(
        "Il2CppScheduleOne.Employees.EEmployeeType&", "System.String&", "System.Boolean&"
    )
    Assert-Condition ($serverPrefix.IsPrivate -and $serverPrefix.IsStatic) "Server hiring integration uses a static Harmony prefix"
    Assert-Call $serverPrefix "*VehicleHandlers.Employees.VehicleHandlerIdentity::IsMarkedEmployeeId*" "Server hiring recognizes persisted Vehicle Handler identity"
    Assert-Call $serverPrefix "*VehicleHandlers.Employees.VehicleHandlerIdentity::MarkEmployeeId*" "Server hiring writes the persistent Vehicle Handler identity"

    $serverPostfix = Get-RequiredMethod "VehicleHandlers.Employees.HandlerCreateEmployeeServerPatch" "Postfix" @(
        "System.Boolean", "Il2CppScheduleOne.Employees.Employee", "System.String"
    )
    Assert-Condition ($serverPostfix.IsPrivate -and $serverPostfix.IsStatic) "Server hiring integration uses a static Harmony postfix"
    Assert-Call $serverPostfix "*VehicleHandlers.Employees.HandlerAttachment::TryAttach*" "Server hiring integration attaches the marker to the returned vanilla employee"

    $initializePostfix = Get-RequiredMethod "VehicleHandlers.Employees.HandlerInitializePatch" "Postfix" @(
        "Il2CppScheduleOne.Employees.Employee", "System.String", "System.String"
    )
    Assert-Condition ($initializePostfix.IsPrivate -and $initializePostfix.IsStatic) "Employee initialization integration uses a static Harmony postfix"
    Assert-Call $initializePostfix "*VehicleHandlers.Employees.VehicleHandlerIdentity::IsMarkedEmployeeId*" "Employee initialization excludes unmarked vanilla Handlers"
    Assert-Call $initializePostfix "*VehicleHandlers.Employees.HandlerAttachment::TryAttach*" "Employee initialization integration covers clients and loaded saves"

    $prefabPrefix = Get-RequiredMethod "VehicleHandlers.Employees.VehicleHandlerPrefabLookupPatch" "Prefix" @(
        "Il2CppScheduleOne.Employees.EmployeeManager", "Il2CppScheduleOne.Employees.EEmployeeType", "Il2CppScheduleOne.Employees.Employee&"
    )
    Assert-Call $prefabPrefix "*Il2CppScheduleOne.Employees.EmployeeManager::get_PackagerPrefab*" "Vehicle Handler pricing and confirmation reuse the vanilla Handler prefab"

    $choiceListPostfix = Get-RequiredMethod "VehicleHandlers.Employees.HandlerHiringChoiceListPatch" "Postfix" @(
        'Il2CppSystem.Collections.Generic.List`1<Il2CppScheduleOne.Dialogue.DialogueChoiceData>&'
    )
    Assert-Call $choiceListPostfix "*Il2CppScheduleOne.Dialogue.DialogueChoiceData::GetCopy*" "Hiring dialogue clones the vanilla Handler route"
    Assert-Call $choiceListPostfix "*Il2CppScheduleOne.Dialogue.DialogueChoiceData::set_ChoiceLabel*" "Hiring dialogue assigns a distinct Vehicle Handler label"
    Assert-Call $choiceListPostfix "*Il2CppScheduleOne.Dialogue.DialogueChoiceData::set_ChoiceText*" "Hiring dialogue assigns distinct Vehicle Handler text"
    Assert-Call $choiceListPostfix "*List*::Add*" "Hiring dialogue appends the distinct choice"

    $choiceCallbackPostfix = Get-RequiredMethod "VehicleHandlers.Employees.HandlerHiringChoiceCallbackPatch" "Postfix" @(
        "Il2CppScheduleOne.Dialogue.DialogueController_Fixer", "System.Boolean"
    )
    Assert-Call $choiceCallbackPostfix "*Il2CppScheduleOne.Dialogue.DialogueController_Fixer::set_selectedEmployeeType*" "Hiring dialogue sends the reserved Vehicle Handler discriminator"

    $lifecycleFilter = Get-RequiredMethod "VehicleHandlers.Employees.HandlerLifecycleFilter" "TryGetMarker" @(
        "Il2CppScheduleOne.Employees.Employee", "VehicleHandlers.Employees.HandlerEmployee&"
    )
    Assert-Call $lifecycleFilter "*GetComponent*VehicleHandlers.Employees.HandlerEmployee*" "Lifecycle filtering excludes every unmarked vanilla Handler"

    $blockVanillaWork = Get-RequiredMethod "VehicleHandlers.Employees.HandlerLifecycleFilter" "BlockVanillaWork" @(
        "Il2CppScheduleOne.Employees.Packager"
    )
    Assert-Call $blockVanillaWork "*VehicleHandlers.Employees.HandlerEmployee::ReportVanillaWorkBlocked*" "Vanilla-work suppression reports through the marked Handler component"

    $shouldIdlePrefix = Get-RequiredMethod "VehicleHandlers.Employees.HandlerShouldIdlePatch" "Prefix" @(
        "Il2CppScheduleOne.Employees.Packager", "System.Boolean&"
    )
    Assert-Call $shouldIdlePrefix "*VehicleHandlers.Employees.HandlerLifecycleFilter::BlockVanillaWork*" "Vehicle Handlers are forced idle instead of entering vanilla Packager work"

    foreach ($workPatch in @(
        "VehicleHandlers.Employees.HandlerStartPackagingPatch",
        "VehicleHandlers.Employees.HandlerStartPressPatch",
        "VehicleHandlers.Employees.HandlerStartMovePackagingPatch",
        "VehicleHandlers.Employees.HandlerStartMovePressPatch"
    )) {
        $workPrefix = Get-RequiredMethod $workPatch "Prefix" @("Il2CppScheduleOne.Employees.Packager")
        Assert-Call $workPrefix "*VehicleHandlers.Employees.HandlerLifecycleFilter::BlockVanillaWork*" "$workPatch rejects vanilla Packager work only for marked Vehicle Handlers"
    }

    $firePrefix = Get-RequiredMethod "VehicleHandlers.Employees.HandlerFirePatch" "Prefix" @(
        "Il2CppScheduleOne.Employees.Packager"
    )
    Assert-Call $firePrefix "*VehicleHandlers.Employees.HandlerEmployee::NotifyUnavailable*" "Firing a Vehicle Handler emits lifecycle cleanup"

    $despawnPrefix = Get-RequiredMethod "VehicleHandlers.Employees.HandlerLeaveAndDespawnPatch" "Prefix" @(
        "Il2CppScheduleOne.Employees.Employee"
    )
    Assert-Call $despawnPrefix "*VehicleHandlers.Employees.HandlerEmployee::NotifyUnavailable*" "Vehicle Handler leave/despawn emits lifecycle cleanup"

    $bridgeUnavailable = Get-RequiredMethod "VehicleHandlers.Employees.HandlerLifecycleBridge" "NotifyUnavailable" @(
        "VehicleHandlers.Contracts.HandlerId", "VehicleHandlers.Contracts.HandlerUnavailableReason"
    )
    Assert-Call $bridgeUnavailable "*VehicleHandlers.Contracts.IHandlerLifecycleSink::HandlerUnavailable*" "Lifecycle bridge forwards unavailable events to the registered sink"

    $bridgeUnloading = Get-RequiredMethod "VehicleHandlers.Employees.HandlerLifecycleBridge" "NotifyModUnloading"
    Assert-Call $bridgeUnloading "*VehicleHandlers.Contracts.IHandlerLifecycleSink::ModUnloading*" "Lifecycle bridge forwards mod unload exactly through the registered sink"

    $register = Get-RequiredMethod "VehicleHandlers.Runtime.HandlerRuntimeRegistration" "Register"
    Assert-Call $register "*ClassInjector::IsTypeRegisteredInIl2Cpp*" "Runtime registration is idempotent"
    Assert-Call $register "*ClassInjector::RegisterTypeInIl2Cpp*" "Runtime registration injects HandlerEmployee"

    $initialize = Get-RequiredMethod "VehicleHandlers.VehicleHandlersMod" "OnInitializeMelon"
    Assert-Call $initialize "*VehicleHandlers.StartupBanner::Print*" "Mod initialization prints the Vehicle Handlers startup banner"
    Assert-Call $initialize "*VehicleHandlers.Assignments.HandlerAssignmentRuntime::Initialize*" "Mod initialization installs the assignment lifecycle sink"

    $onGui = Get-RequiredMethod "VehicleHandlers.VehicleHandlersMod" "OnGUI"
    Assert-Call $onGui "*VehicleHandlers.Clipboard.HandlerClipboardSession::Draw*" "MelonLoader OnGUI renders the Vehicle Handler clipboard controls"

    $deinitialize = Get-RequiredMethod "VehicleHandlers.VehicleHandlersMod" "OnDeinitializeMelon"
    Assert-Call $deinitialize "*VehicleHandlers.Employees.HandlerLifecycleBridge::NotifyModUnloading*" "Mod shutdown enters the lifecycle cleanup boundary"
    Assert-Call $deinitialize "*VehicleHandlers.Clipboard.HandlerClipboardSession::End*" "Mod shutdown closes the clipboard assignment session"
    Assert-Call $deinitialize "*VehicleHandlers.Persistence.HandlerAssignmentPersistenceRuntime::Shutdown*" "Mod shutdown detaches assignment persistence"

    $assignmentRuntimeInitialize = Get-RequiredMethod "VehicleHandlers.Assignments.HandlerAssignmentRuntime" "Initialize"
    Assert-Call $assignmentRuntimeInitialize "*VehicleHandlers.Employees.HandlerLifecycleBridge::SetSink*" "Assignment runtime receives Handler lifecycle cleanup"

    $assignmentBeginRestore = Get-RequiredMethod "VehicleHandlers.Assignments.HandlerAssignmentRuntime" "BeginRestore"
    Assert-Call $assignmentBeginRestore "*VehicleHandlers.Assignments.HandlerAssignmentRegistry::.ctor*" "Load starts with a fresh assignment registry"
    Assert-Call $assignmentBeginRestore "*System.Collections.Generic.Dictionary*::Clear*" "Load clears cached assignment revisions"

    $assignmentLifecycle = Get-RequiredMethod "VehicleHandlers.Assignments.HandlerAssignmentRuntime/AssignmentLifecycleSink" "HandlerUnavailable" @(
        "VehicleHandlers.Contracts.HandlerId", "VehicleHandlers.Contracts.HandlerUnavailableReason"
    )
    Assert-Call $assignmentLifecycle "*VehicleHandlers.Assignments.HandlerAssignmentRegistry::TryRemove*" "Unavailable Handlers release idle vehicle assignments"

    $prepareLoad = Get-RequiredMethod "VehicleHandlers.Persistence.HandlerAssignmentPersistenceRuntime" "PrepareLoad" @("System.String")
    Assert-Call $prepareLoad "*VehicleHandlers.Assignments.HandlerAssignmentRuntime::BeginRestore*" "Persistence resets process-local assignments before each load"
    Assert-Call $prepareLoad "*VehicleHandlers.Persistence.AssignmentPersistenceFile::BuildPath*" "Persistence resolves a per-save UserData document"
    Assert-Call $prepareLoad "*VehicleHandlers.Persistence.AssignmentPersistenceFile::TryLoad*" "Persistence reads the versioned assignment document"
    Assert-Call $prepareLoad "*VehicleHandlers.Contracts.IHandlerAssignmentRegistry::TryRestore*" "Persistence restores only through the authoritative registry"

    $saveActive = Get-RequiredMethod "VehicleHandlers.Persistence.HandlerAssignmentPersistenceRuntime" "SaveActive" @("System.String")
    Assert-Call $saveActive "*VehicleHandlers.Contracts.IHandlerAssignmentRegistry::Snapshot*" "Persistence saves an immutable registry snapshot"
    Assert-Call $saveActive "*VehicleHandlers.Persistence.AssignmentPersistenceFile::Save*" "Persistence writes through the atomic file layer"

    $loadPatch = Get-RequiredMethod "VehicleHandlers.Persistence.HandlerAssignmentLoadPatch" "Postfix" @("Il2CppScheduleOne.Persistence.SaveInfo")
    Assert-Call $loadPatch "*Il2CppScheduleOne.Persistence.SaveInfo::get_SavePath*" "Load integration uses the selected save's exact root"
    Assert-Call $loadPatch "*VehicleHandlers.Persistence.HandlerAssignmentPersistenceRuntime::PrepareLoad*" "StartGame enters assignment restore"
    Assert-Call $loadPatch "*VehicleHandlers.Persistence.HandlerAssignmentPersistenceRuntime::AttachSaveManager*" "Loaded-game integration arms completed-save persistence"

    $attachSaveManager = Get-RequiredMethod "VehicleHandlers.Persistence.HandlerAssignmentPersistenceRuntime" "AttachSaveManager" @("Il2CppScheduleOne.Persistence.SaveManager")
    Assert-Call $attachSaveManager "*UnityEngine.Events.UnityEvent::RemoveListener*" "Persistence attachment is idempotent"
    Assert-Call $attachSaveManager "*UnityEngine.Events.UnityEvent::AddListener*" "Persistence subscribes to the completed-save event"

    $saveCompleted = Get-RequiredMethod "VehicleHandlers.Persistence.HandlerAssignmentPersistenceRuntime" "SaveCompleted"
    Assert-Call $saveCompleted "*VehicleHandlers.Persistence.HandlerAssignmentPersistenceRuntime::SaveActive*" "Completed game saves persist Handler assignments"

    $savePatch = Get-RequiredMethod "VehicleHandlers.Persistence.HandlerAssignmentSavePatch" "Prefix" @("Il2CppScheduleOne.Persistence.SaveManager")
    Assert-Call $savePatch "*VehicleHandlers.Persistence.HandlerAssignmentPersistenceRuntime::AttachSaveManager*" "Default save arms completed-save persistence"

    $namedSavePatch = Get-RequiredMethod "VehicleHandlers.Persistence.HandlerAssignmentNamedSavePatch" "Prefix" @("Il2CppScheduleOne.Persistence.SaveManager")
    Assert-Call $namedSavePatch "*VehicleHandlers.Persistence.HandlerAssignmentPersistenceRuntime::AttachSaveManager*" "Named save arms completed-save persistence"

    $loadCleanupPatch = Get-RequiredMethod "VehicleHandlers.Persistence.HandlerAssignmentLoadCleanupPatch" "Postfix"
    Assert-Call $loadCleanupPatch "*VehicleHandlers.Persistence.HandlerAssignmentPersistenceRuntime::ClearActiveSave*" "Leaving a save clears process-local assignment state"

    $clipboardOpen = Get-RequiredMethod "VehicleHandlers.Clipboard.HandlerManagementOpenPatch" "Prefix" @(
        'Il2CppSystem.Collections.Generic.List`1<Il2CppScheduleOne.Management.IConfigurable>'
    )
    Assert-Call $clipboardOpen "*VehicleHandlers.Clipboard.HandlerClipboardSession::Begin*" "Management clipboard selection enters the Vehicle Handler session"

    $clipboardPanel = Get-RequiredMethod "VehicleHandlers.Clipboard.HandlerManagementPanelPatch" "Postfix" @(
        "Il2CppScheduleOne.Management.ManagementInterface"
    )
    Assert-Call $clipboardPanel "*VehicleHandlers.Clipboard.HandlerClipboardSession::HideVanillaPanel*" "Vehicle Handler selection hides the vanilla Packager panel"

    $clipboardClose = Get-RequiredMethod "VehicleHandlers.Clipboard.HandlerManagementClosePatch" "Postfix"
    Assert-Call $clipboardClose "*VehicleHandlers.Clipboard.HandlerClipboardSession::End*" "Closing the management clipboard ends the Vehicle Handler session"

    $clipboardBegin = Get-RequiredMethod "VehicleHandlers.Clipboard.HandlerClipboardSession" "Begin" @(
        'Il2CppSystem.Collections.Generic.List`1<Il2CppScheduleOne.Management.IConfigurable>'
    )
    Assert-Call $clipboardBegin "*TryCast<Il2CppScheduleOne.Employees.Packager>*" "Clipboard controls require the concrete Packager-backed employee"
    Assert-Call $clipboardBegin "*VehicleHandlers.Employees.HandlerLifecycleFilter::TryGetMarker*" "Clipboard controls exclude ordinary vanilla Handlers"

    $clipboardCanWrite = Get-RequiredMethod "VehicleHandlers.Clipboard.HandlerClipboardSession" "CanWrite"
    Assert-Call $clipboardCanWrite "*Il2CppFishNet.Object.NetworkBehaviour::get_IsServerInitialized*" "Clipboard mutations are restricted to the server or host"

    $clipboardApply = Get-RequiredMethod "VehicleHandlers.Clipboard.HandlerClipboardSession" "Apply"
    Assert-Call $clipboardApply "*VehicleHandlers.Contracts.IHandlerAssignmentRegistry::TryConfigure*" "Apply uses the authoritative assignment registry"

    $clipboardRemove = Get-RequiredMethod "VehicleHandlers.Clipboard.HandlerClipboardSession" "Remove"
    Assert-Call $clipboardRemove "*VehicleHandlers.Contracts.IHandlerAssignmentRegistry::TryRemove*" "Remove uses the authoritative assignment registry"

    $vehicleChoices = Get-RequiredMethod "VehicleHandlers.Clipboard.HandlerClipboardSession" "BuildVehicleChoices"
    Assert-Call $vehicleChoices "*Il2CppScheduleOne.Vehicles.VehicleManager::get_PlayerOwnedVehicles*" "Clipboard vehicle choices come from the player-owned vehicle list"
    Assert-Call $vehicleChoices "*Il2CppScheduleOne.Vehicles.LandVehicle::get_GUID*" "Clipboard vehicle choices retain stable vehicle GUIDs"
    Assert-Call $vehicleChoices "*VehicleHandlers.Contracts.IHandlerAssignmentRegistry::Snapshot*" "Clipboard vehicle choices inspect authoritative assignment claims"
    Assert-Call $vehicleChoices "*VehicleHandlers.Clipboard.HandlerClipboardChoicePolicy::IsVehicleAvailableForHandler*" "Clipboard hides vehicles assigned to other Handlers"

    $vehicleLocation = Get-RequiredMethod "VehicleHandlers.Clipboard.HandlerClipboardSession" "TryFindLoadingDockLocation" @(
        "VehicleHandlers.Contracts.VehicleId", "System.String&", "System.String&"
    )
    Assert-Call $vehicleLocation "*Il2CppScheduleOne.Property.Property::get_OwnedProperties*" "Vehicle location labels inspect owned properties"
    Assert-Call $vehicleLocation "*Il2CppScheduleOne.Delivery.LoadingDock::get_DynamicOccupant*" "Vehicle location labels inspect live dock occupants"
    Assert-Call $vehicleLocation "*Il2CppScheduleOne.Delivery.LoadingDock::get_StaticOccupant*" "Vehicle location labels inspect parked dock occupants"

    $propertyChoices = Get-RequiredMethod "VehicleHandlers.Clipboard.HandlerClipboardSession" "BuildPropertyChoices"
    Assert-Call $propertyChoices "*Il2CppScheduleOne.Property.Property::get_OwnedProperties*" "Clipboard destination choices come from owned properties"
    Assert-Call $propertyChoices "*Il2CppScheduleOne.Delivery.LoadingDock::get_GUID*" "Clipboard bay choices retain stable dock GUIDs"

    $banner = Get-RequiredMethod "VehicleHandlers.StartupBanner" "Print"
    Assert-Condition ($banner.IsPublic -and $banner.IsStatic) "Startup banner exposes a callable static print method"

    $tryApply = Get-RequiredMethod "VehicleHandlers.Employees.HandlerAppearance" "TryApply" @("Il2CppScheduleOne.AvatarFramework.Avatar")
    Assert-Call $tryApply "*Il2CppScheduleOne.AvatarFramework.Avatar::get_Appearance*" "Handler appearance uses the v0.4.7 avatar appearance component"
    Assert-Call $tryApply "*Il2CppScheduleOne.Core.Avatar.Properties.AvatarPropertyCollection::GetFirstColorProperty*" "Handler appearance selects an instance-owned worn-object color"
    Assert-Call $tryApply "*Il2CppScheduleOne.Core.Avatar.Properties.AvatarProperty*::SetValue*" "Handler appearance applies the orange role color"
    Assert-Call $tryApply "*Il2CppScheduleOne.Avatar.AvatarAppearance::RepaintLayers*" "Handler appearance repaints the changed worn-object layers"

    Write-Output "Vehicle Handlers runtime worker verification passed ($script:PassCount checks)."
}
finally {
    $assembly.Dispose()
}
