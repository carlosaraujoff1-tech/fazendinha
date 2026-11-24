# Simulação de balanceamento (gerado)

Gerado por `Tools/run-balance.sh` (BalanceSimulationTests). Robô age 1×/s, caixa manual ~1 item/s.
Patrimônio = moedas + investido (parcelas/móveis) + estoque a preço de fornecedor.

| Estratégia | Horas | Patrimônio | Moedas | Receita | Gasto fornecedor | Vencidos | Atendidos | Bravos | Itens em falta | Recusa por preço | Parcelas | Móveis |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| FarmOnly | 1 | 13876 | 9498 | 18022 | 0 | 0 | 449 | 0 | 817 | 0 | 20 | 8 |
| SupplierOnly | 1 | 4521 | 1598 | 22342 | 19879 | 0 | 444 | 0 | 629 | 0 | 4 | 8 |
| Mixed | 1 | 16221 | 10016 | 35802 | 17378 | 0 | 534 | 0 | 379 | 0 | 20 | 8 |
| FarmOnly | 3 | 44496 | 40261 | 59216 | 0 | 0 | 1378 | 0 | 2441 | 0 | 20 | 8 |
| SupplierOnly | 3 | 21291 | 18436 | 104142 | 84841 | 0 | 1600 | 0 | 1077 | 0 | 4 | 8 |
| Mixed | 3 | 54505 | 48345 | 122166 | 55664 | 0 | 1689 | 0 | 746 | 0 | 20 | 8 |
| FarmOnly | 8 | 122256 | 117762 | 162884 | 0 | 20 | 3638 | 0 | 6378 | 0 | 20 | 8 |
| SupplierOnly | 8 | 62924 | 60056 | 308322 | 247401 | 0 | 4461 | 0 | 2100 | 0 | 4 | 8 |
| Mixed | 8 | 148262 | 141384 | 331900 | 148738 | 0 | 4564 | 0 | 1748 | 0 | 20 | 8 |
