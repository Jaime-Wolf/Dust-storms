param([switch]$LightningOnly)
$ErrorActionPreference='Stop'
$taskSource=Join-Path (Split-Path $PSScriptRoot -Parent) 'Source'
$taskCompiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$taskExe=Join-Path ([IO.Path]::GetTempPath()) ('ApocaDustStorm-preview-'+[Guid]::NewGuid().ToString('N')+'.exe')
$taskOutput=Join-Path $PSScriptRoot 'Sound previews'
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
try {
 & $taskCompiler /nologo /target:exe /langversion:5 /out:$taskExe (Join-Path $taskSource 'SandSound.cs') (Join-Path $taskSource 'StormModel.cs') (Join-Path $taskSource 'DischargeSound.cs') (Join-Path $PSScriptRoot 'WavePreview.cs')
 if($LASTEXITCODE -ne 0){throw 'Sound preview compilation failed'}
 if(!$LightningOnly) {
  & $taskExe (Join-Path $taskOutput 'Sand-on-metal.wav')
  if($LASTEXITCODE -ne 0){throw 'Sound preview export failed'}
 }
 & $taskExe (Join-Path $taskOutput 'Dust-static-crackle.wav') static
 if($LASTEXITCODE -ne 0){throw 'Discharge preview export failed'}
} finally {if(Test-Path -LiteralPath $taskExe){Remove-Item -LiteralPath $taskExe -Force}}
