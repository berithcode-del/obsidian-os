# Referências e precedência da informação

Consultadas em 29/10/2026. As evidências do teste têm prioridade para o comportamento deste aparelho; documentação pública de outra placa não amplia automaticamente a compatibilidade.

## Projeto de origem

- `../evidence/private/2026-10-29/TESTE-INSTALACAO-USB-2026-10-29.md`: histórico integral da instalação.
- `../evidence/private/2026-10-29/resultado-final-instalacao-usb-2026-10-29.json`: resultado consolidado.
- `../evidence/private/2026-10-29/instalacao-usb-boot-2026-10-29.json`: evidência do sistema iniciado.
- `../../01-projeto/`: desenvolvimento atual do sistema e launcher.
- `../../07-REA/INVESTIGACAO-TF2.md`: confirmação anterior do slot TF2 com goodluckOS.

Documentos antigos podem conter tarefas que já foram concluídas posteriormente. Confrontar suas datas e seções de resultado, sem tratar um “pendente” antigo como estado final.

## Fontes técnicas primárias

- [goodluckOS](https://github.com/CodeZombie/goodluckOS).
- [Construtor de imagens goodluckOS](https://github.com/CodeZombie/goodluckOS/blob/main/docs/download.html).
- [sunxi-tools](https://github.com/linux-sunxi/sunxi-tools), [protocolo FEL](https://linux-sunxi.org/FEL/Protocol) e [boot por USB](https://linux-sunxi.org/FEL/USBBoot).
- [Binários Windows do builder eperie v1.3](https://github.com/eperie/build-scripts/releases/tag/v1.3).
- [U-Boot no commit utilizado](https://github.com/u-boot/u-boot/tree/5508406582f6f7120dce0c4b3059819a41788db2).
- [Debianator no commit consultado](https://github.com/Bepok/Debianator/tree/8eadfd86e2c94bf812a6644bd51b6188b586e8d7).
- [Alpine 3.22, arquivos oficiais](https://dl-cdn.alpinelinux.org/alpine/v3.22/releases/x86_64/).
- [PyUSB](https://github.com/pyusb/pyusb) e [libusb-package](https://github.com/pyocd/libusb-package).

## Windows e distribuição

- [Código 28: driver não instalado](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/cm-prob-failed-install).
- [WinUSB e instalação](https://learn.microsoft.com/en-us/windows-hardware/drivers/usbcon/automatic-installation-of-winusb).
- [Remoção segura de dispositivo](https://learn.microsoft.com/en-us/windows/win32/api/cfgmgr32/nf-cfgmgr32-cm_request_device_ejectw).
- [Ciclo de suporte .NET](https://learn.microsoft.com/dotnet/core/releases-and-support).
- [Publicação .NET em arquivo único](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/).
- [Zadig oficial e assinatura do editor](https://zadig.akeo.ie/).
- [FAQ oficial GNU: comercialização e distribuição](https://www.gnu.org/licenses/gpl-faq.en.html#DoesTheGPLAllowMoney).
