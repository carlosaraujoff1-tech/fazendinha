# Inventário dos packs 3D (Etapa 0)

**Fonte analisada:** `packsgame.zip` (24,6 MB; equivalente a `C:\packsgame`), aberto só para leitura,
sem alterar o original. Nada foi importado para `Assets/` ainda (só na Etapa 6; gray box até a Etapa 5).
**Método:** leitura dos arquivos de licença, parse dos FBX binários (malhas, vértices, materiais,
texturas, ossos, animações), do JSON do `.glb` e renderização de prévia do pack de loja.

## 1. Resumo

| # | Pack | Origem provável | Licença | Uso comercial | Modelos | Animados | Tamanho (FBX) | Texturas | Materiais |
|---|---|---|---|---|---|---|---|---|---|
| A | **Nature Crops Pack** | Quaternius (`Preview.png`) | `License (1).txt`: **CC0 1.0** | ✅ Sim, sem crédito obrigatório | 102 | Não | 2,9 MB | Nenhuma (cor por material) | 23 materiais de cor sólida |
| B | **Farm Animals Pack** | Quaternius (`Preview.gif`) | `License.txt`: **CC0 1.0** | ✅ Sim | 7 | ✅ Sim, rig **Generic** | 4,5 MB | Nenhuma | 2–3 por animal |
| C | **Farm Buildings** | Quaternius (`Preview (1).png`) | `License (2).txt`: **CC0 1.0** | ✅ Sim | 13 | Não | 0,7 MB | Nenhuma | 1–7 por modelo (cores compartilhadas) |
| D | **SimplePolygon Store Props** | Autor desconhecido ("SimplePolygon"; caminho interno `C:\Users\Maxim\...`) | ❌ **Sem arquivo de licença** | ⚠️ **Desconhecido** | 27 malhas num só FBX | Não | 0,19 MB | 1 paleta **2048×2048** RGBA (0,29 MB) | 2 (paleta) |
| E | **Stocked Grocery Aisle** (`model.glb`, mais uma cópia idêntica `model (1).glb`) | Gerado/ingerido por **3dassets.dev** | ❌ **Sem arquivo de licença** | ⚠️ **Desconhecido** | 1 cena com 133 objetos (29 malhas) | Não | 0,1 MB (GLB) | Nenhuma | 8 cores sólidas |

**Formatos:** A, B e C vêm em **FBX, OBJ e .blend**. Para o Unity usaremos **só FBX**; `.blend`
(≈ 69 MB no total) e `.obj` não entram no projeto. FBX versão 7.4 (binário, exportado do Blender).

**Pipeline dos materiais:** nenhum pack usa URP. Os FBX importam com materiais **Built-in/Standard**
(cores sólidas, sem textura); o GLB usa PBR do glTF. Na Etapa 6, tudo será convertido para **URP**
(ver §5).

**Nota sobre as licenças:** A, B e C (Quaternius, CC0) podem ser usados comercialmente e redistribuídos,
inclusive em repositório público. **D e E não têm licença.** Até você confirmar a origem e a licença,
eu os trato como **"não usar"** e eles não entram no repositório.

## 2. Detalhe por pack

### A — Nature Crops (Quaternius, CC0)
20 espécies. A maioria tem as fases `_1` a `_4` (crescimento), `_Crop` (o produto colhido,
bom para prateleira e ícone) e `_Harvested` (a planta depois da colheita).

| Espécie | Fases | Crop | Harvested | Vértices (fase 4) |
|---|---|---|---|---|
| Lettuce (alface) | 1–4 | ✅ | ✅ | 714 |
| Carrot (cenoura) | 1–4 | ✅ | — | 271 |
| Tomato (tomate) | 1–4 | ✅ | ✅ | 822 |
| Corn (milho) | 1–4 | ✅ | ✅ | 535 |
| Beet (beterraba) | 1–4 | ✅ | — | 276 |
| Pumpkin (abóbora) | 1–4 | ✅ | ✅ | 1483 |
| Watermelon (melancia) | 1–4 | ✅ | ✅ | 1327 |
| Wheat (trigo), Rice (arroz) | 1–4 | ✅ | — | 53 / 1036 |
| Apple, Orange (árvores) | 1–4 | ✅ | ✅ | 834 / 658 |
| BushBerries (frutas vermelhas) | 1–4 | ✅ | ✅ | 470 |
| Mushroom, Cactus, PalmTree, Bamboo, Flower(s), Grass | 1–4 | vários | vários | 120–1179 |

Total: cerca de 33 mil vértices nos 102 modelos. É leve para mobile.

### B — Farm Animals (Quaternius, CC0)
Cada animal é uma malha com skin (35–39 ossos), rig Generic e 283–679 vértices.

| Animal | Animações |
|---|---|
| Cow (vaca), Horse, Zebra | Idle, Walk, WalkSlow, Run, Jump, Death |
| Pig (porco), Sheep, Llama, Pug | **só Idle e Jump** |

### C — Farm Buildings (Quaternius, CC0)
Barn, BigBarn, SmallBarn, OpenBarn (galpão aberto), Silo, Silo_House, ChickenCoop (galinheiro), Well (poço),
WaterTower, Windmill, TowerWindmill, Fence e Fence2. São 600–3787 vértices, com materiais
de cor compartilhados (DarkRed, LightRed, RoofBlack, White, Brown, Grey...).

### D — SimplePolygon Store Props (⚠️ licença desconhecida)
27 malhas com nomes genéricos (`Cube.050`...). Pela prévia renderizada, o pack contém:
- **gôndolas/estantes** de uma e de duas faces, em vários tamanhos;
- **molduras de portas de vidro** (geladeira/freezer vertical);
- **balcão de caixa com esteira** e um objeto parecido com uma **caixa registradora / maquininha**;
- **caixas de papelão** (4 tamanhos);
- **produtos**: garrafas (verde, azul), caixa de leite, lata, pote;
- 4 **painéis grandes** (parede/piso?) em escala muito diferente das outras peças (cerca de 47×109 unidades).
  A escala das peças é inconsistente e precisa ser normalizada nos prefabs.

Tudo usa **uma paleta (atlas) 2048 px**. É ótimo para desempenho (um material só). Vamos reduzir
a paleta para 256–512 px, pois é uma paleta de cores e não precisa de 2048.

### E — Stocked Grocery Aisle / 3dassets.dev (⚠️ licença desconhecida)
Cena pronta de um corredor de mercado:
- 4 gôndolas de uma face e 3 bancas inclinadas de hortifruti;
- produtos (12 unidades de cada): maçã, pera, laranja;
- produtos (10 unidades de cada): caixa de leite, cereal, geleia, água, café, refrigerante em lata, farinha, enlatado.

São 8 materiais de cor sólida, estilo low-poly. Seria uma ótima fonte de produtos de mercearia e
laticínios, **se a licença permitir**.

## 3. Mapa "necessidade do jogo → asset"

| Necessidade | Asset proposto | Pack | Situação |
|---|---|---|---|
| Parcela / terra arada | — | — | ❌ Falta. É simples de fazer: malha low-poly gerada por script, ou um cubo marrom achatado com sulcos |
| **Alface** (fases + produto) | `Lettuce_1..4`, `Lettuce_Crop` | A | ✅ |
| **Cenoura** | `Carrot_1..4`, `Carrot_Crop` | A | ✅ |
| **Batata** | — | — | ❌ **Falta.** Opções: (a) usar `Beet` com material marrom-claro como batata; (b) trocar a cultura por **Beterraba** ou **Abóbora**, que já existem; (c) procurar um pack com batata |
| **Tomate** | `Tomato_1..4`, `Tomato_Crop`, `Tomato_Harvested` | A | ✅ |
| **Milho** | `Corn_1..4`, `Corn_Crop`, `Corn_Harvested` | A | ✅ |
| Culturas futuras | Abóbora, Melancia, Trigo, Arroz, Beterraba, Frutas vermelhas, Maçã/Laranja (pomar) | A | ✅ Bônus |
| Cerca / decoração da fazenda | `Fence`, `Fence2`, `Well`, `Windmill`, `Grass_*`, `Flower_*`, árvores | A, C | ✅ |
| Armazém central / galpão | `Barn` / `BigBarn` (armazém), `OpenBarn` (galpão), `Silo` | C | ✅ |
| **Caminhão do fornecedor** | — | — | ❌ **Falta** |
| **Fachada do mercadinho** | — | — | ❌ **Falta.** Dá para montar um prédio simples com peças próprias (cubos + toldo) ou buscar um pack de cidade low-poly |
| Interior: piso, paredes | painéis grandes de D? | D ⚠️ | ⚠️ Depende da licença; senão, gerar por script |
| Prateleiras / gôndolas | gôndolas de D **ou** E | D ⚠️ / E ⚠️ | ⚠️ Depende da licença |
| Banca de hortifruti | `produce-tilt-bin` | E ⚠️ | ⚠️ |
| Geladeira (laticínios) | molduras de porta de vidro de D | D ⚠️ | ⚠️ Parcial: precisa do corpo da geladeira |
| Caixa registradora / balcão | balcão com esteira de D | D ⚠️ | ⚠️ |
| Produtos hortifruti | `*_Crop` de A (alface, cenoura, tomate, milho...) + maçã/pera/laranja de E | A ✅ / E ⚠️ | ✅ Com A já dá para abastecer a loja |
| Produtos de laticínios | caixa de leite (D, E) | D/E ⚠️ | ⚠️ Faltam **ovo, queijo, iogurte** |
| Produtos de mercearia | cereal, café, farinha, enlatado, refrigerante, água (E); garrafas/latas (D) | D/E ⚠️ | ⚠️ |
| Carne (porco, Etapa 9) | — | — | ❌ Falta |
| **Clientes animados** | — | — | ❌ **Falta. É a lacuna mais importante** (personagens humanos com Idle/Walk/pegar item) |
| **Funcionários animados** | — | — | ❌ Falta (podem ser os mesmos personagens com uniforme/cor) |
| Vaca, porco | `Cow` (anims completas), `Pig` (só Idle/Jump) | B | ✅ / ⚠️ porco sem Walk |
| **Galinha** | — (só o galinheiro `ChickenCoop`) | C | ❌ **Falta a galinha** |
| Ícones de UI dos produtos | renderizar os modelos `_Crop` | A | ✅ Possível gerar por script na Etapa 5/6 |

### O que falta (resumo, por prioridade)
1. **Personagens humanos animados** (clientes e funcionários): necessários na Etapa 6, mas o gray box segura até lá.
2. **Licença de D e E**: sem ela, faltam todos os móveis da loja e os produtos de mercearia.
3. **Batata**: decidir agora (afeta os dados da Etapa 2): substituir, trocar de cultura ou procurar outro pack.
4. **Galinha** (Etapa 9), **caminhão** e **fachada da loja** (Etapa 6).
5. Produtos de laticínios e carne além do leite.

Sugestão de fontes, **a verificar antes de usar**: outros packs CC0 da própria Quaternius (personagens
animados, animais) e da Kenney (veículos, cidade). Usar packs CC0 do mesmo autor mantém o estilo coerente.

## 4. Os estilos combinam?

- **A + B + C (Quaternius):** ✅ Combinam muito bem: mesmo autor, low-poly facetado, cores sólidas,
  paleta saturada parecida (vermelho celeiro, verdes, marrons). Proporção "realista estilizada".
- **E (3dassets.dev):** ✅ Provavelmente combina, pois também é low-poly com cores sólidas. Precisa de
  conferência visual de saturação e escala.
- **D (SimplePolygon):** ⚠️ O estilo é parecido (low-poly), mas a técnica é outra: textura de
  paleta em vez de cores por material. Algumas peças são muito detalhadas (estantes, cerca de 3,7 mil vértices no total)
  e a escala varia muito entre peças. É compatível com ajuste de materiais.
- **Escala:** cada pack foi exportado com uma escala; os prefabs próprios (§4 do PROMPT_MESTRE) vão normalizar
  isso (ex.: 1 unidade = 1 m; parcela 1×1 m).

## 5. Plano técnico para a Etapa 6 (não executado agora)
- Importar **só os FBX necessários** para `Assets/ThirdParty/Quaternius_Crops/`, `.../Quaternius_FarmAnimals/` e `.../Quaternius_FarmBuildings/`.
- **Materiais:** juntar as cerca de 40 cores sólidas da Quaternius numa **paleta (atlas) 256 px**
  com um único material URP (Simple Lit ou Lit), remapeando as UVs por script de editor. Assim ficam
  pouquíssimos materiais, SRP Batcher e GPU instancing funcionam bem. Alternativa mais simples: um material URP compartilhado por cor.
- Texturas em **ASTC** para Android, no máximo 1024 px (a paleta de D cai para 512 px).
- Animais: rig Generic, Animator simples (Idle/Walk) e GPU skinning.
- Tudo via **Git LFS** (já configurado no `.gitattributes`).

## 6. ⚠️ Repositório público
O repositório `carlosaraujoff1-tech/fazendinha` está **público**. Os packs CC0 podem ficar nele, mas
**D e E só podem entrar se a licença permitir redistribuição**. Recomendo deixar o repositório **privado**
(GitHub, em Settings > General > Danger Zone > Change visibility).
