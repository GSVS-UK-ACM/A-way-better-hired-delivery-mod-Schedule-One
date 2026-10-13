[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $MelonLoaderRoot,

    [string] $ExpectedAssemblySha256 = "CEC1F3D02B38CEBB59A977FB8C23400094203C63FF7AAA5094143925475BB2CC",

    [string] $ExpectedFishNetSha256 = "E8972F81F848077A0A8B337D343D724A44E6B312A1C8B92E3892097175B59C80",

    [string] $ExpectedCoreSha256 = "A4932C4750637F901F122DF90AD0101F09DDFC3E8F204B1F3C3B5BD77F462926",

    [string] $ExpectedInteropSha256 = "A1154A31EC72D4097A0A1DB50E046D97AA6C9B60BCFEB4D0489DF6230960F674",

    [string] $ExpectedUnityCoreSha256 = "E993B0A6388D5A21748B28E5E40EBEC1BC43B01DFD550AEBBCB79F3072A48E87",

    [string] $ExpectedHarmonySha256 = "6C898933B52149E8BCF6722305C7E4D94ADD47C9E53B364096F91721624C0877",

    [string] $ExpectedImguiSha256 = "5B8CBAEB25F4F8024675805B819AE0A26DBC44CEE3EE027969390DBFE4670D20"
)

$ErrorActionPreference = "Stop"
$script:PassCount = 0

$requiredReferences = @(
    "net6\MelonLoader.dll",
    "net6\0Harmony.dll",
    "net6\Mono.Cecil.dll",
    "net6\Il2CppInterop.Runtime.dll",
    "net6\Il2CppInterop.Common.dll",
    "net6\Il2CppInterop.HarmonySupport.dll",
    "Il2CppAssemblies\Assembly-CSharp.dll",
    "Il2CppAssemblies\Il2CppScheduleOne.Core.dll",
    "Il2CppAssemblies\Il2Cppmscorlib.dll",
    "Il2CppAssemblies\Il2CppFishNet.Runtime.dll",
    "Il2CppAssemblies\UnityEngine.dll",
    "Il2CppAssemblies\UnityEngine.CoreModule.dll",
    "Il2CppAssemblies\UnityEngine.IMGUIModule.dll"
)

foreach ($relativePath in $requiredReferences) {
    $referencePath = Join-Path $MelonLoaderRoot $relativePath
    if (-not (Test-Path -LiteralPath $referencePath -PathType Leaf)) {
        throw "Missing required IL2CPP build reference: $referencePath"
    }
}

Write-Output "PASS reference layout contains all required IL2CPP build assemblies"
$script:PassCount++

$assemblyPath = Join-Path $MelonLoaderRoot "Il2CppAssemblies\Assembly-CSharp.dll"
$fishNetPath = Join-Path $MelonLoaderRoot "Il2CppAssemblies\Il2CppFishNet.Runtime.dll"
$corePath = Join-Path $MelonLoaderRoot "Il2CppAssemblies\Il2CppScheduleOne.Core.dll"
$interopPath = Join-Path $MelonLoaderRoot "net6\Il2CppInterop.Runtime.dll"
$unityCorePath = Join-Path $MelonLoaderRoot "Il2CppAssemblies\UnityEngine.CoreModule.dll"
$harmonyPath = Join-Path $MelonLoaderRoot "net6\0Harmony.dll"
$imguiPath = Join-Path $MelonLoaderRoot "Il2CppAssemblies\UnityEngine.IMGUIModule.dll"
$cecilPath = Join-Path $MelonLoaderRoot "net6\Mono.Cecil.dll"

function Assert-FileHash(
    [string] $Path,
    [string] $ExpectedSha256,
    [string] $Label
) {
    $actualHash = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
    if ($ExpectedSha256 -and $actualHash -cne $ExpectedSha256) {
        throw "$Label hash mismatch. Expected $ExpectedSha256, found $actualHash."
    }

    Write-Output "PASS $Label SHA256 $actualHash"
    $script:PassCount++
}

Assert-FileHash $assemblyPath $ExpectedAssemblySha256 "Assembly-CSharp.dll"
Assert-FileHash $fishNetPath $ExpectedFishNetSha256 "Il2CppFishNet.Runtime.dll"
Assert-FileHash $corePath $ExpectedCoreSha256 "Il2CppScheduleOne.Core.dll"
Assert-FileHash $interopPath $ExpectedInteropSha256 "Il2CppInterop.Runtime.dll"
Assert-FileHash $unityCorePath $ExpectedUnityCoreSha256 "UnityEngine.CoreModule.dll"
Assert-FileHash $harmonyPath $ExpectedHarmonySha256 "0Harmony.dll"
Assert-FileHash $imguiPath $ExpectedImguiSha256 "UnityEngine.IMGUIModule.dll"

[void] [System.Reflection.Assembly]::LoadFrom($cecilPath)
$gameAssembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($assemblyPath)
$fishNetAssembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($fishNetPath)
$coreAssembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($corePath)
$interopAssembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($interopPath)
$unityCoreAssembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($unityCorePath)
$harmonyAssembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($harmonyPath)
$imguiAssembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($imguiPath)
$assemblies = @{
    Game = $gameAssembly
    FishNet = $fishNetAssembly
    Core = $coreAssembly
    Interop = $interopAssembly
    Unity = $unityCoreAssembly
    Harmony = $harmonyAssembly
    Imgui = $imguiAssembly
}

try {
    function Get-RequiredType(
        [string] $AssemblyKey,
        [string] $FullName
    ) {
        $type = $assemblies[$AssemblyKey].MainModule.GetTypes() |
            Where-Object { $_.FullName -ceq $FullName } |
            Select-Object -First 1

        if (-not $type) {
            throw "Missing type in $AssemblyKey assembly: $FullName"
        }

        return $type
    }

    function Assert-Type(
        [string] $AssemblyKey,
        [string] $FullName,
        [string] $BaseType = ""
    ) {
        $type = Get-RequiredType $AssemblyKey $FullName
        if (-not ($type.IsPublic -or $type.IsNestedPublic)) {
            throw "Expected public type: $FullName"
        }

        if ($BaseType -and $type.BaseType.FullName -cne $BaseType) {
            throw "Expected $FullName to derive from $BaseType; found $($type.BaseType.FullName)."
        }

        Write-Output "PASS type $FullName"
        $script:PassCount++
    }

    function Assert-Method(
        [string] $AssemblyKey,
        [string] $TypeName,
        [string] $MethodName,
        [string] $ReturnType,
        [string[]] $ParameterTypes = @(),
        [bool] $IsStatic = $false,
        [int] $GenericParameterCount = 0,
        [Nullable[bool]] $IsVirtual = $null
    ) {
        $type = Get-RequiredType $AssemblyKey $TypeName
        $matches = @($type.Methods | Where-Object {
            if ($_.Name -cne $MethodName -or
                $_.Parameters.Count -ne $ParameterTypes.Count -or
                $_.GenericParameters.Count -ne $GenericParameterCount) {
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
            throw "Expected one method: $TypeName::$MethodName($($ParameterTypes -join ', ')); found $($matches.Count)."
        }

        $method = $matches[0]
        if ($method.ReturnType.FullName -cne $ReturnType) {
            throw "Expected $TypeName::$MethodName return type $ReturnType; found $($method.ReturnType.FullName)."
        }

        if (-not $method.IsPublic) {
            throw "Expected public method: $TypeName::$MethodName"
        }

        if ($method.IsStatic -ne $IsStatic) {
            throw "Expected $TypeName::$MethodName static=$IsStatic; found static=$($method.IsStatic)."
        }

        if ($null -ne $IsVirtual -and $method.IsVirtual -ne [bool] $IsVirtual) {
            throw "Expected $TypeName::$MethodName virtual=$IsVirtual; found virtual=$($method.IsVirtual)."
        }

        Write-Output "PASS method $TypeName::$MethodName($($ParameterTypes -join ', ')) -> $ReturnType"
        $script:PassCount++
    }

    function Assert-Property(
        [string] $AssemblyKey,
        [string] $TypeName,
        [string] $PropertyName,
        [string] $PropertyType,
        [bool] $HasSetter,
        [bool] $IsStatic = $false
    ) {
        $type = Get-RequiredType $AssemblyKey $TypeName
        $matches = @($type.Properties | Where-Object {
            $_.Name -ceq $PropertyName -and $_.PropertyType.FullName -ceq $PropertyType
        })

        if ($matches.Count -ne 1) {
            throw "Expected property $TypeName::$PropertyName of type $PropertyType; found $($matches.Count)."
        }

        $property = $matches[0]
        if (-not $property.GetMethod -or -not $property.GetMethod.IsPublic) {
            throw "Expected public getter: $TypeName::$PropertyName"
        }

        if ($property.GetMethod.IsStatic -ne $IsStatic) {
            throw "Expected $TypeName::$PropertyName static=$IsStatic; found static=$($property.GetMethod.IsStatic)."
        }

        $actualHasSetter = $null -ne $property.SetMethod
        if ($actualHasSetter -ne $HasSetter) {
            throw "Expected $TypeName::$PropertyName hasSetter=$HasSetter; found hasSetter=$actualHasSetter."
        }

        if ($HasSetter -and -not $property.SetMethod.IsPublic) {
            throw "Expected public setter: $TypeName::$PropertyName"
        }

        Write-Output "PASS property $TypeName::$PropertyName : $PropertyType"
        $script:PassCount++
    }

    function Assert-EnumValue(
        [string] $TypeName,
        [string] $Name,
        [int] $Value
    ) {
        $type = Get-RequiredType "Game" $TypeName
        $field = $type.Fields | Where-Object { $_.Name -ceq $Name } | Select-Object -First 1
        if (-not $field -or [int] $field.Constant -ne $Value) {
            throw "Expected enum value $TypeName::$Name = $Value."
        }

        Write-Output "PASS enum $TypeName::$Name = $Value"
        $script:PassCount++
    }

    function Assert-EnumValueAvailable(
        [string] $TypeName,
        [int] $Value
    ) {
        $type = Get-RequiredType "Game" $TypeName
        $matches = @($type.Fields | Where-Object {
            $_.HasConstant -and [int] $_.Constant -eq $Value
        })

        if ($matches.Count -ne 0) {
            throw "Expected enum discriminator $TypeName value $Value to be unused; found $($matches.Name -join ', ')."
        }

        Write-Output "PASS enum discriminator $TypeName value $Value is unused"
        $script:PassCount++
    }

    # Runtime type and singleton contracts.
    Assert-Type "Game" 'Il2CppScheduleOne.DevUtilities.Singleton`1' "UnityEngine.MonoBehaviour"
    Assert-Type "Game" 'Il2CppScheduleOne.DevUtilities.NetworkSingleton`1' "Il2CppFishNet.Object.NetworkBehaviour"
    Assert-Type "Game" 'Il2CppScheduleOne.DevUtilities.PersistentSingleton`1' 'Il2CppScheduleOne.DevUtilities.Singleton`1<T>'
    Assert-Property "Game" 'Il2CppScheduleOne.DevUtilities.Singleton`1' "Instance" "T" $true $true
    Assert-Property "Game" 'Il2CppScheduleOne.DevUtilities.Singleton`1' "InstanceExists" "System.Boolean" $false $true
    Assert-Property "Game" 'Il2CppScheduleOne.DevUtilities.NetworkSingleton`1' "Instance" "T" $true $true
    Assert-Property "Game" 'Il2CppScheduleOne.DevUtilities.NetworkSingleton`1' "InstanceExists" "System.Boolean" $false $true

    Assert-Type "Game" "Il2CppScheduleOne.Employees.EmployeeManager" 'Il2CppScheduleOne.DevUtilities.NetworkSingleton`1<Il2CppScheduleOne.Employees.EmployeeManager>'
    Assert-Type "Game" "Il2CppScheduleOne.Employees.Employee" "Il2CppScheduleOne.NPCs.NPC"
    Assert-Type "Game" "Il2CppScheduleOne.Employees.Packager" "Il2CppScheduleOne.Employees.Employee"
    Assert-Type "Game" "Il2CppScheduleOne.Dialogue.DialogueController_Fixer" "Il2CppScheduleOne.Dialogue.DialogueController"
    Assert-Type "Game" "Il2CppScheduleOne.Dialogue.DialogueChoiceData" "Il2CppSystem.Object"
    Assert-Type "Game" "Il2CppScheduleOne.Property.Property" "Il2CppFishNet.Object.NetworkBehaviour"
    Assert-Type "Game" "Il2CppScheduleOne.Delivery.LoadingDock" "UnityEngine.MonoBehaviour"
    Assert-Type "Game" "Il2CppScheduleOne.Vehicles.LandVehicle" "Il2CppFishNet.Object.NetworkBehaviour"
    Assert-Type "Game" "Il2CppScheduleOne.Vehicles.VehicleManager" 'Il2CppScheduleOne.DevUtilities.NetworkSingleton`1<Il2CppScheduleOne.Vehicles.VehicleManager>'
    Assert-Type "Game" "Il2CppScheduleOne.AvatarFramework.Avatar" "UnityEngine.MonoBehaviour"
    Assert-Type "Game" "Il2CppScheduleOne.Avatar.AvatarAppearance" "UnityEngine.MonoBehaviour"
    Assert-Type "Interop" "Il2CppInterop.Runtime.Injection.ClassInjector" "System.Object"
    Assert-Type "Unity" "UnityEngine.Component" "UnityEngine.Object"
    Assert-Type "Unity" "UnityEngine.GameObject" "UnityEngine.Object"
    Assert-Type "Unity" "UnityEngine.Behaviour" "UnityEngine.Component"
    Assert-Type "Unity" "UnityEngine.MonoBehaviour" "UnityEngine.Behaviour"
    Assert-Type "Harmony" "HarmonyLib.HarmonyPatch" "HarmonyLib.HarmonyAttribute"
    Assert-Type "Harmony" "HarmonyLib.HarmonyPostfix" "System.Attribute"
    Assert-Type "Imgui" "UnityEngine.GUI" "Il2CppSystem.Object"

    Assert-Method "Interop" "Il2CppInterop.Runtime.Injection.ClassInjector" "IsTypeRegisteredInIl2Cpp" "System.Boolean" @() $true 1
    Assert-Method "Interop" "Il2CppInterop.Runtime.Injection.ClassInjector" "RegisterTypeInIl2Cpp" "System.Void" @() $true 1
    Assert-Method "Unity" "UnityEngine.Component" "GetComponent" "T" @() $false 1
    Assert-Property "Unity" "UnityEngine.Component" "gameObject" "UnityEngine.GameObject" $false
    Assert-Method "Unity" "UnityEngine.GameObject" "AddComponent" "T" @() $false 1
    Assert-Property "Unity" "UnityEngine.Behaviour" "enabled" "System.Boolean" $true
    Assert-Method "Harmony" "HarmonyLib.HarmonyPatch" ".ctor" "System.Void" @(
        "System.Type", "System.String", "System.Type[]"
    )
    Assert-Method "Harmony" "HarmonyLib.HarmonyPostfix" ".ctor" "System.Void"
    Assert-Property "Imgui" "UnityEngine.GUI" "depth" "System.Int32" $true $true
    Assert-Method "Imgui" "UnityEngine.GUI" "Box" "System.Void" @("UnityEngine.Rect", "System.String") $true
    Assert-Method "Imgui" "UnityEngine.GUI" "Label" "System.Void" @("UnityEngine.Rect", "System.String") $true
    Assert-Method "Imgui" "UnityEngine.GUI" "Button" "System.Boolean" @("UnityEngine.Rect", "System.String") $true
    Assert-Method "Imgui" "UnityEngine.GUI" "Toggle" "System.Boolean" @("UnityEngine.Rect", "System.Boolean", "System.String") $true

    # Handler creation and base employee lifecycle.
    Assert-EnumValue "Il2CppScheduleOne.Employees.EEmployeeType" "Handler" 1
    Assert-EnumValueAvailable "Il2CppScheduleOne.Employees.EEmployeeType" 4
    Assert-Property "Game" "Il2CppScheduleOne.Employees.EmployeeManager" "AllEmployees" 'Il2CppSystem.Collections.Generic.List`1<Il2CppScheduleOne.Employees.Employee>' $true
    Assert-Property "Game" "Il2CppScheduleOne.Employees.EmployeeManager" "PackagerPrefab" "Il2CppScheduleOne.Employees.Packager" $true
    Assert-Method "Game" "Il2CppScheduleOne.Employees.EmployeeManager" "CreateNewEmployee" "System.Void" @(
        "Il2CppScheduleOne.Property.Property", "Il2CppScheduleOne.Employees.EEmployeeType"
    )
    Assert-Method "Game" "Il2CppScheduleOne.Employees.EmployeeManager" "CreateEmployee_Server" "Il2CppScheduleOne.Employees.Employee" @(
        "Il2CppScheduleOne.Property.Property", "Il2CppScheduleOne.Employees.EEmployeeType", "System.String",
        "System.String", "System.String", "System.Boolean", "System.Int32", "UnityEngine.Vector3",
        "UnityEngine.Quaternion", "System.String"
    )
    Assert-Method "Game" "Il2CppScheduleOne.Employees.EmployeeManager" "GetEmployeePrefab" "Il2CppScheduleOne.Employees.Employee" @(
        "Il2CppScheduleOne.Employees.EEmployeeType"
    )
    Assert-Method "Game" "Il2CppScheduleOne.Employees.EmployeeManager" "GetEmployeesByType" 'Il2CppSystem.Collections.Generic.List`1<Il2CppScheduleOne.Employees.Employee>' @(
        "Il2CppScheduleOne.Employees.EEmployeeType"
    )

    Assert-Property "Game" "Il2CppScheduleOne.Employees.Employee" "AssignedProperty" "Il2CppScheduleOne.Property.Property" $true
    Assert-Property "Game" "Il2CppScheduleOne.Employees.Employee" "EmployeeIndex" "System.Int32" $true
    Assert-Property "Game" "Il2CppScheduleOne.Employees.Employee" "PaidForToday" "System.Boolean" $true
    Assert-Property "Game" "Il2CppScheduleOne.Employees.Employee" "Type" "Il2CppScheduleOne.Employees.EEmployeeType" $true
    Assert-Property "Game" "Il2CppScheduleOne.Employees.Employee" "EmployeeType" "Il2CppScheduleOne.Employees.EEmployeeType" $false
    Assert-Property "Game" "Il2CppScheduleOne.Employees.Employee" "Fired" "System.Boolean" $true
    Assert-Method "Game" "Il2CppScheduleOne.Employees.Employee" "Initialize" "System.Void" @(
        "Il2CppFishNet.Connection.NetworkConnection", "System.String", "System.String", "System.String",
        "System.String", "System.String", "System.Boolean", "System.Int32"
    ) $false 0 $true
    Assert-Method "Game" "Il2CppScheduleOne.Employees.Employee" "AssignProperty" "System.Void" @(
        "Il2CppScheduleOne.Property.Property", "System.Boolean"
    ) $false 0 $true
    Assert-Method "Game" "Il2CppScheduleOne.Employees.Employee" "UnassignProperty" "System.Void" @() $false 0 $true
    Assert-Method "Game" "Il2CppScheduleOne.Employees.Employee" "Fire" "System.Void" @() $false 0 $true
    Assert-Method "Game" "Il2CppScheduleOne.Employees.Employee" "LeavePropertyAndDespawn" "System.Void"
    Assert-Method "Game" "Il2CppScheduleOne.Employees.Employee" "GetHome" "Il2CppScheduleOne.Employees.EmployeeHome" @() $false 0 $true
    Assert-Method "Game" "Il2CppScheduleOne.Employees.Employee" "RemoveDailyWage" "System.Void"
    Assert-Method "Game" "Il2CppScheduleOne.Employees.Employee" "ShouldSave" "System.Boolean" @() $false 0 $true
    Assert-Method "Game" "Il2CppScheduleOne.Employees.Employee" "InitializeAppearance" "System.Void" @(
        "System.Boolean", "System.Int32"
    ) $false 0 $true
    Assert-Method "Game" "Il2CppScheduleOne.Employees.Packager" "ShouldIdle" "System.Boolean" @() $false 0 $true
    Assert-Method "Game" "Il2CppScheduleOne.Employees.Packager" "StartPackaging" "System.Void" @(
        "Il2CppScheduleOne.ObjectScripts.PackagingStation"
    )
    Assert-Method "Game" "Il2CppScheduleOne.Employees.Packager" "StartPress" "System.Void" @(
        "Il2CppScheduleOne.ObjectScripts.BrickPress"
    )
    Assert-Method "Game" "Il2CppScheduleOne.Employees.Packager" "StartMoveItem" "System.Void" @(
        "Il2CppScheduleOne.ObjectScripts.PackagingStation"
    )
    Assert-Method "Game" "Il2CppScheduleOne.Employees.Packager" "StartMoveItem" "System.Void" @(
        "Il2CppScheduleOne.ObjectScripts.BrickPress"
    )
    Assert-Method "Game" "Il2CppScheduleOne.Employees.Packager" "Fire" "System.Void" @() $false 0 $true
    Assert-Property "Game" "Il2CppScheduleOne.NPCs.NPC" "Avatar" "Il2CppScheduleOne.AvatarFramework.Avatar" $true
    Assert-Property "Game" "Il2CppScheduleOne.AvatarFramework.Avatar" "Appearance" "Il2CppScheduleOne.Avatar.AvatarAppearance" $false
    Assert-Property "Game" "Il2CppScheduleOne.Avatar.AvatarAppearance" "_wornObjects" 'Il2CppSystem.Collections.Generic.List`1<Il2CppScheduleOne.Core.Avatar.AvatarObject>' $true
    Assert-Property "Core" "Il2CppScheduleOne.Core.Avatar.AvatarObject" "PropertyCollection" "Il2CppScheduleOne.Core.Avatar.Properties.AvatarPropertyCollection" $false
    Assert-Method "Core" "Il2CppScheduleOne.Core.Avatar.Properties.AvatarPropertyCollection" "GetFirstColorProperty" "Il2CppScheduleOne.Core.Avatar.Properties.ColorProperty"
    Assert-Method "Core" 'Il2CppScheduleOne.Core.Avatar.Properties.AvatarProperty`1' "SetValue" "System.Void" @("T")
    Assert-Method "Game" "Il2CppScheduleOne.Avatar.AvatarAppearance" "RepaintLayers" "System.Void"
    Assert-Property "Game" "Il2CppScheduleOne.Dialogue.DialogueController_Fixer" "selectedEmployeeType" "Il2CppScheduleOne.Employees.EEmployeeType" $true
    Assert-Method "Game" "Il2CppScheduleOne.Dialogue.DialogueController_Fixer" "ChoiceCallback" "System.Void" @("System.String") $false 0 $true
    Assert-Method "Game" "Il2CppScheduleOne.Dialogue.DialogueController_Fixer" "ModifyChoiceList" "System.Void" @(
        "System.String", 'Il2CppSystem.Collections.Generic.List`1<Il2CppScheduleOne.Dialogue.DialogueChoiceData>&'
    ) $false 0 $true
    Assert-Method "Game" "Il2CppScheduleOne.Dialogue.DialogueController_Fixer" "Confirm" "System.Void"
    Assert-Property "Game" "Il2CppScheduleOne.Dialogue.DialogueChoiceData" "ChoiceText" "System.String" $true
    Assert-Property "Game" "Il2CppScheduleOne.Dialogue.DialogueChoiceData" "ChoiceLabel" "System.String" $true
    Assert-Method "Game" "Il2CppScheduleOne.Dialogue.DialogueChoiceData" "GetCopy" "Il2CppScheduleOne.Dialogue.DialogueChoiceData"

    # Stable identity and owned-object selection.
    Assert-Type "Game" "Il2Cpp.GUIDManager" "Il2CppSystem.Object"
    Assert-Method "Game" "Il2Cpp.GUIDManager" "GetObject" "T" @("Il2CppSystem.Guid") $true 1
    Assert-Property "Game" "Il2CppScheduleOne.Property.Property" "Properties" 'Il2CppSystem.Collections.Generic.List`1<Il2CppScheduleOne.Property.Property>' $true $true
    Assert-Property "Game" "Il2CppScheduleOne.Property.Property" "OwnedProperties" 'Il2CppSystem.Collections.Generic.List`1<Il2CppScheduleOne.Property.Property>' $true $true
    Assert-Property "Game" "Il2CppScheduleOne.Property.Property" "PropertyCode" "System.String" $false
    Assert-Property "Game" "Il2CppScheduleOne.Property.Property" "PropertyName" "System.String" $false
    Assert-Property "Game" "Il2CppScheduleOne.Property.Property" "IsOwned" "System.Boolean" $true
    Assert-Property "Game" "Il2CppScheduleOne.Property.Property" "LoadingDocks" 'Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray`1<Il2CppScheduleOne.Delivery.LoadingDock>' $true
    Assert-Property "Game" "Il2CppScheduleOne.Property.Property" "LoadingDockCount" "System.Int32" $false
    Assert-Method "Game" "Il2CppScheduleOne.Property.PropertyManager" "GetProperty" "Il2CppScheduleOne.Property.Property" @("System.String")
    Assert-Property "Game" "Il2CppScheduleOne.Vehicles.VehicleManager" "AllVehicles" 'Il2CppSystem.Collections.Generic.List`1<Il2CppScheduleOne.Vehicles.LandVehicle>' $true
    Assert-Property "Game" "Il2CppScheduleOne.Vehicles.VehicleManager" "PlayerOwnedVehicles" 'Il2CppSystem.Collections.Generic.List`1<Il2CppScheduleOne.Vehicles.LandVehicle>' $true
    Assert-Property "Game" "Il2CppScheduleOne.Vehicles.LandVehicle" "GUID" "Il2CppSystem.Guid" $true
    Assert-Property "Game" "Il2CppScheduleOne.Vehicles.LandVehicle" "IsPlayerOwned" "System.Boolean" $true
    Assert-Property "Game" "Il2CppScheduleOne.Vehicles.LandVehicle" "VehicleName" "System.String" $false
    Assert-Property "Game" "Il2CppScheduleOne.Vehicles.LandVehicle" "VehicleCode" "System.String" $false
    Assert-Property "Game" "Il2CppScheduleOne.NPCs.NPC" "FullName" "System.String" $false

    # Management clipboard integration used by Vehicle Handler assignment controls.
    Assert-Type "Game" "Il2CppScheduleOne.Management.ManagementInterface" 'Il2CppScheduleOne.DevUtilities.Singleton`1<Il2CppScheduleOne.Management.ManagementInterface>'
    Assert-Type "Game" "Il2CppScheduleOne.Tools.ManagementClipboard_Equippable" "Il2CppScheduleOne.Equipping.Equippable_Viewmodel"
    Assert-Type "Game" "Il2CppScheduleOne.Management.UI.ConfigPanel" "UnityEngine.MonoBehaviour"
    Assert-Property "Game" "Il2CppScheduleOne.Management.ManagementInterface" "loadedPanel" "Il2CppScheduleOne.Management.UI.ConfigPanel" $true
    Assert-Method "Game" "Il2CppScheduleOne.Management.ManagementInterface" "Open" "System.Void" @(
        'Il2CppSystem.Collections.Generic.List`1<Il2CppScheduleOne.Management.IConfigurable>',
        "Il2CppScheduleOne.Tools.ManagementClipboard_Equippable"
    )
    Assert-Method "Game" "Il2CppScheduleOne.Management.ManagementInterface" "InitializeConfigPanel" "System.Void"
    Assert-Method "Game" "Il2CppScheduleOne.Management.ManagementInterface" "Close" "System.Void" @("System.Boolean")

    # Delivery availability, dock occupancy, parking, and physical movement.
    Assert-Method "Game" "Il2CppScheduleOne.Delivery.DeliveryManager" "IsLoadingBayFree" "System.Boolean" @(
        "Il2CppScheduleOne.Property.Property", "System.Int32"
    )
    Assert-Property "Game" "Il2CppScheduleOne.Delivery.LoadingDock" "GUID" "Il2CppSystem.Guid" $true
    Assert-Property "Game" "Il2CppScheduleOne.Delivery.LoadingDock" "Name" "System.String" $false
    Assert-Property "Game" "Il2CppScheduleOne.Delivery.LoadingDock" "ParentProperty" "Il2CppScheduleOne.Property.Property" $true
    Assert-Property "Game" "Il2CppScheduleOne.Delivery.LoadingDock" "DynamicOccupant" "Il2CppScheduleOne.Vehicles.LandVehicle" $true
    Assert-Property "Game" "Il2CppScheduleOne.Delivery.LoadingDock" "StaticOccupant" "Il2CppScheduleOne.Vehicles.LandVehicle" $true
    Assert-Property "Game" "Il2CppScheduleOne.Delivery.LoadingDock" "IsInUse" "System.Boolean" $false
    Assert-Property "Game" "Il2CppScheduleOne.Delivery.LoadingDock" "Parking" "Il2CppScheduleOne.Map.ParkingLot" $true
    Assert-Method "Game" "Il2CppScheduleOne.Delivery.LoadingDock" "RefreshOccupant" "System.Void"
    Assert-Method "Game" "Il2CppScheduleOne.Delivery.LoadingDock" "SetOccupant" "System.Void" @(
        "Il2CppScheduleOne.Vehicles.LandVehicle"
    )
    Assert-Property "Game" "Il2CppScheduleOne.Map.ParkingSpot" "AlignmentPoint" "UnityEngine.Transform" $true
    Assert-Property "Game" "Il2CppScheduleOne.Map.ParkingSpot" "OccupantVehicle" "Il2CppScheduleOne.Vehicles.LandVehicle" $true
    Assert-Method "Game" "Il2CppScheduleOne.Map.ParkingSpot" "SetOccupant" "System.Void" @(
        "Il2CppScheduleOne.Vehicles.LandVehicle"
    )

    Assert-Property "Game" "Il2CppScheduleOne.Vehicles.LandVehicle" "IsVisible" "System.Boolean" $true
    Assert-Property "Game" "Il2CppScheduleOne.Vehicles.LandVehicle" "CurrentPlayerOccupancy" "System.Int32" $false
    Assert-Property "Game" "Il2CppScheduleOne.Vehicles.LandVehicle" "IsOccupied" "System.Boolean" $true
    Assert-Property "Game" "Il2CppScheduleOne.Vehicles.LandVehicle" "Speed_Kmh" "System.Single" $true
    Assert-Property "Game" "Il2CppScheduleOne.Vehicles.LandVehicle" "isParked" "System.Boolean" $false
    Assert-Property "Game" "Il2CppScheduleOne.Vehicles.LandVehicle" "CurrentParkingSpot" "Il2CppScheduleOne.Map.ParkingSpot" $true
    Assert-Method "Game" "Il2CppScheduleOne.Vehicles.LandVehicle" "SetTransform_Server" "System.Void" @(
        "UnityEngine.Vector3", "UnityEngine.Quaternion"
    )
    Assert-Method "Game" "Il2CppScheduleOne.Vehicles.LandVehicle" "SetTransform" "System.Void" @(
        "UnityEngine.Vector3", "UnityEngine.Quaternion"
    )
    Assert-Method "Game" "Il2CppScheduleOne.Vehicles.LandVehicle" "SetVisible" "System.Void" @("System.Boolean")
    Assert-Method "Game" "Il2CppScheduleOne.Vehicles.LandVehicle" "ExitPark" "System.Void" @("System.Boolean")
    Assert-Method "Game" "Il2CppScheduleOne.Vehicles.LandVehicle" "GetAlignmentTransform" 'Il2CppSystem.Tuple`2<UnityEngine.Vector3,UnityEngine.Quaternion>' @(
        "UnityEngine.Transform", "Il2CppScheduleOne.Vehicles.EParkingAlignment"
    )
    Assert-Property "Game" "Il2CppScheduleOne.NPCs.NPC" "GUID" "Il2CppSystem.Guid" $true
    Assert-Property "Game" "Il2CppScheduleOne.NPCs.NPC" "CurrentVehicle" "Il2CppScheduleOne.Vehicles.LandVehicle" $true
    Assert-Property "Game" "Il2CppScheduleOne.NPCs.NPC" "IsConscious" "System.Boolean" $false
    Assert-Method "Game" "Il2CppScheduleOne.NPCs.NPC" "EnterVehicle" "System.Void" @(
        "Il2CppFishNet.Connection.NetworkConnection", "Il2CppScheduleOne.Vehicles.LandVehicle"
    ) $false 0 $true
    Assert-Method "Game" "Il2CppScheduleOne.NPCs.NPC" "ExitVehicle" "System.Void" @() $false 0 $true

    # Server-authority and replication signals inherited by employees, properties, and vehicles.
    Assert-Type "FishNet" "Il2CppFishNet.Object.NetworkBehaviour" "UnityEngine.MonoBehaviour"
    Assert-Property "FishNet" "Il2CppFishNet.Object.NetworkBehaviour" "IsSpawned" "System.Boolean" $false
    Assert-Property "FishNet" "Il2CppFishNet.Object.NetworkBehaviour" "IsServerInitialized" "System.Boolean" $false
    Assert-Property "FishNet" "Il2CppFishNet.Object.NetworkBehaviour" "IsClientInitialized" "System.Boolean" $false
    Assert-Property "FishNet" "Il2CppFishNet.Object.NetworkBehaviour" "NetworkObject" "Il2CppFishNet.Object.NetworkObject" $false
    Assert-Property "FishNet" "Il2CppFishNet.Object.NetworkBehaviour" "Owner" "Il2CppFishNet.Connection.NetworkConnection" $false

    # Save and load integration points for mod-owned assignment state.
    Assert-Type "Game" "Il2CppScheduleOne.Persistence.SaveManager" 'Il2CppScheduleOne.DevUtilities.PersistentSingleton`1<Il2CppScheduleOne.Persistence.SaveManager>'
    Assert-Type "Game" "Il2CppScheduleOne.Persistence.LoadManager" 'Il2CppScheduleOne.DevUtilities.PersistentSingleton`1<Il2CppScheduleOne.Persistence.LoadManager>'
    Assert-Type "Game" "Il2CppScheduleOne.Persistence.SaveInfo" "Il2CppSystem.Object"
    Assert-Property "Game" "Il2CppScheduleOne.Persistence.SaveManager" "onSaveStart" "UnityEngine.Events.UnityEvent" $true
    Assert-Property "Game" "Il2CppScheduleOne.Persistence.SaveManager" "onSaveComplete" "UnityEngine.Events.UnityEvent" $true
    Assert-Property "Game" "Il2CppScheduleOne.Persistence.SaveManager" "PlayersSavePath" "System.String" $true
    Assert-Property "Game" "Il2CppScheduleOne.Persistence.SaveManager" "SaveName" "System.String" $true
    Assert-Method "Game" "Il2CppScheduleOne.Persistence.SaveManager" "Awake" "System.Void"
    Assert-Method "Game" "Il2CppScheduleOne.Persistence.SaveManager" "Save" "System.Void"
    Assert-Method "Game" "Il2CppScheduleOne.Persistence.SaveManager" "Save" "System.Void" @("System.String")
    Assert-Property "Game" "Il2CppScheduleOne.Persistence.LoadManager" "onLoadComplete" "UnityEngine.Events.UnityEvent" $true
    Assert-Property "Game" "Il2CppScheduleOne.Persistence.LoadManager" "IsLoading" "System.Boolean" $true
    Assert-Property "Game" "Il2CppScheduleOne.Persistence.LoadManager" "LoadedGameFolderPath" "System.String" $true
    Assert-Property "Game" "Il2CppScheduleOne.Persistence.LoadManager" "ActiveSaveInfo" "Il2CppScheduleOne.Persistence.SaveInfo" $true
    Assert-Property "Game" "Il2CppScheduleOne.Persistence.SaveInfo" "SavePath" "System.String" $true
    Assert-Method "Game" "Il2CppScheduleOne.Persistence.LoadManager" "StartGame" "System.Void" @(
        "Il2CppScheduleOne.Persistence.SaveInfo", "System.Boolean", "System.Boolean"
    )
    Assert-Method "Game" "Il2CppScheduleOne.Persistence.LoadManager" "CleanUp" "System.Void" @() $true
    Assert-Method "Unity" "UnityEngine.Events.UnityEvent" "AddListener" "System.Void" @("UnityEngine.Events.UnityAction")
    Assert-Method "Unity" "UnityEngine.Events.UnityEvent" "RemoveListener" "System.Void" @("UnityEngine.Events.UnityAction")

    Write-Output "Schedule I 0.4.7f12 IL2CPP Vehicle Handlers API verification passed ($script:PassCount checks)."
}
finally {
    $gameAssembly.Dispose()
    $fishNetAssembly.Dispose()
    $coreAssembly.Dispose()
    $interopAssembly.Dispose()
    $unityCoreAssembly.Dispose()
    $harmonyAssembly.Dispose()
    $imguiAssembly.Dispose()
}
