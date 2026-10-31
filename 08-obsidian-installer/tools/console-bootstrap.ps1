param(
    [string]$FelTool = 'D:\R36S-sistema-novo\04-ferramentas\FEL\sunxi-tools-win\sunxi-tools-mingw64-530adfa\sunxi-fel.exe',
    [string]$Bootstrap = 'D:\R36S-sistema-novo\08-obsidian-installer\assets\bootstrap\ga36-a33-rhododendron-panel1\bootstrap.bin',
    [int]$TimeoutSeconds = 60
)
# Carrega o carregador de RAM (u-boot) no console em FEL e espera o cartao virar disco USB.
# Nao escreve no cartao: apenas transporta o carregador e observa o Windows enumerar.
$ErrorActionPreference = 'Stop'

function Get-FelDevice {
    Get-PnpDevice -PresentOnly -ErrorAction SilentlyContinue |
        Where-Object { $_.InstanceId -match 'VID_1F3A&PID_EFE8' } | Select-Object -First 1
}
function Get-UsbDisks {
    @(Get-Disk | Where-Object BusType -eq 'USB')
}

Write-Host "=== console em FEL? ==="
$fel = Get-FelDevice
if (-not $fel) {
    Write-Host 'Nenhum dispositivo 1F3A:EFE8 presente.'
    Write-Host 'Procedimento: desligar, retirar o cartao TF1, ligar com o cabo (tela apagada), inserir o cartao.'
    exit 2
}
Write-Host "  encontrado: $($fel.InstanceId) status=$($fel.Status)"

$svc = (Get-CimInstance Win32_PnPSignedDriver -ErrorAction SilentlyContinue |
        Where-Object { $_.DeviceID -match 'VID_1F3A' } | Select-Object -First 1).InfName
Write-Host "  driver INF: $svc"

if (-not (Test-Path $Bootstrap)) { throw "bootstrap ausente: $Bootstrap" }
$hash = (Get-FileHash $Bootstrap -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host "  bootstrap SHA-256: $hash (esperado 433cd84c1b0c92b49afc251b543b83a5b054de682d494dae26359562ba86ccdc)"

Write-Host "=== discos USB antes ==="
$before = (Get-UsbDisks).Number
Write-Host ("  {0}" -f (($before | ForEach-Object { "disco $_" }) -join ', '))

Write-Host "=== enviando u-boot para a RAM ==="
& $FelTool -p uboot $Bootstrap 2>&1 | ForEach-Object { "  $_" }
if ($LASTEXITCODE -ne 0) { throw "sunxi-fel falhou (exit $LASTEXITCODE)" }

Write-Host "=== aguardando o cartao aparecer como disco USB ==="
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
$new = $null
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 2
    $new = Get-UsbDisks | Where-Object { $before -notcontains $_.Number } | Select-Object -First 1
    if ($new) { break }
}
if (-not $new) {
    Write-Host 'Nenhum disco novo apareceu. Confira se o cartao esta inserido no TF1.'
    exit 3
}

Write-Host "=== cartao exposto ==="
$new
Write-Host ("  disco {0} · {1} · serie {2} · {3} bytes · {4}" -f $new.Number, $new.FriendlyName, $new.SerialNumber, $new.Size, $new.PartitionStyle)
foreach ($p in Get-Partition -DiskNumber $new.Number -ErrorAction SilentlyContinue) {
    $vol = Get-Volume -Partition $p -ErrorAction SilentlyContinue
    Write-Host ("  p{0} {1:N2} GB offset {2} {3} {4}" -f $p.PartitionNumber, ($p.Size / 1GB), $p.Offset, $p.Type, ($(if ($vol) { "-> $($vol.DriveLetter): $($vol.FileSystemLabel) $($vol.FileSystem)" } else { 'sem volume' })))
}
Write-Host "DISCO=$($new.Number)"
