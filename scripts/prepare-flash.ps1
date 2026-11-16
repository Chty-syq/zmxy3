param([string]$SevenZip='C:\Program Files\7-Zip\7z.exe')
$ErrorActionPreference='Stop'
Set-Location -LiteralPath (Split-Path -Parent $PSScriptRoot)
if(!(Test-Path -LiteralPath $SevenZip)){throw 'Install 7-Zip or supply -SevenZip with its full path.'}
$url='https://www.flash.cn/flashplayer/hm/3400384/install_flash_player_ppapi_cn.exe'
$download=Join-Path (Get-Location) '.downloads/flash-hm.exe'
New-Item -ItemType Directory -Force '.downloads','runtime' | Out-Null
if(!(Test-Path $download)){Invoke-WebRequest -Uri $url -OutFile $download}
if((Get-FileHash $download -Algorithm SHA256).Hash -ne 'F3101C7CB5BC834E4807CCD127E0B4963EA59FBC49942B84593BD30A0318CE70'){throw 'Installer checksum mismatch'}
if((Get-AuthenticodeSignature $download).Status -ne 'Valid'){throw 'Installer signature is not valid'}
function Expand-Resource($InputFile,$Resource,$Output) {
    $raw=Join-Path $Output 'raw'
    New-Item -ItemType Directory -Path $raw -Force | Out-Null
    & $SevenZip e $InputFile ("-o"+$raw) ('.rsrc/0/RCDATA/'+$Resource) -y | Out-Null
    if($LASTEXITCODE -gt 1){throw 'PE resource extraction failed'}
    $bytes=[IO.File]::ReadAllBytes((Join-Path $raw $Resource))
    # Adobe's resource wrapper stores a big-endian size and LZMA properties.
    [byte[]]$length=@($bytes[10],$bytes[9],$bytes[8],$bytes[7],0,0,0,0)
    $lzma=Join-Path $Output ($Resource+'.lzma')
    [IO.File]::WriteAllBytes($lzma,[byte[]]($bytes[11..15]+$length+$bytes[16..($bytes.Length-1)]))
    & $SevenZip e $lzma ("-o"+$Output) -y | Out-Null
    if($LASTEXITCODE -ne 0){throw 'LZMA extraction failed'}
    return (Join-Path $Output $Resource)
}
$work=Join-Path (Get-Location) '.downloads/official-flash'
$inner=Expand-Resource $download '206' (Join-Path $work 'outer')
$dll=Expand-Resource $inner '215' (Join-Path $work 'x64')
# Final signature check uses the DLL extension so Windows selects the PE provider.
$candidate=Join-Path $work 'pepflashplayer.dll'
Copy-Item -LiteralPath $dll -Destination $candidate -Force
if((Get-FileHash $candidate -Algorithm SHA256).Hash -ne 'F9B40AE54CAA8A551312B3B83CC6CDF027DB51B665C047486BA5C534A6E23955'){throw 'Plugin checksum mismatch'}
if((Get-AuthenticodeSignature $candidate).Status -ne 'Valid'){throw 'Plugin signature is not valid'}
Copy-Item -LiteralPath $candidate -Destination 'runtime/pepflashplayer.dll' -Force
Write-Output 'Original signed Flash PPAPI x64 34.0.0.384 prepared. No installer was executed.'
