param([string]$GameDir='D:\Steam\steamapps\common\Apocalypter')
$ErrorActionPreference='Stop'
$taskSource=Join-Path (Split-Path $PSScriptRoot -Parent) 'Source'
$taskCompiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$taskTemporary=Join-Path ([IO.Path]::GetTempPath()) ('ApocaDustStorm-Harmony-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskTemporary | Out-Null
try {
 $taskStubs=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'RuntimeStubs.cs') -Raw
 $taskStubs="using System;`nusing System.Collections.Generic;`n"+$taskStubs.Substring($taskStubs.IndexOf('namespace UnityEngine'))
 $taskStubs=[regex]::Replace($taskStubs,'internal (void DoGetAxis|void DoSetVelocity|bool BeforeSetVelocity|void Drive|void SwitchState|void DoMethodCall|void OnRenderImage)\(', '[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] internal $1(')
 $taskStubPath=Join-Path $taskTemporary 'RuntimeStubs.cs';Set-Content -LiteralPath $taskStubPath -Value $taskStubs -Encoding utf8
 $taskLibrary=Join-Path $taskTemporary '0Harmony.dll';Copy-Item -LiteralPath (Join-Path $GameDir 'BepInEx\core\0Harmony.dll') -Destination $taskLibrary
 Get-ChildItem -LiteralPath (Join-Path $GameDir 'BepInEx\core') -Filter 'Mono*.dll' -File | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $taskTemporary $_.Name)}
 $taskExe=Join-Path $taskTemporary 'HarmonyCompatibilityChecks.exe'
 $taskFiles=@('StormModel.cs','StormClock.cs','WindMath.cs','StormHazardModel.cs','VehicleProtectionModel.cs','StormSleep.cs','StormShelter.cs','PlayerStormHazards.cs','StormMovement.cs','StormAIMovement.cs','StormView.cs','StormFog.cs','AzureAtmosphere.cs','StormLighting.cs','HorizonMath.cs','StormHorizon.cs','NativeStormClock.cs','DustDischargeModel.cs','DischargeSound.cs','DustLightning.cs','WindLootSchedule.cs','WindblownLizards.cs') | ForEach-Object {Join-Path $taskSource $_}
 & $taskCompiler /nologo /target:exe /langversion:5 /debug+ /optimize- /out:$taskExe /reference:$taskLibrary @taskFiles $taskStubPath (Join-Path $PSScriptRoot 'SleepFixtures.cs') (Join-Path $PSScriptRoot 'HarmonyCompatibilityChecks.cs')
 if($LASTEXITCODE -ne 0){throw 'Harmony integration fixture compilation failed'}
 & $taskExe
 if($LASTEXITCODE -ne 0){throw 'Harmony integration fixtures failed'}
} finally {
 $taskResolved=[IO.Path]::GetFullPath($taskTemporary)
 $taskAllowed=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
 if(!$taskResolved.StartsWith($taskAllowed,[StringComparison]::OrdinalIgnoreCase) -or !(Split-Path $taskResolved -Leaf).StartsWith('ApocaDustStorm-Harmony-')){throw 'Unexpected temporary cleanup target'}
 if(Test-Path -LiteralPath $taskResolved){Remove-Item -LiteralPath $taskResolved -Recurse -Force}
}
