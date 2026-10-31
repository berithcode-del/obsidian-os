param(
    [Parameter(Mandatory = $true)][int]$DiskNumber,
    [Parameter(Mandatory = $true)][string]$ExpectedSerial,
    [Parameter(Mandatory = $true)][long]$ExpectedBytes,
    [string]$LogPath = 'D:\dsh-temp\format-card.log',
    [string]$ReportPath = 'D:\dsh-temp\format-card.json'
)
# Formata um cartão como novo: remove partições (diskpart clean) e grava zeros em
# todo o dispositivo, conferindo amostras na releitura. Exige administrador.
# Travas: recusa disco de sistema/BOOT, disco não-USB, série ou tamanho diferentes.
$ErrorActionPreference = 'Stop'
$script:log = $LogPath
function Write-Log([string]$message) {
    $line = "{0}  {1}" -f (Get-Date).ToString('HH:mm:ss'), $message
    Add-Content -LiteralPath $script:log -Value $line -Encoding UTF8
    Write-Host $line
}
function Fail([string]$message) {
    Write-Log "FALHA: $message"
    @{ success = $false; stage = 'abortado'; message = $message; disk = $DiskNumber } |
        ConvertTo-Json | Set-Content -LiteralPath $ReportPath -Encoding UTF8
    exit 1
}

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $LogPath) | Out-Null
Set-Content -LiteralPath $LogPath -Value "=== formatacao de cartao $(Get-Date -Format o) ===" -Encoding UTF8

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Fail 'o processo não está elevado'
}

$disk = Get-Disk -Number $DiskNumber -ErrorAction SilentlyContinue
if (-not $disk) { Fail "disco $DiskNumber não encontrado" }
Write-Log ("alvo: disco {0} · {1} · série {2} · {3} bytes · {4}" -f $disk.Number, $disk.FriendlyName, $disk.SerialNumber, $disk.Size, $disk.BusType)

if ($disk.IsBoot -or $disk.IsSystem) { Fail 'disco de sistema/BOOT recusado' }
if ($disk.BusType -ne 'USB') { Fail "barramento $($disk.BusType) recusado (esperado USB)" }
if ($disk.SerialNumber -ne $ExpectedSerial) { Fail "série $($disk.SerialNumber) difere da esperada $ExpectedSerial" }
if ($disk.Size -ne $ExpectedBytes) { Fail "tamanho $($disk.Size) difere do esperado $ExpectedBytes" }
if ($disk.Size -lt 1GB) { Fail 'tamanho suspeito' }

# 1. Remover partições e assinaturas com o diskpart.
$dp = Join-Path (Split-Path -Parent $LogPath) ("diskpart-clean-{0}.txt" -f $DiskNumber)
Set-Content -LiteralPath $dp -Value ("select disk {0}`r`nclean`r`nexit" -f $DiskNumber) -Encoding ASCII
$dpOut = & "$env:SystemRoot\System32\diskpart.exe" /s $dp 2>&1 | Out-String
$dpOut.Trim().Split("`n") | ForEach-Object { if ($_.Trim()) { Write-Log ("diskpart: " + $_.Trim()) } }
Start-Sleep -Seconds 2
Update-HostStorageCache -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1
$after = Get-Disk -Number $DiskNumber
Write-Log ("após clean: estilo={0} partições={1}" -f $after.PartitionStyle, ($after | Get-Partition -ErrorAction SilentlyContinue | Measure-Object).Count)
if (($after | Get-Partition -ErrorAction SilentlyContinue | Measure-Object).Count -gt 0) {
    Fail 'o disco ainda tem partições depois do clean'
}

# 2. Gravar zeros no dispositivo inteiro.
$handlePath = "\\.\PhysicalDrive$DiskNumber"
Write-Log "abrindo $handlePath para escrita bruta"
$stream = $null
try {
    $stream = [System.IO.File]::Open($handlePath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::ReadWrite)
} catch {
    Fail ("não foi possível abrir o dispositivo: " + $_.Exception.Message)
}

$bufferSize = 8MB
$buffer = New-Object byte[] $bufferSize
$total = [int64]$disk.Size
$written = [int64]0
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$lastReport = [int64]0
Write-Log ("gravando zeros: {0:N0} bytes em blocos de {1:N0}" -f $total, $bufferSize)
try {
    while ($written -lt $total) {
        $chunk = [int][Math]::Min([int64]$bufferSize, $total - $written)
        $stream.Write($buffer, 0, $chunk)
        $written += $chunk
        if ($written - $lastReport -ge 512MB -or $written -eq $total) {
            $lastReport = $written
            $mb = $written / 1MB
            $mbps = $mb / [Math]::Max($sw.Elapsed.TotalSeconds, 0.001)
            $pct = 100.0 * $written / $total
            Write-Log ("progresso: {0:N2}% · {1:N1} MiB · {2:N1} MiB/s" -f $pct, $mb, $mbps)
        }
    }
    $stream.Flush($true)
} catch {
    Fail ("falha durante a gravação: " + $_.Exception.Message)
} finally {
    if ($stream) { $stream.Dispose() }
}
$sw.Stop()
Write-Log ("gravação concluída: {0:N0} bytes em {1:N1}s ({2:N2} MiB/s)" -f $written, $sw.Elapsed.TotalSeconds, ($written / 1MB / [Math]::Max($sw.Elapsed.TotalSeconds, 0.001)))

# 3. Conferir amostras na releitura.
Start-Sleep -Seconds 2
$verify = $null
try {
    $verify = [System.IO.File]::Open($handlePath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
} catch {
    Fail ("não foi possível reabrir para conferência: " + $_.Exception.Message)
}
$samples = @(0, 1MB, 512MB, 1GB, 4GB, 8GB, 12GB, ($total - 1MB), ($total - 64KB))
$bad = 0
foreach ($offset in $samples) {
    if ($offset -lt 0 -or ($offset + 64KB) -gt $total) { continue }
    $verify.Position = $offset
    $probe = New-Object byte[] 65536
    $read = $verify.Read($probe, 0, $probe.Length)
    $nonZero = 0
    for ($i = 0; $i -lt $read; $i += 4096) {
        if ($probe[$i] -ne 0) { $nonZero++ }
    }
    if ($nonZero -gt 0) { $bad++ }
    Write-Log ("amostra em {0:N0}: {1} bytes lidos, {2} bloco(s) não zero" -f $offset, $read, $nonZero)
}
$verify.Dispose()
if ($bad -gt 0) { Fail "a releitura encontrou $bad amostra(s) com lixo" }

# 4. Estado final visto pelo Windows.
Update-HostStorageCache -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1
$final = Get-Disk -Number $DiskNumber
$parts = @($final | Get-Partition -ErrorAction SilentlyContinue)
$vols = @($final | Get-Volume -ErrorAction SilentlyContinue)
Write-Log ("estado final: estilo={0} partições={1} volumes={2}" -f $final.PartitionStyle, $parts.Count, $vols.Count)

$result = [ordered]@{
    success        = $true
    disk           = $DiskNumber
    model          = $disk.FriendlyName
    serial         = $disk.SerialNumber
    bytes          = $disk.Size
    gib            = [math]::Round($disk.Size / 1GB, 2)
    bytes_written  = $written
    seconds        = [math]::Round($sw.Elapsed.TotalSeconds, 1)
    mbps           = [math]::Round($written / 1MB / [Math]::Max($sw.Elapsed.TotalSeconds, 0.001), 2)
    samples_checked = $samples.Count
    samples_failed  = $bad
    partition_style = [string]$final.PartitionStyle
    partitions      = $parts.Count
    volumes         = $vols.Count
    finished_utc    = (Get-Date).ToUniversalTime().ToString('o')
    log             = $LogPath
}
$result | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $ReportPath -Encoding UTF8
Write-Log 'CONCLUÍDO: cartão zerado, sem partições e sem sistema de arquivos'
exit 0
