# CLAUDE.md — Fazenda & Mercadinho Tycoon

Especificação completa: `Docs/PROMPT_MESTRE.md` (ler antes de qualquer etapa).
Jogo mobile 3D estilizado, retrato, Unity 6.3 LTS + URP, C#, IL2CPP/ARM64. **Só Android na v1.**

## Regras inegociáveis (resumo da seção 0)
1. **Uma etapa por vez.** Nunca começar a próxima sem o "ok" do dono.
2. **Plano curto antes de codar** (arquivos + testes) e esperar "ok" nas etapas 1–6.
3. **Teste contínuo.** Toda regra de jogo vive em `Game.Core` (C# puro) com teste automatizado.
   Etapa só termina com todos os testes passando.
4. **Só Android agora**; serviços de plataforma atrás de interfaces (iOS no futuro).
5. **Conteúdo é dado, não código**: ScriptableObjects com IDs de texto estáveis.
   Proibido `if (id == "tomate")`.
6. **Cenas/prefabs gerados por scripts de editor** (`Assets/_Project/Editor`). Não editar YAML de cena à mão.
7. **Commits pequenos** e claros. Nunca commitar keystore, senhas, chaves de API, licenças.
8. **Depende de olhos humanos** (arte, toque, desempenho no celular)? Parar e dizer o que verificar e onde.
9. **Registrar decisões** em `Docs/decisions/AAAA-MM-DD-titulo.md` (contexto, decisão, alternativas).
10. **Fim de etapa**: o que foi feito, como testar, resultado dos testes, pendências, o que conferir.

## Arquitetura
| Assembly | Pasta | Depende de |
|---|---|---|
| `Game.Core` | `Assets/_Project/Core` | nada (`noEngineReferences: true`) |
| `Game.Content` | `Assets/_Project/Content` | Core, UnityEngine |
| `Game.Services` | `Assets/_Project/Services` | Core |
| `Game.Presentation` | `Assets/_Project/Presentation` | todas |
| `Game.Editor` | `Assets/_Project/Editor` | todas (só Editor) |
| `Game.Tests.EditMode` / `.PlayMode` | `Assets/_Project/Tests/*` | todas |

- Um único `GameState` serializável; tempo por `IClock` (UTC); ações = comandos validados; domínio emite eventos.
- Testes de C# puro ficam em `Assets/_Project/Tests/EditMode/Core/` — **sem UnityEngine** — porque
  também rodam fora do Unity pelo espelho `Tools/CoreTests/CoreTests.csproj` (LangVersion 9 = C# do Unity).
- Packs 3D: originais em `C:\packsgame` (**nunca editar/apagar**). Importar só na etapa em que forem usados,
  para `Assets/ThirdParty/<Pack>/`, sempre envolvidos por prefabs próprios em `Assets/_Project/Presentation/Prefabs/`.
  Inventário: `Docs/assets-inventory.md`.

## Comandos
| O quê | Onde | Comando |
|---|---|---|
| Testes Core (C# puro) | nuvem/Linux | `Tools/run-core-tests.sh` (`--coverage` para cobertura) |
| Configurar projeto | Windows | `.\Tools\setup-project.ps1` |
| Todos os testes | Windows | `.\Tools\run-tests.ps1` (`-Platform EditMode|PlayMode`) |
| APK debug | Windows | `.\Tools\build-android-debug.ps1 [-Install]` |

Os scripts acham o Unity por `$env:UNITY_PATH` ou pelo Unity Hub (versão de `ProjectSettings/ProjectVersion.txt`).
Fechar o Editor antes de rodar scripts em batchmode.

## Ambiente de desenvolvimento
O Claude trabalha numa sessão na nuvem (Linux, sem Unity): escreve o código, roda os testes do Core
com dotnet e faz push para `github.com/carlosaraujoff1-tech/fazendinha`. Tudo que precisa do Editor
Unity (testes EditMode/PlayMode, build, celular) o dono roda no Windows com os scripts acima e devolve a saída.
Ver `Docs/decisions/2026-11-24-ambiente-nuvem-e-testes-core.md`.

## Configuração do projeto (aplicada por `ProjectSetup.cs`)
Pacote `com.fazendamercadinho.tycoon` · retrato fixo · IL2CPP · ARM64 · min API 26 · target API 36 ·
Vulkan + GLES3 · Linear · Input System novo · URP mobile (sem HDR, MSAA 2x, 1 cascata, sem luzes adicionais).
