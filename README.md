# Whip Cursor

Troque o cursor do Windows por uma arma. O Whip Cursor roda na bandeja do sistema, desenha a arma por cima de qualquer tela e usa o clique do mouse para atacar. Também solta bugs pela tela para você esmagar.

Feito em um único arquivo C# (`Whip.cs`), sem dependências além do .NET Framework 4 que já vem no Windows.

## Armas

| Arma | Como usar |
|---|---|
| Chicote | Clique para lançar por cima do ombro e estalar com fogo na ponta. |
| Chicote de corrente | Elos de aço com peso de espinhos e rastro de faíscas. |
| Espada | Combo de 3 golpes: corte descendente, corte ascendente e estocada. |
| Porrete com pregos | Pancada pesada de cima e tacada lateral. |
| Arco e flecha | Uma flecha de fogo por clique. Elas cruzam a tela em arco e cravam na borda de baixo. |
| AK-47 | Clique dá 1 tiro. Segure o botão para rajada (cerca de 12 tiros por segundo), com clarão, cartuchos e rastro de bala. |
| Catapulta de fogo azul | Arma o braço e lança uma bola de fogo azul em arco. Se houver um bug na linha de tiro, ela mira nele sozinha. Ao cair, deixa chamas azuis queimando no chão. |
| Spray | Segure o botão e mova o mouse para pintar na tela. A tinta some depois de uns 5 segundos. Cada apertada troca a cor. Com o botão solto, chacoalhe o mouse de um lado para o outro para sacudir a lata. |

A mira do arco, da AK-47 e da catapulta segue a última direção em que você moveu o mouse: mova na direção do alvo e clique.

## Bugs

Aperte `Ctrl+Alt+E` para aparecerem 5 bugs andando pela tela. Qualquer arma ligada esmaga um bug quando acerta nele. No spray, o bug morre depois de alguns segundos em cima da tinta. Os bugs não bloqueiam o mouse: você clica "através" deles. Aperte `Ctrl+Alt+E` de novo para removê-los, ou para chamar mais 5 depois de matar todos.

## Atalhos globais

| Atalho | Ação |
|---|---|
| `Ctrl+Alt+W` | Liga / desliga a arma |
| `Ctrl+Alt+M` | Troca de arma (se estiver desligado, liga na arma seguinte) |
| `Ctrl+Alt+Q` | Igual ao `Ctrl+Alt+M` |
| `Ctrl+Alt+E` | Liga / desliga os 5 bugs |

Em teclado ABNT2, `Ctrl+Alt` equivale ao AltGr. Por isso `AltGr+W`, `AltGr+Q` e `AltGr+E` ficam ocupados pelos atalhos. As teclas normais continuam funcionando.

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

Clique com o botão direito no ícone (clique duplo liga / desliga). O menu tem: Ativar/Desativar, Próxima arma, escolher a arma, Bugs na tela, Esconder cursor normal, Som, Iniciar com o Windows e Sair. As escolhas ficam salvas no registro do usuário.

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
- Cada bug é uma janela própria, pequena e transparente, que anda sozinha e é acertada por qualquer arma.

## Estrutura

```
Whip.cs        código-fonte completo
WhipCursor.exe executável compilado
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
