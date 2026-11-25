# Terceira pessoa realista (Rodada 1)

## Contexto
O dono achou a visão de cima "infantil" e pediu um jogo em terceira pessoa, estilo GTA, com uma personagem
mulher andando pela plantação, fazenda e mercadinho no mesmo mapa ligados por estrada, e interação por botão
de ação (preparado para uma futura versão "física": carregar caixas, passar item por item). Só assets gratuitos.

## Decisão
- Câmera orbital em terceira pessoa (`ThirdPersonCamera`) com anticolisão; joystick virtual à esquerda,
  arrastar à direita para girar, pinça para zoom (`VirtualJoystick`, `LookArea`).
- `PlayerController` (CharacterController) e `InteractionSystem`: o objeto interagível mais próximo/à frente
  define o texto do botão de ação. Cada visual (canteiro, animal, prateleira, caixa, vendinha, caminhão) é um
  `InteractableBehaviour` — o Core não mudou (as ações continuam sendo comandos).
- `PlayerContext` já carrega `HeldItemId/HeldUnits` para a futura interação física.
- Mundo em escala real gerado por `SceneGenerator`: cerca de madeira, terra batida, estrada de cascalho,
  mercadinho de alvenaria com vitrines e porta na frente, céu HDRI (Poly Haven), texturas PBR (ambientCG/Poly Haven),
  props Poly Haven (CC0). Personagem provisória: Kenney (CC0) até chegarem os modelos Mixamo.
- Removidos `TouchCameraController` e `AreaSwitcher` (visão de cima). Botão "Viajar" teleporta fazenda ⇄ mercadinho.

## Alternativas
- Manter a visão de cima com mais detalhes: rejeitado pelo dono.
- Navegação por toque no chão (point-and-click): menos parecida com o pedido (GTA).
