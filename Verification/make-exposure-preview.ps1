$ErrorActionPreference='Stop'
$taskSource=Join-Path (Split-Path $PSScriptRoot -Parent) 'Source'
$taskCompiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$taskExe=Join-Path ([IO.Path]::GetTempPath()) ('ApocaDustStorm-exposure-preview-'+[Guid]::NewGuid().ToString('N')+'.exe')
$taskOutput=Join-Path $PSScriptRoot 'Previews\Exposure-indicators.png'
try {
 $taskFiles=@('StormModel.cs','WindMath.cs','StormHazardModel.cs','VehicleProtectionModel.cs','StormSleep.cs','ExposureHudMath.cs','StormShelter.cs','PlayerStormHazards.cs','ExposureHud.cs') | ForEach-Object {Join-Path $taskSource $_}
 & $taskCompiler /nologo /target:exe /langversion:5 /out:$taskExe /reference:System.Drawing.dll @taskFiles (Join-Path $PSScriptRoot 'RuntimeStubs.cs') (Join-Path $PSScriptRoot 'ExposureHudStubs.cs') (Join-Path $PSScriptRoot 'ExposurePreview.cs')
 if($LASTEXITCODE -ne 0){throw 'Preview compilation failed'}
 New-Item -ItemType Directory -Path (Split-Path $taskOutput -Parent) -Force | Out-Null
 & $taskExe $taskOutput
 if($LASTEXITCODE -ne 0){throw 'Preview generation failed'}
 Write-Output ('Saved procedural badge preview: '+$taskOutput)
}finally{if(Test-Path -LiteralPath $taskExe){Remove-Item -LiteralPath $taskExe -Force}}
