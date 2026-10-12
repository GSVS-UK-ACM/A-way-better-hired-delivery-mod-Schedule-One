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

    $appearancePostfix = Get-RequiredMethod "VehicleHandlers.Employees.HandlerAppearancePatch" "Postfix" @("Il2CppScheduleOne.Employees.Employee")
    Assert-Condition ($appearancePostfix.IsPrivate -and $appearancePostfix.IsStatic) "Handler appearance is isolated in a static Harmony postfix"
    Assert-Call $appearancePostfix "*Il2CppScheduleOne.Employees.Employee::get_EmployeeType*" "Handler appearance filters on the game Handler role"
    Assert-Call $appearancePostfix "*VehicleHandlers.Employees.HandlerAppearance::TryApply*" "Handler appearance applies the role-specific overlay"

    $serverPostfix = Get-RequiredMethod "VehicleHandlers.Employees.HandlerCreateEmployeeServerPatch" "Postfix" @(
        "Il2CppScheduleOne.Employees.EEmployeeType", "Il2CppScheduleOne.Employees.Employee"
    )
    Assert-Condition ($serverPostfix.IsPrivate -and $serverPostfix.IsStatic) "Server hiring integration uses a static Harmony postfix"
    Assert-Call $serverPostfix "*VehicleHandlers.Employees.HandlerAttachment::TryAttach*" "Server hiring integration attaches the marker to the returned vanilla employee"

    $initializePostfix = Get-RequiredMethod "VehicleHandlers.Employees.HandlerInitializePatch" "Postfix" @("Il2CppScheduleOne.Employees.Employee")
    Assert-Condition ($initializePostfix.IsPrivate -and $initializePostfix.IsStatic) "Employee initialization integration uses a static Harmony postfix"
    Assert-Call $initializePostfix "*Il2CppScheduleOne.Employees.Employee::get_EmployeeType*" "Employee initialization integration filters on the game Handler role"
    Assert-Call $initializePostfix "*VehicleHandlers.Employees.HandlerAttachment::TryAttach*" "Employee initialization integration covers clients and loaded saves"

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
