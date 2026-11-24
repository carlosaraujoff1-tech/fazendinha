# 2026-11-24 — Números do conteúdo em JSON (não em ScriptableObject)

## Contexto
O PROMPT_MESTRE pede conteúdo como ScriptableObjects com IDs estáveis. Mas o Claude trabalha sem Unity,
e ScriptableObjects (.asset YAML) só podem ser lidos/validados dentro do Unity.

## Decisão
- Os **dados de regra** (culturas, produtos, economia, e depois clientes, móveis, metas) ficam em
  `Assets/_Project/Content/Data/catalog.json` (TextAsset). `Game.Core.Catalog.CatalogJson` faz o parse e a
  validação completa; os testes do Core carregam **o arquivo real** e conferem com a tabela do design.
- **ScriptableObjects** continuam sendo usados para o que é **visual/Unity** (prefab por cultura, ícones, sons),
  referenciando o mesmo ID de texto. Entram na Etapa 6.
- Campos desconhecidos no JSON são erro (pega erro de digitação de balanceamento).

## Por quê
O princípio do design é preservado ("conteúdo é dado, não código"; conteúdo novo = editar arquivo, sem código),
com ganhos: validado por testes fora do Unity, diff legível no git, e o mesmo formato serve para remote config (Etapa 12).

## Alternativas
- SOs + editor script de validação: só valida com Unity aberto. Descartado por ora.
- Gerar SOs a partir do JSON: duplicaria a fonte da verdade.
