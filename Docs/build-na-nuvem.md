# Primeira build: APK gerado na nuvem e instalado no celular

Você **não precisa instalar o Unity Editor** nem ligar o celular no PC. O GitHub gera o APK sozinho
(workflow `.github/workflows/build-android.yml`), e você baixa direto no celular.

Só uma coisa exige o PC, uma única vez: tirar o **arquivo de licença** da sua conta Unity.

---

## Passo 1: pegar o arquivo de licença (PC Windows, ~10 min, uma vez só)
1. Instale só o **Unity Hub**: <https://unity.com/download>. Não precisa instalar nenhum Editor.
2. Abra o Hub e entre com a sua conta Unity.
3. No Hub, vá em **Configurações (engrenagem) > Licenses > Add > Get a free personal license** e aceite.
4. Abra o Explorador de Arquivos e cole na barra de endereço:
   ```
   C:\ProgramData\Unity
   ```
5. Abra o arquivo **`Unity_lic.ulf`** com o Bloco de Notas, selecione tudo (Ctrl+A) e copie (Ctrl+C).

> Se você entra na Unity com Google/Apple, crie uma senha em <https://id.unity.com> (Account > Security).
> A build precisa de e-mail + senha.

## Passo 2: cadastrar 3 segredos no GitHub (~3 min, dá para fazer pelo celular)
Abra <https://github.com/carlosaraujoff1-tech/fazendinha/settings/secrets/actions> e clique **New repository secret** três vezes:

| Name | Secret |
|---|---|
| `UNITY_LICENSE` | todo o conteúdo do `Unity_lic.ulf` (o que você copiou) |
| `UNITY_EMAIL` | o e-mail da sua conta Unity |
| `UNITY_PASSWORD` | a senha da sua conta Unity |

Segredos ficam criptografados: ninguém consegue ler depois, nem o Claude.

## Passo 3: disparar a build
1. Abra <https://github.com/carlosaraujoff1-tech/fazendinha/actions/workflows/build-android.yml>.
2. Clique **Run workflow > Run workflow**.
3. A primeira demora (**30–60 min**: baixa a imagem do Unity, importa e compila IL2CPP). As próximas são mais rápidas.
   Depois disso, **todo push do Claude gera um APK novo automaticamente**.

Se der erro, avise o Claude: ele lê os logs do GitHub e corrige. É bem provável que a primeira build acuse algo,
porque o código Unity foi escrito sem um Unity para compilar.

## Passo 4: instalar no celular
1. No celular, abra <https://github.com/carlosaraujoff1-tech/fazendinha/releases/tag/apk-latest>.
2. Toque no arquivo **`FazendaMercadinho-debug.apk`**.
3. O Android vai pedir para **permitir instalar apps desta fonte** (do navegador). Permita e instale.
4. Abra **Fazenda & Mercadinho**. O jogo roda em paisagem: deite o celular.

## O que testar e contar ao Claude
- Abre? Quanto tempo leva para abrir?
- **Fazenda:** toque numa parcela vazia, plante alface, toque de novo para regar, espere 1 min e colha.
  Arrastar com um dedo move a câmera; a pinça dá zoom.
- **Mercadinho** (aba embaixo):
  - toque numa banca e reponha alface;
  - espere os clientes (cápsulas coloridas) e toque em **Passar item** quando houver fila;
  - abra **Fornecedor** e compre leite/arroz.
- **Save:** feche o app pelo multitarefa, abra de novo e confira se o progresso continua.
- **Offline:** feche por alguns minutos e reabra. Deve aparecer "Bem-vindo de volta".
- Travadas, quedas, textos cortados ou fora da tela (diga o modelo do celular).

## Avisos
- O repositório está **público**: qualquer pessoa pode ver o código e baixar o APK. Para deixar privado:
  Settings > General > Danger Zone > Change visibility. No privado, o GitHub dá 2.000 min/mês grátis de build
  (dá para cerca de 30–50 builds).
- Este APK é de **depuração** (não vai para a Play Store). Assinatura e AAB ficam para a Etapa 7.
