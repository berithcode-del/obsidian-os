# Chaves confiáveis de pacotes

`trusted-package-keys.json` é empacotado pelo aplicativo, fora do pacote de imagem. A versão atual tem uma lista vazia; por isso nenhuma assinatura é aceita como confiável e toda instalação continua bloqueada.

O verificador reconhece envelopes `*.sig.json` no formato de `schemas/package-signature.schema.json`. A assinatura ECDSA P-256/SHA-256 cobre os bytes exatos do manifesto, inclusive eventual BOM UTF-8. As chaves públicas são fixadas no aplicativo; uma chave entregue junto do manifesto não ganha confiança automaticamente.

Antes de adicionar uma chave de publicação, o projeto precisa definir responsável, custódia offline da chave privada, rotação/revogação, revisão de distribuição e assinatura Authenticode do aplicativo que contém a lista de chaves. Nunca colocar chave privada neste diretório ou no repositório.
