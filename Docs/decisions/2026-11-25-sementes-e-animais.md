# 2026-11-25 — Vendinha de sementes, trigo e animais

## Contexto
Pedido do dono: sementes não devem "surgir do nada"; parte da colheita alimenta animais; os animais produzem
itens para o mercadinho; mais animais (galinha) e mais culturas (trigo, milho).

## Decisões (todas aprovadas pelo dono)
- **Sementes são estoque** (`FarmState.Seeds`). Compra na vendinha (`BuySeedsCommand`, preço = `seedCost` da cultura);
  plantar gasta 1. No menu Plantar, sem semente, o botão oferece "comprar e plantar" (atalho). Começo: 4 alface + 4 trigo.
- **Trigo**: cultura barata e rápida (90 s, semente 6, rende 6, P = 5), a "ração". **Milho liberado desde o nível 1.**
- **Animais** (`AnimalDef` em dados): comprar → alimentar (gasta comida do armazém, na ordem de preferência) →
  produz após o ciclo → coletar (vai para o armazém como lote da fazenda, com selo). Sem comida, só para de produzir.
  - Galinha: 2 trigo ou milho → 3 ovos a cada 2 min (começa com 1 galinha).
  - Vaca: 3 trigo ou milho → 4 leites a cada 5 min.
  - Ovelha: 3 trigo → 2 lãs a cada 8 min.
  - Porco: só enfeite.
- Preço do próximo animal segue a curva 1,15^n; limite por tipo nos dados.
- **Fornecedor continua** como opção mais cara.
- O tempo dos animais também "dorme" após 4 h fora (TimeShift).

## Visual
Galinha animada (Kenney Cube Pets, CC0), galinheiro (Quaternius), trigo em 4 fases (Quaternius), ovo (Kenney),
novelo de lã (modelo próprio). Galpão aberto vira a vendinha. Pasto cercado. Indicador: ícone da comida pulsando
(com fome), produto girando com brilho (pronto).

## Balanceamento
O simulador passou a deixar os robôs contratarem o caixa (mesma regra para todos). Sem isso, a fila virava o gargalo
e os itens de margem baixa do fornecedor ocupavam o lugar dos itens da fazenda. Misto continua vencendo:
8 h: misto 119,5 mil · só fazenda 109 mil · só fornecedor 56,6 mil.
