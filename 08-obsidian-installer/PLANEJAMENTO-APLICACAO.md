# Planejamento da aplicação — Obsidian Installer

**Versão do planejamento:** 0.1 — 29/10/2026  
**Produto:** aplicativo portátil Windows para instalação, recuperação e manutenção do Obsidian OS.  
**Situação:** viabilidade técnica demonstrada em um aparelho; produto comercial em planejamento.

## 1. O que vamos oferecer

O usuário conecta um console compatível, recebe uma identificação compreensível, escolhe o cartão e instala uma versão verificada do Obsidian OS. O aplicativo prepara as pastas de jogos, confere a gravação e orienta a primeira inicialização. Quem perdeu o sistema de fábrica deve poder recuperar um aparelho pertencente a um perfil homologado.

O serviço comercial reúne a instalação, a compatibilidade validada, as atualizações, o diagnóstico e o atendimento. O benefício é reduzir a dificuldade de escolher firmware, drivers, ferramentas e cartões corretos. A proposta inicial é “recuperar e instalar o Obsidian OS em modelos compatíveis”, sem prometer atender qualquer aparelho vendido como R36S.

O instalador e o sistema terão ciclos separados. A imagem do Obsidian OS ainda está mudando; o aplicativo deve aceitar novas versões por pacotes, sem precisar ser recompilado a cada alteração de tema ou launcher.

## 2. O que a prova já demonstrou

Em 29/10/2026, o console GA36 A33/Rhododendron com painel 1 foi detectado em FEL com cartão preparado sem sistema. Um U-Boot específico foi enviado para a RAM e apresentou o cartão ao Windows como armazenamento USB. Escrevemos e relemos uma imagem de 1 GiB pelo cabo; o SHA-256 coincidiu integralmente. Após o usuário desligar e ligar o console, lemos pela serial o identificador exclusivo gerado durante o boot da imagem instalada. As pastas de jogos foram verificadas.

Essa prova utilizou a base goodluckOS compatível. A interface completa do Obsidian OS não foi integrada à imagem de prova, HOME permaneceu em 512 MiB e a cadeia de partida persistente ainda inclui dados derivados do backup do perfil. O resultado comprova o caminho de instalação no aparelho testado; não comprova um instalador público universal.

## 3. Público e modalidades do serviço

| Modalidade | Entrega | Decisão inicial |
| --- | --- | --- |
| Instalação assistida | Operador acompanha diagnóstico, instalação e primeiro boot | Melhor formato para o piloto, pois permite aprender com outros aparelhos |
| Autoatendimento | Cliente executa o aplicativo com orientações e suporte por ocorrência | Evoluir depois de homologar o fluxo em Windows limpo |
| Recuperação | Reinstalação de uma versão compatível após perda do sistema | Incluir no núcleo técnico do produto |
| Manutenção | Diagnóstico, reinstalação e atualização do sistema/launcher | Separar atualização de instalação destrutiva |
| Uso por assistência técnica | Vários atendimentos, histórico e relatórios por aparelho | Possibilidade posterior, com regras de suporte e licenciamento próprias |
| Kit offline | Aplicativo e pacote de imagem aprovados, sem download durante o atendimento | Planejar desde o início; publicar depois de resolver distribuição dos componentes |

Essas modalidades são propostas a validar, não contratos ou planos de cobrança já definidos. Preço, duração do suporte e canais de atendimento serão definidos com dados do piloto: tempo por atendimento, incidência de falhas, custo de distribuição, taxas de pagamento e necessidade de intervenção humana.

Para o piloto, a cobrança e a confirmação do atendimento podem ocorrer fora do aplicativo. Checkout, conta obrigatória e sistema de assinaturas não devem atrasar o núcleo de instalação. Se houver direito de atualização ou atendimento contratado, ele precisa ser descrito de forma independente das licenças dos componentes de software livre.

## 4. Escopo da primeira versão

### Essencial para o piloto

1. Identificar Windows e arquitetura suportados, portas USB e estado do driver.
2. Detectar FEL e consultar o SoC sem gravar o aparelho.
3. Relacionar as evidências a um perfil de hardware aprovado, bloqueando os casos desconhecidos.
4. Preparar o driver apenas para o dispositivo correto quando necessário.
5. Carregar o bootstrap aprovado na RAM e correlacionar o novo disco USB ao console da sessão.
6. Selecionar uma imagem local ou um pacote de atualização com versão e integridade verificadas.
7. Apresentar cartão de destino, conteúdo que será apagado e etapas previstas antes da confirmação.
8. Executar a gravação por um componente administrativo restrito e conferir a releitura.
9. Preparar armazenamento e pastas de jogos conforme o modo homologado.
10. Orientar o reinício e verificar o sistema efetivamente iniciado.
11. Salvar um relatório local e oferecer exportação para suporte.

O primeiro piloto pode atender um cartão e um perfil. A identificação da capacidade do cartão deve ser dinâmica; a regra fixa de 16 GB usada na prova não pertence ao produto final.

### Evoluções planejadas

- Um cartão com sistema e jogos utilizando o espaço disponível.
- Dois cartões, com escolha explícita entre preservar, migrar ou preparar o cartão de jogos.
- Reinstalação com opção de recuperar saves/configurações quando a origem estiver legível.
- Atualização apenas do launcher, quando kernel, layout e interfaces permanecerem compatíveis.
- Catálogo de perfis e pacotes por canal de versão.
- Diagnóstico e relatórios para assistências técnicas.
- Alternativa por leitor de cartões quando a instalação por USB não for aplicável.

Aparelhos Rockchip, A23 e outras famílias exigem seus próprios perfis e validações. “Clone” não será uma autorização técnica para instalar.

## 5. Experiência do cliente

O fluxo terá seis telas principais: **Conectar**, **Verificar compatibilidade**, **Escolher versão**, **Preparar cartões**, **Instalar** e **Confirmar funcionamento**. O aplicativo deve mostrar ações concretas, como trocar de porta, usar cabo de dados, ligar sem cartão inicializável ou confirmar a solicitação do Windows.

Na etapa de compatibilidade, a interface distingue “processador identificado”, “perfil confirmado” e “variante ainda não atendida”. Se o dispositivo estiver em FEL, o nome comercial da carcaça não basta para avançar. Quando não houver evidências suficientes, o fluxo encaminha para identificação assistida; não tenta vários carregadores aleatoriamente.

A confirmação de apagamento fica associada ao cartão exato. Após uma reconexão, o aplicativo revalida a identidade antes de continuar. Letras como F: e números como Disco 2 são apresentados ao cliente apenas como informação auxiliar, nunca como identidade persistente.

O progresso separa download, extração, preparação, escrita, sincronização, releitura e boot. Uma falha de gravação não aparece como instalação concluída. Se o cliente cancelar a solicitação administrativa, a interface volta a um estado recuperável e oferece tentar novamente; não fica aguardando silenciosamente.

## 6. Aplicativo portátil para Windows

Recomendação inicial: **C# com .NET 10 LTS e WPF**, publicação autossuficiente para Windows x64. A escolha atende ao foco exclusivo em Windows e ao acesso às APIs nativas de USB, discos e elevação. O .NET 10 consta como LTS com suporte até novembro de 2028; a publicação autossuficiente evita exigir a instalação prévia do runtime. [Suporte .NET](https://learn.microsoft.com/dotnet/core/releases-and-support), [publicação em arquivo único](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/).

“Portátil” significa poder abrir a aplicação a partir de uma pasta distribuída ou executável inicial, sem instalar Python, WSL ou ferramentas de desenvolvimento. Isso não elimina a instalação inicial do driver quando necessária, a confirmação administrativa de gravação nem o uso de cache e relatórios locais. A apresentação comercial precisa refletir essa diferença.

Para o piloto, um ZIP portátil com executável principal, helper administrativo, componentes nativos e licenças é mais simples de auditar do que esconder todos os componentes em um único arquivo. Uma versão com executável inicial único pode extrair recursos verificados posteriormente. O formato final será decidido por testes de assinatura, extração, antivírus e atualização, não apenas pela aparência de “um arquivo”.

A prévia Windows já apresenta as seis etapas planejadas — conexão, compatibilidade, escolha da versão, preparação dos cartões, instalação e confirmação do primeiro boot. A navegação deixa claro o que ainda está bloqueado: reconhecimento aprofundado, leitura de discos/cartões, gravação e confirmação do sistema iniciado. Diagnóstico USB manual e conferência local de manifesto/imagem estão disponíveis. A solução compilou em Release com .NET SDK 10.0.403, sem avisos ou erros. O SDK fica isolado no perfil de desenvolvimento e não será incluído no aplicativo. A janela não foi iniciada; falta validação visual, integração do mecanismo de instalação e publicação para clientes.

## 7. Separação entre aplicativo e imagem

Cada pacote deve informar versão do sistema, canal, perfil permitido, tamanho da imagem expandida, SHA-256, layout esperado, política de armazenamento, versão mínima do instalador e procedimento de validação do boot. Pacotes distribuídos precisam de autenticidade verificável, além do hash: um hash obtido junto de um arquivo adulterado não estabelece confiança.

Os canais serão **laboratório**, **candidato**, **beta** e **estável**. Uma alteração na imagem cria uma versão nova; o pacote já homologado permanece imutável. A seleção “mais recente” deve resolver um pacote aprovado para o perfil, não apontar para um arquivo que está sendo alterado por outro processo.

O perfil do bootstrap, o pacote do sistema e o pacote do launcher são objetos distintos. A mudança visual do Obsidian OS não obriga a substituir o carregador temporário comprovado. A mudança de kernel, serviços, assinatura de boot ou layout exige nova classificação e ensaios pertinentes.

Durante o desenvolvimento atual, o modo de laboratório poderá importar uma imagem local e calcular seu hash. Isso produz um candidato privado, não uma versão autorizada para distribuição. O instalador final também deve reconhecer quando uma operação é uma atualização de arquivos ou uma regravação completa.

## 8. Arquitetura e proteção da operação

| Módulo | Responsabilidade |
| --- | --- |
| Aplicação WPF | Orientação, seleção e acompanhamento, executados como usuário comum |
| Núcleo de instalação | Estados, plano imutável, compatibilidade e critérios de sucesso |
| Integração Windows | PnP, WinUSB, correlação USB, discos, volumes e remoção segura |
| Helper administrativo | Executar apenas o plano autorizado: driver, bloqueio, gravação, sincronização e verificação |
| Pacotes | Catálogo, download, extração, hashes, assinaturas e compatibilidade de versão |

O helper não recebe comandos de terminal arbitrários nem confia apenas no número do disco enviado pela interface. Ele confere novamente o alvo, a imagem e o perfil imediatamente antes da operação. A comunicação entre interface e helper deve ser limitada à sessão e ao usuário esperados, com permissões de canal e identificação da tentativa.

As assinaturas, o conteúdo correspondente dos componentes e o mecanismo de atualização serão requisitos de distribuição. Isso não autoriza desabilitar proteções do Windows. O driver do FEL merece um fluxo próprio: na prova, o Windows apresentou código 28 e foi necessário associar WinUSB. A ROM do chip é fixa; não podemos presumir que ela forneça descritores para instalação automática. [Documentação WinUSB](https://learn.microsoft.com/en-us/windows-hardware/drivers/usbcon/automatic-installation-of-winusb).

## 9. Armazenamento e jogos

O modo de um cartão precisa expandir ou criar uma área de dados compatível com a imagem e com o launcher. Hoje a imagem de prova possui raiz ext4 de 384 MiB e HOME FAT32 de 512 MiB, deixando espaço sem aproveitamento em um cartão de 16 GB. Essa expansão é requisito do produto, não resultado da prova já realizada.

No modo de dois cartões, a escrita do sistema e a preparação da biblioteca são operações independentes. Um TF2 já contendo jogos não deve ser formatado por padrão. Quando houver armazenamento interno, ele precisa ser distinguido dos cartões removíveis. A ordem `mmcblk0/mmcblk1` pode variar entre hardware e kernel.

As pastas da biblioteca serão declaradas pelo pacote: por exemplo, `roms/gba`, `roms/nds`, `roms/snes`, `roms/psx`, `roms/psp`, `roms/cps2` e `roms/cps3`. O launcher precisa resolver a raiz da biblioteca nos dois modos; os caminhos fixos encontrados no desenvolvimento atual precisam ser removidos antes de prometer esse suporte.

Criar uma pasta não comprova disponibilidade ou desempenho do emulador. FAT32 já funcionou na configuração testada; arquivos maiores que 4 GiB e outros sistemas de arquivos exigem uma política e testes próprios. O serviço pode preparar a estrutura, sem depender da distribuição de coleções pessoais de jogos, BIOS ou emuladores proprietários.

## 10. Modelo comercial a validar

O piloto deve medir a disposição de pagar pela recuperação e conveniência, a dificuldade real do cliente e o custo do atendimento. Os modelos possíveis são cobrança por instalação assistida, pacote de instalação com período de suporte e licença de operação para assistências técnicas. A escolha depende dos dados, não de uma estimativa de mercado ainda inexistente.

O custo por atendimento deve incluir tempo de suporte, distribuição das imagens, meios de pagamento, assinatura de software, manutenção e incidentes. Registrar também atendimentos incompatíveis encerrados antes da instalação. Não estimamos volume de vendas, tamanho do mercado brasileiro ou preço final sem pesquisa específica.

A documentação GNU admite comercialização de software livre e descreve condições para sua distribuição. O inventário de componentes e o fornecimento das fontes correspondentes precisam ser resolvidos antes da entrega comercial; a licença do aplicativo próprio ainda não foi escolhida. Separar executáveis em processos não resolve, por si só, todas as decisões de licenciamento. [FAQ oficial GNU](https://www.gnu.org/licenses/gpl-faq.en.html#DoesTheGPLAllowMoney).

O posicionamento deve oferecer instalação, recuperação, organização e suporte com critérios claros. Não prometer salvar dados ilegíveis, compatibilidade com qualquer clone, desempenho de todos os emuladores ou continuidade ilimitada de suporte.

## 11. Etapas e critérios de saída

| Etapa | Resultado esperado | Critério de saída |
| --- | --- | --- |
| P0 — conhecimento e organização | Projeto independente e evidências rastreáveis | Documentação, hashes e validação local disponíveis |
| P1 — pacote do Obsidian OS | Imagem candidata completa, com identidade e política de dados | Boot, launcher e bibliotecas verificados com pacote imutável |
| P2 — diagnóstico Windows | Aplicativo portátil detecta modos e identifica lacunas | Teste em Windows limpo, incluindo driver ausente e UAC cancelado |
| P3 — instalação guiada em um cartão | Fluxo completo sem terminal para o cliente | Destino protegido, gravação conferida, reinício orientado e boot confirmado |
| P4 — capacidade e recuperação | Área de jogos expandida e retomada de falhas definida | Ensaios com capacidades diferentes e interrupções em cada fase |
| P5 — dois cartões e novos perfis | Operações separadas de sistema e jogos | Cartão com dados preservados e perfil de cada variante aprovado |
| P6 — piloto comercial | Atendimento limitado e mensurável | Critérios de suporte, componentes redistribuíveis, assinaturas e evidências de qualidade concluídos |

Meta inicial proposta de homologação: repetir instalações com cartões de marcas/capacidades diferentes e, antes de ampliar o atendimento ao público, testar mais de uma unidade física. Os números de unidades e repetições serão fechados no plano de homologação; uma repetição no mesmo aparelho não equivale a validar uma nova placa.

Não fixamos prazo de venda nesta etapa. A estimativa de esforço vem depois de resolver pacote do sistema, instalação do driver e políticas de armazenamento. Esses itens têm mais incerteza que a montagem das telas.

## 12. Métricas e atendimento

Registrar por tentativa: versão do aplicativo, perfil, versões e hashes dos componentes, fases executadas, tempos, resultado, necessidade de intervenção e motivo de falha. Não coletar nomes de jogos, conteúdo de ROMs, documentos do PC ou imagens integrais do cartão para telemetria.

O relatório fica local por padrão. Uma exportação para suporte deve mostrar o conteúdo e permitir remover identificadores de hardware e caminhos pessoais. Pagamentos e histórico de atendimento não precisam armazenar o conteúdo do cartão. Política de retenção, consentimento e acesso ao suporte devem ser definidos antes do piloto comercial.

Indicadores do piloto: conclusão por perfil, falha de driver, cancelamento de elevação, interrupções, falhas na releitura, boot não confirmado, tempo por atendimento e contatos de suporte. O termo “instalação concluída” depende da verificação de boot ou de uma indicação explícita de que o cliente ainda não confirmou essa etapa.

## 13. Decisões que precisam ser fechadas

- Qual imagem candidata do Obsidian OS será a primeira base comercial e como será produzida de forma repetível?
- Como substituir ou regularizar a cadeia persistente de partida hoje derivada do backup do perfil?
- Quais evidências identificam a placa antes de executar um bootstrap em um aparelho desconhecido?
- O primeiro piloto será assistido, com um cartão e um perfil, conforme proposto?
- Como serão preservados e migrados saves/configurações numa reinstalação?
- Qual será o modelo de suporte e cobrança, e qual licença será adotada no código próprio?
- Quais versões de Windows, arquiteturas e capacidades de cartão serão efetivamente homologadas?

## 14. Próxima implementação recomendada

Concluir o contrato do pacote da imagem enquanto o sistema continua evoluindo. Em paralelo, implementar a aplicação Windows primeiro em modo de diagnóstico e seleção de pacote. Migrar a gravação validada para um helper com alvos dinâmicos e plano restrito. Só então integrar o fluxo completo de um cartão, expansão de espaço e confirmação do boot.

O próximo marco não é adicionar pagamento: é reproduzir a prova atual por uma interface clara, em outro Windows e com outro cartão, usando uma imagem candidata identificada do Obsidian OS.
