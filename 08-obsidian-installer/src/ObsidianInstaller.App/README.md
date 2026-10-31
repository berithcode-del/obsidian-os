# Aplicativo Obsidian Installer (Windows)

Aplicativo WPF em português organizado em sete etapas: conexão, compatibilidade,
versão, cartão, gravação, primeiro funcionamento e restauração de conteúdo.

O que ele realmente faz:

- **Conexão**: enumeração passiva de dispositivos USB (SetupAPI) e comparação com
  o catálogo local de perfis. Abrir a janela não consulta hardware.
- **Versão**: lê o manifesto escolhido, compara tamanho e SHA-256 da imagem e
  verifica envelope de assinatura contra as chaves fixadas (catálogo vazio nesta
  versão). `CanInstall` permanece falso: não existe pacote assinado de cliente.
- **Cartão**: inventário de discos físicos por `IOCTL` (modelo, série, tamanho,
  barramento) e volumes montados. O disco do Windows é recusado pelo aplicativo.
- **Gravação**: grava a imagem em `\\.\PhysicalDriveN` com hash incremental,
  **relê** o cartão e compara o SHA-256, declara a partição `p3` até o fim do
  cartão e a formata FAT32 `OBSIDIAN` com o `diskpart`. Exige confirmação por
  escrito (`GRAVAR`) e elevação; sem elevação o aplicativo se relança com
  `--apply-plan` e recebe o resultado em JSON.
- **Restauração**: copia `roms`, `leitura`, `videos` e `saves` de uma pasta ou de
  uma imagem `.img` FAT32, com checagem de espaço, política de conflito, hash por
  arquivo e relatório JSON.

Cada tentativa de gravação e cada restauração deixam relatório em `work/`, ao
lado do executável.

## Compilar e publicar

Com o SDK .NET 10 em `D:\dotnet` (ou no PATH):

```powershell
powershell -ExecutionPolicy Bypass -File tools/build-app.ps1
powershell -ExecutionPolicy Bypass -File tools/build-app.ps1 -PublishPortable
```

O pacote autossuficiente sai em `packaging/out/instalador-win-x64/`
(`ObsidianInstaller.exe`). O script não inicia o aplicativo. A publicação não tem
assinatura comercial e não é uma versão de distribuição.

## Estados e núcleo testável

O núcleo (`ObsidianInstaller.Core`) não depende de WPF e é exercitado por
`ObsidianInstaller.SelfTest`: tabela MBR, gravação e releitura em arquivo, leitura
de FAT32 e restauração com hash. A camada Windows (`PhysicalDiskMedia.cs`) é a
única que abre dispositivos físicos.

## Ligar o console depois da gravação

O console **só liga quando não está conectado ao computador**. Terminada a gravação,
desconecte o cabo USB e ligue o aparelho: a primeira partida mostra a abertura do
Obsidian OS. Para gravar novamente, o console precisa estar desligado, com o cabo
conectado e sem o cartão, para entrar em modo FEL.
