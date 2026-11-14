# Whip Cursor

Troque o cursor do Windows por uma arma. O Whip Cursor roda na bandeja do sistema, desenha a arma por cima de qualquer tela e usa o clique do mouse para atacar. Também solta bugs pela tela para você esmagar.

Feito em um único arquivo C# (`Whip.cs`), sem dependências além do .NET Framework 4 que já vem no Windows.

## Armas

| Arma | Como usar |
|---|---|
| Chicote | Clique para lançar por cima do ombro e estalar com fogo na ponta. |
| Chicote de corrente | Elos de aço com peso de espinhos e rastro de faíscas. |
| Espada | Combo de 3 golpes: corte descendente, corte ascendente e estocada. |
| Porrete com pregos | Pancada pesada de cima e tacada lateral. Deixa rachaduras na tela que somem aos poucos. |
| Arco e flecha | Uma flecha de fogo por clique. Elas cruzam a tela em arco e cravam na borda de baixo. |
| AK-47 | Clique dá 1 tiro. Segure o botão para rajada (cerca de 12 tiros por segundo). Pente de 30 tiros: recarrega sozinha ao acabar ou com a tecla `R`, com contador e barra na tela. |
| Catapulta de fogo azul | Arma o braço e lança uma bola de fogo azul em arco. Se houver um bug na linha de tiro, ela mira nele sozinha. Recarga lenta, com barra embaixo da catapulta. Ao cair, deixa chamas azuis queimando no chão e uma marca de queimado na tela. |
| Spray | Segure o botão e mova o mouse para pintar na tela. A tinta some depois de uns 5 segundos. Cada apertada troca a cor. Com o botão solto, chacoalhe o mouse de um lado para o outro para sacudir a lata. |

A mira do arco, da AK-47 e da catapulta segue a última direção em que você moveu o mouse (ou o cursor, se você ativar a mira no cursor).

## Bugs, fases e pontos

Aperte `Ctrl+Alt+E` para começar. Aparecem bugs andando pela tela e um painel no topo mostra a fase, quantos bugs faltam, os pontos, o tempo, o combo e o recorde. Qualquer arma ligada esmaga um bug quando acerta nele. Os bugs não bloqueiam o mouse: você clica "através" deles.

Ao matar todos, a fase termina, você ganha um bônus de tempo e a próxima começa sozinha, com mais bugs e mais rápidos.

| Bug | Comportamento |
|---|---|
| Normal (verde) | Anda sem rumo. 100 pontos. |
| Corredor (amarelo) | Pequeno e rápido, foge do cursor quando a arma está ligada. 150 pontos. Aparece a partir da fase 2. |
| Tanque (azul) | Grande e lento, aguenta 4 acertos (mostra uma barra de vida). 300 pontos. A partir da fase 3. |
| Explosivo (vermelho) | Explode ao morrer e leva os bugs vizinhos junto. 200 pontos. A partir da fase 4. |

Matar bugs em sequência rápida sobe o combo, que multiplica os pontos (até x5). No spray, o bug morre depois de alguns segundos em cima da tinta. Aperte `Ctrl+Alt+E` de novo para parar o jogo.

## Atalhos globais

| Atalho | Ação |
|---|---|
| `Ctrl+Alt+W` | Liga / desliga a arma |
| `Ctrl+Alt+M` | Troca de arma (se estiver desligado, liga na arma seguinte) |
| `Ctrl+Alt+Q` | Igual ao `Ctrl+Alt+M` |
| `Ctrl+Alt+E` | Liga / desliga os 5 bugs |

Em teclado ABNT2, `Ctrl+Alt` equivale ao AltGr. Por isso `AltGr+W`, `AltGr+Q` e `AltGr+E` ficam ocupados pelos atalhos. As teclas normais continuam funcionando.

## Mira no cursor

Por padrão, o arco, a AK-47 e a catapulta ficam no cursor e miram na última direção em que você moveu o mouse. No menu da bandeja, ative **Mira no cursor (arma fixa embaixo)**: a arma fica embaixo da tela, acompanha o cursor na horizontal e mira exatamente para onde o cursor está, que vira uma mira vermelha.

## Configurações

O programa cria o arquivo `WhipCursor.ini` ao lado do executável na primeira vez que roda. Nele você pode mudar:

- os atalhos (combine `Ctrl`, `Alt`, `Shift` e `Win` com uma letra, número ou F1 a F12, por exemplo `Ctrl+Shift+F9`);
- o volume, de 0 a 100;
- quantos bugs aparecem na fase 1 (3 a 8);
- a dificuldade (1 fácil, 2 normal, 3 difícil);
- o painel de pontos e a mira no cursor.

Depois de editar, use **Recarregar configurações** no menu da bandeja. O menu também tem atalhos para abrir o arquivo, mudar o volume e ligar ou desligar o painel e a mira no cursor.

## Como o mouse se comporta

- O programa inicia desativado: fica quieto na bandeja, sem desenhar nada e sem bloquear cliques.
- Com a arma ligada, os cliques do mouse servem só para usar a arma e não chegam ao programa que está por baixo.
- Segure `Shift` para clicar normalmente (saída de emergência). Nesse clique a arma não dispara.
- Com a arma desligada, o mouse volta 100% ao normal.

## Instalação

### Usando o executável

1. Baixe o `WhipCursor.exe` (ou o zip da release).
2. Abra o `WhipCursor.exe`. O ícone aparece na bandeja, perto do relógio.
3. Use `Ctrl+Alt+W` para ligar.

Se o Windows mostrar o aviso do SmartScreen, clique em "Mais informações" e depois em "Executar assim mesmo". O executável não é assinado.

### Compilando do código-fonte

Não precisa instalar nada: o compilador do .NET Framework 4 já vem no Windows.

```bat
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
%CSC% /nologo /codepage:65001 /target:winexe /optimize /out:WhipCursor.exe ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll Whip.cs
```

Ou rode o `1 - Testar.bat`, que compila e abre o programa. O código usa apenas recursos do C# 5.

### Iniciar com o Windows

Pelo menu da bandeja, marque "Iniciar com o Windows". Isso grava uma entrada no registro do usuário atual (`HKCU\...\Run`) e não precisa de administrador. Para remover, desmarque a opção.

## Menu da bandeja

Clique com o botão direito no ícone (clique duplo liga / desliga). O menu tem: Ativar/Desativar, Próxima arma, escolher a arma, Bugs na tela, Mira no cursor, Painel de pontos, Volume, Esconder cursor normal, Som, Iniciar com o Windows, Abrir e Recarregar configurações e Sair. As escolhas ficam salvas.

## Se o cursor sumir

Se o programa fechar de forma inesperada e o cursor normal não voltar, rode:

```bat
WhipCursor.exe /restaurar
```

## Como funciona

- Janelas em camadas (`UpdateLayeredWindow`), transparentes e atravessáveis pelo mouse, desenhadas com GDI+.
- Um gancho de mouse de baixo nível (`WH_MOUSE_LL`) captura os cliques enquanto a arma está ligada.
- O chicote e a corrente são simulados com física de pontos ligados por restrições de distância.
- Todos os sons são gerados por código na inicialização, então não há arquivos de áudio no projeto.
- Cada bug é uma janela própria, pequena e transparente, que anda sozinha e é acertada por qualquer arma. O painel de pontos é outra janela transparente.
- Rachaduras e marcas de queimado são desenhadas numa camada que cobre a tela e somem com o tempo.

## Estrutura

```
Whip.cs        código-fonte completo
WhipCursor.exe executável compilado
WhipCursor.ini configurações (criado na primeira execução)
LEIA-ME.txt    guia rápido em texto
*.bat          testar, instalar serviço, desinstalar, restaurar cursor
```

## Requisitos

- Windows com .NET Framework 4.x (já incluso no Windows 10 e 11)
- Não precisa de permissão de administrador

## Aviso

O programa instala um gancho global de mouse e substitui o cursor do sistema enquanto a arma está ligada. Antivírus podem desconfiar disso. O código-fonte está aberto justamente para você conferir o que ele faz.

## Licença

Distribuído sob a licença MIT
