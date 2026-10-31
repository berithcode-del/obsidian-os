param(
    [Parameter(Mandatory = $true)][int]$DiskNumber,
    [string]$Label = 'SDCARD',
    [string]$FileSystem = 'fat32',
    [string]$ExpectedSerial = '',
    [string]$LogPath = 'D:\dsh-temp\format-single.log',
    [string]$ReportPath = 'D:\dsh-temp\format-single.json'
)
# Devolve o cartao ao estado de fabrica: tabela nova, uma unica particao e
# formatacao rapida. Recusa disco de sistema, disco nao USB e serie diferente.
$ErrorActionPreference = 'Stop'
function Log([string]$m) {
    $line = "{0}  {1}" -f (Get-Date).ToString('HH:mm:ss'), $m
    Add-Content -LiteralPath $LogPath -Value $line -Encoding UTF8
    Write-Host $line
}
function Fail([string]$m) {
    Log "FALHA: $m"
    @{ success = $false; message = $m } | ConvertTo-Json | Set-Content -LiteralPath $ReportPath -Encoding UTF8
    exit 1
}
function Diskpart([string[]]$lines) {
    $f = Join-Path (Split-Path -Parent $LogPath) ("dps-{0}.txt" -f ([guid]::NewGuid().ToString('N').Substring(0, 8)))
    Set-Content -LiteralPath $f -Value ($lines + 'exit') -Encoding ASCII
    $out = & "$env:SystemRoot\System32\diskpart.exe" /s $f 2>&1 | Out-String
    $out.Trim().Split("`n") | ForEach-Object { if ($_.Trim()) { Log ("  diskpart: " + $_.Trim()) } }
    return $out
}

Set-Content -LiteralPath $LogPath -Value "=== formatacao $(Get-Date -Format o) ===" -Encoding UTF8
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { Fail 'processo nao elevado' }

$disk = Get-Disk -Number $DiskNumber -ErrorAction SilentlyContinue
if (-not $disk) { Fail "disco $DiskNumber nao encontrado" }
Log ("alvo: disco {0} · {1} · serie {2} · {3:N0} bytes · {4}" -f $disk.Number, $disk.FriendlyName, $disk.SerialNumber, $disk.Size, $disk.BusType)
if ($disk.IsBoot -or $disk.IsSystem) { Fail 'disco de sistema recusado' }
if ($disk.BusType -ne 'USB') { Fail "barramento $($disk.BusType) recusado" }
if ($ExpectedSerial -and $disk.SerialNumber -ne $ExpectedSerial) { Fail "serie $($disk.SerialNumber) difere de $ExpectedSerial" }

Log 'criando tabela nova e partição única'
Diskpart @("select disk $DiskNumber", 'clean', 'create partition primary', "format fs=$FileSystem quick label=$Label", 'assign') | Out-Null
Start-Sleep -Seconds 3
Update-HostStorageCache -ErrorAction SilentlyContinue

$partitions = @(Get-Partition -DiskNumber $DiskNumber -ErrorAction SilentlyContinue)
Log ("partições: {0}" -f $partitions.Count)
foreach ($p in $partitions) {
    $v = Get-Volume -Partition $p -ErrorAction SilentlyContinue
    if ($v) { Log ("  p{0} {1:N2} GB {2} -> {3}: {4} {5} · livre {6:N2} GB" -f $p.PartitionNumber, ($p.Size / 1GB), $p.Type, $v.DriveLetter, $v.FileSystemLabel, $v.FileSystem, ($v.SizeRemaining / 1GB)) }
}
if ($partitions.Count -ne 1) { Fail "esperava 1 partição, encontrei $($partitions.Count)" }

$volume = Get-Volume -Partition $partitions[0] -ErrorAction SilentlyContinue
@{ success = $true; disk = $DiskNumber; serial = $disk.SerialNumber; partitions = $partitions.Count;
   file_system = $volume.FileSystem; label = $volume.FileSystemLabel; drive = $volume.DriveLetter;
   size_gb = [math]::Round($disk.Size / 1GB, 2); free_gb = [math]::Round($volume.SizeRemaining / 1GB, 2);
   completed_utc = (Get-Date).ToUniversalTime().ToString('o'); log = $LogPath } |
    ConvertTo-Json | Set-Content -LiteralPath $ReportPath -Encoding UTF8
Log 'CONCLUÍDO'
