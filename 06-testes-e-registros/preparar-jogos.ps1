$ErrorActionPreference='Stop'
$log='D:\R36S-sistema-novo\06-testes-e-registros\preparo-jogos-2026-10-29.txt'
try {
 $d=Get-Disk -Number 2; $v=Get-Volume -DriveLetter G
 if($d.BusType -ne 'USB' -or $d.FriendlyName -ne 'MXT-USB Storage Device' -or $d.Size -ne 15978201088 -or $d.IsReadOnly -or $v.FileSystemLabel -ne 'HOME'){throw 'Dispositivo não corresponde ao cartão confirmado'}
 $dp='D:\R36S-sistema-novo\06-testes-e-registros\diskpart-jogos.txt'
 @('select disk 2','clean','create partition primary','format fs=fat32 quick label=JOGOS','assign letter=F','exit') | Set-Content $dp -Encoding ASCII
 & diskpart.exe /s $dp *>&1 | Set-Content $log -Encoding UTF8
 if($LASTEXITCODE -ne 0){throw "DiskPart retornou $LASTEXITCODE"}
 $disk=Get-Disk -Number 2; $volume=Get-Volume -DriveLetter F -ErrorAction Stop
 if($disk.Size -ne 15978201088 -or $volume.FileSystem -ne 'FAT32' -or $volume.FileSystemLabel -ne 'JOGOS'){throw 'Resultado não confirmado'}
 Add-Content $log 'RESULTADO: CONFIRMADO'
} catch { Add-Content $log ('RESULTADO: FALHA ' + $_.Exception.Message); exit 1 }
