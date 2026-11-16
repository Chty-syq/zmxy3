param([switch]$Restore,[switch]$Test,[string]$OutputDirectory='artifacts/demo')
$ErrorActionPreference='Stop'
$taskRoot=$PSScriptRoot
Set-Location -LiteralPath $taskRoot
$env:DOTNET_CLI_HOME=Join-Path $taskRoot '.dotnet-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
$taskDotnet=Join-Path $taskRoot '.tools/dotnet/dotnet.exe'
if(!(Test-Path $taskDotnet)){$taskDotnet=(Get-Command dotnet -ErrorAction Stop).Source}
$taskOut=[IO.Path]::GetFullPath((Join-Path $taskRoot $OutputDirectory))
New-Item -ItemType Directory -Force $taskOut | Out-Null
foreach($taskProject in @('src/ZmxyDemo/ZmxyDemo.csproj','src/ZmxySubprocess/ZmxySubprocess.csproj')) {
    if($Restore){ & $taskDotnet restore $taskProject; if($LASTEXITCODE -ne 0){throw 'Restore failed'} }
    & $taskDotnet build $taskProject -c Release -p:Platform=x64 --no-restore -o $taskOut -v quiet
    if($LASTEXITCODE -ne 0){throw ('Build failed: '+$taskProject)}
}
$taskZig=Join-Path $taskRoot '.tools/zig-windows-x86_64-0.13.0/zig.exe'
if(!(Test-Path $taskZig)){$taskZig=(Get-Command zig -ErrorAction Stop).Source}
$env:ZIG_GLOBAL_CACHE_DIR=Join-Path $taskRoot '.tools/zig-cache'
$taskMinHook=Join-Path $taskRoot 'native/vendor/minhook-1.3.4'
& $taskZig cc -target x86_64-windows-gnu -shared -O2 -I "$taskMinHook/include" -I "$taskMinHook/src" native/speed.c "$taskMinHook/src/buffer.c" "$taskMinHook/src/hook.c" "$taskMinHook/src/trampoline.c" "$taskMinHook/src/hde/hde64.c" -lwinmm -o "$taskOut/zmxy-speed.dll"
if($LASTEXITCODE -ne 0){throw 'Native build failed'}
if(Test-Path 'runtime/pepflashplayer.dll') { New-Item -ItemType Directory -Force "$taskOut/runtime" | Out-Null; Copy-Item -LiteralPath 'runtime/pepflashplayer.dll' -Destination "$taskOut/runtime/pepflashplayer.dll" -Force }
if(Test-Path 'README.md'){Copy-Item -LiteralPath 'README.md' -Destination "$taskOut/README.md" -Force}
if(Test-Path 'THIRD-PARTY'){Copy-Item -LiteralPath 'THIRD-PARTY' -Destination $taskOut -Recurse -Force}
if($Test){$taskRun=Start-Process -FilePath "$taskOut/ZmxyDemo.exe" -ArgumentList '--self-test' -WindowStyle Hidden -Wait -PassThru; Get-Content "$taskOut/self-test-results.json"; if($taskRun.ExitCode -ne 0){throw 'Self tests failed'}}
Write-Output "Built: $taskOut/ZmxyDemo.exe"
