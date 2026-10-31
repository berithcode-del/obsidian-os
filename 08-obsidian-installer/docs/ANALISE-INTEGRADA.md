# Análise integrada do instalador

**Revisão:** 30/10/2026 · **Escopo:** código e contratos presentes no projeto; sem abrir o aplicativo ou acessar hardware. A gravação e a restauração foram exercitadas em cartão simulado e em pastas locais, não em mídia física.

## Caminho ponta a ponta

```text
Windows → descoberta USB → identidade do console → pacote confiável
        → identidade do cartão → bootstrap → plano autorizado
        → gravação → releitura → biblioteca de jogos → boot confirmado
```

O aplicativo apresenta as sete etapas. O núcleo contém a máquina de estados, o
diagnóstico USB, a leitura de manifesto/imagem, o inventário de discos físicos,
a gravação bruta com releitura, a criação da partição `OBSIDIAN` e a restauração
de conteúdo. A confirmação automática de boot continua manual.

## O que funciona e o que a evidência permite afirmar

| Área | Implementação atual | Limite da conclusão |
|---|---|---|
| Interface | Sete etapas, agora com ação real nas etapas 4, 5 e 7 | Compila sem avisos; não foi aberta nesta rodada |
| Descoberta USB | Enumeração PnP via SetupAPI, iniciada por clique | Consulta inventário do Windows; não lê FEL, SoC, placa nem cartão |
| Catálogo de perfis | Lê JSON local e compara VID/PID aos IDs declarados | Produz candidato; os dados locais não autenticam a unidade física |
| Pacote | Lê manifesto/perfil, confere tamanho/hash e verifica envelope ECDSA | Catálogo de confiança vazio; `CanInstall` fechado |
| Cartões | Inventário de discos físicos (IOCTL) e volumes, com recusa do disco do sistema | Identidade é modelo+série+tamanho lidos pelo Windows, não atestação criptográfica |
| Gravação | Escrita em `\\.\PhysicalDriveN` com hash incremental, releitura e relatório | Exercitada em arquivo simulado; cartão físico pendente |
| Layout de dados | `p3` declarada na MBR até o fim do cartão e formatada FAT32 `OBSIDIAN` via `diskpart` | `diskpart` é específico do Windows; cartão de teste pendente |
| Restauração | Pasta ou `.img` FAT32, espaço, conflito, hash por arquivo, relatório | Testada com 86 livros; saves dependem da home do sistema |
| Boot | Roteiro de verificação manual | Sem confirmação automática; imagem 1.0 não iniciada no console |

## Dependências que bloqueiam a liberação comercial

1. Prova física da imagem 1.0 em cartão separado, com registro de partida, launcher, jogos, leitura, vídeo, USB/MTP, TF2 e energia.
2. Identidade de hardware além de VID/PID para autorizar instalação sem intervenção humana.
3. Raiz de confiança escolhida pelo projeto (chave, custódia e assinatura) para sair de `channel: lab`.
4. Homologação em outras unidades, cartões e versões de Windows.
5. Revisão de licenças dos componentes e das capas de jogos embarcadas na home.

## Decisão de laboratório desta rodada

A etapa 5 grava mesmo sem assinatura comercial, porque o objetivo desta entrega é
colocar o sistema no console. Essa liberação é explícita e limitada: exige
manifesto `channel: lab`, imagem com hash conferido, cartão identificado por
modelo/série/tamanho, confirmação por escrito (`GRAVAR`) e elevação do Windows.
O caminho de cliente continua fechado (`CanInstall` permanece falso) e o
manifesto declara `customer_installation_allowed: false`.

## Resultado da revisão desta rodada

A imagem 1.0 foi montada a partir da base comprovada e de 533 componentes com
hash conferido, sem conteúdo pessoal. O núcleo de gravação/restauração passou 27
verificações automáticas e um ensaio completo em cartão simulado de 14,42 GiB,
com `p3` formatada e montada. Falta a prova física: gravar um cartão real,
iniciar a imagem no console e registrar as funções de uso.

Compilação Release: zero avisos e zero erros. Validador estrutural e 14 testes de
contrato aprovados. Sem execução do aplicativo, teste de hardware ou acesso a
cartão físico nesta rodada.
