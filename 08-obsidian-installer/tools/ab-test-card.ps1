param(
    [Parameter(Mandatory = $true)][int]$DiskNumber,
    [Parameter(Mandatory = $true)][string]$ExpectedSerial,
    [string]$Image10 = 'D:\R36S-sistema-novo\08-obsidian-installer\artifacts\releases\obsidian-os-1.0-r1\obsidian-os-1.0-lab-r1.img',
    [string]$ProofImage = 'D:\R36S-sistema-novo\02-imagens-personalizadas\Obsisidian-prova-USB-base-goodluck-20261029.img',
    [string]$ReportPath = 'D:\dsh-temp\ab-test.json',
    [string]$LogPath = 'D:\dsh-temp\ab-test.log'
)
# Diagnóstico A/B no cartão do console:
#  1) compara o início do cartão (área de boot + MBR) com a imagem 1.0;
#  2) grava a imagem da prova (que já iniciou no console) e confere a releitura.
# Exige administrador. Não toca em discos que não sejam o USB informado.
$ErrorActionPreference = 'Stop'
function Log([string]$m) {
    $line = "{0}  {1}" -f (Get-Date).ToString('HH:mm:ss'), $m
    Add-Content -LiteralPath $LogPath -Value $line -Encoding UTF8
    Write-Host $line
}
function Fail([string]$m) {
    Log "FALHA: $m"
    @{ success = $false; stage = 'abortado'; message = $m } | ConvertTo-Json | Set-Content -LiteralPath $ReportPath -Encoding UTF8
    exit 1
}
function Sha256Bytes([byte[]]$data) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash($data))).Replace('-', '').ToLowerInvariant() } finally { $sha.Dispose() }
}

Set-Content -LiteralPath $LogPath -Value "=== teste A/B $(Get-Date -Format o) ===" -Encoding UTF8
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { Fail 'processo não elevado' }

$disk = Get-Disk -Number $DiskNumber -ErrorAction SilentlyContinue
if (-not $disk) { Fail "disco $DiskNumber não encontrado" }
Log ("alvo: disco {0} · {1} · série {2} · {3} bytes" -f $disk.Number, $disk.FriendlyName, $disk.SerialNumber, $disk.Size)
if ($disk.IsBoot -or $disk.IsSystem) { Fail 'disco de sistema recusado' }
if ($disk.SerialNumber -ne $ExpectedSerial) { Fail "série diferente: $($disk.SerialNumber)" }
if ($disk.BusType -ne 'USB') { Fail "barramento $($disk.BusType) recusado" }
if (-not (Test-Path $Image10)) { Fail "imagem 1.0 ausente: $Image10" }
if (-not (Test-Path $ProofImage)) { Fail "imagem da prova ausente: $ProofImage" }

# bloqueia e desmonta os volumes do cartão (mesmo caminho usado pelo app)
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class VolCtl {
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFile(string name, uint access, uint share,
        IntPtr sec, uint disp, uint flags, IntPtr templ);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool DeviceIoControl(Microsoft.Win32.SafeHandles.SafeFileHandle h, uint code,
        IntPtr inBuf, int inSize, IntPtr outBuf, int outSize, out int returned, IntPtr overlapped);
}
'@
$handles = @()
foreach ($p in Get-Partition -DiskNumber $DiskNumber -ErrorAction SilentlyContinue) {
    $vol = Get-Volume -Partition $p -ErrorAction SilentlyContinue
    if (-not ($vol -and $vol.DriveLetter)) { continue }
    $path = "\\.\" + $vol.DriveLetter + ":"
    $h = [VolCtl]::CreateFile($path, 0xC0000000, 3, [IntPtr]::Zero, 3, 0, [IntPtr]::Zero)
    if ($h.IsInvalid) { Log "volume $($vol.DriveLetter): não abriu para bloqueio"; continue }
    $ret = 0
    $locked = [VolCtl]::DeviceIoControl($h, 0x90018, [IntPtr]::Zero, 0, [IntPtr]::Zero, 0, [ref]$ret, [IntPtr]::Zero)
    $dismounted = $false
    if ($locked) { $dismounted = [VolCtl]::DeviceIoControl($h, 0x90020, [IntPtr]::Zero, 0, [IntPtr]::Zero, 0, [ref]$ret, [IntPtr]::Zero) }
    if ($locked -and $dismounted) { Log "volume $($vol.DriveLetter): bloqueado e desmontado"; $handles += $h }
    else { Log "volume $($vol.DriveLetter): bloqueio não confirmado"; $h.Dispose() }
}

$handle = "\\.\PhysicalDrive$DiskNumber"
Log "abrindo $handle"
$stream = [IO.File]::Open($handle, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::ReadWrite)

$comparison = @{}
try {
    # ---- 1) comparação do início do cartão com a imagem 1.0
    $size = 4MB
    $card = New-Object byte[] $size
    $image = New-Object byte[] $size
    $stream.Position = 0
    $read = 0
    while ($read -lt $size) { $n = $stream.Read($card, $read, $size - $read); if ($n -le 0) { break }; $read += $n }
    $fs = [IO.File]::OpenRead($Image10)
    try { $fs.Read($image, 0, $size) | Out-Null } finally { $fs.Dispose() }
    $cardHash = Sha256Bytes $card
    $imageHash = Sha256Bytes $image
    $mbrEqual = (Sha256Bytes $card[0..511]) -eq (Sha256Bytes $image[0..511])
    Log ("primeiros 4 MiB: cartão={0} imagem={1} iguais={2}" -f $cardHash.Substring(0,16), $imageHash.Substring(0,16), ($cardHash -eq $imageHash))
    Log ("MBR (setor 0): iguais={0}" -f $mbrEqual)
    $diff = -1
    for ($i = 0; $i -lt $size; $i++) { if ($card[$i] -ne $image[$i]) { $diff = $i; break } }
    if ($diff -ge 0) { Log ("primeira diferença no byte {0} (setor {1}, offset {2})" -f $diff, [math]::Floor($diff/512), ($diff % 512)) }
    $comparison = @{
        first_4mib_equal = ($cardHash -eq $imageHash)
        mbr_equal        = $mbrEqual
        first_diff_byte  = $diff
        card_head_sha256 = $cardHash
        image_head_sha256 = $imageHash
    }

    # ---- 2) grava a imagem da prova
    $proof = Get-Item $ProofImage
    Log ("gravando imagem da prova: {0} bytes" -f $proof.Length)
    $buffer = New-Object byte[] (4MB)
    $written = [int64]0
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $sha = [Security.Cryptography.SHA256]::Create()
    $stream.Position = 0
    $src = [IO.File]::OpenRead($ProofImage)
    try {
        while (($count = $src.Read($buffer, 0, $buffer.Length)) -gt 0) {
            $stream.Write($buffer, 0, $count)
            $null = $sha.TransformBlock($buffer, 0, $count, $null, 0)
            $written += $count
        }
        $null = $sha.TransformFinalBlock((New-Object byte[] 0), 0, 0)
        $writtenHash = ([BitConverter]::ToString($sha.Hash)).Replace('-', '').ToLowerInvariant()
    } finally { $src.Dispose(); $sha.Dispose() }
    $stream.Flush($true)
    $sw.Stop()
    Log ("gravados {0} bytes em {1:N1}s ({2:N2} MiB/s) sha={3}" -f $written, $sw.Elapsed.TotalSeconds, ($written / 1MB / $sw.Elapsed.TotalSeconds), $writtenHash)
    if ($written -ne $proof.Length) { Fail 'quantidade gravada diferente do tamanho da imagem' }

    # releitura
    Log 'relando para conferir'
    $stream.Position = 0
    $sha2 = [Security.Cryptography.SHA256]::Create()
    $remaining = $written
    try {
        while ($remaining -gt 0) {
            $want = [int][Math]::Min([int64]$buffer.Length, $remaining)
            $n = $stream.Read($buffer, 0, $want)
            if ($n -le 0) { Fail 'leitura curta na verificação' }
            $null = $sha2.TransformBlock($buffer, 0, $n, $null, 0)
            $remaining -= $n
        }
        $null = $sha2.TransformFinalBlock((New-Object byte[] 0), 0, 0)
        $verifyHash = ([BitConverter]::ToString($sha2.Hash)).Replace('-', '').ToLowerInvariant()
    } finally { $sha2.Dispose() }
    Log ("releitura: {0} (confere={1})" -f $verifyHash, ($verifyHash -eq $writtenHash))
} finally {
    $stream.Dispose()
}

foreach ($h in $handles) { try { $h.Dispose() } catch { } }
Update-HostStorageCache -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2
$after = Get-Disk -Number $DiskNumber
$parts = @(Get-Partition -DiskNumber $DiskNumber -ErrorAction SilentlyContinue)
Log ("depois: estilo={0} partições={1}" -f $after.PartitionStyle, $parts.Count)
foreach ($p in $parts) {
    $vol = Get-Volume -Partition $p -ErrorAction SilentlyContinue
    Log ("  p{0} {1:N2} GB {2} -> {3}" -f $p.PartitionNumber, ($p.Size/1GB), $p.Type, ($(if ($vol) { "$($vol.DriveLetter): $($vol.FileSystemLabel) $($vol.FileSystem)" } else { 'sem volume' })))
}

@{ success = $true; disk = $DiskNumber; comparison = $comparison; wrote_proof_image = $true; log = $LogPath;
   finished_utc = (Get-Date).ToUniversalTime().ToString('o') } |
    ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $ReportPath -Encoding UTF8
Log 'CONCLUÍDO: comparação e gravação da imagem da prova'
