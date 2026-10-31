# Preparacao especifica do cartao autorizado pelo usuario em 29/10/2026.
# Executar elevado. Nenhum outro disco pode ser escolhido por este arquivo.
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$reportPath = 'D:\R36S-sistema-novo\06-testes-e-registros\preparo-cartao-fel-2026-10-29.json'
$logPath = 'D:\R36S-sistema-novo\06-testes-e-registros\preparo-cartao-fel-2026-10-29.log'
$expectedUniqueId = 'USBSTOR\DISK&VEN_MXT-USB&PROD_STORAGE_DEVICE&REV_1501\150101V01&0:DESKTOP-CVKBV4T'
$expectedSize = [uint64]15728640000
$backupPath = 'D:\R36S-backup-sistema-2026-10-28.img'
$transcriptStarted = $false

function Save-State([string]$Phase, [string]$Detail) {
    [pscustomobject][ordered]@{
        time = (Get-Date).ToString('o')
        phase = $Phase
        detail = $Detail
        disk_number = 2
        expected_size = $expectedSize
        expected_unique_id = $expectedUniqueId
        backup_path = $backupPath
    } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $reportPath -Encoding UTF8
}

function Assert-Target {
    $target = Get-Disk -Number 2 -ErrorAction Stop
    if ($target.UniqueId -ne $expectedUniqueId -or [uint64]$target.Size -ne $expectedSize -or
        [string]$target.BusType -ne 'USB' -or $target.IsBoot -or $target.IsSystem -or
        $target.IsReadOnly -or $target.IsOffline -or $target.SerialNumber.Trim() -ne '150101v01') {
        throw 'O disco conectado mudou ou nao corresponde ao cartao autorizado. Operacao interrompida.'
    }
    $protected = @(Get-Partition | Where-Object { $_.DriveLetter -in @('C', 'D', 'E') -or $_.IsBoot -or $_.IsSystem })
    if (@($protected | Where-Object DiskNumber -eq 2).Count -gt 0) {
        throw 'O destino contem uma unidade protegida.'
    }
    $backup = Get-Item -LiteralPath $backupPath -ErrorAction Stop
    if ($backup.Length -ne 15728640000) { throw 'Backup original ausente ou com tamanho inesperado.' }
    return $target
}

try {
    Start-Transcript -LiteralPath $logPath -Append | Out-Null
    $transcriptStarted = $true
    $principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'O Windows nao concedeu permissao de administrador.'
    }
    $null = Assert-Target
    $occupiedF = @(Get-Partition | Where-Object { $_.DriveLetter -eq 'F' })
    if (@($occupiedF | Where-Object DiskNumber -ne 2).Count -gt 0) { throw 'A unidade F pertence a outro disco.' }
    $targetLetters = @(Get-Partition -DiskNumber 2 | Where-Object { $_.DriveLetter -and [int][char]$_.DriveLetter -ne 0 -and $_.DriveLetter -ne 'F' })
    if ($targetLetters.Count -gt 0) { throw 'O cartao apresenta uma letra de unidade inesperada.' }
    Save-State 'identified' 'Cartao USB F identificado; backup original em outro disco.'

    # Remove as particoes antes de acessar setores, evitando volumes montados.
    $null = Assert-Target
    Save-State 'clearing' 'Removendo a tabela de particoes do cartao autorizado.'
    Clear-Disk -Number 2 -RemoveData -RemoveOEM -Confirm:$false -ErrorAction Stop
    $null = Assert-Target
    Save-State 'clearing_boot' 'Zerando os primeiros 128 MiB para remover a partida antiga.'
    $stream = [IO.File]::Open('\\.\PhysicalDrive2', [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::ReadWrite)
    $blockHasher = [Security.Cryptography.SHA256]::Create()
    try {
        $zeroBuffer = New-Object byte[] (1MB)
        $zeroHash = [BitConverter]::ToString($blockHasher.ComputeHash($zeroBuffer))
        for ($blockIndex = 0; $blockIndex -lt 128; $blockIndex++) {
            $stream.Write($zeroBuffer, 0, $zeroBuffer.Length)
        }
        $stream.Flush($true)
        $stream.Position = 0
        $verifyBuffer = New-Object byte[] (1MB)
        for ($blockIndex = 0; $blockIndex -lt 128; $blockIndex++) {
            $count = $stream.Read($verifyBuffer, 0, $verifyBuffer.Length)
            if ($count -ne $verifyBuffer.Length) { throw 'Leitura incompleta ao verificar a limpeza.' }
            $readHash = [BitConverter]::ToString($blockHasher.ComputeHash($verifyBuffer))
            if ($readHash -ne $zeroHash) { throw 'A verificacao encontrou dados na area que deveria estar zerada.' }
        }
    } finally { $stream.Dispose(); $blockHasher.Dispose() }

    Update-HostStorageCache -ErrorAction Stop
    $null = Assert-Target
    Save-State 'formatting' 'Criando MBR e uma particao FAT32 sem sistema inicializavel.'
    # Este leitor apresenta midia sem MBR como uma particao implicita em offset 0.
    # DiskPart cria a tabela e a particao reais sem depender de Initialize-Disk.
    $diskpartPath = 'D:\R36S-sistema-novo\06-testes-e-registros\diskpart-fel-2026-10-29.txt'
    @('select disk 2', 'clean', 'create partition primary align=1024', 'format fs=fat32 label=TESTE_FEL quick', 'assign letter=F', 'exit') |
        Set-Content -LiteralPath $diskpartPath -Encoding ASCII
    $null = Assert-Target
    & "$env:SystemRoot\System32\diskpart.exe" /s $diskpartPath
    if ($LASTEXITCODE -ne 0) { throw 'DiskPart nao concluiu a preparacao do cartao.' }
    Update-HostStorageCache -ErrorAction Stop

    $null = Assert-Target
    $volume = Get-Volume -DriveLetter F -ErrorAction Stop
    $partition = Get-Partition -DriveLetter F -ErrorAction Stop
    if ($volume.FileSystem -ne 'FAT32' -or $volume.FileSystemLabel -ne 'TESTE_FEL' -or
        $partition.DiskNumber -ne 2 -or $partition.Offset -ne 1MB) {
        throw 'O resultado da formatacao nao corresponde ao esperado.'
    }
    $stream = [IO.File]::Open('\\.\PhysicalDrive2', [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
    try {
        foreach ($bootOffset in @(8192, 131072)) {
            $stream.Position = $bootOffset
            $bootBytes = New-Object byte[] 512
            if ($stream.Read($bootBytes, 0, 512) -ne 512) { throw 'Falha ao verificar os setores de partida.' }
            $nonzero = [Array]::FindIndex($bootBytes, [Predicate[byte]] { param($value) $value -ne 0 })
            if ($nonzero -ge 0) { throw 'Dados inesperados na area reservada de partida.' }
        }
    } finally { $stream.Dispose() }
    Save-State 'complete' ('Cartao F FAT32 TESTE_FEL pronto, primeiros 128 MiB limpos e verificados antes da formatacao; area de partida conferida depois. Capacidade do volume: ' + $volume.Size + ' bytes.')
} catch {
    Save-State 'failed' $_.Exception.Message
    Write-Error $_.Exception.Message
    exit 1
} finally {
    if ($transcriptStarted) { Stop-Transcript | Out-Null }
}
