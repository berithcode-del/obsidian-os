$Host.UI.RawUI.WindowTitle = 'Acompanhamento do cartao GA36'
$path='D:\R36S-sistema-novo\06-testes-e-registros\gravacao-original-experimental.txt'
while ($true) {
 Clear-Host
 Write-Host 'VERIFICACAO DO CARTAO - deixe o leitor conectado' -ForegroundColor Cyan
 Write-Host 'A janela acompanha o processo separado e pode ser fechada sem interrompe-lo.'
 Write-Host ''
 try {
  $lines=Get-Content -LiteralPath $path -Encoding UTF8 -ErrorAction Stop | ForEach-Object { $_ -replace "`0", '' } | Where-Object { $_.Trim() -ne '' }
  $lines | Select-Object -Last 8 | ForEach-Object { Write-Host $_ }
  $all=$lines -join ' '
  if ($all -match 'RESULTADO: VERIFICADO') { Write-Host "`nVERIFICACAO CONCLUIDA. Pode ejetar o cartao com seguranca." -ForegroundColor Green; break }
  if ($all -match 'RESULTADO: FALHA|ERRO:') { Write-Host "`nHOUVE FALHA. Nao retire o cartao; informe o resultado ao Codex." -ForegroundColor Red; break }
 } catch { Write-Host 'Aguardando leitura do registro...' }
 if (-not (Get-Process -Id 15388 -ErrorAction SilentlyContinue)) { Write-Host "`nO processo terminou. Verifique o resultado acima antes de ejetar." -ForegroundColor Yellow; break }
 Start-Sleep -Seconds 5
}
Write-Host "`nPressione Enter para fechar esta janela."
[void](Read-Host)
