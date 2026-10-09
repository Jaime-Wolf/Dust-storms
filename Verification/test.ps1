$ErrorActionPreference='Stop'
$taskSource=Join-Path (Split-Path $PSScriptRoot -Parent) 'Source'
$taskCompiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$taskExe=Join-Path ([IO.Path]::GetTempPath()) ('ApocaDustStorm-checks-'+[Guid]::NewGuid().ToString('N')+'.exe')
try {
 $taskPure=@('StormModel.cs','StormClock.cs','WindMath.cs','DustMotion.cs','HorizonMath.cs','DistanceFogMath.cs','SandSound.cs','DustDischargeModel.cs','DischargeSound.cs','StormHazardModel.cs','VehicleProtectionModel.cs','ExposureHudMath.cs','WindLootSchedule.cs') | ForEach-Object {Join-Path $taskSource $_}
 & $taskCompiler /nologo /target:exe /langversion:5 /out:$taskExe @taskPure (Join-Path $PSScriptRoot 'LightningChecks.cs') (Join-Path $PSScriptRoot 'HazardChecks.cs') (Join-Path $PSScriptRoot 'Program.cs')
 if($LASTEXITCODE -ne 0){throw 'Model check compilation failed'}
 & $taskExe
 if($LASTEXITCODE -ne 0){throw 'Model checks failed'}
 $taskRuntime=$taskPure+(@('NativeStormClock.cs','AzureAtmosphere.cs','NativeStormGuard.cs','StormView.cs','StormDistanceFog.cs','StormFog.cs','StormLighting.cs','StormCameraCache.cs','StormDiscovery.cs','DebrisMotion.cs','VehicleWind.cs','StormHorizon.cs','SandAudio.cs','DustLightning.cs','WindAudio.cs','StormShelter.cs','PlayerStormHazards.cs','StormHealthFlash.cs','StormSleep.cs','ExposureHud.cs','StormMovement.cs','StormAIMovement.cs','WindblownLizards.cs') | ForEach-Object {Join-Path $taskSource $_})
 & $taskCompiler /nologo /target:exe /langversion:5 /out:$taskExe @taskRuntime (Join-Path $PSScriptRoot 'RuntimeStubs.cs') (Join-Path $PSScriptRoot 'SleepFixtures.cs') (Join-Path $PSScriptRoot 'ExposureHudStubs.cs') (Join-Path $PSScriptRoot 'HazardRuntimeChecks.cs') (Join-Path $PSScriptRoot 'ExposureRuntimeChecks.cs') (Join-Path $PSScriptRoot 'HealthFlashRuntimeChecks.cs') (Join-Path $PSScriptRoot 'PerformanceRuntimeChecks.cs') (Join-Path $PSScriptRoot 'RuntimeProgram.cs')
 if($LASTEXITCODE -ne 0){throw 'Runtime harness compilation failed'}
 & $taskExe
 if($LASTEXITCODE -ne 0){throw 'Runtime checks failed'}
} finally {if(Test-Path -LiteralPath $taskExe){Remove-Item -LiteralPath $taskExe -Force}}
