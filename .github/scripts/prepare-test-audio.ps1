$ErrorActionPreference = 'Stop'

function Get-PlaybackDevice {
    Get-PnpDevice -PresentOnly -Class AudioEndpoint -Status OK -ErrorAction SilentlyContinue |
        Where-Object { $_.InstanceId -like 'SWD\MMDEVAPI\{0.0.0.*' } |
        Select-Object -First 1
}

try {
    Start-Service Audiosrv
    if (Get-PlaybackDevice) { return }

    # The hosted VM has no sound card. Give the installed app a real WASAPI endpoint.
    $stage = Join-Path $env:RUNNER_TEMP 'zmxy-audio'
    $archive = Join-Path $env:RUNNER_TEMP 'VBCABLE_Driver_Pack46.zip'
    Invoke-WebRequest -Uri 'https://download.vb-audio.com/Download_CABLE/VBCABLE_Driver_Pack46.zip' -OutFile $archive -MaximumRetryCount 2
    if ((Get-FileHash $archive -Algorithm SHA256).Hash -ne 'bc44e8974191a5a2427d6c979e2dab08dbb09925f3f435dfa2f0ba3e45b6eb90') {
        throw 'Test audio driver archive checksum mismatch'
    }
    Expand-Archive -LiteralPath $archive -DestinationPath $stage -Force
    $catalog = Join-Path $stage 'vbaudio_cable64_win10.cat'
    if ((Get-AuthenticodeSignature $catalog).Status -ne 'Valid') { throw 'Invalid audio driver catalog signature' }

    $kit = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\Tools'
    $devcon = Get-ChildItem "$kit\*\x64\devcon.exe", "$kit\x64\devcon.exe" -ErrorAction SilentlyContinue | Select-Object -Last 1
    if (!$devcon) { throw 'Missing DevCon from the Windows Driver Kit' }
    & $devcon.FullName install (Join-Path $stage 'vbMmeCable64_win10.inf') VBAudioVACWDM
    if ($LASTEXITCODE -notin 0, 1) { throw "Installing the test playback device failed ($LASTEXITCODE)" }

    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        $device = Get-PlaybackDevice
        if ($device) {
            Write-Output "Playback device ready: $($device.FriendlyName)"
            exit 0
        }
        Start-Sleep -Seconds 1
    }
    throw 'No active playback device appeared on the test VM'
} catch {
    Write-Output "::error::$($_.Exception.Message)"
    throw
}
