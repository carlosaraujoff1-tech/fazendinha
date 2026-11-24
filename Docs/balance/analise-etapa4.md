# Análise de balanceamento — Etapa 4 (2026-11-24)

Fonte: `Docs/balance/resumo.md` e os CSV ao lado (gerados por `Tools/run-balance.sh`).
Robôs (`Game.Core.Balance.BalanceBot`) jogando continuamente, caixa manual ~1 item/s, preços = P.

## Critérios de aceite da Etapa 4
| Critério | Resultado |
|---|---|
| Misto supera "só fazenda" e "só fornecedor" (1 h, 3 h, 8 h) | ✅ 8 h: misto 148 mil · fazenda 122 mil · fornecedor 63 mil (patrimônio) |
| Nenhuma estratégia trava sem saída | ✅ nenhuma ficou sem dinheiro + sem estoque + sem plantio + sem pedido |

## O que os números mostram
1. **Fazenda é a fonte de lucro; fornecedor dá alcance.** "Só fornecedor" fatura parecido com o misto, mas paga 80% ao fornecedor.
   "Só fazenda" tem margem alta, mas deixa de vender laticínios e mercearia (mais itens em falta).
   O misto junta os dois, que é exatamente o objetivo do design ("produzir ou comprar").
2. **Progressão rápida demais.** O robô da fazenda compra as 20 parcelas em menos de 1 h e termina 8 h com cerca de 118 mil moedas
   sem ter onde gastar. Ainda não existem XP, níveis, metas nem sumidouros (Etapas 5, 9 e 10). Sugestões, para quando houver o que comprar:
   - `plotBaseCost` de 50 para cerca de 150 e/ou crescimento de parcela 1,15 → 1,25;
   - liberar parcelas por nível (Etapa 5), em vez de só por dinheiro.
3. **Ninguém sai bravo.** Com o jogador passando 1 item/s, a fila nunca estoura. A paciência só vai pesar quando o jogador
   estiver ocupado na fazenda. Vale medir no celular (Etapa 5/7) antes de mexer.
4. **Quase nada vence** com o robô ativo (20 unidades em 8 h no "só fazenda"). A validade só pune quem some com o estoque cheio,
   e o limite offline de 4 h protege quem dorme.
5. **Ajuste feito nesta etapa:** cliente aceita substituto da mesma categoria com 60% de chance (`substituteAcceptance`).
   Sem isso, estocar só o produto mais caro de cada categoria vendia tudo, o que tornava a variedade inútil.

## Limitações do simulador
- O robô é ótimo demais (age a cada segundo, nunca sai do jogo). Sessões curtas com ausências (o jogo real) ainda não são simuladas.
- Preço fixo em P. Os testes de fórmula cobrem preço, mas o robô não explora preço ótimo.
- Sem XP/níveis: o milho (nível 5) nunca é usado.
