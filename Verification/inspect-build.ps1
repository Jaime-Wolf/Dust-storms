param([string]$GameDir='D:\Steam\steamapps\common\Apocalypter',[string]$PluginPath)
$ErrorActionPreference='Stop'
Add-Type -Path (Join-Path $GameDir 'BepInEx\core\Mono.Cecil.dll')
$taskRoot=Split-Path $PSScriptRoot -Parent
$taskModules=@{}
if(!$PluginPath){$PluginPath=Join-Path $taskRoot 'Source\build\ApocaDustStorm.dll'}
try {
 foreach($taskAssembly in @('PlayMaker.dll','Assembly-CSharp.dll')) {
  $taskModules[$taskAssembly]=[Mono.Cecil.ModuleDefinition]::ReadModule((Join-Path $GameDir ('Apocalypter_Data\Managed\'+$taskAssembly)))
 }
 $taskPlugin=[Mono.Cecil.ModuleDefinition]::ReadModule($PluginPath)
 try {
  $taskCount=0
  foreach($taskType in $taskPlugin.Types) {
   foreach($taskAttr in $taskType.CustomAttributes | Where-Object {$_.AttributeType.FullName -eq 'HarmonyLib.HarmonyPatch'}) {
    $taskTarget=$taskAttr.ConstructorArguments[0].Value.FullName
    $taskMethod=$taskAttr.ConstructorArguments[1].Value
    $taskCandidates=@(foreach($taskModule in $taskModules.Values){$taskModule.Types | Where-Object FullName -eq $taskTarget})
    $taskMethods=@($taskCandidates.Methods | Where-Object Name -eq $taskMethod)
    if($taskMethods.Count -ne 1 -or !$taskMethods[0].HasBody){throw ('Patch target unavailable or ambiguous: '+$taskTarget+'::'+$taskMethod)}
    $taskCount++
   }
  }
  if($taskCount -ne 11){throw ('Expected eleven scoped patches; got '+$taskCount)}
  $taskAzureType=$taskModules['Assembly-CSharp.dll'].Types | Where-Object FullName -eq 'UnityEngine.AzureSky.AzureFogScattering'
  $taskAzurePass=$taskAzureType.Methods | Where-Object Name -eq 'OnRenderImage'
  $taskRayTransforms=@($taskAzurePass.Body.Instructions | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.FullName -eq 'UnityEngine.Vector3 UnityEngine.Transform::TransformVector(UnityEngine.Vector3)'})
  if($taskRayTransforms.Count -ne 4){throw 'Native Azure fog frustum-ray transform layout changed'}
  $taskNativeEnviro=$taskModules['Assembly-CSharp.dll'].Types | Where-Object FullName -eq 'EnviroSkyRenderingLW'
  $taskNativeImage=$taskNativeEnviro.Methods | Where-Object Name -eq 'OnRenderImage'
  $taskDistanceType=$taskPlugin.Types | Where-Object FullName -eq 'ApocaDustStorm.StormDistanceFog'
  $taskDistanceImage=$taskDistanceType.Methods | Where-Object Name -eq 'OnRenderImage'
  foreach($taskImage in @($taskNativeImage,$taskDistanceImage)){
   if(!($taskImage.CustomAttributes | Where-Object {$_.AttributeType.FullName -eq 'UnityEngine.ImageEffectOpaque'})){throw ('Opaque image stage missing: '+$taskImage.FullName)}
  }
  $taskDistanceRender=$taskDistanceType.Methods | Where-Object Name -eq 'Render'
  if($taskDistanceRender.Body.Instructions | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.FullName -match 'RenderTexture::GetTemporary'}){throw 'Distance fog must preserve the original opaque-stage source, without preparation copy'}
  if(!($taskDistanceRender.Body.Instructions | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.FullName -eq 'System.Void UnityEngine.Graphics::Blit(UnityEngine.Texture,UnityEngine.RenderTexture,UnityEngine.Material,System.Int32)'})){throw 'Explicit native fog pass0 draw missing'}
  Write-Output 'Native and added Enviro fog both use ImageEffectOpaque; original source retained; explicit fog pass0 checked.'
  function Get-TaskOwnedTypes($taskTypes){foreach($taskOwnedType in $taskTypes){$taskOwnedType; if($taskOwnedType.HasNestedTypes){Get-TaskOwnedTypes $taskOwnedType.NestedTypes}}}
  $taskOwnedTypes=@(Get-TaskOwnedTypes $taskPlugin.Types)
  $taskLightingWrites=@(foreach($taskType in $taskOwnedTypes){foreach($taskMethod in $taskType.Methods){if($taskMethod.HasBody){foreach($taskInstruction in $taskMethod.Body.Instructions){if($taskInstruction.Operand -is [Mono.Cecil.MethodReference] -and $taskInstruction.Operand.FullName -match 'RenderSettings::set_ambientProbe'){$taskMethod.FullName}}}}})
  if($taskLightingWrites.Count){throw 'Storm rendering must not install or restore a generated ambient probe'}
  $taskGiCalls=@(foreach($taskType in $taskOwnedTypes){foreach($taskMethod in $taskType.Methods){if($taskMethod.HasBody){foreach($taskInstruction in $taskMethod.Body.Instructions){if($taskInstruction.Operand -is [Mono.Cecil.MethodReference] -and $taskInstruction.Operand.FullName -match 'DynamicGI::UpdateEnvironment'){$taskMethod.FullName}}}}})
  if($taskGiCalls.Count -ne 1 -or $taskGiCalls[0] -notmatch 'StormLighting::Recover'){throw 'Environment refresh must be restricted to calm lighting recovery'}
  $taskRunnerType=$taskPlugin.Types | Where-Object FullName -eq 'ApocaDustStorm.StormRunner'
  $taskRunnerUpdate=$taskRunnerType.Methods | Where-Object Name -eq 'Update'
  $taskFirstCall=$taskRunnerUpdate.Body.Instructions | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference]} | Select-Object -First 1
  if($taskFirstCall.Operand.FullName -notmatch 'StormFog::Restore'){throw 'Runner must unwind interrupted camera state at the next frame boundary'}
  $taskRunnerLate=$taskRunnerType.Methods | Where-Object Name -eq 'LateUpdate'
  if(!($taskRunnerLate.Body.Instructions | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.FullName -match 'StormLighting::Recover'})){throw 'Calm environment recovery must run after native Update lighting'}
  Write-Output 'No ambient-probe setter in plugin; one guarded recovery GI call; next-frame override cleanup and LateUpdate recovery checked.'
  $taskForbidden=@('System.Net','System.Net.Http','System.IO.File','UnityEngine.Networking.UnityWebRequest','UnityEngine.Rigidbody::AddExplosionForce','UnityEngine.Rigidbody::.ctor','AzureTimeController::SetTimeline','AzureTimeController::SetDate','AzureTimeController::SetNewDayLength')
  $taskRefs=foreach($taskType in $taskPlugin.Types){foreach($taskMethod in $taskType.Methods){if($taskMethod.HasBody){foreach($taskInstruction in $taskMethod.Body.Instructions){if($taskInstruction.Operand -is [Mono.Cecil.MethodReference]){$taskInstruction.Operand.FullName}}}}}
  foreach($taskForbiddenName in $taskForbidden){if(@($taskRefs | Where-Object {$_.Contains($taskForbiddenName)}).Count){throw ('Unexpected IO, networking or force reference: '+$taskForbiddenName)}}
  $taskVelocityCalls=@(foreach($taskType in $taskPlugin.Types){foreach($taskMethod in $taskType.Methods){if($taskMethod.HasBody){foreach($taskInstruction in $taskMethod.Body.Instructions){if($taskInstruction.Operand -is [Mono.Cecil.MethodReference] -and $taskInstruction.Operand.FullName -match 'UnityEngine.Rigidbody::set_(velocity|angularVelocity)'){[PSCustomObject]@{Owner=$taskMethod.FullName;Call=$taskInstruction.Operand.FullName}}}}}})
  if($taskVelocityCalls.Count -ne 2 -or @($taskVelocityCalls | Where-Object {$_.Owner -notmatch 'ApocaDustStorm.WindblownLizards::PreCull'}).Count){throw 'Velocity initialization must be restricted to the newly spawned native food item'}
  $taskLootType=$taskPlugin.Types | Where-Object FullName -eq 'ApocaDustStorm.WindblownLizards'
  $taskLootSpawn=$taskLootType.Methods | Where-Object Name -eq 'PreCull'
  if(!($taskLootSpawn.Body.Instructions | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.FullName -match 'UnityEngine.Object::Instantiate'})){throw 'Fresh native food instantiation is required before initial wind toss'}
  $taskHealthCalls=@(foreach($taskType in $taskPlugin.Types){foreach($taskMethod in $taskType.Methods){if($taskMethod.HasBody){foreach($taskInstruction in $taskMethod.Body.Instructions){if($taskInstruction.Operand -is [Mono.Cecil.MethodReference] -and $taskInstruction.Operand.FullName -match 'FsmFloat::set_Value'){[PSCustomObject]@{Owner=$taskMethod.FullName}}}}}})
  if($taskHealthCalls.Count -ne 2 -or @($taskHealthCalls | Where-Object {$_.Owner -notmatch 'ApocaDustStorm.PlayerStormHazards::Tick|ApocaDustStorm.PlayerStormMovementPatch::Postfix'}).Count){throw 'Unexpected health/input variable write outside exact-player exposure and fresh input scaling'}
  $taskForceCalls=@(foreach($taskType in $taskPlugin.Types){foreach($taskMethod in $taskType.Methods){if($taskMethod.HasBody){foreach($taskInstruction in $taskMethod.Body.Instructions){if($taskInstruction.Operand -is [Mono.Cecil.MethodReference] -and $taskInstruction.Operand.FullName -match 'UnityEngine.Rigidbody::Add'){[PSCustomObject]@{Owner=$taskMethod.FullName;Call=$taskInstruction.Operand.FullName}}}}}})
  if($taskForceCalls.Count -ne 1 -or $taskForceCalls[0].Owner -notmatch 'ApocaDustStorm.VehicleWind::Tick' -or $taskForceCalls[0].Call -notmatch '::AddForceAtPosition'){throw 'Unexpected vehicle/part force call outside the capped driver wind function'}
  $taskProbe=$taskPlugin.Types | Where-Object FullName -eq 'ApocaDustStorm.DebrisWorld'
  $taskProbeCtor=$taskProbe.Methods | Where-Object Name -eq '.ctor'
  if(!($taskProbeCtor.Body.Instructions | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.FullName -match 'Collider::set_enabled'})){throw 'Disabled query-shape setup not found'}
  $taskAssets=$taskPlugin.Types | Where-Object FullName -eq 'ApocaDustStorm.StormAssets'
  if($taskAssets.Methods.Name -contains 'Tumbleweed'){throw 'Removed tumbleweed generator is still in this build'}
  $taskNpcPath=Join-Path $GameDir 'BepInEx\plugins\NPCAI\NPCAI.dll'
  if(Test-Path -LiteralPath $taskNpcPath){
   $taskNpc=[Mono.Cecil.ModuleDefinition]::ReadModule($taskNpcPath)
   try {
    foreach($taskTarget in @(@('NPCAI.Brain','BeforeSetVelocity'),@('NPCAI.Idle','Drive'))){
     $taskNpcType=$taskNpc.Types | Where-Object FullName -eq $taskTarget[0]
     $taskNpcMethods=@($taskNpcType.Methods | Where-Object Name -eq $taskTarget[1])
     if($taskNpcMethods.Count -ne 1 -or !$taskNpcMethods[0].IsStatic){throw 'Optional NPCAI method changed'}
     $taskNpcAssignments=@($taskNpcMethods[0].Body.Instructions | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.FullName -match 'UnityEngine.Rigidbody::set_velocity'})
     if($taskNpcAssignments.Count -ne 1){throw 'Optional NPCAI command layout changed'}
    }
    $taskNpcIdle=$taskNpc.Types | Where-Object FullName -eq 'NPCAI.Idle'
    $taskNpcController=$taskNpcIdle.NestedTypes | Where-Object Name -eq 'Ctl'
    if(!($taskNpcController.Fields | Where-Object {$_.Name -eq 'A' -and $_.FieldType.FullName -eq 'NPCAI.Senses/Agent'})){throw 'Optional NPCAI controller actor bridge changed'}
    $taskNpcSenses=$taskNpc.Types | Where-Object FullName -eq 'NPCAI.Senses'
    $taskNpcAgent=$taskNpcSenses.NestedTypes | Where-Object Name -eq 'Agent'
    if(!($taskNpcAgent.Fields | Where-Object {$_.Name -eq 'T' -and $_.FieldType.FullName -eq 'UnityEngine.Transform'})){throw 'Optional NPCAI actor transform changed'}
    Write-Output ('Installed NPCAI combat/wander assignment layouts verified: '+(Get-FileHash -LiteralPath $taskNpcPath -Algorithm SHA256).Hash)
   }finally{$taskNpc.Dispose()}
  }
  Write-Output ('All '+$taskCount+' Harmony targets exist; two optional NPCAI targets verified; one capped driver-wind force; fresh-food initialization only; player-only health/input variable writes; no explosion-force, networking or file-write calls.')
 } finally {$taskPlugin.Dispose()}
} finally {foreach($taskModule in $taskModules.Values){$taskModule.Dispose()}}
