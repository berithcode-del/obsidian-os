param(
    [string]$Storage = 'Obsidian',
    [string]$LeafPath = '',           # '' = storage root
    [string]$FileName,
    [string]$DestDir = 'D:\dsh-temp\pull'
)
# Pull a single file from the console over MTP (WPD). Read-only on the console.
$ErrorActionPreference = 'Stop'
if ($FileName) {
    New-Item -ItemType Directory -Force -Path $DestDir | Out-Null
    $shell = New-Object -ComObject Shell.Application
    $device = $shell.NameSpace(17).Items() | Where-Object { $_.Name -match 'GA36' } | Select-Object -First 1
    if (-not $device) { throw 'Console MTP device not found' }
    $folder = ($device.GetFolder).Items() | Where-Object { $_.Name -eq $Storage } | Select-Object -First 1
    if (-not $folder) { throw "Storage '$Storage' not found" }
    $current = $folder.GetFolder
    foreach ($part in ($LeafPath -split '/' | Where-Object { $_ })) {
        $next = $current.Items() | Where-Object { $_.Name -eq $part } | Select-Object -First 1
        if (-not $next) { throw "Path segment '$part' not found" }
        $current = $next.GetFolder
    }
    # MTP DisplayName usually drops the extension: accept exact name or "<name>.<ext>".
    $item = $current.Items() | Where-Object { $_.Name -eq $FileName -or $_.Name -eq [System.IO.Path]::GetFileNameWithoutExtension($FileName) } | Select-Object -First 1
    if (-not $item) { throw "File '$FileName' not found in $Storage/$LeafPath" }
    $destNs = $shell.NameSpace($DestDir)
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $destNs.CopyHere($item, 0x14)
    $target = Join-Path $DestDir $FileName
    $lastSize = -1; $stable = 0
    $maxMinutes = 15
    while ($sw.Elapsed.TotalMinutes -lt $maxMinutes) {
        Start-Sleep -Milliseconds 500
        if (Test-Path -LiteralPath $target) {
            $size = (Get-Item -LiteralPath $target).Length
            if ($size -eq $lastSize -and $size -gt 0) { $stable++ } else { $stable = 0 }
            $lastSize = $size
            if ($stable -ge 4) { break }
        }
    }
    $sw.Stop()
    if (Test-Path -LiteralPath $target) {
        $g = Get-Item -LiteralPath $target
        $mb = [math]::Round($g.Length / 1MB, 2)
        $sec = [math]::Round($sw.Elapsed.TotalSeconds, 1)
        [pscustomobject]@{
            File = $g.FullName; Bytes = $g.Length; MB = $mb; Seconds = $sec
            MBps = if ($sec -gt 0) { [math]::Round($mb / $sec, 2) } else { 0 }
            SHA256 = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLower()
        } | Format-List
    } else {
        throw 'Copy did not produce the destination file'
    }
}
