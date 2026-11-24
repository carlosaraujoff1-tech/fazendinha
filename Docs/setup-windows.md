# Alternativa: Unity no PC (Windows) e celular via USB

> **Caminho recomendado agora:** `Docs/build-na-nuvem.md` (sem instalar o Editor). Este guia é para quando
> você quiser abrir o projeto no Unity do PC.

## 1. Programas (uma vez só)
1. **Git for Windows**: <https://git-scm.com/download/win>. Já vem com o Git LFS. Depois, no PowerShell:
   ```powershell
   git lfs install
   ```
2. **Unity Hub**: <https://unity.com/download>. Entre com sua conta Unity (a licença Personal é grátis).
3. No Unity Hub, em **Installs > Install Editor**, escolha **Unity 6.3 LTS** (versão `6000.3.x`) e marque os módulos:
   - ✅ **Android Build Support**
     - ✅ **OpenJDK**
     - ✅ **Android SDK & NDK Tools**
   - (Opcional) Microsoft Visual Studio Community, para editar código.
4. (Opcional) **.NET SDK 8**: <https://dotnet.microsoft.com/download>. Com ele, o `run-tests.ps1` também roda
   os testes do Core sem abrir o Unity.

## 2. Baixar o projeto
```powershell
cd C:\
git clone https://github.com/carlosaraujoff1-tech/fazendinha.git
cd fazendinha
```

## 3. Abrir uma vez no Unity
1. No Unity Hub, clique em **Add > Add project from disk** e escolha `C:\fazendinha`.
2. Se o Hub avisar que a versão é diferente (o projeto pede `6000.3.0f1`), escolha **a sua 6000.3.x** e confirme.
3. Espere importar. Na primeira vez demora alguns minutos e baixa os pacotes.
4. Se aparecer **"Enable new Input System backends?" / reiniciar**, aceite.
5. No menu, clique em **Fazenda > Configurar projeto (Etapa 0)** (gera também a cena `Main`).
6. Feche o Unity. Os scripts abaixo rodam o Unity em segundo plano e precisam do Editor fechado.

## 4. Rodar os testes e configurar por linha de comando
No PowerShell, dentro de `C:\fazendinha`:
```powershell
Set-ExecutionPolicy -Scope CurrentUser RemoteSigned   # uma vez só, para permitir scripts locais
.\Tools\setup-project.ps1
.\Tools\run-tests.ps1
```
O resultado esperado termina com **TODOS OS TESTES PASSARAM**. Se falhar, me mande a saída e os
arquivos `TestResults\EditMode.log` / `PlayMode.log`.

## 5. Preparar o celular (Depuração USB)
1. Abra **Configurações > Sobre o telefone** e toque **7 vezes** em **Número da versão** (em Xiaomi: "Versão MIUI/HyperOS";
   em Samsung: Informações do software > Número de compilação). Vai aparecer "Você agora é um desenvolvedor".
2. Abra **Configurações > Sistema > Opções do desenvolvedor** e ligue **Depuração USB**.
   Em Xiaomi, ligue também **Instalar via USB**.
3. Ligue o celular no PC com um cabo **de dados**, não só de carga.
4. No celular, aceite **"Permitir depuração USB?"** e marque "Sempre permitir neste computador".

## 6. Gerar e instalar o APK
```powershell
.\Tools\build-android-debug.ps1 -Install
```
A primeira build IL2CPP demora de 10 a 20 minutos. No fim, o app **Fazenda & Mercadinho** abre no
celular, em paisagem, na fazenda (gray box).

## 7. Depois de tudo funcionar, commitar o que o Unity gerou
```powershell
git add -A
git commit -m "Etapa 0: ProjectSettings e cena Boot gerados pelo Unity"
git push
```
Isso inclui `ProjectSettings/`, `Packages/packages-lock.json`, o URP asset e a cena Boot. O Claude precisa disso
para as próximas etapas.

## O que me devolver
- A saída do `run-tests.ps1`.
- Se o APK instalou e abriu, e o modelo do celular.
- A versão exata do Unity instalada (aparece no Hub, ex.: `6000.3.4f1`).
