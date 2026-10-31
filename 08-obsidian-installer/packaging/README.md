# Empacotamento planejado

Formato inicial proposto: ZIP portátil Windows x64, com um executável principal para o cliente abrir.

```text
ObsidianInstaller/
  ObsidianInstaller.exe
  ObsidianInstaller.Elevated.exe
  native/
  profiles/
  licenses/
  images/        # opcional no kit offline
```

Essa árvore é um contrato de saída, não uma distribuição já montada. O pipeline precisa validar arquitetura, integridade, assinaturas, catálogo e licenças. O cliente não receberá WSL, Python, compiladores, scripts com identidade de disco fixa, relatórios privados ou a imagem pessoal de fábrica.

A publicação autossuficiente deve ser medida em uma máquina sem runtime prévio. Antes de escolher um arquivo inicial único, testar extração, diretório protegido do helper e carregamento de DLLs. Autenticar o pacote e seus componentes antes da elevação e reconferir no helper.

Um pacote de laboratório nunca é promovido copiando arquivos para esta árvore. Publicação depende dos critérios de `docs/HOMOLOGACAO.md` e da revisão dos componentes em `third_party/`.
