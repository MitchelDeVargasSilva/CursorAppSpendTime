using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

public partial class Whip
{
    // ===================================================================
    //  CONFIGURAÇÃO (WhipCursor.ini, ao lado do executável)
    // ===================================================================
    string cfgPath = "";
    string kToggleS = "Ctrl+Alt+W", kCycleS = "Ctrl+Alt+M", kCycle2S = "Ctrl+Alt+Q", kBugsS = "Ctrl+Alt+E";
    uint   kToggleM = MOD_CONTROL | MOD_ALT, kToggleV = VK_W;
    uint   kCycleM  = MOD_CONTROL | MOD_ALT, kCycleV  = VK_M;
    uint   kCycle2M = MOD_CONTROL | MOD_ALT, kCycle2V = VK_Q;
    uint   kBugsM   = MOD_CONTROL | MOD_ALT, kBugsV   = VK_E;
    float  cfgScale = 1f;
    int    cfgVolume = 100, cfgBugs = 5, cfgDiff = 2;
    bool   cfgHud = true, aimCursor = false;
    ToolStripMenuItem itCycle, itAim, itHud, itVol;

    static bool ParseHotkey(string s, out uint mod, out uint vk) { return GameLogic.ParseHotkey(s, out mod, out vk); }

    static void SetKey(string v, ref string s, ref uint m, ref uint vk)
    {
        uint nm, nv;
        if (ParseHotkey(v, out nm, out nv)) { s = v; m = nm; vk = nv; }
    }

    static int ParseInt(string v, int def)
    {
        int n;
        return int.TryParse(v, out n) ? n : def;
    }

    static int ClampInt(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }

    void LoadConfig()
    {
        try { cfgPath = Path.Combine(Application.StartupPath, "WhipCursor.ini"); } catch { cfgPath = ""; }
        try { if (cfgPath.Length > 0 && !File.Exists(cfgPath)) SaveConfig(); } catch { }
        try
        {
            if (cfgPath.Length == 0 || !File.Exists(cfgPath)) return;
            foreach (string raw in File.ReadAllLines(cfgPath, System.Text.Encoding.UTF8))
            {
                string ln = raw.Trim();
                if (ln.Length == 0 || ln[0] == ';' || ln[0] == '#' || ln[0] == '[') continue;
                int eq = ln.IndexOf('=');
                if (eq <= 0) continue;
                string k = ln.Substring(0, eq).Trim().ToLowerInvariant();
                string v = ln.Substring(eq + 1).Trim();
                if      (k == "ligar")        SetKey(v, ref kToggleS, ref kToggleM, ref kToggleV);
                else if (k == "trocararma")   SetKey(v, ref kCycleS,  ref kCycleM,  ref kCycleV);
                else if (k == "trocararma2")  SetKey(v, ref kCycle2S, ref kCycle2M, ref kCycle2V);
                else if (k == "bugs")         SetKey(v, ref kBugsS,   ref kBugsM,   ref kBugsV);
                else if (k == "volume")       cfgVolume = ClampInt(ParseInt(v, 100), 0, 100);
                else if (k == "bugsiniciais") cfgBugs = ClampInt(ParseInt(v, 5), 3, 8);
                else if (k == "dificuldade")  cfgDiff = ClampInt(ParseInt(v, 2), 1, 3);
                else if (k == "painel")       cfgHud = ParseInt(v, 1) != 0;
                else if (k == "miranocursor") aimCursor = ParseInt(v, 0) != 0;
                else if (k == "modo")         cfgMode = ClampInt(ParseInt(v, 0), 0, 2);
                else if (k == "vidas")        cfgLives = ClampInt(ParseInt(v, 5), 0, 9);
                else if (k == "escala")       cfgScale = ClampInt(ParseInt(v, 100), 60, 160) / 100f;
                else if (k == "tema")         cfgTheme = ClampInt(ParseInt(v, 0), 0, 3);
                else if (k == "idioma")       cfgEn = v.ToLowerInvariant().StartsWith("en");
                else if (k == "roda")         cfgWheel = ParseInt(v, 1) != 0;
                else if (k == "armadireta")   { uint dm, dv; if (ParseHotkey(v + "+1", out dm, out dv)) kDirectS = v; }
            }
        }
        catch { }
        Vol = cfgVolume / 100.0;
    }

    void SaveConfig()
    {
        if (cfgPath == null || cfgPath.Length == 0) return;
        try
        {
            string t =
"; Whip Cursor - configuracoes\r\n" +
"; Edite e use \"Recarregar configuracoes\" no menu da bandeja (ou reinicie o programa).\r\n" +
"; Atalhos: combine Ctrl, Alt, Shift e Win com uma letra, numero ou F1-F12. Exemplo: Ctrl+Shift+F9\r\n\r\n" +
"[Atalhos]\r\n" +
"Ligar=" + kToggleS + "\r\n" +
"TrocarArma=" + kCycleS + "\r\n" +
"TrocarArma2=" + kCycle2S + "\r\n" +
"Bugs=" + kBugsS + "\r\n\r\n" +
"[Opcoes]\r\n" +
"; Volume de 0 a 100\r\n" +
"Volume=" + cfgVolume + "\r\n" +
"; Quantidade de bugs na fase 1 (3 a 8)\r\n" +
"BugsIniciais=" + cfgBugs + "\r\n" +
"; Dificuldade: 1 facil, 2 normal, 3 dificil\r\n" +
"Dificuldade=" + cfgDiff + "\r\n" +
"; Painel de pontos na tela: 1 liga, 0 desliga\r\n" +
"Painel=" + (cfgHud ? 1 : 0) + "\r\n" +
"; Mira no cursor: arco, AK-47 e catapulta ficam fixos embaixo da tela e miram onde esta o cursor\r\n" +
"MiraNoCursor=" + (aimCursor ? 1 : 0) + "\r\n" +
"; Modo: 0 fases (chefe a cada 5), 1 contra o relogio, 2 infinito\r\n" +
"Modo=" + cfgMode + "\r\n" +
"; Vidas: 0 = infinitas (bug encostando no cursor tira uma vida)\r\n" +
"Vidas=" + cfgLives + "\r\n" +
"; Tamanho da AK, lanca-chamas e granada em % (60 a 160)\r\n" +
"Escala=" + (int)(cfgScale * 100f + 0.5f) + "\r\n" +
"; Tema de cores: 0 padrao, 1 gelo, 2 toxico, 3 neon\r\n" +
"Tema=" + cfgTheme + "\r\n" +
"; Idioma: pt ou en\r\n" +
"Idioma=" + (cfgEn ? "en" : "pt") + "\r\n" +
"; Roda do mouse troca de arma enquanto a arma esta ligada (Shift rola normal): 1 liga, 0 desliga\r\n" +
"Roda=" + (cfgWheel ? 1 : 0) + "\r\n" +
"; Arma direta: prefixo + 1..9 e 0 escolhem a arma (1 chicote ... 0 granada)\r\n" +
"ArmaDireta=" + kDirectS + "\r\n";
            File.WriteAllText(cfgPath, t, new System.Text.UTF8Encoding(true));
        }
        catch { }
    }

    void RegisterHotkeys(bool first)
    {
        for (int i = 0; i < 4; i++) { try { UnregisterHotKey(Handle, HOTKEY_ID + i); } catch { } }
        RegisterDirectHotkeys();
        string fail = "";
        if (!RegisterHotKey(Handle, HOTKEY_ID,     kToggleM, kToggleV)) fail += " " + kToggleS;
        if (!RegisterHotKey(Handle, HOTKEY_ID + 1, kCycleM,  kCycleV))  fail += " " + kCycleS;
        if (!RegisterHotKey(Handle, HOTKEY_ID + 2, kCycle2M, kCycle2V)) fail += " " + kCycle2S;
        if (!RegisterHotKey(Handle, HOTKEY_ID + 3, kBugsM,   kBugsV))   fail += " " + kBugsS;
        if (fail.Length > 0)
            tray.ShowBalloonTip(5000, "Whip Cursor", "Atalho(s) já em uso por outro programa:" + fail + ". Use o menu da bandeja ou mude no WhipCursor.ini.", ToolTipIcon.Warning);
        else if (first && !bootLaunch)
            tray.ShowBalloonTip(4000, "Whip Cursor", "Pronto! " + kToggleS + " liga/desliga. " + kCycleS + " troca de arma. " + kBugsS + " liga/desliga os bugs.", ToolTipIcon.Info);
    }

    void ReloadConfig()
    {
        LoadConfig();
        ApplyTheme();
        BuildSounds();
        RegisterHotkeys(false);
        if (itAim != null) itAim.Checked = aimCursor;
        if (itHud != null) itHud.Checked = cfgHud;
        MarkVolume();
        started = false;
        RelabelMenu();
        UpdateTray();
        UpdateHud();
    }

    void MarkVolume()
    {
        if (itVol == null) return;
        foreach (ToolStripItem it in itVol.DropDownItems)
        {
            ToolStripMenuItem mi = it as ToolStripMenuItem;
            if (mi != null) mi.Checked = (mi.Text == cfgVolume + "%");
        }
    }

    void BuildSounds()
    {
        Mixer.Init(); Mixer.Master = Vol;
        if (hissPlayer != null) { try { hissPlayer.Stop(); } catch { } hissOn = false; }
        crackPlayer = Snd.Make("crack", MakeCrackWav());
        whooshPlayer = Snd.Make("whoosh", MakeWhooshWav());
        thudPlayer = Snd.Make("thud", MakeThudWav());
        slashPlayer = Snd.Make("slash", MakeSlashWav());
        clangPlayer = Snd.Make("clang", MakeClangWav());
        clankPlayer = Snd.Make("clank", MakeClankWav());
        shootPlayer = Snd.Make("shoot", MakeShootWav());
        stickPlayer = Snd.Make("stick", MakeStickWav());
        akPlayer = Snd.Make("ak", MakeAkWav());
        catPlayer = Snd.Make("cat", MakeCatapultWav());
        boomPlayer = Snd.Make("boom", MakeBoomWav());
        hissPlayer = Snd.Make("hiss", MakeHissWav());
        squashPlayer = Snd.Make("squash", MakeSquashWav());
        rattlePlayer = Snd.Make("rattle", MakeRattleWav());
        reloadPlayer = Snd.Make("reload", MakeReloadWav());
        beepPlayer = Snd.Make("beep", MakeBeepWav());
        StopFlameSound();
        flamePlayer = Snd.Make("flame", MakeFlameWav());
        try
        {
            crackPlayer.Load(); whooshPlayer.Load(); thudPlayer.Load();
            slashPlayer.Load(); clangPlayer.Load();
            clankPlayer.Load(); shootPlayer.Load(); stickPlayer.Load();
            akPlayer.Load(); catPlayer.Load(); boomPlayer.Load(); hissPlayer.Load(); squashPlayer.Load(); rattlePlayer.Load();
            reloadPlayer.Load(); beepPlayer.Load();
        }
        catch { }
    }

}
