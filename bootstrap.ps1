param([switch]$Flash)
$ErrorActionPreference='Stop'
Set-Location -LiteralPath $PSScriptRoot
New-Item -ItemType Directory -Force '.downloads','.tools','native/vendor' | Out-Null
function Get-Verified($Url,$File,$Hash,$Algorithm='SHA256') {
    if(!(Test-Path -LiteralPath $File)){Invoke-WebRequest -Uri $Url -OutFile $File}
    if((Get-FileHash -LiteralPath $File -Algorithm $Algorithm).Hash -ne $Hash){throw "Checksum mismatch: $File"}
}
if(!(Test-Path '.tools/dotnet/dotnet.exe')) {
    Get-Verified 'https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.404/dotnet-sdk-10.0.404-win-x64.zip' '.downloads/dotnet-sdk.zip' '330ddd2665b43cd2e428216a0d8ea1ac7bb19b5434fdc530e62010dc3b52823a620bf95489d628e1ed6f5122401793ee4887cca4a019122361a1231497f632ee' 'SHA512'
    Expand-Archive -LiteralPath '.downloads/dotnet-sdk.zip' -DestinationPath '.tools/dotnet' -Force
}
if(!(Test-Path '.tools/zig-windows-x86_64-0.13.0/zig.exe')) {
    Get-Verified 'https://ziglang.org/download/0.13.0/zig-windows-x86_64-0.13.0.zip' '.downloads/zig.zip' 'd859994725ef9402381e557c60bb57497215682e355204d754ee3df75ee3c158'
    Expand-Archive -LiteralPath '.downloads/zig.zip' -DestinationPath '.tools' -Force
}
if(!(Test-Path 'native/vendor/minhook-1.3.4/src/hook.c')) {
    Get-Verified 'https://github.com/TsudaKageyu/minhook/archive/refs/tags/v1.3.4.zip' '.downloads/minhook.zip' '172708123daa0c98d20d3a980b16a50be14af243dc95dee6f79c24193ad010e4'
    Expand-Archive -LiteralPath '.downloads/minhook.zip' -DestinationPath 'native/vendor' -Force
}
if($Flash){ & "$PSScriptRoot/scripts/prepare-flash.ps1" }
Write-Output 'Dependencies ready. Run ./build.ps1 -Restore -Test'
