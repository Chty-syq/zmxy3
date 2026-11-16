param([Parameter(Mandatory=$true)][string]$Path)
$ErrorActionPreference='Stop'
$capture=Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
$groups=$capture.traceEvents | Where-Object {$_.ph -in @('X','B','I','i','b')} | Group-Object pid,name | Sort-Object Count -Descending | Select-Object -First 45
$summary=foreach($group in $groups){
 $times=@($group.Group | Sort-Object ts | Select-Object -ExpandProperty ts)
 $gaps=@(for($i=1;$i -lt $times.Count;$i++){($times[$i]-$times[$i-1])/1000}) | Sort-Object
 $durations=@($group.Group | Where-Object {$null -ne $_.dur} | ForEach-Object {$_.dur/1000}) | Sort-Object
 [pscustomobject]@{event=$group.Name;count=$group.Count;gapP50Ms=if($gaps.Count){$gaps[[int][Math]::Floor(($gaps.Count-1)*.5)]};gapP95Ms=if($gaps.Count){$gaps[[int][Math]::Floor(($gaps.Count-1)*.95)]};durationP95Ms=if($durations.Count){$durations[[int][Math]::Floor(($durations.Count-1)*.95)]};durationMaxMs=if($durations.Count){$durations[-1]}}
}
[pscustomobject]@{page=$capture.page;mode=$capture.mode;softwareRendering=$capture.softwareRendering;durationMs=$capture.durationMs;workerCpuCorePercent=$capture.workerCpuCorePercent;foregroundSamples=$capture.foregroundSamples;samples=$capture.samples;timeouts=$capture.timeouts;uiQueue=$capture.uiQueue;cefQueue=$capture.cefQueue;droppedEvents=$capture.droppedEvents;eventCount=$capture.traceEvents.Count;events=$summary} | ConvertTo-Json -Depth 6
