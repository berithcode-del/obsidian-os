# Organização do código da aplicação

O primeiro incremento já tem uma interface WPF em implementação, enumeração USB manual e conferência de manifesto/imagem local. A separação de módulos ainda reserva contratos para integração completa e helper administrativo. Não há código de gravação. A prova executável histórica está no laboratório.

| Módulo | Responsabilidade | Referência |
| --- | --- | --- |
| ObsidianInstaller.App | WPF, telas, mensagens e progresso | docs/FLUXO-CLIENTE.md |
| ObsidianInstaller.Core | Máquina de estados, plano e políticas de autorização | contracts/install-state-machine.json |
| ObsidianInstaller.Windows | Descoberta PnP, transporte USB, discos e remoção segura | docs/ARQUITETURA.md |
| ObsidianInstaller.Elevated | Driver e escrita restrita ao plano validado | docs/ARQUITETURA.md |
| ObsidianInstaller.Packages | Catálogo, integridade, autenticidade e compatibilidade | schemas/ e docs/INTEGRACAO-IMAGENS.md |

Primeira fatia de implementação: detectar dispositivos e importar um pacote local em modo de diagnóstico, sem gravação. A fatia seguinte introduz o helper e reproduz a instalação de um cartão com as proteções do plano.
