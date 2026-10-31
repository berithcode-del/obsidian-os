# Obsidian OS 1.0 — notas da versão (r3)

**Canal:** laboratório (`lab`). Distribuição para testes com o console GA36/R36S
(Allwinner A33, painel 1). Ainda não é a versão de varejo.

## O que vai no pacote

| Arquivo | Conteúdo |
| --- | --- |
| `ObsidianOS-1.0-Instalador-win64.zip` | Instalador Windows (self-contained) **com a imagem embutida** + `LEIA-ME.txt` |
| `obsidian-os-1.0-lab-r3.img` | Imagem crua do cartão (1.073.741.824 bytes) para gravação manual |
| `obsidian-os-1.0-lab-r3.manifest.json` | Manifesto com SHA-256 e tamanho conferidos pelo instalador |
| `SHA256SUMS.txt` | Hashes de todos os arquivos da release |

Imagem r3: **1.073.741.824 bytes** · SHA-256
`c8543e46c7075ec82db3cc8f4335e3d914e8dd6d3cdc2d154f854aa132211b8a`

## O que mudou nesta revisão

- **Sistema inicia no console**: corrigida a permissão dos scripts de inicialização
  (a imagem anterior dava `Permission denied` em todos os serviços e ficava na tela preta).
- **Instalador grava sozinho**: confere o hash, apaga a tabela de partições, testa o
  enlace com 16 MiB, grava, relê e prepara a área `OBSIDIAN` — e envia o carregador ao
  console em modo FEL, sem precisar de leitor de cartões.
- **Ordem de gravação segura**: corpo da imagem primeiro, tabela de partições por
  último; sem tabela não há volume montado e o Windows não bloqueia a escrita.
- **Interface nova**: três passos (Conectar → Cartão → Gravar), identidade do site
  (laranja vivo `#ff6b1a`, preto absoluto, Sora), splash com a arte do sistema e ícone
  embutido no executável.

## Requisitos

- Windows 10/11, 64 bits, com autorização de administrador (a gravação no cartão exige).
- Cartão microSD de **16 GB** (mínimo 2 GB) — todo o conteúdo é apagado.
- Console GA36/R36S com o cartão no slot **TF1**.

## Verificado

- Gravação pelo instalador com o cartão **no leitor USB**: `success: true` em 329 s, releitura integral.
- Gravação com o cartão **no console em FEL**: `success: true` em 557 s, carregador enviado pelo próprio instalador.
- Boot do sistema no console com a revisão r3: hostname `obsidian`, `Obsidian OS 1.0`,
  todos os serviços com `rc=0`, launcher `de6fa0a3…` em execução, DRM ativo e área
  `OBSIDIAN` montada em `/media/obsidian`.

## Limitações conhecidas

- O executável **não é assinado** (SmartScreen pode pedir "Executar mesmo assim").
  A assinatura de código está prevista para antes de uma versão de varejo.
- O instalador recusa discos de sistema e só grava em disco USB.
- Restaurar o conteúdo do usuário (livros, jogos, vídeos) é feito à parte, pela tela
  opcional **Restaurar conteúdo** do aplicativo ou copiando para a `OBSIDIAN`.
- Nunca desligue o console durante a gravação; se interromper, grave novamente.

## Como instalar (resumo)

1. Console **desligado**, cabo USB no computador, **sem o cartão** → modo FEL.
2. Abra o instalador → *Continuar* → insira o cartão → confirme → *Gravar sistema*.
3. Ao terminar, **desconecte o cabo** e ligue o console (ele não liga conectado).
