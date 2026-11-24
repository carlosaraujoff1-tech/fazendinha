# 2026-11-24 — Ambiente na nuvem e testes do Core fora do Unity

## Contexto
O PROMPT_MESTRE supõe o Claude Code rodando no Windows do dono, com Unity e `C:\packsgame`.
Na prática o Claude roda numa sessão na nuvem (contêiner Linux temporário, **sem Unity**),
e o dono escolheu continuar assim. Os packs chegaram como `packsgame.zip`.

## Decisão
1. O código vive no repositório GitHub `carlosaraujoff1-tech/fazendinha`; o Claude faz push a cada passo.
2. Os testes de `Game.Core` são C# puro e ficam em `Assets/_Project/Tests/EditMode/Core/`.
   Eles rodam **em dois lugares**:
   - no Unity Test Runner (fonte da verdade), pela assembly `Game.Tests.EditMode`;
   - na nuvem, por `Tools/CoreTests/CoreTests.csproj` (net8.0, **LangVersion 9** = C# do Unity),
     que compila os mesmos `.cs`. Assim a regra "testar continuamente" vale mesmo sem Unity.
3. Configuração do projeto (Player Settings, URP, cena Boot) é feita pelo script de editor
   `ProjectSetup.cs`, idempotente — não se edita `ProjectSettings/*.asset` à mão.
4. Os `.meta` de `Assets/` foram gerados com GUIDs aleatórios para que as referências fiquem estáveis
   desde o primeiro commit.
5. Pacote Android `com.fazendamercadinho.tycoon` (provisório, mas difícil de mudar depois de publicar),
   min API 26 (Android 8.0), target API 36.

## Alternativas consideradas
- **Claude Code direto no Windows / Remote Control**: rodaria o Unity em cada passo, mas o dono preferiu a nuvem.
- **Instalar Unity Linux no contêiner**: exige licença ativada e download de vários GB a cada sessão
  temporária; não compensa.
- **Só testar no Unity**: deixaria o Claude sem feedback automático; descartado.

## Consequências
- Testes EditMode/PlayMode que dependem de UnityEngine e o build do APK só rodam no PC do dono
  (`Tools/run-tests.ps1`, `Tools/build-android-debug.ps1`); o dono devolve a saída.
- `ProjectSettings/` (exceto `ProjectVersion.txt`) é gerado no primeiro `setup-project.ps1` e deve ser commitado pelo dono.
- Código do Core não pode usar recursos de C# > 9 nem APIs do .NET ausentes no perfil .NET Standard 2.1 do Unity.
