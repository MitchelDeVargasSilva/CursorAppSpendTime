# Whip Cursor

Troque o cursor do Windows por uma arma. O Whip Cursor roda na bandeja do sistema, desenha a arma por cima de qualquer tela e usa o clique do mouse para atacar. Também solta bugs pela tela para você esmagar, com fases, chefe, modos de jogo, vidas, itens e conquistas.

Escrito em C# (WinForms), sem dependências além do .NET Framework 4 que já vem no Windows.

## Armas

| Arma | Como usar | Dano |
|---|---|---|
| Chicote | Clique para lançar por cima do ombro e estalar com fogo na ponta. | 2 |
| Chicote de corrente | Elos de aço com peso de espinhos e rastro de faíscas. | 1 na ponta, 2 na pancada |
| Espada | Combo de 3 golpes. Cada corte deixa um talho na tela. | 2 |
| Porrete com pregos | Pancada pesada e tacada lateral. Deixa rachaduras na tela. | 3 |
| Arco e flecha | Uma flecha de fogo por clique. | 2 |
| AK-47 | Clique dá 1 tiro, segurar dá rajada. Pente de 30, recarrega sozinha ou com `R`. | 1 |
| Catapulta de fogo azul | Lança uma bola azul em arco, com recarga lenta. Deixa chamas e queimado. | 4 em área |
| Spray | Segure e mova para pintar. Chacoalhe o mouse com o botão solto para sacudir a lata. Bugs sobre a tinta morrem aos poucos. | veneno |
| Lança-chamas | Segure o botão. O combustível dura cerca de 4 s e recarrega ao soltar. Se zerar, superaquece. | 1 por chama |
| Granada | Clique lança, ela cai, pisca e explode. | 3 em área |

A mira do arco, da AK-47, da catapulta, do lança-chamas e da granada segue a última direção em que você moveu o mouse, ou o cursor se você ativar a mira no cursor.

## O jogo dos bugs

Aperte `Ctrl+Alt+E` para começar. Um painel no topo mostra fase, bugs, pontos, tempo, combo, vidas e recorde. Os bugs não bloqueiam o mouse.

| Bug | Comportamento | Pontos |
|---|---|---|
| Normal (verde) | Anda sem rumo. | 100 |
| Corredor (amarelo) | Pequeno e rápido, foge do cursor. A partir da fase 2. | 150 |
| Tanque (azul) | Grande, aguenta 4 de dano. A partir da fase 3. | 300 |
| Explosivo (vermelho) | Explode ao morrer e leva os vizinhos. A partir da fase 4. | 200 |
| Chefe (roxo, com coroa) | A cada 5 fases. Muita vida e chama bugs pequenos de tempos em tempos. | 2000 |

**Modos** (escolha em Configurações): fases com chefe, contra o relógio (60 s, cada bug dá +1 s) e infinito. Cada modo tem recorde próprio.

**Vidas:** com a arma ligada, um bug encostando no cursor tira uma vida. Com 0 vidas no menu elas ficam infinitas.

**Itens:** bugs podem soltar itens. Passe o cursor por cima para pegar: *Munição* (recarrega AK, lança-chamas e granada), *Gelo* (congela os bugs por 5 s) e *Fúria* (pontos em dobro por 8 s).

**Combo:** matar em sequência rápida multiplica os pontos, até x5. Explosões grandes dão uma câmera lenta curta nos bugs.

**Conquistas** (menu da bandeja): primeiro bug, combo x5, fase 5, derrotar um chefe, 100 bugs e fase sem levar mordida.

## Atalhos

| Atalho | Ação |
|---|---|
| `Ctrl+Alt+W` | Liga / desliga a arma |
| `Ctrl+Alt+M` ou `Ctrl+Alt+Q` | Próxima arma |
| `Ctrl+Alt+1` a `9` e `0` | Escolhe a arma direto (1 chicote ... 0 granada) |
| `Ctrl+Alt+E` | Liga / desliga o jogo dos bugs |
| Roda do mouse | Troca de arma com a arma ligada. `Shift` rola normalmente. |

Todos os atalhos mudam em **Configurações...** no menu da bandeja. Em teclado ABNT2, `Ctrl+Alt` equivale ao AltGr.

## Configurações

O menu da bandeja abre uma janela com: perfil (Casual, Normal, Difícil), modo de jogo, dificuldade, bugs iniciais, vidas, volume, tamanho da AK, lança-chamas e granada, tema de cores (Padrão, Gelo, Tóxico, Neon), idioma (Português ou English), painel, mira no cursor, roda do mouse e todos os atalhos. Também é possível copiar os recordes para a área de transferência.

O programa guarda isso em `WhipCursor.ini`, ao lado do executável. Você pode editar o arquivo à mão e usar **Recarregar configurações**.

## Personalização

- **Sons:** coloque arquivos `.wav` na pasta `Sons` com o nome do som (`ak.wav`, `boom.wav`, `flame.wav`...). A lista está em `Sons/LEIA-ME.txt`. Sem arquivo, o programa usa os sons gerados por código. Vários sons tocam ao mesmo tempo.
- **Bug:** coloque um `bug.png` com fundo transparente ao lado do executável para usar sua própria imagem no lugar do bug desenhado.

## Instalação

### Usando o executável

1. Baixe o zip da release e extraia.
2. Abra o `WhipCursor.exe`. O ícone aparece na bandeja, perto do relógio.
3. Use `Ctrl+Alt+W` para ligar.

O executável não é assinado digitalmente. O SmartScreen pode avisar: clique em "Mais informações" e depois em "Executar assim mesmo". Antivírus podem desconfiar do gancho global de mouse. O código está aberto para você conferir.

### Compilando

Não precisa instalar nada: o compilador do .NET Framework 4 já vem no Windows. Rode o `1 - Testar (recompila).bat`, que compila tudo de `src` e abre o programa. O código usa apenas recursos do C# 5.

Para rodar os testes da lógica do jogo, use o `2 - Rodar testes.bat`.

### GitHub Actions

O arquivo `.github/workflows/build.yml` roda os testes, compila o executável e guarda o zip como artefato a cada push. Ao criar uma tag que começa com `v` (por exemplo `v8`), ele também publica uma release com o `WhipCursor.zip` e o `WhipCursor.exe`.

### Iniciar com o Windows

Marque "Iniciar com o Windows" no menu da bandeja. Isso grava uma entrada em `HKCU\...\Run`, sem precisar de administrador.

## Como o mouse se comporta

- O programa inicia desativado e não bloqueia nada.
- Com a arma ligada, os cliques servem só para usar a arma. A roda do mouse troca de arma.
- Segure `Shift` para clicar e rolar normalmente.
- Com a arma desligada, o mouse volta 100% ao normal.

Se o cursor sumir depois de uma falha, rode `WhipCursor.exe /restaurar`.

## Como funciona

- Janelas em camadas (`UpdateLayeredWindow`), transparentes e atravessáveis pelo mouse, desenhadas com GDI+.
- Um gancho de mouse de baixo nível (`WH_MOUSE_LL`) captura cliques e a roda.
- Os sons são gerados por código e tocados por um mixer próprio sobre o `waveOut`, com `SoundPlayer` como reserva.
- Cada bug e cada item é uma janela própria; o painel e as marcas na tela são outras janelas transparentes.

## Estrutura

```
src/
  Logic.cs        regras do jogo sem Windows Forms (testável)
  Tests.cs        testes da lógica
  Game.cs         fases, chefe, modos, vidas, itens, conquistas
  Controls.cs     janela de configurações, roda, atalhos, temas, idioma
  Config.cs       WhipCursor.ini e registro
  Audio.cs        mixer e leitura de .wav
  Sounds.cs       sons gerados por código
  Weapon.*.cs     uma arma por arquivo (Extra: lança-chamas e granada)
  Render.cs, Aim.cs, Decals.cs, Input.cs   desenho, mira, marcas e mouse
  Bug.cs, Item.cs, Hud.cs                  janelas do jogo
Sons/             sons e bug.png personalizados (opcional)
.github/workflows/build.yml
```

## Limitações conhecidas

- O executável não é assinado e não há placar online; os recordes ficam no registro do Windows do seu usuário.
- A escala de tamanho vale para AK-47, lança-chamas e granada; as outras armas têm tamanho fixo.
- O tema de cores muda chamas, tiros, explosão e painel, não todas as armas.
- Com vários monitores o programa usa a DPI do sistema (`SetProcessDPIAware`); telas com escalas diferentes podem desalinhar a mira.

## Requisitos

- Windows com .NET Framework 4.x (já incluso no Windows 10 e 11)
- Não precisa de permissão de administrador

## Licença

Defina a licença do projeto antes de publicar (por exemplo, MIT) e adicione um arquivo `LICENSE`.
