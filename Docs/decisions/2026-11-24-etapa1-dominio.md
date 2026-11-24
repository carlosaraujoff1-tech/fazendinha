# 2026-11-24 — Etapa 1: decisões do núcleo do domínio

## Contexto
O dono pediu para seguir sem aprovar etapa a etapa até a primeira build (jogo de uso pessoal).
As decisões abaixo foram tomadas por padrão razoável e podem ser revistas.

## Decisões
1. **Tempo em `long` ms Unix UTC** em todo o estado (nada de `DateTime` serializado): sem fuso,
   sem ambiguidade de formato, comparação barata. `IClock.UtcNowMs`; `ManualClock` para testes/simulador.
2. **Relógio voltando no tempo é ignorado** pela simulação (não "desvence" nem reprocessa).
   Proteção mais forte contra relógio alterado fica para a Etapa 7.
3. **Estoque FEFO** (vence primeiro, sai primeiro); empate pela produção mais antiga. Retirada é
   tudo-ou-nada e devolve os pedaços com origem, o que é necessário para o selo "Da nossa fazenda".
   Lotes idênticos (produto, origem, produção, validade) são fundidos.
4. **Lote vence quando `agora >= validade`.** `ShelfLifeSeconds = 0` significa que não vence.
5. **Comandos**: `ICommandHandler<T>` com `Validate` (sem efeitos) e `Apply`. O `CommandExecutor`
   avança a simulação antes de validar, para que um comando nunca use estado "atrasado" (ex.: estoque já vencido).
   Erros são códigos de texto estáveis (`CommandErrors`), para a UI traduzir.
6. **Save**: Newtonsoft com `TypeNameHandling.None` (seguro), migrações sobre `JObject` em cadeia
   (v→v+1). Um save de versão mais nova que o jogo é recusado. Se principal e backup estiverem
   ilegíveis, `Load` lança exceção e **nunca** começa um jogo novo por cima em silêncio.
7. **Gravação atômica** em `Game.Services.FileSystem.AtomicFileSaveStore`: `.tmp`, depois `flush(true)`,
   depois `File.Replace` (o principal anterior vira `.bak`), com fallback manual para sistemas de
   arquivos sem `Replace`. É C# puro e testado fora do Unity.
8. **Espelho de testes** passou a compilar `Game.Core` e a parte pura de Services como `netstandard2.1`
   (mesmo perfil de API do Unity) e falha com cobertura de linhas abaixo de 80%.
9. `link.xml` preserva `Game.Core`/`Game.Services` contra o stripping do IL2CPP (o save usa reflexão).

## Alternativas
- `DateTime`/ISO-8601 no save: mais legível, mas sujeito a fuso/cultura. Descartado.
- FIFO por data de produção: perderia mais produtos por vencimento. Descartado.
- `System.Text.Json`: não vem no perfil do Unity. Newtonsoft já é pacote oficial.
