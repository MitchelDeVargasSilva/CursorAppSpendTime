using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

// Janela de configurações, roda do mouse, atalhos por arma, perfis, temas e idioma.
public partial class Whip
{
    public const int WEAPON_COUNT = 10;
    int    cfgTheme = 0;                 // 0 padrão, 1 gelo, 2 tóxico, 3 neon
    bool   cfgWheel = true;
    string kDirectS = "Ctrl+Alt";        // prefixo das teclas 1..9,0 -> armas
    uint   kDirectM = MOD_CONTROL | MOD_ALT;
    volatile int wheelPend = 0;
    ToolStripMenuItem itSettings, itRecord, itAch;

    // arma por posição na lista: 1..9 e 0
    static uint DirectVk(int i) { return (uint)(i == 9 ? '0' : '1' + i); }

    void RegisterDirectHotkeys()
    {
        for (int i = 0; i < WEAPON_COUNT; i++) { try { UnregisterHotKey(Handle, HOTKEY_ID + 10 + i); } catch { } }
        uint m, v;
        if (!ParseHotkey(kDirectS + "+1", out m, out v)) return;
        kDirectM = m;
        for (int i = 0; i < WEAPON_COUNT; i++)
        {
            try { RegisterHotKey(Handle, HOTKEY_ID + 10 + i, kDirectM, DirectVk(i)); } catch { }
        }
    }

    void DirectWeapon(int i)
    {
        if (i < 0 || i >= WEAPON_COUNT) return;
        if (!enabled) SetEnabled(true);
        SelectWeapon((WeaponMode)i);
    }

    // d > 0 = próxima arma, d < 0 = anterior
    void CycleBy(int d)
    {
        int n = ((int)mode + (d > 0 ? 1 : WEAPON_COUNT - 1)) % WEAPON_COUNT;
        SelectWeapon((WeaponMode)n);
    }

    void ConsumeWheel()
    {
        int w = wheelPend;
        if (w == 0) return;
        wheelPend = 0;
        if (enabled) CycleBy(w > 0 ? -1 : 1);
    }

    // ---------- temas: gira os canais de cor das chamas, tiros e do painel ----------
    Color Tc(Color c)
    {
        if (cfgTheme == 1) return Color.FromArgb(c.A, c.B, c.G, c.R);
        if (cfgTheme == 2) return Color.FromArgb(c.A, c.G, c.R, c.B);
        if (cfgTheme == 3) return Color.FromArgb(c.A, c.G, c.B, c.R);
        return c;
    }

    void ApplyTheme()
    {
        HudForm.Accent = Tc(Color.FromArgb(255, 255, 214, 70));
    }

    string ThemeName(int t)
    {
        if (t == 1) return L("Gelo", "Ice");
        if (t == 2) return L("Tóxico", "Toxic");
        if (t == 3) return L("Neon", "Neon");
        return L("Padrão", "Default");
    }

    // ---------- perfis ----------
    void ApplyProfile(string name)
    {
        int b, d, l;
        GameLogic.Profile(name, out b, out d, out l);
        cfgBugs = b; cfgDiff = d; cfgLives = l;
    }

    // ---------- recorde / conquistas ----------
    string RecordText()
    {
        return "Whip Cursor - " + L("recordes", "records") + ": " +
               L("Fases ", "Phases ") + bests[0] + " | " + L("Relógio ", "Time attack ") + bests[1] +
               " | " + L("Infinito ", "Endless ") + bests[2] + " | " + L("bugs mortos ", "bugs killed ") + totalKills;
    }

    void CopyRecord()
    {
        try { Clipboard.SetText(RecordText()); tray.ShowBalloonTip(3000, "Whip Cursor", L("Recorde copiado para a área de transferência.", "Record copied to the clipboard."), ToolTipIcon.Info); }
        catch { }
    }

    void ShowAchievements()
    {
        string t = "";
        for (int i = 0; i < 6; i++)
        {
            bool on = (ach & (1 << i)) != 0;
            t += (on ? "[x] " : "[  ] ") + L(GameLogic.AchNames[i], GameLogic.AchNamesEn[i]) + "\r\n";
        }
        MessageBox.Show(t, L("Conquistas", "Achievements"));
    }

    // ---------- textos do menu (idioma) ----------
    void RelabelMenu()
    {
        if (itWhip  != null) itWhip.Text  = ModeName(WeaponMode.Whip);
        if (itChain != null) itChain.Text = ModeName(WeaponMode.Chain);
        if (itSword != null) itSword.Text = ModeName(WeaponMode.Sword);
        if (itMace  != null) itMace.Text  = ModeName(WeaponMode.Mace);
        if (itBow   != null) itBow.Text   = ModeName(WeaponMode.Bow);
        if (itAk    != null) itAk.Text    = ModeName(WeaponMode.Ak);
        if (itCat   != null) itCat.Text   = ModeName(WeaponMode.Catapult);
        if (itSpray != null) itSpray.Text = ModeName(WeaponMode.Spray);
        if (itFlame != null) itFlame.Text = ModeName(WeaponMode.Flame);
        if (itGren  != null) itGren.Text  = ModeName(WeaponMode.Grenade);
        if (itAim   != null) itAim.Text   = L("Mira no cursor (arma fixa embaixo)", "Aim at cursor (weapon fixed at bottom)");
        if (itHud   != null) itHud.Text   = L("Painel de pontos", "Score panel");
        if (itVol   != null) itVol.Text   = L("Volume", "Volume");
        if (itHide  != null) itHide.Text  = L("Esconder cursor normal", "Hide normal cursor");
        if (itSnd   != null) itSnd.Text   = L("Som", "Sound");
        if (itAuto  != null) itAuto.Text  = L("Iniciar com o Windows", "Start with Windows");
        if (itCfg   != null) itCfg.Text   = L("Abrir arquivo de configuração (WhipCursor.ini)", "Open config file (WhipCursor.ini)");
        if (itReloadCfg != null) itReloadCfg.Text = L("Recarregar configurações", "Reload settings");
        if (itSettings != null) itSettings.Text = L("Configurações...", "Settings...");
        if (itRecord != null) itRecord.Text = L("Copiar recordes", "Copy records");
        if (itAch != null) itAch.Text = L("Conquistas", "Achievements");
        if (itExit2 != null) itExit2.Text = L("Sair", "Exit");
    }
    ToolStripMenuItem itExit2;

    // ===================================================================
    //  JANELA DE CONFIGURAÇÕES
    // ===================================================================
    Form settingsForm;

    void ShowSettings()
    {
        if (settingsForm != null && !settingsForm.IsDisposed) { settingsForm.Activate(); return; }
        Form f = new Form();
        settingsForm = f;
        f.Text = L("Whip Cursor - Configurações", "Whip Cursor - Settings");
        f.FormBorderStyle = FormBorderStyle.FixedDialog;
        f.MaximizeBox = false; f.MinimizeBox = false;
        f.StartPosition = FormStartPosition.CenterScreen;
        f.ClientSize = new Size(560, 560);
        f.Font = new Font("Segoe UI", 9f);
        f.TopMost = true;

        int y = 14;
        Action<string, Control, int> row = delegate(string label, Control c, int w)
        {
            Label l = new Label(); l.Text = label; l.Left = 16; l.Top = y + 3; l.Width = 210; f.Controls.Add(l);
            c.Left = 232; c.Top = y; c.Width = w; f.Controls.Add(c);
            y += 30;
        };

        ComboBox cbProfile = new ComboBox(); cbProfile.DropDownStyle = ComboBoxStyle.DropDownList;
        cbProfile.Items.AddRange(new object[] { L("Casual", "Casual"), L("Normal", "Normal"), L("Difícil", "Hard"), L("Personalizado", "Custom") });
        cbProfile.SelectedIndex = 3;
        row(L("Perfil", "Profile"), cbProfile, 150);

        ComboBox cbMode = new ComboBox(); cbMode.DropDownStyle = ComboBoxStyle.DropDownList;
        cbMode.Items.AddRange(new object[] { L("Fases (com chefe)", "Phases (with boss)"), L("Contra o relógio", "Time attack"), L("Infinito", "Endless") });
        cbMode.SelectedIndex = GameLogic.ClampInt(cfgMode, 0, 2);
        row(L("Modo de jogo", "Game mode"), cbMode, 190);

        ComboBox cbDiff = new ComboBox(); cbDiff.DropDownStyle = ComboBoxStyle.DropDownList;
        cbDiff.Items.AddRange(new object[] { L("Fácil", "Easy"), L("Normal", "Normal"), L("Difícil", "Hard") });
        cbDiff.SelectedIndex = GameLogic.ClampInt(cfgDiff, 1, 3) - 1;
        row(L("Dificuldade", "Difficulty"), cbDiff, 150);

        NumericUpDown nBugs = new NumericUpDown(); nBugs.Minimum = 3; nBugs.Maximum = 8; nBugs.Value = GameLogic.ClampInt(cfgBugs, 3, 8);
        row(L("Bugs na fase 1", "Bugs in phase 1"), nBugs, 60);

        NumericUpDown nLives = new NumericUpDown(); nLives.Minimum = 0; nLives.Maximum = 9; nLives.Value = GameLogic.ClampInt(cfgLives, 0, 9);
        row(L("Vidas (0 = infinitas)", "Lives (0 = unlimited)"), nLives, 60);

        TrackBar tVol = new TrackBar(); tVol.Minimum = 0; tVol.Maximum = 100; tVol.TickFrequency = 25; tVol.Value = GameLogic.ClampInt(cfgVolume, 0, 100); tVol.Height = 30;
        row(L("Volume", "Volume"), tVol, 190);

        TrackBar tScale = new TrackBar(); tScale.Minimum = 60; tScale.Maximum = 160; tScale.TickFrequency = 20;
        tScale.Value = GameLogic.ClampInt((int)(cfgScale * 100f + 0.5f), 60, 160); tScale.Height = 30;
        row(L("Tamanho da AK/lança-chamas/granada (%)", "AK/flamethrower/grenade size (%)"), tScale, 190);

        ComboBox cbTheme = new ComboBox(); cbTheme.DropDownStyle = ComboBoxStyle.DropDownList;
        for (int i = 0; i < 4; i++) cbTheme.Items.Add(ThemeName(i));
        cbTheme.SelectedIndex = GameLogic.ClampInt(cfgTheme, 0, 3);
        row(L("Tema de cores", "Color theme"), cbTheme, 150);

        ComboBox cbLang = new ComboBox(); cbLang.DropDownStyle = ComboBoxStyle.DropDownList;
        cbLang.Items.AddRange(new object[] { "Português", "English" });
        cbLang.SelectedIndex = cfgEn ? 1 : 0;
        row(L("Idioma", "Language"), cbLang, 150);

        CheckBox ckHud = new CheckBox(); ckHud.Checked = cfgHud; ckHud.Text = L("Painel de pontos", "Score panel"); ckHud.Left = 232; ckHud.Top = y; ckHud.Width = 300; f.Controls.Add(ckHud); y += 24;
        CheckBox ckAim = new CheckBox(); ckAim.Checked = aimCursor; ckAim.Text = L("Mira no cursor (arma fixa embaixo)", "Aim at cursor (weapon fixed at bottom)"); ckAim.Left = 232; ckAim.Top = y; ckAim.Width = 300; f.Controls.Add(ckAim); y += 24;
        CheckBox ckWheel = new CheckBox(); ckWheel.Checked = cfgWheel; ckWheel.Text = L("Roda do mouse troca de arma (Shift rola normal)", "Mouse wheel switches weapon (Shift scrolls)"); ckWheel.Left = 232; ckWheel.Top = y; ckWheel.Width = 310; f.Controls.Add(ckWheel); y += 30;

        TextBox tbToggle = new TextBox(); tbToggle.Text = kToggleS; row(L("Atalho: ligar/desligar", "Hotkey: on/off"), tbToggle, 150);
        TextBox tbCycle = new TextBox(); tbCycle.Text = kCycleS; row(L("Atalho: trocar de arma", "Hotkey: next weapon"), tbCycle, 150);
        TextBox tbCycle2 = new TextBox(); tbCycle2.Text = kCycle2S; row(L("Atalho: trocar de arma (2)", "Hotkey: next weapon (2)"), tbCycle2, 150);
        TextBox tbBugs = new TextBox(); tbBugs.Text = kBugsS; row(L("Atalho: bugs na tela", "Hotkey: bugs"), tbBugs, 150);
        TextBox tbDirect = new TextBox(); tbDirect.Text = kDirectS; row(L("Arma direta (prefixo + 1..9,0)", "Direct weapon (prefix + 1..9,0)"), tbDirect, 150);

        Label hint = new Label();
        hint.Text = L("Ex.: Ctrl+Alt+W. Combine Ctrl, Alt, Shift, Win com letra, número ou F1-F12.", "E.g. Ctrl+Alt+W. Combine Ctrl, Alt, Shift, Win with a letter, digit or F1-F12.");
        hint.Left = 16; hint.Top = y; hint.Width = 530; hint.Height = 34; hint.ForeColor = Color.DimGray; f.Controls.Add(hint);
        y += 40;

        cbProfile.SelectedIndexChanged += delegate
        {
            string[] names = new string[] { "casual", "normal", "dificil" };
            int i = cbProfile.SelectedIndex;
            if (i < 0 || i > 2) return;
            int b, d, l; GameLogic.Profile(names[i], out b, out d, out l);
            nBugs.Value = b; cbDiff.SelectedIndex = d - 1; nLives.Value = l;
        };

        Button ok = new Button(); ok.Text = L("Salvar", "Save"); ok.Left = 232; ok.Top = y; ok.Width = 90;
        Button cancel = new Button(); cancel.Text = L("Cancelar", "Cancel"); cancel.Left = 330; cancel.Top = y; cancel.Width = 90;
        Button rec = new Button(); rec.Text = L("Copiar recordes", "Copy records"); rec.Left = 16; rec.Top = y; rec.Width = 130;
        f.Controls.Add(ok); f.Controls.Add(cancel); f.Controls.Add(rec);
        f.ClientSize = new Size(560, y + 46);
        f.AcceptButton = ok; f.CancelButton = cancel;

        cancel.Click += delegate { f.Close(); };
        rec.Click += delegate { CopyRecord(); };
        ok.Click += delegate
        {
            uint m, v; string bad = "";
            if (!ParseHotkey(tbToggle.Text, out m, out v)) bad += " " + L("ligar", "on/off");
            if (!ParseHotkey(tbCycle.Text, out m, out v)) bad += " " + L("trocar", "next");
            if (!ParseHotkey(tbCycle2.Text, out m, out v)) bad += " " + L("trocar 2", "next 2");
            if (!ParseHotkey(tbBugs.Text, out m, out v)) bad += " " + L("bugs", "bugs");
            if (!ParseHotkey(tbDirect.Text + "+1", out m, out v)) bad += " " + L("arma direta", "direct");
            if (bad.Length > 0)
            {
                MessageBox.Show(L("Atalho inválido em:", "Invalid hotkey in:") + bad, "Whip Cursor");
                return;
            }
            SetKey(tbToggle.Text, ref kToggleS, ref kToggleM, ref kToggleV);
            SetKey(tbCycle.Text, ref kCycleS, ref kCycleM, ref kCycleV);
            SetKey(tbCycle2.Text, ref kCycle2S, ref kCycle2M, ref kCycle2V);
            SetKey(tbBugs.Text, ref kBugsS, ref kBugsM, ref kBugsV);
            kDirectS = tbDirect.Text.Trim();
            cfgMode = cbMode.SelectedIndex; cfgDiff = cbDiff.SelectedIndex + 1;
            cfgBugs = (int)nBugs.Value; cfgLives = (int)nLives.Value;
            cfgVolume = tVol.Value; Vol = cfgVolume / 100.0;
            cfgScale = tScale.Value / 100f;
            cfgTheme = cbTheme.SelectedIndex; cfgEn = (cbLang.SelectedIndex == 1);
            cfgHud = ckHud.Checked; aimCursor = ckAim.Checked; cfgWheel = ckWheel.Checked;
            ApplySettingsEverywhere();
            f.Close();
        };
        f.Show();
    }

    // reaplica tudo depois de mudar opções (janela ou .ini)
    void ApplySettingsEverywhere()
    {
        SaveConfig();
        ApplyTheme();
        BuildSounds();
        RegisterHotkeys(false);
        RegisterDirectHotkeys();
        if (itAim != null) itAim.Checked = aimCursor;
        if (itHud != null) itHud.Checked = cfgHud;
        MarkVolume();
        started = false;
        RelabelMenu();
        UpdateTray();
        UpdateHud();
    }
}
