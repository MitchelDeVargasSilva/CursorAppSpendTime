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

public partial class Whip : Form
{
    // ===== Dimensões =====
    const int W = 600, H = 600;
    const int N = 24;              // chicote: segmentos
    const float SEG = 5.5f;
    const float GRIP = 26f;
    const int CN = 22;             // corrente: elos
    const float CSEG = 9f;
    const float CGRIP = 26f;
    const double STEP_MS = 1000.0 / 60.0;
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string SettingsKey = @"Software\WhipCursor";

    // ===== Atalhos globais =====
    const int HOTKEY_ID = 1;                 // Ctrl+Alt+W  liga/desliga
    const uint MOD_CONTROL = 0x0002;
    const uint MOD_ALT     = 0x0001;
    const uint VK_W = 0x57;
    const uint VK_M = 0x4D;                  // Ctrl+Alt+M  troca de arma
    const uint VK_Q = 0x51;                  // Ctrl+Alt+Q  (igual ao M)
    const uint VK_E = 0x45;                  // Ctrl+Alt+E  liga/desliga os bugs

    [StructLayout(LayoutKind.Sequential)] struct POINT { public int x, y; }
    [StructLayout(LayoutKind.Sequential)] struct SIZE  { public int cx, cy; }
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
    [StructLayout(LayoutKind.Sequential)]
    struct BITMAPINFOHEADER
    {
        public uint biSize; public int biWidth; public int biHeight;
        public ushort biPlanes; public ushort biBitCount; public uint biCompression;
        public uint biSizeImage; public int biXPelsPerMeter; public int biYPelsPerMeter;
        public uint biClrUsed; public uint biClrImportant;
    }

    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vk);
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
    [DllImport("user32.dll", SetLastError = true)]
    static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
        IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr o);
    [DllImport("gdi32.dll")]
    static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER pbmi, uint iUsage,
        out IntPtr ppvBits, IntPtr hSection, uint dwOffset);
    [DllImport("user32.dll")]
    static extern IntPtr CreateCursor(IntPtr hInst, int xHot, int yHot, int w, int h, byte[] andMask, byte[] xorMask);
    [DllImport("user32.dll")] static extern bool SetSystemCursor(IntPtr hcur, uint id);
    [DllImport("user32.dll")] static extern bool SystemParametersInfo(uint a, uint b, IntPtr c, uint d);
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    static readonly uint[] CursorIds = new uint[] {
        32512, 32513, 32514, 32515, 32516, 32642, 32643, 32644, 32645, 32646, 32648, 32649, 32650
    };

    Pt[] pts = new Pt[N];
    float[] inv = new float[N];
    Pt[] cpts = new Pt[CN];
    float[] cinv = new float[CN];
    List<Spark> sparks = new List<Spark>();
    List<Ring> rings = new List<Ring>();
    List<PointF> tipHist = new List<PointF>();
    List<FireParticle> fires = new List<FireParticle>();
    List<Shard> shards = new List<Shard>();
    List<Trail> trails = new List<Trail>();
    Random rnd = new Random();
    Pen pen;
    Bitmap bmp;
    Graphics gfx;
    IntPtr memDc, hDib;
    Timer timer;
    Stopwatch sw = new Stopwatch();
    NotifyIcon tray;
    ToolStripMenuItem itToggle, itWhip, itMace, itSword, itChain, itBow, itAk, itCat, itSpray, itFlame, itGren, itBugs, itHide, itSnd, itAuto, itCfg, itReloadCfg;
    Snd crackPlayer, whooshPlayer, thudPlayer, slashPlayer, clangPlayer;
    Snd clankPlayer, shootPlayer, stickPlayer;
    Snd akPlayer, catPlayer, boomPlayer, hissPlayer, squashPlayer, rattlePlayer, reloadPlayer, beepPlayer;

    static bool bootLaunch = false;     // iniciado pelo Windows (/boot)

    bool started = false, prevDown = false, cursorHidden = false, cracked = false;
    bool hideCursor = true, soundOn = true;
    bool enabled = false;               // COMEÇA DESATIVADO: só liga pelo atalho
    int throwF = -1, facing = 1, mx, my;
    float gripX = 0.64f, gripY = -0.77f, tipSpeed = 0f;
    double acc = 0;

    WeaponMode mode = WeaponMode.Whip;

    public Whip()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar   = false;
        TopMost         = true;
        StartPosition   = FormStartPosition.Manual;
        Location        = new Point(-32000, -32000);
        Size            = new Size(W, H);

        for (int i = 0; i < N; i++)
        {
            pts[i] = new Pt();
            float t = (float)i / (N - 1);
            inv[i] = (i == 0) ? 0f : 0.30f + 0.70f * (float)Math.Pow(t, 1.4);
        }
        for (int i = 0; i < CN; i++)
        {
            cpts[i] = new Pt();
            cinv[i] = (i == 0) ? 0f : (i == CN - 1 ? 0.22f : 0.55f);   // peso pesado na ponta
        }

        // DIB com alpha pré-multiplicado
        IntPtr sdc = GetDC(IntPtr.Zero);
        memDc = CreateCompatibleDC(sdc);
        BITMAPINFOHEADER bi = new BITMAPINFOHEADER();
        bi.biSize = 40; bi.biWidth = W; bi.biHeight = -H;
        bi.biPlanes = 1; bi.biBitCount = 32; bi.biCompression = 0;
        IntPtr bits;
        hDib = CreateDIBSection(sdc, ref bi, 0, out bits, IntPtr.Zero, 0);
        ReleaseDC(IntPtr.Zero, sdc);
        SelectObject(memDc, hDib);
        bmp = new Bitmap(W, H, W * 4, PixelFormat.Format32bppPArgb, bits);
        gfx = Graphics.FromImage(bmp);
        gfx.SmoothingMode = SmoothingMode.AntiAlias;

        pen = new Pen(Color.Black);
        pen.StartCap = LineCap.Round;
        pen.EndCap   = LineCap.Round;
        pen.LineJoin = LineJoin.Round;

        LoadConfig();
        BuildSounds();

        LoadSettings();
        BuildTray();
        ApplyTheme(); RelabelMenu();
        // NÃO esconde o cursor aqui: o programa nasce desativado.
        AppDomain.CurrentDomain.ProcessExit += delegate { RemoveMouseHook(); RestoreCursor(); };

        UpdateTray();
        RegisterHotkeys(true);

        timer = new Timer();
        timer.Interval = 8;
        timer.Tick += OnTick;
        timer.Start();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x80000 | 0x20 | 0x80 | 0x8 | 0x08000000;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation { get { return true; } }

    // ---------- Atalhos globais ----------
    protected override void WndProc(ref Message m)
    {
        const int WM_HOTKEY = 0x0312;
        if (m.Msg == WM_HOTKEY)
        {
            int id = m.WParam.ToInt32();
            if (id == HOTKEY_ID) SetEnabled(!enabled);
            else if (id == HOTKEY_ID + 1 || id == HOTKEY_ID + 2) CycleWeapon();
            else if (id == HOTKEY_ID + 3) ToggleBugs();
            else if (id >= HOTKEY_ID + 10 && id < HOTKEY_ID + 10 + WEAPON_COUNT) DirectWeapon(id - HOTKEY_ID - 10);
        }
        base.WndProc(ref m);
    }

    // ---------- Estado ----------
    string ModeName(WeaponMode m)
    {
        if (m == WeaponMode.Whip)  return L("Chicote", "Whip");
        if (m == WeaponMode.Mace)  return L("Porrete com pregos", "Spiked club");
        if (m == WeaponMode.Sword) return L("Espada", "Sword");
        if (m == WeaponMode.Bow)   return L("Arco e flecha de fogo", "Fire bow");
        if (m == WeaponMode.Ak)    return "AK-47";
        if (m == WeaponMode.Catapult) return L("Catapulta de fogo azul", "Blue fire catapult");
        if (m == WeaponMode.Spray) return L("Spray", "Spray can");
        if (m == WeaponMode.Flame) return L("Lança-chamas", "Flamethrower");
        if (m == WeaponMode.Grenade) return L("Granada", "Grenade");
        return L("Chicote de corrente", "Chain whip");
    }

    void ResetState()
    {
        sparks.Clear(); rings.Clear(); fires.Clear(); shards.Clear();
        trails.Clear(); tipHist.Clear(); arrows.Clear(); bowF = -1;
        bullets.Clear(); balls.Clear(); burns.Clear(); dots.Clear(); decals.Clear();
        flames.Clear(); grens.Clear(); blasts.Clear(); StopFlameSound(); grReq = false;
        catF = -1; akCd = 0; akShotReq = false; akKick = 0f; akFlash = 0; spWasHeld = false;
        StopHiss();
        throwF = -1; cracked = false;
        swF = -1; mcF = -1; chF = -1;
        swStrikePrev = false; swStrikeNow = false;
    }

    // Liga/desliga. Desligado = nada na tela e cursor normal intacto.
    void SetEnabled(bool on)
    {
        enabled  = on;
        started  = false;
        prevDown = false;
        ResetState();
        if (on)
        {
            if (hideCursor) HideSystemCursor();
            InstallMouseHook();           // clique só dispara a habilidade
        }
        else
        {
            RemoveMouseHook();            // cliques voltam ao normal
            gfx.Clear(Color.Transparent);
            Present();
            RestoreCursor();
        }
        UpdateTray();
    }

    void SelectWeapon(WeaponMode m)
    {
        mode = m;
        SaveSettings();
        if (!enabled) SetEnabled(true);
        else { started = false; UpdateTray(); }
    }

    void CycleWeapon()
    {
        SelectWeapon((WeaponMode)(((int)mode + 1) % 10));
    }

    void UpdateTray()
    {
        if (tray == null) return;
        tray.Text = enabled ? "Whip Cursor - " + L("Ativo: ", "On: ") + ModeName(mode)
                            : "Whip Cursor - " + L("Desativado (", "Off (") + kToggleS + ")";
        if (itToggle != null) itToggle.Text = (enabled ? L("Desativar  [", "Turn off  [") : L("Ativar  [", "Turn on  [")) + kToggleS + "]";
        if (itCycle  != null) itCycle.Text  = L("Próxima arma  [", "Next weapon  [") + kCycleS + "]";
        if (itWhip  != null) itWhip.Checked  = (mode == WeaponMode.Whip);
        if (itChain != null) itChain.Checked = (mode == WeaponMode.Chain);
        if (itSword != null) itSword.Checked = (mode == WeaponMode.Sword);
        if (itMace  != null) itMace.Checked  = (mode == WeaponMode.Mace);
        if (itBow   != null) itBow.Checked   = (mode == WeaponMode.Bow);
        if (itAk    != null) itAk.Checked    = (mode == WeaponMode.Ak);
        if (itCat   != null) itCat.Checked   = (mode == WeaponMode.Catapult);
        if (itSpray != null) itSpray.Checked = (mode == WeaponMode.Spray);
        if (itFlame != null) itFlame.Checked = (mode == WeaponMode.Flame);
        if (itGren  != null) itGren.Checked  = (mode == WeaponMode.Grenade);
        if (itBugs  != null) { itBugs.Checked = (phase > 0 || bugs.Count > 0); itBugs.Text = L("Bugs na tela  [", "Bugs on screen  [") + kBugsS + "]"; }
    }

    // ---------- Registro ----------
    static bool IsAutoStart()
    {
        try
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
                return k != null && k.GetValue("WhipCursor") != null;
        }
        catch { return false; }
    }

    static void SetAutoStart(bool on)
    {
        try
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                if (k == null) return;
                if (on) k.SetValue("WhipCursor", "\"" + Application.ExecutablePath + "\" /boot");
                else k.DeleteValue("WhipCursor", false);
            }
        }
        catch { }
    }

    void LoadSettings()
    {
        try
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(SettingsKey))
            {
                if (k == null) return;
                object v = k.GetValue("Mode");
                if (v != null) { int m = Convert.ToInt32(v); if (m >= 0 && m <= 9) mode = (WeaponMode)m; }
                v = k.GetValue("Hide");
                if (v != null) hideCursor = Convert.ToInt32(v) != 0;
                v = k.GetValue("Sound");
                if (v != null) soundOn = Convert.ToInt32(v) != 0;
                v = k.GetValue("Best");
                if (v != null) bestScore = Convert.ToInt32(v);
                v = k.GetValue("Kills");
                if (v != null) totalKills = Convert.ToInt32(v);
                bests[0] = bestScore;
                v = k.GetValue("Best1"); if (v != null) bests[1] = Convert.ToInt32(v);
                v = k.GetValue("Best2"); if (v != null) bests[2] = Convert.ToInt32(v);
                v = k.GetValue("Ach"); if (v != null) ach = Convert.ToInt32(v);
            }
        }
        catch { }
    }

    void SaveSettings()
    {
        try
        {
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(SettingsKey))
            {
                k.SetValue("Mode", (int)mode);
                k.SetValue("Hide", hideCursor ? 1 : 0);
                k.SetValue("Sound", soundOn ? 1 : 0);
                k.SetValue("Best", bests[0]);
                k.SetValue("Best1", bests[1]);
                k.SetValue("Best2", bests[2]);
                k.SetValue("Ach", ach);
                k.SetValue("Kills", totalKills);
            }
        }
        catch { }
    }

    Icon MakeIcon()
    {
        using (Bitmap b = new Bitmap(32, 32))
        using (Graphics gg = Graphics.FromImage(b))
        {
            gg.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen p = new Pen(Color.FromArgb(120, 70, 30), 3f))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                gg.DrawBezier(p, 6f, 27f, 8f, 4f, 22f, 28f, 28f, 5f);
            }
            using (Pen p2 = new Pen(Color.FromArgb(214, 172, 74), 5f))
            {
                p2.StartCap = LineCap.Round; p2.EndCap = LineCap.Round;
                gg.DrawLine(p2, 4f, 29f, 9f, 24f);
            }
            return Icon.FromHandle(b.GetHicon());
        }
    }

    void BuildTray()
    {
        ContextMenuStrip menu = new ContextMenuStrip();

        itToggle = new ToolStripMenuItem("Ativar  [Ctrl+Alt+W]");
        itToggle.Click += delegate { SetEnabled(!enabled); };

        itCycle = new ToolStripMenuItem("Próxima arma");
        itCycle.Click += delegate { CycleWeapon(); };

        itWhip  = new ToolStripMenuItem("Chicote");
        itWhip.Click  += delegate { SelectWeapon(WeaponMode.Whip); };
        itChain = new ToolStripMenuItem("Chicote de corrente");
        itChain.Click += delegate { SelectWeapon(WeaponMode.Chain); };
        itSword = new ToolStripMenuItem("Espada");
        itSword.Click += delegate { SelectWeapon(WeaponMode.Sword); };
        itMace  = new ToolStripMenuItem("Porrete com pregos");
        itMace.Click  += delegate { SelectWeapon(WeaponMode.Mace); };
        itBow = new ToolStripMenuItem("Arco e flecha de fogo");
        itBow.Click   += delegate { SelectWeapon(WeaponMode.Bow); };
        itAk = new ToolStripMenuItem("AK-47");
        itAk.Click    += delegate { SelectWeapon(WeaponMode.Ak); };
        itCat = new ToolStripMenuItem("Catapulta de fogo azul");
        itCat.Click   += delegate { SelectWeapon(WeaponMode.Catapult); };
        itSpray = new ToolStripMenuItem("Spray");
        itSpray.Click += delegate { SelectWeapon(WeaponMode.Spray); };
        itFlame = new ToolStripMenuItem("Lança-chamas");
        itFlame.Click += delegate { SelectWeapon(WeaponMode.Flame); };
        itGren = new ToolStripMenuItem("Granada");
        itGren.Click  += delegate { SelectWeapon(WeaponMode.Grenade); };
        itBugs = new ToolStripMenuItem("Bugs na tela");
        itBugs.Click  += delegate { ToggleBugs(); };

        itHide = new ToolStripMenuItem("Esconder cursor normal");
        itHide.CheckOnClick = true;
        itHide.Checked = hideCursor;
        itHide.CheckedChanged += delegate
        {
            hideCursor = itHide.Checked;
            SaveSettings();
            // só mexe no cursor se a arma estiver ATIVA (corrige o sumiço do cursor)
            if (enabled)
            {
                if (hideCursor) HideSystemCursor(); else RestoreCursor();
            }
        };

        itSnd = new ToolStripMenuItem("Som");
        itSnd.CheckOnClick = true;
        itSnd.Checked = soundOn;
        itSnd.CheckedChanged += delegate { soundOn = itSnd.Checked; SaveSettings(); };

        itAuto = new ToolStripMenuItem("Iniciar com o Windows");
        itAuto.CheckOnClick = true;
        itAuto.Checked = IsAutoStart();
        itAuto.CheckedChanged += delegate { SetAutoStart(itAuto.Checked); };

        ToolStripMenuItem itExit = new ToolStripMenuItem("Sair");
        itExit2 = itExit;
        itExit.Click += delegate { StopGame(); RestoreCursor(); tray.Visible = false; Mixer.Shutdown(); Application.Exit(); };

        menu.Items.Add(itToggle);
        menu.Items.Add(itCycle);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(itWhip);
        menu.Items.Add(itChain);
        menu.Items.Add(itSword);
        menu.Items.Add(itMace);
        menu.Items.Add(itBow);
        menu.Items.Add(itAk);
        menu.Items.Add(itCat);
        menu.Items.Add(itSpray);
        menu.Items.Add(itFlame);
        menu.Items.Add(itGren);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(itBugs);
        menu.Items.Add(new ToolStripSeparator());
        itAim = new ToolStripMenuItem("Mira no cursor (arma fixa embaixo)");
        itAim.CheckOnClick = true; itAim.Checked = aimCursor;
        itAim.CheckedChanged += delegate { aimCursor = itAim.Checked; SaveConfig(); started = false; };
        itHud = new ToolStripMenuItem("Painel de pontos");
        itHud.CheckOnClick = true; itHud.Checked = cfgHud;
        itHud.CheckedChanged += delegate { cfgHud = itHud.Checked; SaveConfig(); UpdateHud(); };
        itVol = new ToolStripMenuItem("Volume");
        foreach (int vv in new int[] { 0, 25, 50, 75, 100 })
        {
            int v2 = vv;
            ToolStripMenuItem sv = new ToolStripMenuItem(v2 + "%");
            sv.Click += delegate { cfgVolume = v2; Vol = v2 / 100.0; BuildSounds(); SaveConfig(); MarkVolume(); };
            itVol.DropDownItems.Add(sv);
        }
        MarkVolume();
        itCfg = new ToolStripMenuItem("Abrir configurações (WhipCursor.ini)");
        itCfg.Click += delegate
        {
            SaveConfig();
            try { Process.Start("notepad.exe", "\"" + cfgPath + "\""); } catch { }
        };
        itReloadCfg = new ToolStripMenuItem("Recarregar configurações");
        itReloadCfg.Click += delegate { ReloadConfig(); };

        menu.Items.Add(itAim);
        menu.Items.Add(itHud);
        menu.Items.Add(itVol);
        menu.Items.Add(itHide);
        menu.Items.Add(itSnd);
        menu.Items.Add(itAuto);
        menu.Items.Add(new ToolStripSeparator());
        itSettings = new ToolStripMenuItem("Configurações...");
        itSettings.Click += delegate { ShowSettings(); };
        itRecord = new ToolStripMenuItem("Copiar recordes");
        itRecord.Click += delegate { CopyRecord(); };
        itAch = new ToolStripMenuItem("Conquistas");
        itAch.Click += delegate { ShowAchievements(); };
        menu.Items.Add(itSettings);
        menu.Items.Add(itRecord);
        menu.Items.Add(itAch);
        menu.Items.Add(itCfg);
        menu.Items.Add(itReloadCfg);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(itExit);

        tray = new NotifyIcon();
        try { tray.Icon = MakeIcon(); } catch { tray.Icon = SystemIcons.Application; }
        tray.Text = "Whip Cursor";
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += delegate { SetEnabled(!enabled); };
        tray.Visible = true;
    }

    // ---------- Cursor do sistema ----------
    void HideSystemCursor()
    {
        if (cursorHidden) return;
        byte[] andMask = new byte[128];
        byte[] xorMask = new byte[128];
        for (int i = 0; i < 128; i++) andMask[i] = 0xFF;
        foreach (uint id in CursorIds)
        {
            IntPtr c = CreateCursor(IntPtr.Zero, 0, 0, 32, 32, andMask, xorMask);
            SetSystemCursor(c, id);
        }
        cursorHidden = true;
    }

    public static void RestoreCursorStatic()
    {
        SystemParametersInfo(0x57, 0, IntPtr.Zero, 0);
    }

    void RestoreCursor()
    {
        if (!cursorHidden) return;
        RestoreCursorStatic();
        cursorHidden = false;
    }

    // ---------- Loop principal ----------
    void OnTick(object sender, EventArgs e)
    {
        POINT p;
        GetCursorPos(out p);
        mx = p.x; my = p.y;

        StepBugsTimed();

        if (!enabled)
        {
            prevDown = false;
            return;
        }

        if (!started)
        {
            ResetState();
            if      (mode == WeaponMode.Whip)  InitRope();
            else if (mode == WeaponMode.Mace)  InitMace();
            else if (mode == WeaponMode.Sword) InitSword();
            else if (mode == WeaponMode.Bow)   InitBow();
            else if (mode == WeaponMode.Ak)    InitAk();
            else if (mode == WeaponMode.Catapult) InitCatapult();
            else if (mode == WeaponMode.Spray) InitSpray();
            else if (mode == WeaponMode.Flame) InitFlame();
            else if (mode == WeaponMode.Grenade) InitGren();
            else                               InitChain();
            started = true;
            sw.Restart();
            acc = 0;
        }

        // com o gancho de mouse ativo, só o clique "engolido" dispara a arma
        bool trigger = false;
        if (hookId != IntPtr.Zero)
        {
            if (clickPending) { clickPending = false; trigger = true; }
        }
        else
        {
            bool down = ((GetAsyncKeyState(1) & 0x8000) != 0) || ((GetAsyncKeyState(2) & 0x8000) != 0);
            if (down && !prevDown) trigger = true;
            prevDown = down;
        }
        ConsumeWheel();
        if (trigger) OnClick();

        acc += sw.Elapsed.TotalMilliseconds;
        sw.Restart();
        if (acc > 100) acc = 100;
        int steps = 0;
        while (acc >= STEP_MS && steps < 4)
        {
            StepActive();
            acc -= STEP_MS;
            steps++;
        }
        if (steps > 0) Render();
    }

    void OnClick()
    {
        if      (mode == WeaponMode.Whip)  { if (throwF < 0 || throwF > 22) StartThrow(); }
        else if (mode == WeaponMode.Mace)  StartMaceSwing();
        else if (mode == WeaponMode.Sword) StartSwordSlash();
        else if (mode == WeaponMode.Bow)   StartBowShot();
        else if (mode == WeaponMode.Ak)    akShotReq = true;
        else if (mode == WeaponMode.Catapult) StartCatapult();
        else if (mode == WeaponMode.Spray) { /* spray age enquanto o botão está apertado */ }
        else if (mode == WeaponMode.Flame) { /* segura o botão */ }
        else if (mode == WeaponMode.Grenade) grReq = true;
        else                               StartChainThrow();
    }

    void StepActive()
    {
        if      (mode == WeaponMode.Whip)  StepFrame();
        else if (mode == WeaponMode.Mace)  StepMace();
        else if (mode == WeaponMode.Sword) StepSword();
        else if (mode == WeaponMode.Bow)   StepBow();
        else if (mode == WeaponMode.Ak)    StepAk();
        else if (mode == WeaponMode.Catapult) StepCatapult();
        else if (mode == WeaponMode.Spray) StepSpray();
        else if (mode == WeaponMode.Flame) StepFlame();
        else if (mode == WeaponMode.Grenade) StepGren();
        else                               StepChain();
    }

    // ---------- Utilidades de movimento ----------
    int PickFacing()
    {
        Screen scr = Screen.FromPoint(new Point(mx, my));
        return ((mx - scr.Bounds.Left) < scr.Bounds.Width / 2) ? 1 : -1;
    }

    float Rf(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }

    static float Deg(float d) { return d * (float)Math.PI / 180f; }
    static float Mix(float a, float b, float t) { return a + (b - a) * t; }
    static float Clamp01(float u) { return u < 0f ? 0f : (u > 1f ? 1f : u); }
    static float Sm(float u)   { u = Clamp01(u); return u * u * (3f - 2f * u); }   // suave
    static float EAcc(float u) { u = Clamp01(u); return u * u; }                   // acelera
    static float EDec(float u) { u = Clamp01(u); return 1f - (1f - u) * (1f - u); } // freia

}
