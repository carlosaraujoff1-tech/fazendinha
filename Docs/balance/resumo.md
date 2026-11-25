# Simulação de balanceamento (gerado)

Gerado por `Tools/run-balance.sh` (BalanceSimulationTests). Robô age 1×/s, caixa manual ~1 item/s.
Patrimônio = moedas + investido (parcelas/móveis) + estoque a preço de fornecedor.

| Estratégia | Horas | Patrimônio | Moedas | Receita | Gasto fornecedor | Vencidos | Atendidos | Bravos | Itens em falta | Recusa por preço | Parcelas | Móveis |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| FarmOnly | 1 | 11819 | 6916 | 15470 | 0 | 0 | 420 | 0 | 876 | 0 | 20 | 8 |
| SupplierOnly | 1 | 4010 | 997 | 20992 | 18860 | 0 | 441 | 0 | 708 | 0 | 4 | 8 |
| Mixed | 1 | 14242 | 5911 | 28992 | 14911 | 0 | 529 | 0 | 385 | 0 | 20 | 8 |
| FarmOnly | 3 | 39003 | 34045 | 52389 | 0 | 0 | 1267 | 0 | 2603 | 0 | 20 | 8 |
| SupplierOnly | 3 | 18884 | 15856 | 98964 | 81253 | 0 | 1577 | 0 | 1254 | 0 | 4 | 8 |
| Mixed | 3 | 43369 | 36104 | 104971 | 52795 | 0 | 1669 | 0 | 830 | 0 | 20 | 8 |
| FarmOnly | 8 | 109093 | 102698 | 146249 | 0 | 22 | 3463 | 0 | 6801 | 0 | 20 | 8 |
| SupplierOnly | 8 | 56580 | 53505 | 293976 | 236816 | 0 | 4413 | 0 | 2485 | 0 | 4 | 8 |
| Mixed | 8 | 119536 | 111241 | 295088 | 147926 | 0 | 4542 | 0 | 1929 | 0 | 20 | 8 |
