# Estados e política da instalação

Contém modelos de observação USB, catálogo local de perfis, inspeção de pacote, verificador de assinatura ECDSA P-256 e a máquina de estados tipada com guardas de evidência.

O fluxo WPF usa atualmente as transições de início de diagnóstico, identificação incompleta e falha. As transições de bootstrap, mídia, gravação, armazenamento e boot ainda não têm produtores reais de evidência nem integração com hardware/helper. A assinatura destacada cobre os bytes exatos do manifesto, mas o catálogo de chaves está vazio; `CanInstall` permanece fechado.

Referências: `../../docs/ARQUITETURA.md`, `../../contracts/install-state-machine.json` e `../../docs/ANALISE-INTEGRADA.md`.
