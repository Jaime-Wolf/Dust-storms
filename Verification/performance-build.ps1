param([string]$GameDir='D:\Steam\steamapps\common\Apocalypter',[string]$BaselinePath,[switch]$NightFogTest,[switch]$LightingRecovery)
$ErrorActionPreference='Stop'
Add-Type -Path (Join-Path $GameDir 'BepInEx\core\Mono.Cecil.dll')
$perfRoot=Split-Path $PSScriptRoot -Parent
if(!$BaselinePath){$BaselinePath=Join-Path (Split-Path $perfRoot -Parent) 'Backups\ApocaDustStorm-0.1.39-before-elevation-overcast-0.1.40\Source\build\ApocaDustStorm.dll'}
$perfOldPath=$BaselinePath
function Get-PerfTypes($types){foreach($type in $types){$type;if($type.HasNestedTypes){Get-PerfTypes $type.NestedTypes}}}
function Get-PerfCalls($method){@($method.Body.Instructions | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference]} | ForEach-Object {$_.Operand.FullName})}
function Get-PerfBody($method){
 (($method.Body.Instructions | ForEach-Object {
  if($_.Operand -is [Mono.Cecil.FieldReference] -and $_.Operand.DeclaringType.Name -like '<PrivateImplementationDetails>*'){
   'Embedded literal: '+[BitConverter]::ToString($_.Operand.Resolve().InitialValue)
  }else{$_.ToString()}
 }) -join "`n")
}
$perfOld=[Mono.Cecil.ModuleDefinition]::ReadModule($perfOldPath)
try {
 foreach($perfVariant in @('build','build-dev')) {
  $perfNew=[Mono.Cecil.ModuleDefinition]::ReadModule((Join-Path $perfRoot ('Source\'+$perfVariant+'\ApocaDustStorm.dll')))
  try {
   $perfTypes=@(Get-PerfTypes $perfNew.Types)
   $perfPreserved=@('StormLighting','StormView','StormHorizon','StormCameraCache','WindAudio','SandAudio','VehicleWind','StormShelter','StormSleep','DustDischargeModel','DischargeSound')
   $perfCompared=0
   foreach($perfType in $perfOld.Types | Where-Object {$_.Name -in $perfPreserved}) {
    $perfOther=$perfNew.Types | Where-Object FullName -eq $perfType.FullName
    foreach($perfMethod in $perfType.Methods | Where-Object HasBody) {
     # The approved recovery fix changes just these three root lighting methods.
     if($LightingRecovery -and $perfType.Name -eq 'StormLighting' -and $perfMethod.Name -in @('Scan','Recover','Clear')){continue}
     $perfMatch=$perfOther.Methods | Where-Object FullName -eq $perfMethod.FullName
     $perfBefore=Get-PerfBody $perfMethod
     $perfAfter=Get-PerfBody $perfMatch
     if($perfBefore -ne $perfAfter){throw ('Protected gameplay/render method changed: '+$perfMethod.FullName)}
     $perfCompared++
    }
   }
   $perfVisual=$perfNew.Types | Where-Object Name -eq 'StormVisuals'
   $perfTick=$perfVisual.Methods | Where-Object Name -eq 'Tick'
   $perfCalls=Get-PerfCalls $perfTick
   if(@($perfCalls | Where-Object {$_ -match 'DustEmissionBudget::Add'}).Count -ne 2){throw 'Both dust pools must use capacity-aware budgets'}
   if(@($perfCalls | Where-Object {$_ -match 'ParticleSystem::get_particleCount'}).Count -ne 2){throw 'Both remaining dust pools must check capacity'}
   $perfCreate=$perfVisual.Methods | Where-Object Name -eq 'Create'
   $perfOldVisual=$perfOld.Types | Where-Object Name -eq 'StormVisuals'
   $perfOldCreate=$perfOldVisual.Methods | Where-Object Name -eq 'Create'
   if((($perfCreate.Body.Instructions | ForEach-Object ToString) -join "`n") -ne (($perfOldCreate.Body.Instructions | ForEach-Object ToString) -join "`n")){throw 'Particle appearance or collision setup changed'}
   if(@($perfCalls | Where-Object {$_ -match 'DustPickupSources::TryGet'}).Count -ne 2){throw 'Both dust emitters must use validated cached spawn anchors'}
   $perfAdvect=$perfVisual.Methods | Where-Object Name -eq 'Advect'
   if(!(Get-PerfCalls $perfAdvect | Where-Object {$_ -match 'DustVisibility::Hidden'})){throw 'Hidden particle retirement not connected to the production particle loop'}
   if(!(Get-PerfCalls $perfAdvect | Where-Object {$_ -match 'DustOccupancy::Observe'})){throw 'Live dust positions must rebuild occupancy in the existing scan'}
   if(@($perfCalls | Where-Object {$_ -match 'DustOccupancy::TryReserve'}).Count -ne 2){throw 'Both dust emitters must reject crowded births'}
   if(@($perfCalls | Where-Object {$_ -match 'DustVisibility::Hidden'}).Count -ne 2){throw 'Both near pools must reject invisible emission'}
   if(!(Get-PerfCalls $perfTick | Where-Object {$_ -match 'HaboobWall::Tick'})){throw 'Volumetric wall is not connected to production visuals'}
   if(!(Get-PerfCalls $perfTick | Where-Object {$_ -match 'StormBillows::Tick'})){throw 'Animated local billows are not connected to production visuals'}
   if($perfVisual.Fields | Where-Object Name -eq 'front'){throw 'Particle arrival front must be removed'}
   $perfWall=$perfNew.Types | Where-Object Name -eq 'HaboobWall'
   $perfOldMath=$perfOld.Types | Where-Object Name -eq 'HaboobMath'
   $perfNewMath=$perfNew.Types | Where-Object Name -eq 'HaboobMath'
   foreach($perfName in @('Edge','Arrival','WallFade','HeightSlot','SideFade','TopFade')){
    $before=$perfOldMath.Methods | Where-Object Name -eq $perfName
    $after=$perfNewMath.Methods | Where-Object Name -eq $perfName
    if((Get-PerfBody $before) -ne (Get-PerfBody $after)){throw ('Protected storm-front/handoff math changed: '+$perfName)}
   }
   $perfWallTick=$perfWall.Methods | Where-Object Name -eq 'Tick'
   $perfWallFields=@($perfWallTick.Body.Instructions | Where-Object {$_.Operand -is [Mono.Cecil.FieldReference]} | ForEach-Object {$_.Operand.FullName})
   if(!($perfWallFields | Where-Object {$_ -match 'Plugin::WallStrength$'}) -or ($perfWallFields | Where-Object {$_ -match 'Plugin::Dust$'})){throw 'Wall opacity must use its own control, independent of local dust amount'}
   if(Get-PerfCalls $perfWallTick | Where-Object {$_ -match 'HaboobMath::TopFade'}){throw 'Volume opacity must not duplicate an old sheet top fade'}
   if(!($perfWall.Fields | Where-Object {$_.Name -eq 'particles' -and $_.FieldType.FullName -eq 'UnityEngine.ParticleSystem/Particle[]'})){throw 'Front volume must use a reusable native particle buffer'}
   if($perfWall.Fields | Where-Object {$_.FieldType.FullName -eq 'UnityEngine.Mesh'}){throw 'Old intersecting wall mesh remains active'}
   if(!(Get-PerfCalls $perfWallTick | Where-Object {$_ -match 'HaboobVolumeMath::TerrainStep'})){throw 'Wall terrain targets must be eased continuously'}
   foreach($instruction in $perfWallTick.Body.Instructions){
    if($instruction.OpCode.Code -in @([Mono.Cecil.Cil.Code]::Newarr,[Mono.Cecil.Cil.Code]::Box)){throw 'Wall hot loop must reuse arrays and avoid boxing'}
    if($instruction.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Newobj -and $instruction.Operand.DeclaringType.FullName -notin @('UnityEngine.Vector2','UnityEngine.Vector3','UnityEngine.Color')){throw ('Unexpected wall hot-loop allocation: '+$instruction.Operand.FullName)}
   }
   foreach($method in $perfWall.Methods | Where-Object HasBody){
    if(Get-PerfCalls $method | Where-Object {$_ -match 'FindObjectsOfType|GameObject::Find|Physics::|RenderTexture::|Camera::set_|Rigidbody::|Collider::'}){throw 'Wall must not add scene searches, image passes, camera changes or physics'}
   }
   foreach($perfEffect in @('HaboobTransition','StormNearDust','StormBillows','StormLightScatter','StormVegetationWind')){
    $perfEffectType=$perfNew.Types | Where-Object Name -eq $perfEffect
    foreach($perfEffectMethod in $perfEffectType.Methods | Where-Object {$_.HasBody -and $_.Name -in @('Step','Tick','PreCull')}){
     foreach($instruction in $perfEffectMethod.Body.Instructions){
      if($instruction.OpCode.Code -in @([Mono.Cecil.Cil.Code]::Newarr,[Mono.Cecil.Cil.Code]::Box)){throw ('Effect hot path must reuse arrays without boxing: '+$perfEffectMethod.FullName)}
      if($instruction.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Newobj -and $instruction.Operand.DeclaringType.FullName -notin @('UnityEngine.Vector3','UnityEngine.Color','UnityEngine.ParticleSystem/EmitParams','ApocaDustStorm.HaboobFrame')){throw ('Unexpected effect hot-path allocation: '+$instruction.Operand.FullName)}
     }
     if(Get-PerfCalls $perfEffectMethod | Where-Object {$_ -match 'FindObjectsOfType|GameObject::Find|Physics::|RenderTexture::|Camera::set_|Rigidbody::|Collider::'}){throw ('New effect must not add global searches, image passes, camera changes or physics: '+$perfEffectMethod.FullName)}
    }
   }
   $perfLease=$perfNew.Types | Where-Object Name -eq 'StormBillowDepth'
   $perfLeaseCalls=Get-PerfCalls ($perfLease.Methods | Where-Object Name -eq 'Select')
   if(!($perfLeaseCalls | Where-Object {$_ -match 'StormDepthRequest::Acquire'}) -or !($perfLeaseCalls | Where-Object {$_ -match 'StormDepthRequest::Release'})){throw 'Soft particle depth must use cooperative acquisition and release'}
   $perfNear=$perfNew.Types | Where-Object Name -eq 'StormNearDust'
   $perfNearTick=$perfNear.Methods | Where-Object Name -eq 'Tick'
   if(!(Get-PerfCalls $perfNearTick | Where-Object {$_ -match 'StormRunner::get_ImpactStrength'})){throw 'Entry curve must drive real near-dust intensity'}
   if(!(Get-PerfCalls $perfTick | Where-Object {$_ -match 'StormNearDust::Tick'}) -or !(Get-PerfCalls $perfTick | Where-Object {$_ -match 'StormLightScatter::Tick'})){throw 'Near dust and scatter must connect to production visuals'}
   $perfRunner=$perfNew.Types | Where-Object Name -eq 'StormRunner'
   $perfRunnerCalls=Get-PerfCalls ($perfRunner.Methods | Where-Object Name -eq 'Update')
   foreach($required in @('HaboobTransition::Step','StormBrownout::Tick','StormVegetationWind::Tick','StormRunner::get_WindStrength')){
    if(!($perfRunnerCalls | Where-Object {$_ -match [regex]::Escape($required)})){throw ('Missing production phase connection: '+$required)}
   }
   $perfBrown=$perfNew.Types | Where-Object Name -eq 'StormBrownout'
   if(Get-PerfCalls ($perfBrown.Methods | Where-Object Name -eq 'Before') | Where-Object {$_ -match 'ScriptableObject::CreateInstance|PostProcessProfile::AddSettings|FindObjectsOfType|RenderTexture::'}){throw 'Per-camera grading scope must not allocate profiles, search scenes or add image passes'}
   if(!(Get-PerfCalls ($perfRunner.Methods | Where-Object Name -eq 'ShiftOrigin') | Where-Object {$_ -match 'StormVisuals::ShiftOrigin'})){throw 'Coordinate observer must update real production visuals'}
   if(!(Get-PerfCalls ($perfRunner.Methods | Where-Object Name -eq 'ShiftOrigin') | Where-Object {$_ -match 'StormModel::ShiftOrigin'})){throw 'Coordinate observer must update real production front'}
   $perfOccupancy=$perfNew.Types | Where-Object Name -eq 'DustOccupancy'
   foreach($perfMethod in $perfOccupancy.Methods | Where-Object {$_.HasBody -and $_.Name -ne '.ctor'}) {
    if($perfMethod.Body.Instructions | Where-Object {$_.OpCode.Code -in @([Mono.Cecil.Cil.Code]::Newobj,[Mono.Cecil.Cil.Code]::Newarr,[Mono.Cecil.Cil.Code]::Box)}){throw 'Occupancy hot paths must allocate no managed storage'}
   }
   $perfMotion=$perfNew.Types | Where-Object Name -eq 'DebrisMotion'
   foreach($perfMethod in $perfMotion.Methods | Where-Object HasBody){
    if(Get-PerfCalls $perfMethod | Where-Object {$_ -match 'UnityEngine.Physics::|Collider|Rigidbody'}){throw 'Cosmetic stick integration must perform zero collision or body-force queries'}
   }
   if($perfNew.Types | Where-Object {$_.Name -in @('DebrisWorld','IDebrisWorld','DebrisHit')}){throw 'Obsolete stick collision world remains in the installable assembly'}
   $perfDistance=$perfNew.Types | Where-Object Name -eq 'StormDistanceFog'
   if(!(Get-PerfCalls ($perfDistance.Methods | Where-Object Name -eq 'EnsureCamera') | Where-Object {$_ -match 'StormDistanceFog::Needed'})){throw 'No-op fog pass must be disabled before rendering'}
   if(!(Get-PerfCalls ($perfDistance.Methods | Where-Object Name -eq 'SetActive') | Where-Object {$_ -match 'StormDepthRequest::Release'})){throw 'Inactive fog must release its depth lease'}
   foreach($perfName in @('DustColour','NightWeather','BlendWeather')){
    # 0.1.32 intentionally changes DustColour; its day/night math and native
    # state restoration are covered by test.ps1. Keep the older guard by default.
    if($NightFogTest -and $perfName -eq 'DustColour'){continue}
    $before=($perfOld.Types | Where-Object Name -eq 'StormFog').Methods | Where-Object Name -eq $perfName
    $after=($perfNew.Types | Where-Object Name -eq 'StormFog').Methods | Where-Object Name -eq $perfName
    if((Get-PerfBody $before) -ne (Get-PerfBody $after)){throw ('Protected fog color behavior changed: '+$perfName)}
   }
   $perfLightning=$perfNew.Types | Where-Object Name -eq 'DustLightning'
   foreach($perfMethod in $perfLightning.Methods | Where-Object HasBody) {
    if(Get-PerfCalls $perfMethod | Where-Object {$_ -match 'DischargeSound::Generate|Task.*::Wait\('}){throw 'Lightning callbacks must not synthesize audio or wait for a task'}
   }
   $perfPrepare=$perfLightning.Methods | Where-Object Name -eq 'PrepareAudio'
   if(@(Get-PerfCalls $perfPrepare | Where-Object {$_ -match 'AudioClip::Create'}).Count -ne 1){throw 'Expected one bounded clip upload site'}
   $perfSamples=@($perfTypes | Where-Object {$_.FullName -like 'ApocaDustStorm.DischargeSamples*'})
   $perfSampleCalls=@(foreach($perfType in $perfSamples){foreach($perfMethod in $perfType.Methods | Where-Object HasBody){Get-PerfCalls $perfMethod}})
   if($perfSampleCalls | Where-Object {$_ -match 'UnityEngine\.'}){throw 'Worker/cache must not call Unity APIs'}
   if(!($perfSampleCalls | Where-Object {$_ -match 'DischargeSound::Generate'})){throw 'Background sample generator missing'}
   $perfPlugin=$perfNew.Types | Where-Object Name -eq 'Plugin'
   $perfAwake=$perfPlugin.Methods | Where-Object Name -eq 'Awake'
   if(!($perfAwake.Body.Instructions | Where-Object {$_.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldstr -and $_.Operand -eq 'Damage grace period'})){throw 'Grace slider is absent from real settings binding'}
   if(!($perfAwake.Body.Instructions | Where-Object {$_.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldstr -and $_.Operand -eq 'Approaching wall strength'})){throw 'Independent wall slider is absent from real settings binding'}
   if(!($perfPlugin.Fields | Where-Object Name -eq 'DamageGrace')){throw 'Grace configuration field missing'}
   $perfHazards=$perfNew.Types | Where-Object Name -eq 'PlayerStormHazards'
   if(!(Get-PerfCalls ($perfHazards.Methods | Where-Object Name -eq 'Tick') | Where-Object {$_ -match 'DamageGraceModel::DamageSeconds'})){throw 'Grace model not connected to production damage'}
   Write-Output ($perfVariant+': '+$perfCompared+' protected methods and particle setup unchanged; cached spawn anchors, hidden-particle retirement, collision-free stick motion, conditional fog/depth, pure audio worker and live grace binding verified. Night palette exception: '+$NightFogTest)
  } finally {$perfNew.Dispose()}
 }
} finally {$perfOld.Dispose()}

