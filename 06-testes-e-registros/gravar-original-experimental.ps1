$ErrorActionPreference='Stop'
$log='D:\R36S-sistema-novo\06-testes-e-registros\gravacao-original-experimental.txt'
try {
 $disk=Get-Disk -Number 2
 $parts=@(Get-Partition -DiskNumber 2 -ErrorAction SilentlyContinue)
 if ($disk.BusType -ne 'USB' -or $disk.FriendlyName -ne 'MXT-USB Storage Device' -or $disk.Size -ne 15485370368 -or $disk.IsReadOnly -or $parts.Count -ne 0) { throw 'Identificação não corresponde ao cartão vazio preparado. Nenhuma gravação iniciada.' }
 'Cartão identificado. Iniciando gravação.' | Set-Content -LiteralPath $log -Encoding UTF8
 & 'C:\Users\marco\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' -u 'D:\R36S-sistema-novo\06-testes-e-registros\gravar-original-experimental.py' *>> $log
 if ($LASTEXITCODE -ne 0) { Add-Content -LiteralPath $log -Value "RESULTADO: FALHA ($LASTEXITCODE)"; exit 1 }
} catch { Add-Content -LiteralPath $log -Value ("RESULTADO: FALHA " + $_.Exception.Message); exit 1 }
