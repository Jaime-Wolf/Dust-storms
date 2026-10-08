param(
 [string]$GameDir='D:\Steam\steamapps\common\Apocalypter',
 [string]$OutputDir=(Join-Path $PSScriptRoot 'build'),
 [switch]$Development
)
$ErrorActionPreference='Stop'
if ($Development -and !$PSBoundParameters.ContainsKey('OutputDir')) { $OutputDir=Join-Path $PSScriptRoot 'build-dev' }
$taskManaged=Join-Path $GameDir 'Apocalypter_Data\Managed'
$taskCore=Join-Path $GameDir 'BepInEx\core'
$taskCompiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
$taskReferences=@('mscorlib.dll','System.dll','System.Core.dll','netstandard.dll','UnityEngine.dll',
 'UnityEngine.CoreModule.dll','UnityEngine.PhysicsModule.dll','UnityEngine.InputLegacyModule.dll',
 'UnityEngine.ParticleSystemModule.dll','UnityEngine.AudioModule.dll','UnityEngine.IMGUIModule.dll','UnityEngine.TextRenderingModule.dll','UnityEngine.UI.dll','UnityEngine.UIModule.dll',
 'NWH.VehiclePhysics2.dll','NWH.Common.dll','PlayMaker.dll','Assembly-CSharp.dll') | ForEach-Object {Join-Path $taskManaged $_}
$taskReferences+=Join-Path $taskCore 'BepInEx.dll'
$taskReferences+=Join-Path $taskCore '0Harmony.dll'
$taskReferences+=Join-Path $GameDir 'BepInEx\plugins\Apocasetter\Apocasetter.dll'
foreach($taskReference in $taskReferences){if(!(Test-Path -LiteralPath $taskReference)){throw "Missing build reference: $taskReference"}}
$taskArgs=@('/nologo','/noconfig','/nostdlib+','/target:library','/langversion:5','/optimize+','/warn:4',('/out:'+(Join-Path $OutputDir 'ApocaDustStorm.dll')))
if ($Development) { $taskArgs+='/define:APOCA_DEV' }
$taskArgs+=$taskReferences | ForEach-Object {'/reference:'+$_}
$taskArgs+=Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' -File | ForEach-Object FullName
& $taskCompiler @taskArgs
if($LASTEXITCODE -ne 0){throw 'Storm compilation failed'}
Write-Output ('Built '+$(if($Development){'development'}else{'release'})+' '+(Join-Path $OutputDir 'ApocaDustStorm.dll'))
