param([string]$GameDir='D:\Steam\steamapps\common\Apocalypter')
$ErrorActionPreference='Stop'
Add-Type -Path (Join-Path $GameDir 'BepInEx\core\Mono.Cecil.dll')
$taskRoot=Split-Path $PSScriptRoot -Parent
$taskVersion=[regex]::Match((Get-Content -LiteralPath (Join-Path $taskRoot 'Source\Plugin.cs') -Raw),'VERSION\s*=\s*"([^"]+)"').Groups[1].Value
if(!$taskVersion){throw 'Source plugin version not found'}
function Get-VariantTypes($taskTypes){foreach($taskType in $taskTypes){$taskType;if($taskType.HasNestedTypes){Get-VariantTypes $taskType.NestedTypes}}}
$taskRelease=[Mono.Cecil.ModuleDefinition]::ReadModule((Join-Path $taskRoot 'Source\build\ApocaDustStorm.dll'))
$taskDev=[Mono.Cecil.ModuleDefinition]::ReadModule((Join-Path $taskRoot 'Source\build-dev\ApocaDustStorm.dll'))
try {
 $taskReleaseTypes=@(Get-VariantTypes $taskRelease.Types);$taskDevTypes=@(Get-VariantTypes $taskDev.Types)
 foreach($taskVariant in @(@($taskReleaseTypes,$false),@($taskDevTypes,$true))){
  $taskTypes=$taskVariant[0];$taskDevelopment=$taskVariant[1]
  $taskPlugin=$taskTypes | Where-Object FullName -eq 'ApocaDustStorm.Plugin'
  $taskAttr=$taskPlugin.CustomAttributes | Where-Object {$_.AttributeType.FullName -eq 'BepInEx.BepInPlugin'}
  $taskExpectedName=if($taskDevelopment){'ApocaDustStorm (dev)'}else{'ApocaDustStorm'}
  if($taskAttr.ConstructorArguments[0].Value -ne 'local.apocalypter.duststorm' -or $taskAttr.ConstructorArguments[1].Value -ne $taskExpectedName -or $taskAttr.ConstructorArguments[2].Value -ne $taskVersion){throw 'Variant plugin identity mismatch'}
  $taskKeyFields=@($taskPlugin.Fields | Where-Object {$_.Name -in @('PreviewKey','DischargeKey')})
  $taskCalls=@(foreach($taskType in $taskTypes){foreach($taskMethod in $taskType.Methods){if($taskMethod.HasBody){foreach($taskInstruction in $taskMethod.Body.Instructions){if($taskInstruction.Operand -is [Mono.Cecil.MethodReference] -and $taskInstruction.Operand.FullName -match 'UnityEngine.Input::GetKeyDown'){$taskInstruction.Operand.FullName}}}}})
  $taskStrings=@(foreach($taskType in $taskTypes){foreach($taskMethod in $taskType.Methods){if($taskMethod.HasBody){foreach($taskInstruction in $taskMethod.Body.Instructions){if($taskInstruction.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldstr){$taskInstruction.Operand}}}}})
  $taskExpectedKeys=if($taskDevelopment){2}else{0}
  if($taskKeyFields.Count -ne $taskExpectedKeys -or $taskCalls.Count -ne $taskExpectedKeys){throw 'Test input must be present only in the development DLL'}
  $taskPreviewStrings=@($taskStrings | Where-Object {$_ -eq 'Preview storm' -or $_ -eq 'Preview dust lightning' -or $_ -like 'DUST STORM TEST*'})
  if(($taskDevelopment -and $taskPreviewStrings.Count -ne 3) -or (!$taskDevelopment -and $taskPreviewStrings.Count -ne 0)){throw 'Preview settings/HUD leaked into the release variant or disappeared from dev'}
  if($taskStrings -notcontains 'Show storm notices' -or $taskStrings -notcontains 'Show test status'){throw 'Storm notice preference migration missing'}
 }
 $taskCompared=0
 foreach($taskType in $taskReleaseTypes | Where-Object {$_.FullName -notin @('ApocaDustStorm.Plugin','ApocaDustStorm.StormRunner') -and $_.FullName -notlike '<PrivateImplementationDetails>*'}){
  $taskOther=$taskDevTypes | Where-Object FullName -eq $taskType.FullName
  if(!$taskOther){throw 'Gameplay type absent in development variant'}
  foreach($taskMethod in $taskType.Methods | Where-Object HasBody){
   $taskMatch=$taskOther.Methods | Where-Object FullName -eq $taskMethod.FullName
   # The legacy compiler includes each assembly's random ID in literal-array owners.
   $taskLeft=(($taskMethod.Body.Instructions | ForEach-Object {$_.ToString()}) -join "`n") -replace '<PrivateImplementationDetails>\{[0-9A-Fa-f-]+\}','<PrivateImplementationDetails>'
   $taskRight=(($taskMatch.Body.Instructions | ForEach-Object {$_.ToString()}) -join "`n") -replace '<PrivateImplementationDetails>\{[0-9A-Fa-f-]+\}','<PrivateImplementationDetails>'
   if($taskLeft -ne $taskRight){throw ('Variant gameplay diverged: '+$taskMethod.FullName)}
   $taskCompared++
  }
 }
 $taskReleaseData=@($taskReleaseTypes | Where-Object {$_.FullName -like '<PrivateImplementationDetails>*'} | ForEach-Object {$_.Fields | Where-Object {$_.InitialValue.Length -gt 0} | ForEach-Object {[BitConverter]::ToString($_.InitialValue)}} | Sort-Object)
 $taskDevData=@($taskDevTypes | Where-Object {$_.FullName -like '<PrivateImplementationDetails>*'} | ForEach-Object {$_.Fields | Where-Object {$_.InitialValue.Length -gt 0} | ForEach-Object {[BitConverter]::ToString($_.InitialValue)}} | Sort-Object)
 if(($taskReleaseData -join '|') -ne ($taskDevData -join '|')){throw 'Embedded gameplay literal arrays diverged'}
 Write-Output ('Release: no preview fields, bindings, test HUD or test-key input calls. Dev: both test bindings/input handlers retained. '+$taskCompared+' shared gameplay methods identical; identities and notice migration checked.')
}finally{$taskRelease.Dispose();$taskDev.Dispose()}
