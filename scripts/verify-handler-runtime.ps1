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
        "Il2CppScheduleOne.Employees.Employee", "System.String"
    )
    Assert-Call $attachment "*VehicleHandlers.Employees.HandlerAppearance::TryApply*" "Marker attachment applies the Vehicle Handler appearance after initialization"

    $serverPrefix = Get-RequiredMethod "VehicleHandlers.Employees.HandlerCreateEmployeeServerPatch" "Prefix" @(
        "Il2CppScheduleOne.Employees.EEmployeeType&", "System.String&", "System.Boolean&"
    )
    Assert-Condition ($serverPrefix.IsPrivate -and $serverPrefix.IsStatic) "Server hiring integration uses a static Harmony prefix"
    Assert-Call $serverPrefix "*VehicleHandlers.Employees.VehicleHandlerIdentity::IsMarkedEmployeeId*" "Server hiring recognizes persisted Vehicle Handler identity"
    Assert-Call $serverPrefix "*VehicleHandlers.Employees.VehicleHandlerIdentity::MarkEmployeeId*" "Server hiring writes the persistent Vehicle Handler identity"

    $serverPostfix = Get-RequiredMethod "VehicleHandlers.Employees.HandlerCreateEmployeeServerPatch" "Postfix" @(
        "System.Boolean", "Il2CppScheduleOne.Employees.Employee"
    )
    Assert-Condition ($serverPostfix.IsPrivate -and $serverPostfix.IsStatic) "Server hiring integration uses a static Harmony postfix"
    Assert-Call $serverPostfix "*VehicleHandlers.Employees.HandlerAttachment::TryAttach*" "Server hiring integration attaches the marker to the returned vanilla employee"

    $initializePostfix = Get-RequiredMethod "VehicleHandlers.Employees.HandlerInitializePatch" "Postfix" @(
        "Il2CppScheduleOne.Employees.Employee", "System.String"
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

    $register = Get-RequiredMethod "VehicleHandlers.Runtime.HandlerRuntimeRegistration" "Register"
    Assert-Call $register "*ClassInjector::IsTypeRegisteredInIl2Cpp*" "Runtime registration is idempotent"
    Assert-Call $register "*ClassInjector::RegisterTypeInIl2Cpp*" "Runtime registration injects HandlerEmployee"

    $initialize = Get-RequiredMethod "VehicleHandlers.VehicleHandlersMod" "OnInitializeMelon"
    Assert-Call $initialize "*VehicleHandlers.StartupBanner::Print*" "Mod initialization prints the Vehicle Handlers startup banner"

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
