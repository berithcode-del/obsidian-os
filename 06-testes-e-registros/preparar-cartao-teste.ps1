$ErrorActionPreference = 'Stop'
$log = 'D:\R36S-sistema-novo\06-testes-e-registros\preparo-diskpart-2026-10-29.txt'
try {
  $disk = Get-Disk -Number 2
  $partitions = @(Get-Partition -DiskNumber 2)
  $letters = @($partitions | Where-Object DriveLetter -ne ([char]0) | ForEach-Object { $_.DriveLetter })
  $volumes = @($letters | ForEach-Object { Get-Volume -DriveLetter $_ -ErrorAction SilentlyContinue })
  if ($disk.BusType -ne 'USB' -or $disk.Size -ne 15485370368 -or $disk.FriendlyName -ne 'MXT-USB Storage Device' -or $disk.IsReadOnly -or -not ($volumes.FileSystemLabel -contains 'HOME')) { throw 'Identificação do cartão divergiu; nenhuma alteração realizada.' }
  $instructions = 'D:\R36S-sistema-novo\06-testes-e-registros\diskpart-cartao-teste.txt'
  @('select disk 2','clean','exit') | Set-Content -LiteralPath $instructions -Encoding ASCII
  $result = & diskpart.exe /s $instructions 2>&1
  $result | Set-Content -LiteralPath $log -Encoding UTF8
  if ($LASTEXITCODE -ne 0 -or ($result -join "`n") -notmatch 'DiskPart succeeded in cleaning|DiskPart obteve êxito ao limpar|DiskPart concluiu a limpeza') { Add-Content -LiteralPath $log -Value "`nRESULTADO: VERIFICAR (código $LASTEXITCODE)"; exit 1 }
  Add-Content -LiteralPath $log -Value "`nRESULTADO: LIMPEZA CONCLUÍDA"
} catch { $_ | Out-String | Set-Content -LiteralPath $log -Encoding UTF8; Add-Content -LiteralPath $log -Value 'RESULTADO: FALHOU'; exit 1 }
