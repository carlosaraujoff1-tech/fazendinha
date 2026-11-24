# PROMPT MESTRE — Fazenda & Mercadinho Tycoon (para o Claude Code)

> Como usar: salve este arquivo em `Docs/PROMPT_MESTRE.md` dentro do projeto Unity
> e, na primeira conversa do Claude Code, diga:
> **"Leia Docs/PROMPT_MESTRE.md inteiro e execute somente a Etapa 0."**
> Nas conversas seguintes: **"Leia Docs/PROMPT_MESTRE.md e o CLAUDE.md e execute a Etapa N."**

---

## 0. Quem você é e como vai trabalhar

Você é o desenvolvedor principal deste jogo, trabalhando direto nos arquivos do projeto
Unity no meu computador (Windows). Eu sou o dono do projeto e aprovo cada etapa.

Regras inegociáveis:

1. **Uma etapa por vez.** Nunca comece a etapa seguinte sem eu aprovar a atual.
   Não tente construir o jogo inteiro de uma vez.
2. **Antes de codar, mostre um plano curto** (arquivos que vai criar/alterar, testes que vai
   escrever) e espere meu "ok" nas etapas grandes (1 a 6).
3. **Teste continuamente.** Toda regra de jogo vive em `Game.Core` (C# puro) e tem teste
   automatizado. Rode os testes em `-batchmode` depois de cada passo. Etapa só termina com
   todos os testes passando.
4. **Primeira versão é SÓ ANDROID.** Não configure iOS, Xcode, TestFlight nem App Store agora.
   Mas mantenha o código pronto para iOS no futuro (serviços de plataforma atrás de interfaces).
5. **Conteúdo é dado, não código.** Culturas, animais, produtos, móveis, clientes, melhorias e
   metas são ScriptableObjects com IDs de texto estáveis. Proibido `if (id == "tomate")` no código.
6. **Cenas e prefabs gerados por scripts de editor** em `Tools/` / `Assets/_Project/Editor`
   sempre que possível. Não edite YAML de cena na mão.
7. **Commits pequenos** com mensagem clara ao fim de cada passo que funciona.
   Nunca commite keystore, senhas, chaves de API ou arquivos de licença proibidos de redistribuir.
8. **Quando algo depender de olhos humanos** (arte, sensação do toque, desempenho no celular),
   pare e me diga exatamente o que verificar e onde.
9. **Registre decisões** em `Docs/decisions/AAAA-MM-DD-titulo.md` (contexto, decisão, alternativas).
10. **Ao terminar cada etapa**, entregue: o que foi feito, como testar, resultado dos testes,
    pendências e o que eu preciso conferir.

---

## 1. O jogo

**Fazenda & Mercadinho Tycoon** (nome provisório) — jogo mobile de simulação e gestão, grátis
com monetização justa, em 3D estilizado, colorido e leve, orientação **retrato**.

O jogador tem uma pequena fazenda e um pequeno mercadinho. **O diferencial: os dois negócios são
integrados.** O que a fazenda produz abastece as prateleiras da própria loja. O que a fazenda não
produz pode ser comprado de um fornecedor, mais caro. A decisão central é **produzir ou comprar**.

Loop de uma sessão (3 a 7 min):
1. Entrar e coletar o ganho offline da fazenda (até 4 h).
2. Colher parcelas prontas e replantar.
3. Colheita vai para o **armazém central** (em lotes, com validade).
4. Repor prateleiras da loja a partir do armazém.
5. Ajustar preços; comprar do fornecedor o que faltar.
6. Atender a fila do caixa.
7. Gastar o dinheiro numa melhoria; ver a próxima meta.

Pilares: integração real fazenda↔loja · sessões curtas, progresso longo · mobile primeiro
(uma mão, toque, 60 fps em celular intermediário) · monetização justa (nada de pagar para
progredir) · base extensível (conteúdo novo = arquivo novo).

### Fazenda
- Grade de parcelas: tocar parcela vazia → escolher cultura → plantar (paga semente).
- Crescimento por tempo real (carimbo UTC, não por frame). Regar é opcional e reduz 25% do tempo restante.
- Colher manda os produtos para o armazém.
- Depois do MVP: animais (galinha → ovos, vaca → leite, porco → carne) que consomem ração,
  máquinas, galpões, terrenos novos, funcionários (fazendeiro automatiza colher/replantar).

### Mercadinho
- Grade da loja com móveis; cada prateleira tem categoria (hortifruti, laticínios, mercearia) e capacidade.
- Preço definido por produto, com **preço de referência de mercado (P)** sempre visível e
  indicador "barato / justo / caro".
- Estoque em lotes com validade; vencido vira perda.
- Clientes com lista de compras (1–4 itens), sensibilidade a preço e paciência; fila e caixa.
- Caixa manual no MVP (toque para passar compras); depois funcionários: repositor, caixa, atendente.

### Clientes (fórmula)
Cada tipo aceita até `P × (1 + a)` sem hesitar; acima disso a chance cai linearmente até zero em `P × (1 + a + b)`:

```
chance(p) = clamp(1 - (p/P - (1 + a)) / b, 0, 1)
```

| Tipo | a | b | Peso inicial |
|---|---|---|---|
| Econômico | 0,00 | 0,20 | 50% |
| Família | 0,10 | 0,30 | 35% |
| Gourmet | 0,25 | 0,50 | 15% |

- Selo **"Da nossa fazenda"** (produto vindo da fazenda própria): +0,05 em `a`.
- Preço abaixo de P aumenta a quantidade comprada em metade do desconto relativo
  (20% de desconto → +10% de unidades).
- Item em falta: cliente tenta substituto da mesma categoria ou desiste do item.
- Fila longa reduz paciência; paciência zero = cliente sai sem pagar o que faltou.

### Fornecedor
- Vende qualquer produto, sem limite, entrega em ~2 min, a **0,8 × P**.
- Produzir na fazenda custa ~25% de P. Fazenda = margem alta, lenta, limitada.
  Fornecedor = margem baixa, imediato, ilimitado.

### Economia inicial (tudo em dados, para balancear sem mexer em código)

| Cultura | Tempo | Semente | Rendimento | P | Fornecedor |
|---|---|---|---|---|---|
| Alface | 1 min | 10 | 4 | 10 | 8 |
| Cenoura | 3 min | 18 | 5 | 14 | 11 |
| Batata | 8 min | 27 | 6 | 18 | 14 |
| Tomate | 15 min | 36 | 6 | 24 | 19 |
| Milho | 30 min | 60 | 8 | 30 | 24 (desbloqueia no nível 5) |

- Custos repetíveis (parcela nova, prateleira, nível de funcionário): `custo(n) = custo_base × 1,15^n`.
- Salários debitados a cada 10 min de jogo; sem saldo, o funcionário para. **Não existe game over.**
- Offline: fazenda produz até 4 h; a loja **não** vende offline.
- Progressão: XP (colher, vender, metas) → níveis que desbloqueiam culturas, produtos, móveis,
  terrenos; reputação da loja (sem falta, fila curta, preço justo) aumenta fluxo e mix de clientes;
  uma meta curta ativa por vez com recompensa.

### Monetização (só na Etapa 8)
Anúncio recompensado **sempre opcional** (dobrar ganho offline, acelerar colheita), gemas,
compra única "sem anúncios", cosméticos. Tudo que acelera também se consegue jogando.
Concessão de compra idempotente, restaurar compras, consentimento de privacidade.

---

## 2. Tecnologia

- **Unity 6.3 LTS**, **URP** configurado para mobile, **C#**, **IL2CPP**, **ARM64**.
- **Android**: target API **36** (exigência do Google Play desde 31/08/2026), formato **AAB** para a loja.
- Retrato, Canvas Scaler + Safe Area, uGUI + TextMeshPro, Input System (toque, arrastar, pinça).
- ScriptableObjects para conteúdo; Newtonsoft JSON para save.
- Unity Test Framework (EditMode + PlayMode). Testes de `Game.Core` rodam sem cena.
- Serviços (Etapa 8+): Unity IAP (Google Play Billing), SDK de anúncios recompensados
  (LevelPlay ou AdMob — perguntar antes), analytics, remote config.

## 3. Arquitetura

Princípios:
1. **Domínio sem `UnityEngine`** na assembly `Game.Core`.
2. **Um único `GameState` serializável**; salvar = serializar ele.
3. **Tempo por `IClock`** (UTC); rodar testes com relógio falso.
4. **Ações do jogador são comandos** (`PlantCommand`, `HarvestCommand`, `WaterCommand`,
   `RestockCommand`, `SetPriceCommand`, `BuyFromSupplierCommand`, `BuyUpgradeCommand`…)
   validados por um executor. A UI nunca altera o estado direto.
5. **Domínio emite eventos** (`IEventBus`); a apresentação escuta.
6. **Plataforma atrás de interfaces**: `IPurchaseService`, `IAdService`, `IAnalytics`,
   `ICloudSave`, `ICrashReporter`, com implementação falsa para editor e testes.

| Assembly | Conteúdo | Depende de |
|---|---|---|
| `Game.Core` | Estado, comandos, regras, economia, clientes, catálogo, interfaces | nada |
| `Game.Content` | ScriptableObjects e conversão para o catálogo | Core, UnityEngine |
| `Game.Services` | Save em disco, relógio real, IAP, anúncios, analytics | Core |
| `Game.Presentation` | Cenas, câmera, UI, personagens, áudio, VFX | todas |
| `Game.Tests` | EditMode e PlayMode | todas |

Pastas:

```
Assets/
  _Project/
    Core/  Content/  Services/  Presentation/  Editor/  Tests/
  ThirdParty/          // packs 3D importados (ver seção 4)
Docs/                  // PROMPT_MESTRE.md, GDD, decisions/, balanceamento, inventário
Tools/                 // scripts .ps1/.bat para testes e build em batchmode
CLAUDE.md
```

Save: JSON com `schemaVersion` e migrações; gravação atômica (temp + troca) e backup do último
válido; autosave a cada 30 s, ao pausar e ao sair do app.

Desempenho: alvo 60 fps (opção 30 fps), poucos materiais em atlas, GPU instancing, SRP Batcher,
modelos low-poly, luz simples/baked, pooling de clientes e efeitos, zero alocação por frame no loop.

---

## 4. Packs 3D — ficam em `C:\packsgame`

Eu já baixei os packs de arte 3D que o jogo vai usar. **Eles estão em `C:\packsgame`**
(pasta vinculada ao seu ambiente). Regras:

1. **Nunca edite nem apague nada em `C:\packsgame`.** É a fonte original.
2. **Na Etapa 0, só faça o inventário** e salve em `Docs/assets-inventory.md`, com, para cada pack:
   nome e origem provável; arquivo de licença e se permite uso comercial em jogo;
   quantidade de modelos e categorias; personagens animados (Humanoid/Generic);
   tamanho total e das texturas; pipeline dos materiais (Built-in/URP/próprio).
   Depois faça um **mapa "necessidade do jogo → asset"** cobrindo: parcelas/terra, as 5 culturas
   (idealmente em fases de crescimento), cerca/decoração da fazenda, armazém/galpão, caminhão,
   fachada e interior do mercadinho, prateleiras/gôndolas/geladeira, caixa registradora,
   produtos (hortifruti, laticínios, mercearia), clientes e funcionários animados, animais.
   Liste o que **falta** e se os estilos combinam entre si (paleta, proporção, detalhe).
3. **Importe só o necessário, e só na etapa em que for usado** (gray box até a Etapa 5;
   arte final na Etapa 6). Copie para `Assets/ThirdParty/<NomeDoPack>/`, sem bagunçar a estrutura.
4. **Converta materiais para URP** e padronize: poucos materiais compartilhados, texturas
   comprimidas para Android (ASTC), máximo 1024 px salvo justificativa.
5. Assets entram no git via **Git LFS**. Se uma licença proibir redistribuição em repositório
   público, me avise; o repositório deve ser privado.
6. Crie prefabs próprios em `Assets/_Project/Presentation/Prefabs/` que **envolvem** o modelo
   do pack. O jogo referencia o prefab próprio, nunca o modelo do pack direto — assim trocar
   de pack depois não quebra nada.

---

## 5. Etapas (execute UMA por vez)

Cada etapa: objetivo → entregas → critério de aceite. Só avance com meu "ok".

### Fase A — MVP jogável (Android)

**Etapa 0 — Fundação**
- Entregas: git + `.gitignore` Unity + Git LFS; estrutura de pastas e as 5 assemblies;
  `CLAUDE.md` com as regras da seção 0 resumidas; URP mobile, retrato, IL2CPP, ARM64,
  target API 36; um teste de exemplo; `Tools/run-tests.ps1` e `Tools/build-android-debug.ps1`
  em `-batchmode`; **inventário dos packs** (seção 4) em `Docs/assets-inventory.md`.
- Aceite: testes passam por linha de comando; APK debug instala e abre no meu celular;
  inventário entregue com o mapa de necessidades e lacunas.
- Pare e me diga o que eu preciso fazer no Unity Hub e no celular (depuração USB).

**Etapa 1 — Núcleo do domínio (sem cena)**
- `GameState`, `IClock`, catálogo, `IEventBus`, executor de comandos, inventário em lotes
  (data de colheita, validade), carteira de moedas e gemas, save/load versionado e atômico.
- Aceite: testes de comandos válidos/inválidos, passagem de tempo com relógio falso,
  vencimento de lotes, ida e volta do save; cobertura dos sistemas centrais acima de 80%.

**Etapa 2 — Fazenda jogável (gray box)**
- Parcelas, plantar, regar, colher para o armazém; 5 culturas como dados; cena gerada por
  script com formas simples; câmera de toque (arrastar, pinça); HUD com moedas e meta.
- Aceite: no celular, ciclo plantar→regar→colher funciona a 60 fps; fechar e reabrir mantém o progresso.

**Etapa 3 — Mercadinho e clientes (gray box)**
- Grade da loja, prateleiras com capacidade, reposição armazém→prateleira, preço com P visível,
  clientes com os 3 tipos e a fórmula de compra, fila, caixa manual, vendas creditam moedas.
- Aceite: testes da fórmula e do fluxo do cliente; no celular, 1 minuto de loja mostra clientes
  comprando, desistindo por preço ou falta, e pagando.

**Etapa 4 — Integração, fornecedor e economia**
- Fornecedor (0,8 P), selo "Da nossa fazenda", salários, ganho offline (até 4 h).
- Simulador headless nos testes: joga 1 h, 3 h e 8 h com estratégias "só fazenda",
  "só fornecedor" e "misto"; gera CSV de dinheiro, estoque e perdas em `Docs/balance/`.
- Aceite: o misto supera os extremos e nenhuma estratégia trava sem saída.

**Etapa 5 — Interface, tutorial e progressão**
- UI final em uGUI com safe area (telas: Fazenda, Plantar, Armazém, Mercadinho, Definir preço,
  Fornecedor, Caixa, Metas, Melhorias, Bem-vindo de volta — seguir os mockups aprovados);
  barra inferior com 5 abas; XP, níveis e desbloqueios por dados; metas; tutorial dos 5
  primeiros minutos como sequência de passos em dados; configurações (som, idioma, qualidade).
- Aceite: UI correta em 4 proporções de tela (incluindo com recorte de câmera);
  5 pessoas novas concluem o tutorial sem ajuda.

**Etapa 6 — Arte 3D e áudio (packs de `C:\packsgame`)**
- Trocar gray box pelos prefabs que envolvem os modelos dos packs; materiais URP padronizados;
  animações de clientes, plantas crescendo, efeitos de colheita e venda; música e sons.
- Aceite: metas de desempenho mantidas nos 2 celulares Android de referência; eu aprovo o visual.

**Etapa 7 — Otimização, QA e teste interno**
- Profiler nos aparelhos; relatório de falhas atrás de `ICrashReporter`; testes de robustez do save
  (matar o app durante gravação, relógio alterado, disco cheio); script de **AAB assinado**
  (keystore fora do repositório, senha por variável de ambiente); envio ao teste interno do Google Play.
- Aceite: 30 min sem falha nos dois aparelhos; save sobrevive aos testes; 10 testadores jogaram.

### Fase B — Jogo completo

**Etapa 8 — Monetização**: `IAdService` e `IPurchaseService` reais (perguntar qual SDK de anúncios),
anúncios recompensados opcionais, gemas, sem-anúncios, cosméticos, concessão idempotente,
restaurar compras, consentimento, política de privacidade. Aceite: compras de teste não creditam
duas vezes; nenhum anúncio sem o jogador escolher; nada de progressão só pago.

**Etapa 9 — Animais, máquinas e funcionários**: galinhas/vacas/porcos com ração e ciclos;
máquinas; funcionários de fazenda e loja com salário e nível; produtos e clientes novos — tudo dados.
Aceite: simulador reexecutado sem estratégia dominante nem ponto sem saída.

**Etapa 10 — Expansão**: terrenos, galpões, reforma e ampliação da loja, novos setores,
segunda loja e regiões, gerentes, missões diárias, conquistas. Aceite: simulação de 20 h estável.

**Etapa 11 — Publicação no Google Play**: ícone, capturas, vídeo, textos em pt-BR, classificação
etária, formulário de segurança de dados, política de privacidade publicada, conferir target API 36,
teste fechado (verificar no Play Console se minha conta exige período de teste antes da produção),
lançamento suave antes do global.

**Etapa 12 — Operação**: analytics e funis, remote config para balancear sem nova versão,
eventos sazonais, save na nuvem, atualização anual de SDKs e do target API.

**Etapa futura — iOS**: só depois do lançamento Android, num Mac com Xcode.

---

## 6. Agora

Execute **somente a Etapa 0**. Comece me mostrando o plano curto e espere meu "ok".
