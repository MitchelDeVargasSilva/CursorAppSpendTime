using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

public class Pt { public float x, y, px, py; }
public class Spark
{
    public float x, y, vx, vy;
    public int life, max;
    public bool isFire, hot, custom, blue;
    public float drag = 0.92f, g = 0.3f;
}
public class Ring { public float x, y; public int age; }
public class FireParticle { public float x, y, vx, vy; public int life, max; public bool blue; }
public class Shard { public float x, y, vx, vy, rot, rotV, len, w; public int life, max; }
public class Trail { public float tx, ty, bx, by; public int age; public bool start; }
public class Arrow { public float x, y, vx, vy, ux, uy; public int age, stuck; public bool isStuck; }

// ---- Armas ----
public class Bullet { public float x, y, px, py, vx, vy; public int age; }
public class Ball { public float sx, sy, tx, ty, z0, peak; public int age, dur; }
public class Burn { public float x, y; public int life; }
public class Dot { public float x, y, vx, vy, r; public int life, max, col, kind; }

public enum WeaponMode { Whip = 0, Mace = 1, Sword = 2, Chain = 3, Bow = 4, Ak = 5, Catapult = 6, Spray = 7 }

public class Whip : Form
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
    ToolStripMenuItem itToggle, itWhip, itMace, itSword, itChain, itBow, itAk, itCat, itSpray, itBugs, itHide, itSnd, itAuto;
    SoundPlayer crackPlayer, whooshPlayer, thudPlayer, slashPlayer, clangPlayer;
    SoundPlayer clankPlayer, shootPlayer, stickPlayer;
    SoundPlayer akPlayer, catPlayer, boomPlayer, hissPlayer, squashPlayer;

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

        crackPlayer      = new SoundPlayer(new MemoryStream(MakeCrackWav()));
        whooshPlayer     = new SoundPlayer(new MemoryStream(MakeWhooshWav()));
        thudPlayer       = new SoundPlayer(new MemoryStream(MakeThudWav()));
        slashPlayer      = new SoundPlayer(new MemoryStream(MakeSlashWav()));
        clangPlayer      = new SoundPlayer(new MemoryStream(MakeClangWav()));
        clankPlayer      = new SoundPlayer(new MemoryStream(MakeClankWav()));
        shootPlayer      = new SoundPlayer(new MemoryStream(MakeShootWav()));
        stickPlayer      = new SoundPlayer(new MemoryStream(MakeStickWav()));
        akPlayer         = new SoundPlayer(new MemoryStream(MakeAkWav()));
        catPlayer        = new SoundPlayer(new MemoryStream(MakeCatapultWav()));
        boomPlayer       = new SoundPlayer(new MemoryStream(MakeBoomWav()));
        hissPlayer       = new SoundPlayer(new MemoryStream(MakeHissWav()));
        squashPlayer     = new SoundPlayer(new MemoryStream(MakeSquashWav()));
        try
        {
            crackPlayer.Load(); whooshPlayer.Load(); thudPlayer.Load();
            slashPlayer.Load(); clangPlayer.Load();
            clankPlayer.Load(); shootPlayer.Load(); stickPlayer.Load();
            akPlayer.Load(); catPlayer.Load(); boomPlayer.Load(); hissPlayer.Load(); squashPlayer.Load();
        }
        catch { }

        LoadSettings();
        BuildTray();
        // NÃO esconde o cursor aqui: o programa nasce desativado.
        AppDomain.CurrentDomain.ProcessExit += delegate { RemoveMouseHook(); RestoreCursor(); };

        bool ok1 = RegisterHotKey(Handle, HOTKEY_ID,     MOD_CONTROL | MOD_ALT, VK_W);
        RegisterHotKey(Handle, HOTKEY_ID + 1, MOD_CONTROL | MOD_ALT, VK_M);
        RegisterHotKey(Handle, HOTKEY_ID + 2, MOD_CONTROL | MOD_ALT, VK_Q);
        RegisterHotKey(Handle, HOTKEY_ID + 3, MOD_CONTROL | MOD_ALT, VK_E);

        UpdateTray();
        if (!ok1)
            tray.ShowBalloonTip(5000, "Whip Cursor", "O atalho Ctrl+Alt+W já está em uso por outro programa. Use o menu da bandeja.", ToolTipIcon.Warning);
        else if (!bootLaunch)
            tray.ShowBalloonTip(4000, "Whip Cursor", "Pronto! Ctrl+Alt+W liga/desliga. Ctrl+Alt+M troca de arma. Ctrl+Alt+E liga/desliga os bugs.", ToolTipIcon.Info);

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
        }
        base.WndProc(ref m);
    }

    // ---------- Estado ----------
    static string ModeName(WeaponMode m)
    {
        if (m == WeaponMode.Whip)  return "Chicote";
        if (m == WeaponMode.Mace)  return "Porrete com pregos";
        if (m == WeaponMode.Sword) return "Espada";
        if (m == WeaponMode.Bow)   return "Arco e flecha de fogo";
        if (m == WeaponMode.Ak)    return "AK-47";
        if (m == WeaponMode.Catapult) return "Catapulta de fogo azul";
        if (m == WeaponMode.Spray) return "Spray";
        return "Chicote de corrente";
    }

    void ResetState()
    {
        sparks.Clear(); rings.Clear(); fires.Clear(); shards.Clear();
        trails.Clear(); tipHist.Clear(); arrows.Clear(); bowF = -1;
        bullets.Clear(); balls.Clear(); burns.Clear(); dots.Clear();
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
        SelectWeapon((WeaponMode)(((int)mode + 1) % 8));
    }

    void UpdateTray()
    {
        if (tray == null) return;
        tray.Text = enabled ? "Whip Cursor - Ativo: " + ModeName(mode)
                            : "Whip Cursor - Desativado (Ctrl+Alt+W)";
        if (itToggle != null) itToggle.Text = enabled ? "Desativar  [Ctrl+Alt+W]" : "Ativar  [Ctrl+Alt+W]";
        if (itWhip  != null) itWhip.Checked  = (mode == WeaponMode.Whip);
        if (itChain != null) itChain.Checked = (mode == WeaponMode.Chain);
        if (itSword != null) itSword.Checked = (mode == WeaponMode.Sword);
        if (itMace  != null) itMace.Checked  = (mode == WeaponMode.Mace);
        if (itBow   != null) itBow.Checked   = (mode == WeaponMode.Bow);
        if (itAk    != null) itAk.Checked    = (mode == WeaponMode.Ak);
        if (itCat   != null) itCat.Checked   = (mode == WeaponMode.Catapult);
        if (itSpray != null) itSpray.Checked = (mode == WeaponMode.Spray);
        if (itBugs  != null) itBugs.Checked  = (bugs.Count > 0);
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
                if (v != null) { int m = Convert.ToInt32(v); if (m >= 0 && m <= 7) mode = (WeaponMode)m; }
                v = k.GetValue("Hide");
                if (v != null) hideCursor = Convert.ToInt32(v) != 0;
                v = k.GetValue("Sound");
                if (v != null) soundOn = Convert.ToInt32(v) != 0;
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

        ToolStripMenuItem itCycle = new ToolStripMenuItem("Próxima arma  [Ctrl+Alt+M]");
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
        itBugs = new ToolStripMenuItem("Bugs na tela  [Ctrl+Alt+E]");
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
        itExit.Click += delegate { ClearBugs(); RestoreCursor(); tray.Visible = false; Application.Exit(); };

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
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(itBugs);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(itHide);
        menu.Items.Add(itSnd);
        menu.Items.Add(itAuto);
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

    // ===================================================================
    //  MODO CHICOTE
    // ===================================================================

    void InitRope()
    {
        float gx = facing * 0.64f;
        float sx = mx + gx * GRIP, sy = my + (-0.77f) * GRIP;
        for (int i = 0; i < N; i++)
        {
            pts[i].x = pts[i].px = sx;
            pts[i].y = pts[i].py = sy + i * SEG;
        }
    }

    void StartThrow()
    {
        Screen scr = Screen.FromPoint(new Point(mx, my));
        facing = ((mx - scr.Bounds.Left) < scr.Bounds.Width / 2) ? 1 : -1;
        throwF  = 0;
        cracked = false;
    }

    static double HandleAngle(int f)
    {
        double deg = Math.PI / 180.0;
        double rest = -50 * deg, back = -165 * deg, fwd = 5 * deg;
        if (f < 0)  return rest;
        if (f < 10) { double u = f / 10.0; u = 1 - (1 - u) * (1 - u); return rest + (back - rest) * u; }
        if (f < 15) { double u = (f - 10) / 5.0; u = u * u; return back + (fwd - back) * u; }
        if (f < 30) { double u = (f - 15) / 15.0; u = 1 - (1 - u) * (1 - u); return fwd + (rest - fwd) * u; }
        return rest;
    }

    void StepFrame()
    {
        double th = HandleAngle(throwF);
        float gx = (float)(facing * Math.Cos(th));
        float gy = (float)Math.Sin(th);
        gripX = gx; gripY = gy;

        pts[0].x = pts[0].px = mx + gx * GRIP;
        pts[0].y = pts[0].py = my + gy * GRIP;

        if (throwF == 11) PlayWhoosh();
        if (throwF >= 11 && throwF <= 17)
        {
            for (int i = 1; i < N; i++)
            {
                float k = (float)i / N; k *= k;
                pts[i].px -= facing * 4.0f * k;
            }
        }

        for (int i = 1; i < N; i++)
        {
            Pt pt = pts[i];
            float vx = (pt.x - pt.px) * 0.985f;
            float vy = (pt.y - pt.py) * 0.985f + 0.42f;
            pt.px = pt.x; pt.py = pt.y;
            pt.x += vx; pt.y += vy;
        }

        for (int i = 1; i <= 7; i++)
        {
            float dxs, dys;
            if (i == 1) { dxs = gx; dys = gy; }
            else
            {
                dxs = pts[i - 1].x - pts[i - 2].x;
                dys = pts[i - 1].y - pts[i - 2].y;
                float l = (float)Math.Sqrt(dxs * dxs + dys * dys);
                if (l < 0.0001f) l = 1f;
                dxs /= l; dys /= l;
            }
            float tx = pts[i - 1].x + dxs * SEG, ty = pts[i - 1].y + dys * SEG;
            float kk = (i == 1) ? 0.5f : 0.28f * (1f - (i - 2) / 7f);
            pts[i].x += (tx - pts[i].x) * kk;
            pts[i].y += (ty - pts[i].y) * kk;
        }

        for (int it = 0; it < 24; it++)
        {
            for (int i = 1; i < N; i++)
            {
                Pt a = pts[i - 1], b = pts[i];
                float dx = b.x - a.x, dy = b.y - a.y;
                float d = (float)Math.Sqrt(dx * dx + dy * dy);
                if (d < 0.0001f) d = 0.0001f;
                float diff = (d - SEG) / d;
                float wa = inv[i - 1], wb = inv[i], ws = wa + wb;
                a.x += dx * diff * wa / ws; a.y += dy * diff * wa / ws;
                b.x -= dx * diff * wb / ws; b.y -= dy * diff * wb / ws;
            }
        }

        Pt tip = pts[N - 1];
        float tvx = tip.x - tip.px, tvy = tip.y - tip.py;
        tipSpeed = (float)Math.Sqrt(tvx * tvx + tvy * tvy);
        tipHist.Add(new PointF(tip.x, tip.y));
        if (tipHist.Count > 8) tipHist.RemoveAt(0);
        if (tipSpeed > 9f) HitBugsSeg(tip.px, tip.py, tip.x, tip.y, 9f);

        // Fogo no rastro da ponta quando está rápido
        if (tipSpeed > 10f)
        {
            int fireCount = (int)(tipSpeed / 4f);
            for (int f = 0; f < fireCount; f++)
            {
                FireParticle fp = new FireParticle();
                fp.x = tip.x + (float)(rnd.NextDouble() - 0.5) * 2f;
                fp.y = tip.y + (float)(rnd.NextDouble() - 0.5) * 2f;
                fp.vx = (float)(rnd.NextDouble() - 0.5) * 1.2f + tvx * 0.08f;
                fp.vy = -1.5f - (float)rnd.NextDouble() * 2f;
                fp.max = fp.life = 10 + rnd.Next(10);
                fires.Add(fp);
            }
        }

        if (throwF >= 0)
        {
            float ex = tip.x - mx, ey = tip.y - my;
            float dist = (float)Math.Sqrt(ex * ex + ey * ey);
            float full = GRIP + (N - 1) * SEG;
            if (!cracked && ((throwF >= 14 && dist > 0.75f * full) || throwF >= 24))
            {
                cracked = true;
                DoCrack(tip.x, tip.y);
            }
            throwF++;
            if (throwF > 34) throwF = -1;
        }

        StepParticles();
    }

    void DoCrack(float x, float y)
    {
        HitBugsSeg(x - 18f, y, x + 18f, y, 26f);
        rings.Add(new Ring { x = x, y = y, age = 0 });

        // Faíscas normais
        for (int i = 0; i < 12; i++)
        {
            Spark s = new Spark();
            double ang = rnd.NextDouble() * Math.PI * 2;
            double spd = 3 + rnd.NextDouble() * 7;
            s.x = x; s.y = y;
            s.vx = (float)(Math.Cos(ang) * spd);
            s.vy = (float)(Math.Sin(ang) * spd);
            s.max = s.life = 12 + rnd.Next(8);
            s.isFire = false;
            sparks.Add(s);
        }

        // Faíscas de fogo
        for (int i = 0; i < 20; i++)
        {
            Spark s = new Spark();
            double ang = rnd.NextDouble() * Math.PI * 2;
            double spd = 1.5 + rnd.NextDouble() * 5;
            s.x = x; s.y = y;
            s.vx = (float)(Math.Cos(ang) * spd);
            s.vy = (float)(Math.Sin(ang) * spd) - 1f;
            s.max = s.life = 18 + rnd.Next(14);
            s.isFire = true;
            sparks.Add(s);
        }

        // Partículas de fogo extras
        for (int i = 0; i < 15; i++)
        {
            FireParticle fp = new FireParticle();
            double ang = rnd.NextDouble() * Math.PI * 2;
            double spd = rnd.NextDouble() * 4;
            fp.x = x; fp.y = y;
            fp.vx = (float)(Math.Cos(ang) * spd);
            fp.vy = (float)(Math.Sin(ang) * spd) - 2f;
            fp.max = fp.life = 20 + rnd.Next(15);
            fires.Add(fp);
        }

        PlayCrack();
    }

    // ===================================================================
    //  MODO ESPADA  (combo de 3 golpes: corte descendente, corte
    //  ascendente e estocada; o braço gira no pulso, sem "chicotear")
    // ===================================================================
    const float SWORD_LEN = 96f;
    const float GUARD_W   = 17f;
    const float GRIP_LEN  = 22f;

    int   swF = -1, swType = 0, swCombo = 0, swFacing = 1, swIdle = 0;
    float swAng, swReach = 1f, swHand = 0f;
    float swFromAng, swFromReach = 1f, swFromHand = 0f;
    float swPrevTipX, swPrevTipY, swPulse = 0f;
    bool  swStrikeNow = false, swStrikePrev = false;

    void InitSword()
    {
        swFacing = PickFacing();
        swAng = Deg(-60f); swReach = 1f; swHand = 0f;
        swF = -1; swType = 0; swCombo = 0; swIdle = 0; swPulse = 0f;
        swPrevTipX = mx; swPrevTipY = my;
    }

    // só aceita novo golpe parado ou já na fase de recuperação (combo fluido)
    bool SwordCanChain()
    {
        int rec = (swType == 1) ? 11 : 13;
        return swF >= rec;
    }

    void StartSwordSlash()
    {
        if (swF >= 0 && !SwordCanChain()) return;
        if (swF < 0) swFacing = PickFacing();
        if (swIdle > 50) swCombo = 0;
        swType  = swCombo;
        swCombo = (swCombo + 1) % 3;
        swFromAng = swAng; swFromReach = swReach; swFromHand = swHand;
        swF = 0;
        swIdle = 0;
    }

    // Pose da espada em cada quadro: preparar -> cortar -> segurar -> recuperar
    void SwordPose(int type, int f, out float ang, out float reach, out float hand)
    {
        float rest = Deg(-60f);
        ang = rest; reach = 1f; hand = 0f;

        if (type == 0)   // corte descendente (de cima para baixo, na diagonal)
        {
            float aw = Deg(-125f), ae = Deg(55f);
            if (f < 6)       { float u = EDec((f + 1) / 6f); ang = Mix(swFromAng, aw, u); reach = Mix(swFromReach, 1f, u); hand = Mix(swFromHand, -6f, u); }
            else if (f < 11) { float u = EAcc((f - 5) / 5f); ang = Mix(aw, ae, u); hand = Mix(-6f, 10f, u); }
            else if (f < 13) { ang = ae + Deg(3f) * (f - 10); hand = 10f; }
            else             { float u = Sm((f - 12) / 12f); ang = Mix(ae + Deg(6f), rest, u); hand = Mix(10f, 0f, u); }
        }
        else if (type == 1)   // corte ascendente (reverso, de baixo para cima)
        {
            float aw = Deg(35f), ae = Deg(-140f);
            if (f < 4)       { float u = EDec((f + 1) / 4f); ang = Mix(swFromAng, aw, u); reach = Mix(swFromReach, 1f, u); hand = Mix(swFromHand, -4f, u); }
            else if (f < 9)  { float u = EAcc((f - 3) / 5f); ang = Mix(aw, ae, u); hand = Mix(-4f, 8f, u); }
            else if (f < 11) { ang = ae - Deg(3f) * (f - 8); hand = 8f; }
            else             { float u = Sm((f - 10) / 12f); ang = Mix(ae - Deg(6f), rest, u); hand = Mix(8f, 0f, u); }
        }
        else   // estocada (a lâmina avança em linha reta)
        {
            float a0 = Deg(-14f), a1 = Deg(0f);
            if (f < 6)       { float u = EDec((f + 1) / 6f); ang = Mix(swFromAng, a0, u); reach = Mix(swFromReach, 0.84f, u); hand = Mix(swFromHand, -7f, u); }
            else if (f < 10) { float u = EAcc((f - 5) / 4f); ang = Mix(a0, a1, u); reach = Mix(0.84f, 1.2f, u); hand = Mix(-7f, 16f, u); }
            else if (f < 13) { ang = a1; reach = 1.2f; hand = 16f; }
            else             { float u = Sm((f - 12) / 10f); ang = Mix(a1, rest, u); reach = Mix(1.2f, 1f, u); hand = Mix(16f, 0f, u); }
        }
    }

    void StepSword()
    {
        float rest = Deg(-60f);
        bool strike = false, impact = false;

        if (swF >= 0)
        {
            SwordPose(swType, swF, out swAng, out swReach, out swHand);
            int sStart = (swType == 1) ? 5 : 7;                         // começa o corte (som + rastro)
            int sEnd   = (swType == 0) ? 10 : (swType == 1 ? 8 : 9);    // fim do corte (impacto)
            int sLast  = (swType == 0) ? 24 : 22;
            if (swF == sStart) PlaySlash();
            strike = (swF >= sStart && swF <= sEnd);
            impact = (swF == sEnd);
            swF++;
            if (swF > sLast) { swF = -1; swIdle = 0; }
        }
        else
        {
            swIdle++;
            swPulse += 0.06f;
            swAng = rest + Deg(2.5f) * (float)Math.Sin(swPulse);
            swReach = 1f; swHand = 0f;
        }

        float ca = (float)Math.Cos(swAng) * swFacing, sa = (float)Math.Sin(swAng);
        float hx = mx + swFacing * swHand, hy = my;
        float tipX = hx + ca * SWORD_LEN * swReach;
        float tipY = hy + sa * SWORD_LEN * swReach;
        float svx = tipX - swPrevTipX, svy = tipY - swPrevTipY;

        // rastro em crescente
        for (int i = trails.Count - 1; i >= 0; i--)
        {
            trails[i].age++;
            if (trails[i].age > 10) trails.RemoveAt(i);
        }
        if (strike)
        {
            Trail tr = new Trail();
            tr.tx = tipX; tr.ty = tipY;
            tr.bx = hx + ca * SWORD_LEN * swReach * 0.42f;
            tr.by = hy + sa * SWORD_LEN * swReach * 0.42f;
            tr.age = 0;
            tr.start = !swStrikePrev;
            trails.Add(tr);

            // faíscas de aço rasgando o ar
            for (int i = 0; i < 2; i++)
            {
                float kk = 0.55f + (float)rnd.NextDouble() * 0.45f;
                AddSteelSpark(hx + ca * SWORD_LEN * swReach * kk, hy + sa * SWORD_LEN * swReach * kk,
                              svx * 0.12f + Rf(-1.5f, 1.5f), svy * 0.12f + Rf(-1.5f, 1.5f) - 0.4f);
            }
        }
        if (strike)
        {
            HitBugsSeg(hx + ca * SWORD_LEN * swReach * 0.35f, hy + sa * SWORD_LEN * swReach * 0.35f, tipX, tipY, 11f);
            HitBugsSeg(swPrevTipX, swPrevTipY, tipX, tipY, 14f);
        }
        swStrikePrev = strike;
        swStrikeNow  = strike;

        if (impact) DoSwordImpact(tipX, tipY, svx, svy);

        swPrevTipX = tipX; swPrevTipY = tipY;
        StepParticles();
    }

    void AddSteelSpark(float x, float y, float vx, float vy)
    {
        if (sparks.Count > 400) return;
        Spark s = new Spark();
        s.x = x; s.y = y; s.vx = vx; s.vy = vy;
        s.max = s.life = 8 + rnd.Next(10);
        s.custom = true; s.drag = 0.90f; s.g = 0.25f;
        sparks.Add(s);
    }

    void DoSwordImpact(float x, float y, float vx, float vy)
    {
        rings.Add(new Ring { x = x, y = y, age = 0 });
        float sp = (float)Math.Sqrt(vx * vx + vy * vy);
        if (sp < 0.01f) sp = 1f;
        float ux = vx / sp, uy = vy / sp;
        for (int i = 0; i < 22; i++)
        {
            double ang = rnd.NextDouble() * Math.PI * 2;
            float spd = 2f + (float)rnd.NextDouble() * 8f;
            // as faíscas saem mais na direção do corte
            AddSteelSpark(x, y,
                ux * spd * 0.9f + (float)Math.Cos(ang) * spd * 0.55f,
                uy * spd * 0.9f + (float)Math.Sin(ang) * spd * 0.55f);
        }
        PlayClang();
    }

    // ===================================================================
    //  MODO PORRETE COM PREGOS  (pancada pesada de cima + tacada lateral)
    // ===================================================================
    const float MC_LEN = 88f;      // da mão até a ponta da cabeça

    int   mcF = -1, mcType = 0, mcCombo = 0, mcFacing = 1, mcIdle = 0, mcShake = 0;
    float mcAng, mcPrevAng, mcHand = 0f, mcFromAng, mcFromHand, mcPulse = 0f;

    static readonly float[] MC_S = new float[] { 0f, 0.28f, 0.50f, 0.66f, 0.80f, 0.92f, 1.0f };
    static readonly float[] MC_W = new float[] { 2.6f, 2.6f, 4.2f, 6.4f, 8.2f, 8.8f, 6.2f };
    static readonly float[] NAIL_S    = new float[] { 0.64f, 0.70f, 0.76f, 0.82f, 0.88f, 0.94f, 0.67f, 0.79f, 0.91f };
    static readonly int[]   NAIL_SIDE = new int[]   { 1, -1, 1, -1, 1, -1, -1, 1, -1 };
    static readonly float[] NAIL_TILT = new float[] { -0.35f, 0.30f, -0.10f, 0.35f, 0.15f, -0.30f, 0.10f, -0.25f, 0.20f };

    void InitMace()
    {
        mcFacing = PickFacing();
        mcAng = mcPrevAng = Deg(-78f);
        mcHand = 0f;
        mcF = -1; mcType = 0; mcCombo = 0; mcIdle = 0; mcShake = 0; mcPulse = 0f;
    }

    void StartMaceSwing()
    {
        // pesado: só aceita outro golpe na fase de recuperação
        if (mcF >= 0)
        {
            int rec = (mcType == 0) ? 25 : 17;
            if (mcF < rec) return;
        }
        if (mcF < 0) mcFacing = PickFacing();
        if (mcIdle > 60) mcCombo = 0;
        mcType  = mcCombo;
        mcCombo = (mcCombo + 1) % 2;
        mcFromAng = mcAng; mcFromHand = mcHand;
        mcF = 0;
        mcIdle = 0;
    }

    void MacePose(int type, int f, out float ang, out float hand)
    {
        float rest = Deg(-78f);
        ang = rest; hand = 0f;

        if (type == 0)   // pancada de cima: sobe por trás do ombro, pausa, e desce com tudo
        {
            if (f < 10)      { float u = EDec((f + 1) / 10f); ang = Mix(mcFromAng, Deg(-155f), u); hand = Mix(mcFromHand, -6f, u); }
            else if (f < 12) { ang = Deg(-155f) - Deg(4f) * (f - 9); hand = -8f; }
            else if (f < 17) { float u = EAcc((f - 11) / 5f); ang = Mix(Deg(-163f), Deg(62f), u); hand = Mix(-8f, 12f, u); }
            else if (f < 25) { int k = f - 16; ang = Deg(62f) - Deg(14f) * (float)(Math.Exp(-0.35 * k) * Math.Sin(1.2 * k)); hand = 12f; }
            else             { float u = Sm((f - 24) / 14f); ang = Mix(Deg(62f), rest, u); hand = Mix(12f, 0f, u); }
        }
        else   // tacada lateral: leva o porrete atrás e varre a frente de baixo para cima
        {
            if (f < 8)       { float u = EDec((f + 1) / 8f); ang = Mix(mcFromAng, Deg(125f), u); hand = Mix(mcFromHand, -8f, u); }
            else if (f < 10) { ang = Deg(125f) + Deg(4f) * (f - 7); hand = -10f; }
            else if (f < 15) { float u = EAcc((f - 9) / 5f); ang = Mix(Deg(133f), Deg(-85f), u); hand = Mix(-10f, 14f, u); }
            else if (f < 17) { ang = Deg(-85f) - Deg(4f) * (f - 14); hand = 14f; }
            else             { float u = Sm((f - 16) / 14f); ang = Mix(Deg(-93f), rest, u); hand = Mix(14f, 0f, u); }
        }
    }

    void StepMace()
    {
        float rest = Deg(-78f);
        mcPrevAng = mcAng;

        if (mcF >= 0)
        {
            MacePose(mcType, mcF, out mcAng, out mcHand);
            int whooshF = (mcType == 0) ? 12 : 10;
            int hitF    = (mcType == 0) ? 16 : 13;
            int lastF   = (mcType == 0) ? 38 : 30;
            if (mcF == whooshF) PlayWhoosh();
            if (mcF == hitF) DoMaceHit();
            mcF++;
            if (mcF > lastF) { mcF = -1; mcIdle = 0; }
        }
        else
        {
            mcIdle++;
            mcPulse += 0.05f;
            mcAng = rest + Deg(2f) * (float)Math.Sin(mcPulse);
            mcHand = 0f;
        }

        if (mcShake > 0) mcShake--;
        StepParticles();
    }

    void DoMaceHit()
    {
        float ca = (float)Math.Cos(mcAng) * mcFacing, sa = (float)Math.Sin(mcAng);
        float x = mx + mcFacing * mcHand + ca * MC_LEN * 0.97f;
        float y = my + sa * MC_LEN * 0.97f;

        rings.Add(new Ring { x = x, y = y, age = 0 });
        HitBugsSeg(x - 24f, y, x + 24f, y, 40f);

        // lascas de madeira
        int shardCount = 16 + rnd.Next(10);
        for (int i = 0; i < shardCount; i++)
        {
            Shard sh = new Shard();
            double ang = rnd.NextDouble() * Math.PI * 2;
            double spd = 2.5 + rnd.NextDouble() * 9;
            sh.x = x + (float)(rnd.NextDouble() - 0.5) * 6;
            sh.y = y + (float)(rnd.NextDouble() - 0.5) * 6;
            sh.vx = (float)(Math.Cos(ang) * spd);
            sh.vy = (float)(Math.Sin(ang) * spd) - (float)(rnd.NextDouble() * 3);
            sh.rot = (float)(rnd.NextDouble() * Math.PI * 2);
            sh.rotV = (float)(rnd.NextDouble() - 0.5) * 0.55f;
            sh.len = 4f + (float)(rnd.NextDouble() * 11);
            sh.w   = 1.2f + (float)(rnd.NextDouble() * 2.2);
            sh.max = sh.life = 22 + rnd.Next(20);
            shards.Add(sh);
        }

        // poeira
        for (int i = 0; i < 22; i++)
        {
            Spark s = new Spark();
            double ang = rnd.NextDouble() * Math.PI * 2;
            double spd = 1 + rnd.NextDouble() * 5;
            s.x = x; s.y = y;
            s.vx = (float)(Math.Cos(ang) * spd);
            s.vy = (float)(Math.Sin(ang) * spd) - 0.5f;
            s.max = s.life = 10 + rnd.Next(12);
            sparks.Add(s);
        }

        // faíscas dos pregos batendo
        for (int i = 0; i < 9; i++)
        {
            Spark s = new Spark();
            double ang = rnd.NextDouble() * Math.PI * 2;
            double spd = 2 + rnd.NextDouble() * 7;
            s.x = x; s.y = y;
            s.vx = (float)(Math.Cos(ang) * spd);
            s.vy = (float)(Math.Sin(ang) * spd) - 1f;
            s.max = s.life = 10 + rnd.Next(10);
            s.hot = true; s.custom = true; s.drag = 0.92f; s.g = 0.3f;
            sparks.Add(s);
        }

        mcShake = 7;
        PlayThud();
    }

    // ===================================================================
    //  MODO CHICOTE DE CORRENTE  (elos de aço, peso com espinhos,
    //  barulho de corrente e rastro de faíscas)
    // ===================================================================
    int   chF = -1, chType = 0, chCombo = 0, chFacing = 1, chRattleCd = 0;
    bool  chSlammed = false;
    float chGx = 0.64f, chGy = -0.77f, chTipSpeed = 0f;

    void InitChain()
    {
        chFacing = PickFacing();
        chF = -1; chType = 0; chCombo = 0; chRattleCd = 0; chSlammed = false;
        float gx = chFacing * 0.64f, gy = -0.77f;
        chGx = gx; chGy = gy;
        float sx = mx + gx * CGRIP, sy = my + gy * CGRIP;
        for (int i = 0; i < CN; i++)
        {
            cpts[i].x = cpts[i].px = sx;
            cpts[i].y = cpts[i].py = sy + i * CSEG;
        }
    }

    void StartChainThrow()
    {
        if (chF >= 0 && chF <= 28) return;
        chFacing  = PickFacing();
        chType    = chCombo;
        chCombo   = (chCombo + 1) % 2;      // alterna: chicotada / giro
        chF       = 0;
        chSlammed = false;
    }

    // giro: o cabo dá duas voltas e depois solta a corrente
    static double ChainSpinAngle(int f)
    {
        double rest = -50.0 * Math.PI / 180.0;
        if (f < 0 || f >= 22) return rest;
        double u = f / 22.0;
        u = u * u * (3.0 - 2.0 * u);
        return rest + 4.0 * Math.PI * u;
    }

    void EmitChainSpark(float x, float y, float vx, float vy)
    {
        if (sparks.Count > 450) return;
        Spark s = new Spark();
        s.x = x; s.y = y; s.vx = vx; s.vy = vy;
        s.max = s.life = 16 + rnd.Next(22);
        s.hot = true; s.custom = true; s.drag = 0.95f; s.g = 0.28f;
        sparks.Add(s);
    }

    void StepChain()
    {
        double th;
        if (chF < 0) th = -50.0 * Math.PI / 180.0;
        else th = (chType == 0) ? HandleAngle(chF) : ChainSpinAngle(chF);
        float gx = (float)(chFacing * Math.Cos(th));
        float gy = (float)Math.Sin(th);
        chGx = gx; chGy = gy;

        cpts[0].x = cpts[0].px = mx + gx * CGRIP;
        cpts[0].y = cpts[0].py = my + gy * CGRIP;

        if (chF >= 0)
        {
            if (chType == 0)
            {
                if (chF == 9) PlayWhoosh();
                if (chF >= 11 && chF <= 17)
                {
                    for (int i = 1; i < CN; i++)
                    {
                        float k1 = (float)i / CN; k1 *= k1;
                        cpts[i].px -= chFacing * 5.0f * k1;
                    }
                }
            }
            else
            {
                if (chF == 1) PlayWhoosh();
                if (chF >= 2 && chF <= 19)
                {
                    // força tangencial: faz a corrente girar ao redor da mão
                    for (int i = 3; i < CN; i++)
                    {
                        float rx = cpts[i].x - mx, ry = cpts[i].y - my;
                        float rl = (float)Math.Sqrt(rx * rx + ry * ry);
                        if (rl < 1f) continue;
                        float k2 = (float)i / CN;
                        cpts[i].px -= (-ry / rl) * chFacing * 1.1f * k2;
                        cpts[i].py -= ( rx / rl) * chFacing * 1.1f * k2;
                    }
                }
                if (chF >= 20 && chF <= 22)
                {
                    for (int i = 1; i < CN; i++)
                    {
                        float k3 = (float)i / CN; k3 *= k3;
                        cpts[i].px -= chFacing * 5.0f * k3;
                    }
                }
            }
        }

        // integração (corrente pesada: gravidade alta, pouco amortecimento)
        for (int i = 1; i < CN; i++)
        {
            Pt pt = cpts[i];
            float ivx = (pt.x - pt.px) * 0.992f;
            float ivy = (pt.y - pt.py) * 0.992f + 0.55f;
            pt.px = pt.x; pt.py = pt.y;
            pt.x += ivx; pt.y += ivy;
        }

        // pequena rigidez na base
        for (int i = 1; i <= 3; i++)
        {
            float dxs, dys;
            if (i == 1) { dxs = gx; dys = gy; }
            else
            {
                dxs = cpts[i - 1].x - cpts[i - 2].x;
                dys = cpts[i - 1].y - cpts[i - 2].y;
                float l = (float)Math.Sqrt(dxs * dxs + dys * dys);
                if (l < 0.0001f) l = 1f;
                dxs /= l; dys /= l;
            }
            float tx = cpts[i - 1].x + dxs * CSEG, ty = cpts[i - 1].y + dys * CSEG;
            float kk = (i == 1) ? 0.45f : 0.16f * (1f - (i - 2) / 3f);
            cpts[i].x += (tx - cpts[i].x) * kk;
            cpts[i].y += (ty - cpts[i].y) * kk;
        }

        // restrições de distância entre elos
        for (int it = 0; it < 20; it++)
        {
            for (int i = 1; i < CN; i++)
            {
                Pt a = cpts[i - 1], b = cpts[i];
                float dx = b.x - a.x, dy = b.y - a.y;
                float d = (float)Math.Sqrt(dx * dx + dy * dy);
                if (d < 0.0001f) d = 0.0001f;
                float diff = (d - CSEG) / d;
                float wa = cinv[i - 1], wb = cinv[i], ws = wa + wb;
                a.x += dx * diff * wa / ws; a.y += dy * diff * wa / ws;
                b.x -= dx * diff * wb / ws; b.y -= dy * diff * wb / ws;
            }
        }

        // energia da corrente (para o barulho) e velocidade da ponta
        float en = 0f;
        for (int i = 1; i < CN; i++)
        {
            float ddx = cpts[i].x - cpts[i].px, ddy = cpts[i].y - cpts[i].py;
            en += (float)Math.Sqrt(ddx * ddx + ddy * ddy);
        }
        en /= (CN - 1);

        Pt tip = cpts[CN - 1];
        float tvx = tip.x - tip.px, tvy = tip.y - tip.py;
        chTipSpeed = (float)Math.Sqrt(tvx * tvx + tvy * tvy);
        if (chTipSpeed > 8f) HitBugsSeg(tip.px, tip.py, tip.x, tip.y, 13f);

        // faíscas: resquícios saindo da ponta e dos elos quando em movimento
        if (chTipSpeed > 7f)
        {
            int cnt = (int)(chTipSpeed / 5f);
            if (cnt > 6) cnt = 6;
            for (int c = 0; c < cnt; c++)
                EmitChainSpark(tip.x, tip.y, tvx * 0.18f + Rf(-2f, 2f), tvy * 0.18f + Rf(-2.5f, 1.5f));
        }
        if (chF >= 6 && en > 3f && rnd.NextDouble() < 0.6)
        {
            int li = CN / 2 + rnd.Next(CN / 2);
            float lvx = cpts[li].x - cpts[li].px, lvy = cpts[li].y - cpts[li].py;
            EmitChainSpark(cpts[li].x, cpts[li].y, lvx * 0.15f + Rf(-1.5f, 1.5f), lvy * 0.15f + Rf(-2f, 1f));
        }

        // movimento: mesmo "whoosh" do chicote normal (só em balançadas fortes)
        if (chRattleCd > 0) chRattleCd--;
        if (chF < 0 && chTipSpeed > 14f && chRattleCd <= 0)
        {
            PlayWhoosh();
            chRattleCd = 30;
        }

        // batida no fim do golpe
        if (chF >= 0)
        {
            float ex = tip.x - mx, ey = tip.y - my;
            float dist = (float)Math.Sqrt(ex * ex + ey * ey);
            float full = CGRIP + (CN - 1) * CSEG;
            bool ready;
            if (chType == 0) ready = (chF >= 14 && dist > 0.78f * full) || chF >= 24;
            else             ready = chF >= 23 && (dist > 0.78f * full || chF >= 28);
            if (!chSlammed && ready)
            {
                chSlammed = true;
                DoChainSlam(tip.x, tip.y);
            }
            chF++;
            if (chF > 36) chF = -1;
        }

        StepParticles();
    }

    void DoChainSlam(float x, float y)
    {
        HitBugsSeg(x - 20f, y, x + 20f, y, 34f);
        rings.Add(new Ring { x = x, y = y, age = 0 });
        for (int i = 0; i < 34; i++)
        {
            double ang = rnd.NextDouble() * Math.PI * 2;
            double spd = 3 + rnd.NextDouble() * 10;
            Spark s = new Spark();
            s.x = x; s.y = y;
            s.vx = (float)(Math.Cos(ang) * spd);
            s.vy = (float)(Math.Sin(ang) * spd) - 1.5f;
            s.max = s.life = 20 + rnd.Next(26);
            s.hot = true; s.custom = true; s.drag = 0.94f; s.g = 0.3f;
            sparks.Add(s);
        }
        PlayClank();
    }

    // ===================================================================
    //  MODO ARCO E FLECHA MEDIEVAL  (uma flecha de fogo por clique)
    //  A mira acompanha a direção em que o mouse foi movido por último.
    // ===================================================================
    int   bowF = -1;
    float bowAim, bowPull = 0f, bowFromPull = 0f;
    int   bowPrevMx, bowPrevMy;
    List<Arrow> arrows = new List<Arrow>();

    // camada grande só para o arco (as flechas atravessam a tela)
    IntPtr bowDc, bowDib;
    Bitmap bowBmp;
    Graphics gfxBow;
    int bowW, bowH;

    void EnsureBowDib()
    {
        if (gfxBow != null) return;
        try
        {
            Rectangle vs = SystemInformation.VirtualScreen;
            bowW = Math.Min(vs.Width, 3840);
            bowH = Math.Min(vs.Height, 2160);
            if (bowW < 800) bowW = 800;
            if (bowH < 600) bowH = 600;
            IntPtr sdc = GetDC(IntPtr.Zero);
            bowDc = CreateCompatibleDC(sdc);
            BITMAPINFOHEADER bi = new BITMAPINFOHEADER();
            bi.biSize = 40; bi.biWidth = bowW; bi.biHeight = -bowH;
            bi.biPlanes = 1; bi.biBitCount = 32; bi.biCompression = 0;
            IntPtr bits;
            bowDib = CreateDIBSection(sdc, ref bi, 0, out bits, IntPtr.Zero, 0);
            ReleaseDC(IntPtr.Zero, sdc);
            SelectObject(bowDc, bowDib);
            bowBmp = new Bitmap(bowW, bowH, bowW * 4, PixelFormat.Format32bppPArgb, bits);
            gfxBow = Graphics.FromImage(bowBmp);
            gfxBow.SmoothingMode = SmoothingMode.AntiAlias;
        }
        catch { gfxBow = null; }
    }

    void InitBow()
    {
        EnsureBowDib();
        int fc = PickFacing();
        bowAim = (fc == 1) ? Deg(-8f) : Deg(188f);
        bowPull = 0f; bowFromPull = 0f; bowF = -1;
        bowPrevMx = mx; bowPrevMy = my;
    }

    void StartBowShot()
    {
        if (bowF >= 0 && bowF < 9) return;     // quase pronto para outro tiro
        bowFromPull = bowPull;
        bowF = 0;
    }

    void StepBow()
    {
        // a mira segue a direção do movimento do mouse
        float mvx = mx - bowPrevMx, mvy = my - bowPrevMy;
        bowPrevMx = mx; bowPrevMy = my;
        if (mvx * mvx + mvy * mvy > 9f)
        {
            float target = (float)Math.Atan2(mvy, mvx);
            float diff = target - bowAim;
            while (diff > (float)Math.PI)  diff -= 2f * (float)Math.PI;
            while (diff < -(float)Math.PI) diff += 2f * (float)Math.PI;
            bowAim += diff * 0.18f;
        }

        if (bowF >= 0)
        {
            if (bowF < 6)
            {
                float u = EDec((bowF + 1) / 6f);
                bowPull = Mix(bowFromPull, 24f, u);
            }
            else
            {
                if (bowF == 6) FireArrow();
                int k = bowF - 6;                   // corda vibra depois de soltar
                bowPull = 24f * (float)(Math.Exp(-0.30 * k) * Math.Cos(1.9 * k));
            }
            bowF++;
            if (bowF > 16) { bowF = -1; bowPull = 0f; }

            // flecha encaixada já pegando fogo
            if (bowF >= 1 && bowF <= 6)
            {
                float ca = (float)Math.Cos(bowAim), sa = (float)Math.Sin(bowAim);
                float hn = 40f - bowPull;
                EmitArrowFire(mx + ca * hn, my + sa * hn, 0f, 0f, 1);
            }
        }

        StepArrows();
        StepParticles();
    }

    void FireArrow()
    {
        float ang = bowAim + Rf(-0.02f, 0.02f);
        float cf = -12f - bowPull + 26f;           // centro da flecha encaixada
        Arrow ar = new Arrow();
        ar.ux = (float)Math.Cos(ang); ar.uy = (float)Math.Sin(ang);
        ar.x = mx + (float)Math.Cos(bowAim) * cf;
        ar.y = my + (float)Math.Sin(bowAim) * cf;
        ar.vx = ar.ux * 25f; ar.vy = ar.uy * 25f;
        arrows.Add(ar);
        while (arrows.Count > 16) arrows.RemoveAt(0);
        PlayShoot();
    }

    void EmitArrowFire(float hx, float hy, float avx, float avy, int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (fires.Count > 400) break;
            FireParticle fp = new FireParticle();
            fp.x = hx; fp.y = hy;
            fp.vx = -avx * 0.12f + Rf(-0.7f, 0.7f);
            fp.vy = -avy * 0.12f + Rf(-1.4f, -0.2f);
            fp.max = fp.life = 12 + rnd.Next(10);
            fires.Add(fp);
        }
        if (sparks.Count < 450 && rnd.NextDouble() < 0.7)
        {
            Spark s = new Spark();
            s.x = hx; s.y = hy;
            s.vx = -avx * 0.06f + Rf(-0.8f, 0.8f);
            s.vy = -avy * 0.06f + Rf(-1.4f, 0.2f);
            s.max = s.life = 18 + rnd.Next(18);
            s.hot = true; s.custom = true; s.drag = 0.97f; s.g = 0.04f;
            sparks.Add(s);
        }
    }

    // true se o ponto está colado na borda inferior de tela (sem outro monitor abaixo)
    bool IsBottomEdge(float x, float y)
    {
        Point below = new Point((int)x, (int)(y + 8f));
        foreach (Screen s1 in Screen.AllScreens)
            if (s1.Bounds.Contains(below)) return false;
        Point here = new Point((int)x, (int)y);
        foreach (Screen s2 in Screen.AllScreens)
            if (s2.Bounds.Contains(here)) return true;
        return false;
    }

    void StepArrows()
    {
        Rectangle vs = SystemInformation.VirtualScreen;
        for (int i = arrows.Count - 1; i >= 0; i--)
        {
            Arrow ar = arrows[i];
            ar.age++;
            if (!ar.isStuck)
            {
                float ohx = ar.x + ar.ux * 26f, ohy = ar.y + ar.uy * 26f;
                ar.x += ar.vx; ar.y += ar.vy;
                ar.vy += 0.11f;                       // leve queda em arco
                float sp = (float)Math.Sqrt(ar.vx * ar.vx + ar.vy * ar.vy);
                if (sp > 0.01f) { ar.ux = ar.vx / sp; ar.uy = ar.vy / sp; }
                float hx = ar.x + ar.ux * 26f, hy = ar.y + ar.uy * 26f;
                EmitArrowFire(hx, hy, ar.vx, ar.vy, 2);
                HitBugsSeg(ohx, ohy, hx, hy, 11f);
                if (ar.vy > 0f && IsBottomEdge(hx, hy))
                {
                    ar.isStuck = true; ar.stuck = 0;
                    ar.vx = 0f; ar.vy = 0f;
                    DoArrowStick(hx, hy);
                }
                else if (ar.x < vs.Left - 300 || ar.x > vs.Right + 300 || ar.y < vs.Top - 500 || ar.age > 260)
                {
                    arrows.RemoveAt(i);
                }
            }
            else
            {
                ar.stuck++;
                EmitArrowFire(ar.x + ar.ux * 26f, ar.y + ar.uy * 26f, 0f, 0f, 1);
                if (ar.stuck > 110) arrows.RemoveAt(i);
            }
        }
    }

    // flecha cravada na borda da tela: explosão de fogo e brasas
    void DoArrowStick(float x, float y)
    {
        rings.Add(new Ring { x = x, y = y, age = 0 });
        for (int i = 0; i < 14; i++)
        {
            FireParticle fp = new FireParticle();
            fp.x = x; fp.y = y;
            fp.vx = Rf(-3.5f, 3.5f); fp.vy = Rf(-5f, -0.5f);
            fp.max = fp.life = 14 + rnd.Next(12);
            fires.Add(fp);
        }
        for (int i = 0; i < 10; i++)
        {
            Spark s = new Spark();
            s.x = x; s.y = y;
            s.vx = Rf(-5f, 5f); s.vy = Rf(-7f, -1f);
            s.max = s.life = 16 + rnd.Next(16);
            s.hot = true; s.custom = true; s.drag = 0.94f; s.g = 0.22f;
            sparks.Add(s);
        }
        PlayStick();
    }

    // ---------- Renderização do arco (camada grande, só a área usada) ----------
    static void Grow(ref float x0, ref float x1, ref float y0, ref float y1, float x, float y, float m)
    {
        if (x - m < x0) x0 = x - m;
        if (x + m > x1) x1 = x + m;
        if (y - m < y0) y0 = y - m;
        if (y + m > y1) y1 = y + m;
    }

    void RenderBow()
    {
        if (gfxBow == null)
        {
            // sem camada grande: desenha na pequena (flechas cortadas nas bordas)
            gfx.Clear(Color.Transparent);
            DrawBowScene(mx - W / 2f, my - H / 2f);
            Present();
            return;
        }

        float minX = mx - 90f, maxX = mx + 90f, minY = my - 90f, maxY = my + 90f;
        foreach (Arrow ar in arrows)      Grow(ref minX, ref maxX, ref minY, ref maxY, ar.x, ar.y, 80f);
        foreach (FireParticle fp in fires) Grow(ref minX, ref maxX, ref minY, ref maxY, fp.x, fp.y, 14f);
        foreach (Spark sp in sparks)       Grow(ref minX, ref maxX, ref minY, ref maxY, sp.x, sp.y, 16f);
        foreach (Ring rg in rings)         Grow(ref minX, ref maxX, ref minY, ref maxY, rg.x, rg.y, 120f);
        foreach (Bullet bu in bullets)     Grow(ref minX, ref maxX, ref minY, ref maxY, bu.x, bu.y, 60f);
        foreach (Ball ba in balls)         Grow(ref minX, ref maxX, ref minY, ref maxY, BallX(ba), BallY(ba), 70f);
        foreach (Burn bn in burns)         Grow(ref minX, ref maxX, ref minY, ref maxY, bn.x, bn.y, 50f);
        foreach (Dot dt in dots)           Grow(ref minX, ref maxX, ref minY, ref maxY, dt.x, dt.y, dt.r + 4f);
        if (mode != WeaponMode.Bow) { Grow(ref minX, ref maxX, ref minY, ref maxY, mx, my, 150f); }

        int left = (int)Math.Floor(minX), top = (int)Math.Floor(minY);
        int bw = (int)Math.Ceiling(maxX) - left, bh = (int)Math.Ceiling(maxY) - top;
        if (bw > bowW) { left = mx - bowW / 2; bw = bowW; }
        if (bh > bowH) { top  = my - bowH / 2; bh = bowH; }
        if (bw < 1) bw = 1;
        if (bh < 1) bh = 1;

        // limpa só a região que vai ser usada
        gfxBow.ResetClip();
        gfxBow.CompositingMode = CompositingMode.SourceCopy;
        using (SolidBrush clr = new SolidBrush(Color.FromArgb(0, 0, 0, 0)))
            gfxBow.FillRectangle(clr, 0, 0, bw, bh);
        gfxBow.CompositingMode = CompositingMode.SourceOver;

        Graphics saved = gfx;
        gfx = gfxBow;
        DrawBowScene(left, top);
        gfx = saved;

        SIZE size = new SIZE(); size.cx = bw; size.cy = bh;
        POINT src = new POINT();
        POINT dst = new POINT(); dst.x = left; dst.y = top;
        BLENDFUNCTION bf = new BLENDFUNCTION();
        bf.BlendOp = 0; bf.BlendFlags = 0; bf.SourceConstantAlpha = 255; bf.AlphaFormat = 1;
        UpdateLayeredWindow(Handle, IntPtr.Zero, ref dst, ref size, bowDc, ref src, 0, ref bf, 2);
    }

    void DrawBowScene(float ox, float oy)
    {
        if (mode == WeaponMode.Ak)       { DrawAkScene(ox, oy); return; }
        if (mode == WeaponMode.Catapult) { DrawCatScene(ox, oy); return; }
        if (mode == WeaponMode.Spray)    { DrawSprayScene(ox, oy); return; }
        DrawFireParticles(ox, oy);
        foreach (Arrow ar in arrows)
        {
            float tx = ar.x - ar.ux * 26f - ox, ty = ar.y - ar.uy * 26f - oy;
            float spd = ar.isStuck ? 0f : (float)Math.Sqrt(ar.vx * ar.vx + ar.vy * ar.vy);
            DrawArrow(tx, ty, ar.ux, ar.uy, 52f, spd);
        }
        DrawBowBody(ox, oy);
        DrawEffects(ox, oy);
    }

    // (tailX, tailY) = cauda da flecha; (ux, uy) = direção; chama na ponta
    void DrawArrow(float tailX, float tailY, float ux, float uy, float len, float spd)
    {
        float qx = -uy, qy = ux;
        float hxp = tailX + ux * len, hyp = tailY + uy * len;

        // haste
        pen.Color = Color.FromArgb(255, 36, 22, 10); pen.Width = 3.8f;
        gfx.DrawLine(pen, tailX, tailY, hxp, hyp);
        pen.Color = Color.FromArgb(255, 176, 130, 70); pen.Width = 2.2f;
        gfx.DrawLine(pen, tailX, tailY, hxp, hyp);

        // penas
        for (int k = 0; k < 3; k++)
        {
            float sd = 2f + k * 5f;
            float bx2 = tailX + ux * sd, by2 = tailY + uy * sd;
            pen.Color = (k == 1) ? Color.FromArgb(255, 226, 222, 210) : Color.FromArgb(255, 190, 48, 40);
            pen.Width = 2f;
            gfx.DrawLine(pen, bx2, by2, bx2 - ux * 4f + qx * 5f, by2 - uy * 4f + qy * 5f);
            gfx.DrawLine(pen, bx2, by2, bx2 - ux * 4f - qx * 5f, by2 - uy * 4f - qy * 5f);
        }

        // trapo embebido em piche atrás da ponta
        float rx = hxp - ux * 11f, ry = hyp - uy * 11f;
        pen.Color = Color.FromArgb(255, 40, 26, 16); pen.Width = 6f;
        gfx.DrawLine(pen, rx, ry, rx + ux * 6f, ry + uy * 6f);

        // ponta de aço
        PointF[] head = new PointF[] {
            new PointF(hxp + ux * 4f, hyp + uy * 4f),
            new PointF(hxp - ux * 5f + qx * 3.6f, hyp - uy * 5f + qy * 3.6f),
            new PointF(hxp - ux * 5f - qx * 3.6f, hyp - uy * 5f - qy * 3.6f) };
        using (SolidBrush steel = new SolidBrush(Color.FromArgb(255, 178, 184, 196)))
            gfx.FillPolygon(steel, head);
        pen.Color = Color.FromArgb(255, 30, 30, 36); pen.Width = 1f;
        gfx.DrawPolygon(pen, head);

        DrawFlame(hxp, hyp, ux, uy, spd);
    }

    void DrawFlame(float hx, float hy, float ux, float uy, float spd)
    {
        float fl = 14f + spd * 0.7f + Rf(0f, 5f);
        float qx = -uy, qy = ux;
        using (SolidBrush glow = new SolidBrush(Color.FromArgb(55, 255, 140, 30)))
            gfx.FillEllipse(glow, hx - 18f, hy - 18f, 36f, 36f);

        PointF[] outer = new PointF[] {
            new PointF(hx + ux * 3f + qx * 6f, hy + uy * 3f + qy * 6f),
            new PointF(hx - ux * fl, hy - uy * fl - 3f),
            new PointF(hx + ux * 3f - qx * 6f, hy + uy * 3f - qy * 6f) };
        using (SolidBrush fo = new SolidBrush(Color.FromArgb(215, 255, 110, 20)))
            gfx.FillPolygon(fo, outer);

        float fi = fl * 0.62f;
        PointF[] inner = new PointF[] {
            new PointF(hx + ux * 2f + qx * 3.2f, hy + uy * 2f + qy * 3.2f),
            new PointF(hx - ux * fi, hy - uy * fi - 2f),
            new PointF(hx + ux * 2f - qx * 3.2f, hy + uy * 2f - qy * 3.2f) };
        using (SolidBrush fi2 = new SolidBrush(Color.FromArgb(235, 255, 220, 90)))
            gfx.FillPolygon(fi2, inner);

        using (SolidBrush core = new SolidBrush(Color.FromArgb(240, 255, 255, 230)))
            gfx.FillEllipse(core, hx - ux * 3f - 2.4f, hy - uy * 3f - 2.4f, 4.8f, 4.8f);
    }

    void DrawBowBody(float ox, float oy)
    {
        float ca = (float)Math.Cos(bowAim), sa = (float)Math.Sin(bowAim);
        float px = -sa, py = ca;
        float gx = mx - ox, gy = my - oy;            // empunhadura = cursor
        float pull = bowPull;
        float tipF = -0.22f * pull;
        float R = 44f - pull * 0.12f;

        PointF[] limb = new PointF[21];
        for (int i = 0; i <= 20; i++)
        {
            float t = (i - 10) / 10f;
            float fwd = t * t * (tipF - 12f);
            limb[i] = new PointF(gx + px * R * t + ca * fwd, gy + py * R * t + sa * fwd);
        }
        PointF nock = new PointF(gx - ca * (12f + pull), gy - sa * (12f + pull));

        // corda
        pen.Color = Color.FromArgb(235, 226, 220, 196); pen.Width = 1.6f;
        gfx.DrawLine(pen, limb[0], nock);
        gfx.DrawLine(pen, nock, limb[20]);

        // flecha encaixada
        if (bowF >= 1 && bowF <= 6) DrawArrow(nock.X, nock.Y, ca, sa, 52f, 0f);

        // braços do arco
        for (int i = 1; i <= 20; i++)
        {
            float t = Math.Abs((i - 10.5f) / 10f);
            float w = 3.2f + 4.2f * (1f - t);
            pen.Color = Color.FromArgb(255, 36, 20, 8); pen.Width = w + 2.2f;
            gfx.DrawLine(pen, limb[i - 1], limb[i]);
        }
        for (int i = 1; i <= 20; i++)
        {
            float t = Math.Abs((i - 10.5f) / 10f);
            float w = 3.2f + 4.2f * (1f - t);
            pen.Color = Lerp(Color.FromArgb(120, 72, 32), Color.FromArgb(172, 118, 62), t);
            pen.Width = w;
            gfx.DrawLine(pen, limb[i - 1], limb[i]);
        }

        // ponteiras de ferro
        using (SolidBrush iron = new SolidBrush(Color.FromArgb(255, 70, 70, 80)))
        {
            gfx.FillEllipse(iron, limb[0].X - 3f, limb[0].Y - 3f, 6f, 6f);
            gfx.FillEllipse(iron, limb[20].X - 3f, limb[20].Y - 3f, 6f, 6f);
        }

        // empunhadura de couro
        pen.Color = Color.FromArgb(255, 56, 32, 16); pen.Width = 8f;
        gfx.DrawLine(pen, gx - px * 8f, gy - py * 8f, gx + px * 8f, gy + py * 8f);
        for (int b = 0; b < 3; b++)
        {
            float off = -5f + b * 5f;
            pen.Color = (b == 1) ? Color.FromArgb(255, 206, 160, 70) : Color.FromArgb(255, 30, 18, 8);
            pen.Width = 1.8f;
            gfx.DrawLine(pen, gx + px * off - ca * 4f, gy + py * off - sa * 4f, gx + px * off + ca * 4f, gy + py * off + sa * 4f);
        }
        gfx.FillEllipse(Brushes.White, gx - 2f, gy - 2f, 4f, 4f);
        gfx.DrawEllipse(Pens.Black, gx - 2f, gy - 2f, 4f, 4f);
    }

    // ===================================================================
    //  BLOQUEIO DE CLIQUES: com a arma ligada, o clique do mouse só dispara
    //  a habilidade e NÃO chega na tela/programa por baixo (vale em qualquer tela).
    //  Segurar SHIFT deixa o clique passar normalmente (saída de emergência).
    // ===================================================================
    delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData; public uint flags; public uint time; public IntPtr dwExtraInfo; }

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)] static extern IntPtr GetModuleHandle(string name);
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(POINT p);
    [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);

    LowLevelMouseProc mouseProc;
    IntPtr hookId = IntPtr.Zero;
    volatile bool clickPending = false;
    bool passLeft = false, passRight = false;
    volatile bool heldLeft = false, heldRight = false;
    uint ownPid = 0;

    void InstallMouseHook()
    {
        if (hookId != IntPtr.Zero) return;
        clickPending = false; passLeft = false; passRight = false; heldLeft = false; heldRight = false;
        try
        {
            ownPid = (uint)Process.GetCurrentProcess().Id;
            mouseProc = MouseHookProc;
            using (Process cur = Process.GetCurrentProcess())
            using (ProcessModule mod = cur.MainModule)
                hookId = SetWindowsHookEx(14, mouseProc, GetModuleHandle(mod.ModuleName), 0);
        }
        catch { hookId = IntPtr.Zero; }
    }

    void RemoveMouseHook()
    {
        if (hookId != IntPtr.Zero)
        {
            try { UnhookWindowsHookEx(hookId); } catch { }
            hookId = IntPtr.Zero;
        }
        clickPending = false; heldLeft = false; heldRight = false;
    }

    // true se o clique caiu numa janela deste programa (ex.: menu da bandeja)
    bool IsOwnWindowAt(IntPtr lParam)
    {
        try
        {
            MSLLHOOKSTRUCT ms = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
            IntPtr hw = WindowFromPoint(ms.pt);
            if (hw == IntPtr.Zero || hw == Handle) return false;
            if (Control.FromHandle(hw) is BugForm) return false;     // bug nunca bloqueia a arma
            if (GetAncestor(hw, 2) == Handle) return false;      // a camada da arma nunca conta
            uint pid;
            GetWindowThreadProcessId(hw, out pid);
            return pid == ownPid;
        }
        catch { return false; }
    }

    IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && enabled)
        {
            int msg = wParam.ToInt32();
            if (msg == 0x0201 || msg == 0x0204)           // botão esquerdo / direito apertado
            {
                bool shift = (GetAsyncKeyState(0x10) & 0x8000) != 0;
                bool pass = shift || IsOwnWindowAt(lParam);
                if (msg == 0x0201) passLeft = pass; else passRight = pass;
                if (!pass)
                {
                    if (msg == 0x0201) heldLeft = true; else heldRight = true;
                    clickPending = true;                   // dispara a habilidade
                    return (IntPtr)1;                      // e engole o clique
                }
            }
            else if (msg == 0x0202 || msg == 0x0205)      // soltou
            {
                bool pass = (msg == 0x0202) ? passLeft : passRight;
                if (msg == 0x0202) { passLeft = false; heldLeft = false; } else { passRight = false; heldRight = false; }
                if (!pass) return (IntPtr)1;
            }
        }
        return CallNextHookEx(hookId, nCode, wParam, lParam);
    }

    void StepParticles()
    {
        for (int s = sparks.Count - 1; s >= 0; s--)
        {
            Spark sp = sparks[s];
            sp.x += sp.vx; sp.y += sp.vy;
            if (sp.custom)
            {
                sp.vx *= sp.drag;
                sp.vy  = sp.vy * sp.drag + sp.g;
            }
            else
            {
                sp.vx *= sp.isFire ? 0.88f : 0.92f;
                sp.vy  = sp.vy * (sp.isFire ? 0.88f : 0.92f) + (sp.isFire ? 0.15f : 0.3f);
            }
            sp.life--;
            if (sp.life <= 0) sparks.RemoveAt(s);
        }
        for (int r = rings.Count - 1; r >= 0; r--)
        {
            rings[r].age++;
            if (rings[r].age > 14) rings.RemoveAt(r);
        }
        for (int f = fires.Count - 1; f >= 0; f--)
        {
            FireParticle fp = fires[f];
            fp.x += fp.vx; fp.y += fp.vy;
            fp.vx *= 0.90f;
            fp.vy  = fp.vy * 0.90f - 0.1f;
            fp.life--;
            if (fp.life <= 0) fires.RemoveAt(f);
        }
        for (int s = shards.Count - 1; s >= 0; s--)
        {
            Shard sh = shards[s];
            sh.x   += sh.vx;
            sh.y   += sh.vy;
            sh.vx  *= 0.93f;
            sh.vy   = sh.vy * 0.93f + 0.45f;
            sh.rot += sh.rotV;
            sh.life--;
            if (sh.life <= 0) shards.RemoveAt(s);
        }
    }

    // ===================================================================
    //  RENDERIZAÇÃO
    // ===================================================================
    void Render()
    {
        if (UsesBigLayer()) { RenderBow(); return; }
        gfx.Clear(Color.Transparent);
        float ox = mx - W / 2f, oy = my - H / 2f;

        if (mode == WeaponMode.Whip)
        {
            // rastro de fogo na ponta quando rápido
            if (tipSpeed > 10f && tipHist.Count > 1)
            {
                for (int j = 1; j < tipHist.Count; j++)
                {
                    float f = (float)j / tipHist.Count;
                    int r = 255, g = Cl(255 * f), b2 = 0;
                    pen.Color = Color.FromArgb(Cl(160 * f), r, g, b2);
                    pen.Width = 1f + 3f * f;
                    gfx.DrawLine(pen, tipHist[j - 1].X - ox, tipHist[j - 1].Y - oy,
                                      tipHist[j].X - ox,     tipHist[j].Y - oy);
                }
            }
            DrawFireParticles(ox, oy);
            DrawWhip(ox, oy);
            DrawGrip(ox, oy);
        }
        else if (mode == WeaponMode.Mace)
        {
            DrawMace(ox, oy);
            DrawShards(ox, oy);
        }
        else if (mode == WeaponMode.Sword)
        {
            DrawSword(ox, oy);
        }
        else
        {
            DrawChain(ox, oy);
        }

        DrawEffects(ox, oy);
        Present();
    }

    // ponto de clique real (usado quando a "mão" da arma se desloca do cursor)
    void DrawHotspot(float ox, float oy)
    {
        float x = mx - ox, y = my - oy;
        gfx.FillEllipse(Brushes.White, x - 2.5f, y - 2.5f, 5f, 5f);
        gfx.DrawEllipse(Pens.Black, x - 2.5f, y - 2.5f, 5f, 5f);
    }

    // ---------- Espada ----------
    void DrawSword(float ox, float oy)
    {
        // rastro em crescente (arco do corte)
        for (int j = 1; j < trails.Count; j++)
        {
            Trail ta = trails[j - 1], tb = trails[j];
            if (tb.start) continue;
            float al = 1f - tb.age / 10f;
            if (al <= 0f) continue;
            float kq = 0.35f + 0.65f * j / trails.Count;
            PointF[] quad = new PointF[] {
                new PointF(ta.tx - ox, ta.ty - oy), new PointF(tb.tx - ox, tb.ty - oy),
                new PointF(tb.bx - ox, tb.by - oy), new PointF(ta.bx - ox, ta.by - oy) };
            using (SolidBrush br = new SolidBrush(Color.FromArgb(Cl(200 * al * kq), 190, 220, 255)))
                gfx.FillPolygon(br, quad);
            pen.Color = Color.FromArgb(Cl(255 * al * kq), 245, 250, 255);
            pen.Width = 2f;
            gfx.DrawLine(pen, ta.tx - ox, ta.ty - oy, tb.tx - ox, tb.ty - oy);
        }

        float ca = (float)Math.Cos(swAng) * swFacing, sa = (float)Math.Sin(swAng);
        float nx = -sa, ny = ca;
        float hx = mx + swFacing * swHand - ox, hy = my - oy;
        float L = SWORD_LEN * swReach;
        float tipX = hx + ca * L, tipY = hy + sa * L;

        // brilho durante o corte
        if (swStrikeNow)
        {
            pen.Color = Color.FromArgb(70, 200, 225, 255);
            pen.Width = 11f;
            gfx.DrawLine(pen, hx, hy, tipX, tipY);
        }

        // --- lâmina ---
        PointF[] blade = new PointF[] {
            new PointF(hx + nx * 3.6f, hy + ny * 3.6f),
            new PointF(hx + ca * L * 0.55f + nx * 2.9f, hy + sa * L * 0.55f + ny * 2.9f),
            new PointF(hx + ca * L * 0.90f + nx * 1.8f, hy + sa * L * 0.90f + ny * 1.8f),
            new PointF(tipX, tipY),
            new PointF(hx + ca * L * 0.90f - nx * 1.8f, hy + sa * L * 0.90f - ny * 1.8f),
            new PointF(hx + ca * L * 0.55f - nx * 2.9f, hy + sa * L * 0.55f - ny * 2.9f),
            new PointF(hx - nx * 3.6f, hy - ny * 3.6f) };
        using (SolidBrush sb = new SolidBrush(Color.FromArgb(255, 176, 184, 196)))
            gfx.FillPolygon(sb, blade);
        // metade sombreada (volume)
        PointF[] half = new PointF[] {
            new PointF(hx, hy),
            new PointF(hx + ca * L * 0.55f - nx * 2.9f, hy + sa * L * 0.55f - ny * 2.9f),
            new PointF(hx + ca * L * 0.90f - nx * 1.8f, hy + sa * L * 0.90f - ny * 1.8f),
            new PointF(tipX, tipY) };
        using (SolidBrush sh2 = new SolidBrush(Color.FromArgb(150, 92, 100, 114)))
            gfx.FillPolygon(sh2, half);
        pen.Color = Color.FromArgb(255, 22, 22, 28); pen.Width = 1.2f;
        gfx.DrawPolygon(pen, blade);
        // fio brilhante
        pen.Color = Color.FromArgb(235, 245, 250, 255); pen.Width = 1f;
        gfx.DrawLine(pen, hx + nx * 3.1f + ca * 6f, hy + ny * 3.1f + sa * 6f, tipX, tipY);

        // --- guarda ---
        float gx2 = hx + ca * 4f, gy2 = hy + sa * 4f;
        pen.Color = Color.FromArgb(255, 50, 48, 55); pen.Width = 6f;
        gfx.DrawLine(pen, gx2 + nx * GUARD_W, gy2 + ny * GUARD_W, gx2 - nx * GUARD_W, gy2 - ny * GUARD_W);
        pen.Color = Color.FromArgb(255, 176, 150, 70); pen.Width = 2.4f;
        gfx.DrawLine(pen, gx2 + nx * (GUARD_W - 2f), gy2 + ny * (GUARD_W - 2f), gx2 - nx * (GUARD_W - 2f), gy2 - ny * (GUARD_W - 2f));
        using (SolidBrush gold = new SolidBrush(Color.FromArgb(255, 200, 172, 80)))
        {
            gfx.FillEllipse(gold, gx2 + nx * GUARD_W - 4f, gy2 + ny * GUARD_W - 4f, 8f, 8f);
            gfx.FillEllipse(gold, gx2 - nx * GUARD_W - 4f, gy2 - ny * GUARD_W - 4f, 8f, 8f);
        }

        // --- cabo ---
        float g1x = hx - ca * 4f, g1y = hy - sa * 4f;
        float g2x = hx - ca * (4f + GRIP_LEN), g2y = hy - sa * (4f + GRIP_LEN);
        pen.Color = Color.FromArgb(255, 50, 30, 15); pen.Width = 6.5f;
        gfx.DrawLine(pen, g1x, g1y, g2x, g2y);
        for (int w = 0; w < 5; w++)
        {
            float fr = w / 4f;
            float wx = g1x + (g2x - g1x) * fr, wy = g1y + (g2y - g1y) * fr;
            pen.Color = Color.FromArgb(255, 200, 165, 55); pen.Width = 1.8f;
            gfx.DrawLine(pen, wx + nx * 3.3f, wy + ny * 3.3f, wx - nx * 3.3f, wy - ny * 3.3f);
        }
        using (SolidBrush pom = new SolidBrush(Color.FromArgb(255, 176, 160, 64)))
            gfx.FillEllipse(pom, g2x - 6f, g2y - 6f, 12f, 12f);
        pen.Color = Color.FromArgb(255, 50, 44, 20); pen.Width = 1.5f;
        gfx.DrawEllipse(pen, g2x - 6f, g2y - 6f, 12f, 12f);

        if (Math.Abs(swHand) > 1f) DrawHotspot(ox, oy);
    }

    // ---------- Porrete com pregos ----------
    static float McHalfW(float s)
    {
        if (s <= 0f) return MC_W[0];
        for (int i = 1; i < MC_S.Length; i++)
        {
            if (s <= MC_S[i])
            {
                float u = (s - MC_S[i - 1]) / (MC_S[i] - MC_S[i - 1]);
                return MC_W[i - 1] + (MC_W[i] - MC_W[i - 1]) * u;
            }
        }
        return MC_W[MC_W.Length - 1];
    }

    void DrawMace(float ox, float oy)
    {
        float ca = (float)Math.Cos(mcAng) * mcFacing, sa = (float)Math.Sin(mcAng);
        float nx = -sa, ny = ca;
        float shx = 0f, shy = 0f;
        if (mcShake > 0) { shx = Rf(-3f, 3f) * mcShake / 7f; shy = Rf(-3f, 3f) * mcShake / 7f; }
        float hx = mx + mcFacing * mcHand - ox + shx;
        float hy = my - oy + shy;
        float L = MC_LEN;

        // borrão de movimento da cabeça quando o golpe é rápido
        float dAng = mcAng - mcPrevAng;
        if (Math.Abs(dAng) > Deg(7f))
        {
            for (int gh = 1; gh <= 3; gh++)
            {
                float ga = mcAng - dAng * gh * 0.45f;
                float gc = (float)Math.Cos(ga) * mcFacing, gs = (float)Math.Sin(ga);
                pen.Color = Color.FromArgb(Cl(70 - gh * 18), 235, 225, 205);
                pen.Width = 16f - gh * 2f;
                gfx.DrawLine(pen, hx + gc * L * 0.55f, hy + gs * L * 0.55f, hx + gc * L * 0.98f, hy + gs * L * 0.98f);
            }
        }

        // corpo de madeira (fino na mão, grosso na cabeça)
        PointF[] poly = new PointF[14];
        for (int i = 0; i < 7; i++)
        {
            float bx = hx + ca * L * MC_S[i], by = hy + sa * L * MC_S[i];
            poly[i]      = new PointF(bx + nx * MC_W[i], by + ny * MC_W[i]);
            poly[13 - i] = new PointF(bx - nx * MC_W[i], by - ny * MC_W[i]);
        }
        using (SolidBrush wood = new SolidBrush(Color.FromArgb(255, 118, 74, 34)))
            gfx.FillPolygon(wood, poly);
        pen.Color = Color.FromArgb(255, 42, 24, 10); pen.Width = 2f;
        gfx.DrawPolygon(pen, poly);

        float cx = hx + ca * L * 0.985f, cy = hy + sa * L * 0.985f;
        using (SolidBrush wood2 = new SolidBrush(Color.FromArgb(255, 118, 74, 34)))
            gfx.FillEllipse(wood2, cx - 6.2f, cy - 6.2f, 12.4f, 12.4f);
        float dirDeg = (float)(Math.Atan2(sa, ca) * 180.0 / Math.PI);
        gfx.DrawArc(pen, cx - 6.2f, cy - 6.2f, 12.4f, 12.4f, dirDeg - 90f, 180f);

        // veios da madeira e nó
        pen.Width = 1.1f;
        for (int side = -1; side <= 1; side += 2)
        {
            pen.Color = Color.FromArgb(110, 70, 40, 16);
            float o1 = side * 0.35f;
            float s0 = 0.30f, s1 = 0.92f;
            float w0 = McHalfW(s0) * o1, w1 = McHalfW(s1) * o1;
            gfx.DrawLine(pen, hx + ca * L * s0 + nx * w0, hy + sa * L * s0 + ny * w0,
                              hx + ca * L * s1 + nx * w1, hy + sa * L * s1 + ny * w1);
        }
        using (SolidBrush kn = new SolidBrush(Color.FromArgb(140, 60, 34, 14)))
            gfx.FillEllipse(kn, hx + ca * L * 0.55f - 2.2f + nx * 1.5f, hy + sa * L * 0.55f - 2.2f + ny * 1.5f, 4.4f, 3.2f);

        // empunhadura enrolada
        pen.Color = Color.FromArgb(255, 58, 34, 18); pen.Width = 5.6f;
        gfx.DrawLine(pen, hx + ca * L * 0.02f, hy + sa * L * 0.02f, hx + ca * L * 0.27f, hy + sa * L * 0.27f);
        for (int bnd = 0; bnd < 6; bnd++)
        {
            float sb2 = 0.04f + bnd * 0.043f;
            float wx = hx + ca * L * sb2, wy = hy + sa * L * sb2;
            pen.Color = (bnd % 2 == 0) ? Color.FromArgb(255, 206, 160, 70) : Color.FromArgb(255, 30, 18, 8);
            pen.Width = 2f;
            gfx.DrawLine(pen, wx + nx * 3.4f, wy + ny * 3.4f, wx - nx * 3.4f, wy - ny * 3.4f);
        }
        using (SolidBrush iron = new SolidBrush(Color.FromArgb(255, 74, 74, 84)))
            gfx.FillEllipse(iron, hx - 4.4f, hy - 4.4f, 8.8f, 8.8f);
        gfx.DrawEllipse(Pens.Black, hx - 4.4f, hy - 4.4f, 8.8f, 8.8f);

        // pregos cravados na cabeça
        for (int i = 0; i < NAIL_S.Length; i++)
        {
            float s = NAIL_S[i]; int sd = NAIL_SIDE[i]; float tl = NAIL_TILT[i];
            float w = McHalfW(s);
            float bxp = hx + ca * L * s + nx * sd * w, byp = hy + sa * L * s + ny * sd * w;
            float dxn = nx * sd * (float)Math.Cos(tl) + ca * (float)Math.Sin(tl);
            float dyn = ny * sd * (float)Math.Cos(tl) + sa * (float)Math.Sin(tl);
            float nl = 9f + (i % 3) * 1.5f;
            float txn = bxp + dxn * nl, tyn = byp + dyn * nl;
            pen.Color = Color.FromArgb(255, 30, 30, 36); pen.Width = 3.6f;
            gfx.DrawLine(pen, bxp, byp, txn, tyn);
            pen.Color = Color.FromArgb(255, 150, 152, 162); pen.Width = 1.9f;
            gfx.DrawLine(pen, bxp, byp, txn, tyn);
            pen.Color = Color.FromArgb(255, 70, 66, 70); pen.Width = 1.9f;
            gfx.DrawLine(pen, bxp + dxn * nl * 0.65f, byp + dyn * nl * 0.65f, txn, tyn);
            using (SolidBrush hd = new SolidBrush(Color.FromArgb(255, 120, 84, 60)))
                gfx.FillEllipse(hd, bxp - 2.2f, byp - 2.2f, 4.4f, 4.4f);
        }
        // prego na ponta
        float tx0 = cx + ca * 5f, ty0 = cy + sa * 5f;
        pen.Color = Color.FromArgb(255, 30, 30, 36); pen.Width = 3.6f;
        gfx.DrawLine(pen, tx0, ty0, tx0 + ca * 8f, ty0 + sa * 8f);
        pen.Color = Color.FromArgb(255, 150, 152, 162); pen.Width = 1.9f;
        gfx.DrawLine(pen, tx0, ty0, tx0 + ca * 8f, ty0 + sa * 8f);

        if (Math.Abs(mcHand) > 1f) DrawHotspot(ox, oy);
    }

    // ---------- Chicote de corrente ----------
    void DrawChain(float ox, float oy)
    {
        PointF[] q = new PointF[CN];
        for (int i = 0; i < CN; i++) q[i] = new PointF(cpts[i].x - ox, cpts[i].y - oy);

        // cabo de aço com couro
        float bx = mx - ox, by = my - oy;
        float ex = q[0].X, ey = q[0].Y;
        float hnx = -chGy, hny = chGx;
        pen.Color = Color.FromArgb(22, 22, 28); pen.Width = 11f;
        gfx.DrawLine(pen, bx, by, ex, ey);
        pen.Color = Color.FromArgb(70, 56, 46); pen.Width = 8.5f;
        gfx.DrawLine(pen, bx, by, ex, ey);
        for (int k = 1; k <= 5; k++)
        {
            float fr = k / 6f;
            float cx = bx + (ex - bx) * fr, cy = by + (ey - by) * fr;
            pen.Color = (k % 2 == 0) ? Color.FromArgb(170, 176, 190) : Color.FromArgb(30, 24, 20);
            pen.Width = 2f;
            gfx.DrawLine(pen, cx - hnx * 4.5f, cy - hny * 4.5f, cx + hnx * 4.5f, cy + hny * 4.5f);
        }
        using (SolidBrush steel = new SolidBrush(Color.FromArgb(150, 156, 168)))
        {
            gfx.FillEllipse(steel, ex - 5f, ey - 5f, 10f, 10f);
            gfx.DrawEllipse(Pens.Black, ex - 5f, ey - 5f, 10f, 10f);
            gfx.FillEllipse(steel, bx - 5.5f, by - 5.5f, 11f, 11f);
        }
        gfx.DrawEllipse(Pens.Black, bx - 5.5f, by - 5.5f, 11f, 11f);
        gfx.FillEllipse(Brushes.White, bx - 1.8f, by - 1.8f, 3.6f, 3.6f);

        // elos: alternam entre elo de frente (anel) e elo de perfil
        for (int i = 1; i < CN; i++)
        {
            float dx = q[i].X - q[i - 1].X, dy = q[i].Y - q[i - 1].Y;
            float cx = (q[i].X + q[i - 1].X) / 2f, cy = (q[i].Y + q[i - 1].Y) / 2f;
            float ln = (float)Math.Sqrt(dx * dx + dy * dy);
            if (ln < 0.01f) continue;
            float ux = dx / ln, uy = dy / ln;
            if (i % 2 == 0)
            {
                float angd = (float)(Math.Atan2(dy, dx) * 180.0 / Math.PI);
                gfx.TranslateTransform(cx, cy);
                gfx.RotateTransform(angd);
                pen.Color = Color.FromArgb(255, 34, 36, 44); pen.Width = 4.4f;
                gfx.DrawEllipse(pen, -7f, -3.6f, 14f, 7.2f);
                pen.Color = Color.FromArgb(255, 152, 158, 170); pen.Width = 2.2f;
                gfx.DrawEllipse(pen, -7f, -3.6f, 14f, 7.2f);
                pen.Color = Color.FromArgb(210, 232, 238, 248); pen.Width = 0.9f;
                gfx.DrawArc(pen, -6f, -3.2f, 12f, 6.4f, 200f, 120f);
                gfx.ResetTransform();
            }
            else
            {
                pen.Color = Color.FromArgb(255, 34, 36, 44); pen.Width = 4.8f;
                gfx.DrawLine(pen, cx - ux * 4.5f, cy - uy * 4.5f, cx + ux * 4.5f, cy + uy * 4.5f);
                pen.Color = Color.FromArgb(255, 120, 126, 138); pen.Width = 2.6f;
                gfx.DrawLine(pen, cx - ux * 4.5f, cy - uy * 4.5f, cx + ux * 4.5f, cy + uy * 4.5f);
            }
        }

        // peso com espinhos na ponta
        float tx = q[CN - 1].X, ty = q[CN - 1].Y;
        if (chTipSpeed > 8f)
        {
            using (SolidBrush glow = new SolidBrush(Color.FromArgb(Cl(30 + chTipSpeed * 4f), 255, 150, 40)))
                gfx.FillEllipse(glow, tx - 13f, ty - 13f, 26f, 26f);
        }
        double ba = Math.Atan2(q[CN - 1].Y - q[CN - 2].Y, q[CN - 1].X - q[CN - 2].X);
        for (int s = 0; s < 8; s++)
        {
            double a = ba + s * Math.PI / 4.0;
            float c1 = (float)Math.Cos(a), s1 = (float)Math.Sin(a);
            pen.Color = Color.FromArgb(255, 24, 24, 30); pen.Width = 3.8f;
            gfx.DrawLine(pen, tx + c1 * 6f, ty + s1 * 6f, tx + c1 * 12.5f, ty + s1 * 12.5f);
            pen.Color = Color.FromArgb(255, 150, 154, 166); pen.Width = 1.9f;
            gfx.DrawLine(pen, tx + c1 * 6f, ty + s1 * 6f, tx + c1 * 11.5f, ty + s1 * 11.5f);
        }
        using (SolidBrush ball = new SolidBrush(Color.FromArgb(255, 56, 58, 68)))
            gfx.FillEllipse(ball, tx - 7.5f, ty - 7.5f, 15f, 15f);
        pen.Color = Color.FromArgb(255, 20, 20, 26); pen.Width = 1.6f;
        gfx.DrawEllipse(pen, tx - 7.5f, ty - 7.5f, 15f, 15f);
        using (SolidBrush hl = new SolidBrush(Color.FromArgb(120, 210, 214, 226)))
            gfx.FillEllipse(hl, tx - 4f, ty - 5.2f, 5f, 3.4f);
    }

    // ---------- Chicote ----------
    void DrawWhip(float ox, float oy)
    {
        PointF[] q = new PointF[N];
        for (int i = 0; i < N; i++) q[i] = new PointF(pts[i].x - ox, pts[i].y - oy);
        int body = N - 5;

        // contorno
        for (int i = 1; i < body; i++)
        {
            pen.Color = Color.FromArgb(255, 28, 16, 8);
            pen.Width = WidthAt(i) + 1.8f;
            gfx.DrawLine(pen, q[i - 1], q[i]);
        }
        // couro
        Color c1 = Color.FromArgb(120, 72, 34), c2 = Color.FromArgb(170, 114, 62);
        for (int i = 1; i < body; i++)
        {
            pen.Color = Lerp(c1, c2, (float)i / body);
            pen.Width = WidthAt(i);
            gfx.DrawLine(pen, q[i - 1], q[i]);
        }
        // hachuras
        for (int i = 2; i < body; i++)
        {
            float tx = q[i].X - q[i-1].X, ty = q[i].Y - q[i-1].Y;
            float l = (float)Math.Sqrt(tx*tx + ty*ty); if (l < 0.01f) continue;
            tx /= l; ty /= l;
            float nx = -ty, ny = tx;
            float hw = WidthAt(i) * 0.5f;
            float cx = (q[i].X + q[i-1].X) / 2f, cy = (q[i].Y + q[i-1].Y) / 2f;
            pen.Color = (i % 2 == 0) ? Color.FromArgb(170, 40, 22, 10) : Color.FromArgb(140, 222, 172, 110);
            pen.Width = 1.0f;
            gfx.DrawLine(pen, cx - nx*hw - tx*2f, cy - ny*hw - ty*2f,
                              cx + nx*hw + tx*2f, cy + ny*hw + ty*2f);
        }
        // fall + estalo
        pen.Color = Color.FromArgb(226, 206, 168); pen.Width = 1.3f;
        for (int i = body; i < N; i++) gfx.DrawLine(pen, q[i-1], q[i]);

        // ponta desfiada
        float dx = q[N-1].X - q[N-2].X, dy = q[N-1].Y - q[N-2].Y;
        double baseAng = Math.Atan2(dy, dx);
        for (int k = -1; k <= 1; k++)
        {
            double a = baseAng + k * 0.35 + (rnd.NextDouble() - 0.5) * 0.3;
            float len = 5f + (float)rnd.NextDouble() * 4f;
            pen.Color = Color.FromArgb(200, 232, 214, 180); pen.Width = 1f;
            gfx.DrawLine(pen, q[N-1].X, q[N-1].Y,
                (float)(q[N-1].X + Math.Cos(a)*len), (float)(q[N-1].Y + Math.Sin(a)*len));
        }
    }

    void DrawGrip(float ox, float oy)
    {
        float bx = mx - ox, by = my - oy;
        float ex = pts[0].x - ox, ey = pts[0].y - oy;
        float nx = -gripY, ny = gripX;

        pen.Color = Color.FromArgb(28, 16, 8); pen.Width = 11f;
        gfx.DrawLine(pen, bx, by, ex, ey);
        pen.Color = Color.FromArgb(92, 48, 24); pen.Width = 8.5f;
        gfx.DrawLine(pen, bx, by, ex, ey);

        for (int k = 1; k <= 5; k++)
        {
            float f = k / 6f;
            float cx = bx + (ex-bx)*f, cy = by + (ey-by)*f;
            pen.Color = (k%2==0) ? Color.FromArgb(214,172,74) : Color.FromArgb(60,30,14);
            pen.Width = 2f;
            gfx.DrawLine(pen, cx - nx*4.5f, cy - ny*4.5f, cx + nx*4.5f, cy + ny*4.5f);
        }

        using (SolidBrush gold = new SolidBrush(Color.FromArgb(222, 180, 66)))
        {
            gfx.FillEllipse(gold, ex-5f, ey-5f, 10f, 10f);
            gfx.DrawEllipse(Pens.Black, ex-5f, ey-5f, 10f, 10f);
            gfx.FillEllipse(gold, bx-5.5f, by-5.5f, 11f, 11f);
        }
        gfx.DrawEllipse(Pens.Black, bx-5.5f, by-5.5f, 11f, 11f);
        gfx.FillEllipse(Brushes.White, bx-1.8f, by-1.8f, 3.6f, 3.6f);
    }

    // ---------- Fogo ----------
    void DrawFireParticles(float ox, float oy)
    {
        foreach (FireParticle fp in fires)
        {
            float a = (float)fp.life / fp.max;
            // branco no centro, amarelo/laranja/vermelho ao envelhecer
            int rr, gg, bb;
            if (fp.blue)
            {
                if (a > 0.7f)      { rr=Cl(150+105*(a-0.7f)/0.3f); gg=Cl(225+30*(a-0.7f)/0.3f); bb=255; }
                else if (a > 0.4f) { rr=Cl(30+120*(a-0.4f)/0.3f); gg=Cl(110+115*(a-0.4f)/0.3f); bb=255; }
                else               { rr=10; gg=Cl(30+80*a/0.4f); bb=Cl(120+135*a/0.4f); }
            }
            else if (a > 0.7f)      { rr=255; gg=255; bb=Cl(255*(a-0.7f)/0.3f); }
            else if (a > 0.4f) { rr=255; gg=Cl(200*(a-0.4f)/0.3f+55); bb=0; }
            else               { rr=Cl(220*a/0.4f+35); gg=0; bb=0; }

            int alpha = Cl(200 * a);
            pen.Color = Color.FromArgb(alpha, rr, gg, bb);
            pen.Width = 2.5f * a + 0.5f;
            float fx = fp.x - ox, fy = fp.y - oy;
            gfx.DrawLine(pen, fx, fy, fx - fp.vx*1.2f, fy - fp.vy*1.2f);
        }
    }

    void DrawShards(float ox, float oy)
    {
        foreach (Shard sh in shards)
        {
            float a = (float)sh.life / sh.max;
            // Cor: madeira clara / concreto / cinza esfarelado
            int bright = Cl(180 + 60 * a);
            int alpha  = Cl(240 * a);
            pen.Color = Color.FromArgb(alpha, bright, Cl(bright * 0.82f), Cl(bright * 0.58f));
            pen.Width = sh.w * a + 0.5f;

            float cx = sh.x - ox, cy = sh.y - oy;
            float hlen = sh.len * 0.5f;
            float dx = (float)(Math.Cos(sh.rot) * hlen);
            float dy = (float)(Math.Sin(sh.rot) * hlen);
            gfx.DrawLine(pen, cx - dx, cy - dy, cx + dx, cy + dy);

            // fragmento secundário mais curto (dá aspecto de "lascas")
            if (sh.len > 7f)
            {
                pen.Width = (sh.w * 0.5f) * a + 0.3f;
                float dx2 = (float)(Math.Cos(sh.rot + 0.6) * hlen * 0.5f);
                float dy2 = (float)(Math.Sin(sh.rot + 0.6) * hlen * 0.5f);
                pen.Color = Color.FromArgb(Cl(alpha * 0.6f), bright, Cl(bright * 0.75f), Cl(bright * 0.5f));
                gfx.DrawLine(pen, cx - dx2, cy - dy2, cx + dx2, cy + dy2);
            }
        }
    }

    void DrawEffects(float ox, float oy)
    {
        foreach (Ring r in rings)
        {
            float f   = r.age / 14f;
            float rad = 5f + r.age * 7f;
            float x   = r.x - ox, y = r.y - oy;
            if (mode == WeaponMode.Mace)
            {
                // anel de impacto + flash
                pen.Color = Color.FromArgb(Cl(240 * (1 - f)), 230, 220, 200);
                pen.Width = 2.5f * (1 - f) + 0.5f;
                gfx.DrawEllipse(pen, x - rad, y - rad, rad * 2, rad * 2);
                if (r.age < 3)
                {
                    using (SolidBrush br = new SolidBrush(Color.FromArgb(Cl(160 * (1 - r.age / 3f)), 255, 255, 240)))
                        gfx.FillEllipse(br, x - 18f, y - 18f, 36f, 36f);
                }
                // rachaduras radiais
                int nl = 8;
                for (int k = 0; k < nl; k++)
                {
                    double ca = k * (Math.PI * 2 / nl) + r.x * 0.013;
                    float l0 = 5f + r.age * 1.6f;
                    float l1 = l0 + 10f + (k % 3) * 7f;
                    pen.Color = Color.FromArgb(Cl(220 * (1 - f)), 38, 28, 18);
                    pen.Width = 1.8f;
                    gfx.DrawLine(pen, x + (float)Math.Cos(ca) * l0, y + (float)Math.Sin(ca) * l0,
                                      x + (float)Math.Cos(ca) * l1, y + (float)Math.Sin(ca) * l1);
                }
            }
            else if (mode == WeaponMode.Sword)
            {
                pen.Color = Color.FromArgb(Cl(230 * (1 - f)), 200, 225, 255);
                pen.Width = 2.5f * (1 - f) + 0.5f;
                gfx.DrawEllipse(pen, x - rad, y - rad, rad * 2, rad * 2);
                if (r.age < 3)
                {
                    using (SolidBrush br = new SolidBrush(Color.FromArgb(Cl(170 * (1 - r.age / 3f)), 235, 245, 255)))
                        gfx.FillEllipse(br, x - 14f, y - 14f, 28f, 28f);
                }
            }
            else if (mode == WeaponMode.Catapult)
            {
                pen.Color = Color.FromArgb(Cl(235 * (1 - f)), 110, 190, 255);
                pen.Width = 4f * (1 - f) + 0.6f;
                gfx.DrawEllipse(pen, x - rad, y - rad, rad * 2, rad * 2);
                pen.Color = Color.FromArgb(Cl(120 * (1 - f)), 40, 90, 255);
                pen.Width = 9f * (1 - f) + 0.6f;
                gfx.DrawEllipse(pen, x - rad * 0.7f, y - rad * 0.7f, rad * 1.4f, rad * 1.4f);
                if (r.age < 5)
                {
                    using (SolidBrush br = new SolidBrush(Color.FromArgb(Cl(200 * (1 - r.age / 5f)), 190, 230, 255)))
                        gfx.FillEllipse(br, x - 40f, y - 40f, 80f, 80f);
                }
            }
            else
            {
                // chicote e corrente: anel laranja + flash quente
                pen.Color = Color.FromArgb(Cl(230 * (1 - f)), 255, 180, 60);
                pen.Width = 3f * (1 - f) + 0.5f;
                gfx.DrawEllipse(pen, x - rad, y - rad, rad * 2, rad * 2);
                if (r.age < 4)
                {
                    using (SolidBrush br = new SolidBrush(Color.FromArgb(Cl(180 * (1 - r.age / 4f)), 255, 200, 80)))
                        gfx.FillEllipse(br, x - 14f, y - 14f, 28f, 28f);
                }
            }
        }

        foreach (Spark s in sparks)
        {
            float a = (float)s.life / s.max;
            if (s.hot && s.blue)
            {
                pen.Color = Color.FromArgb(Cl(255 * a), Cl(60 + 190 * a * a), Cl(140 + 115 * a), 255);
                pen.Width = 2.2f * a + 0.6f;
            }
            else if (s.hot)
            {
                // faísca quente: branco-amarelo -> laranja -> vermelho
                pen.Color = Color.FromArgb(Cl(255 * a), 255, Cl(110 + 145 * a * a), Cl(230 * a * a * a));
                pen.Width = 2.2f * a + 0.6f;
            }
            else if (mode == WeaponMode.Mace)
            {
                int br = Cl(160 + 80 * a);
                pen.Color = Color.FromArgb(Cl(220 * a), br, Cl(br * 0.88f), Cl(br * 0.70f));
                pen.Width = 1.5f * a + 0.4f;
            }
            else if (mode == WeaponMode.Sword)
            {
                // faíscas de aço: branco -> amarelo pálido
                pen.Color = Color.FromArgb(Cl(255 * a), 255, Cl(220 + 35 * a), Cl(160 * a));
                pen.Width = 2.2f * a + 0.4f;
            }
            else if (s.isFire)
            {
                pen.Color = Color.FromArgb(Cl(255 * a), 255, Cl(140 * (1 - a) + 80), 0);
                pen.Width = 2.5f * a + 0.5f;
            }
            else
            {
                pen.Color = Color.FromArgb(Cl(255 * a), 255, 230, 100);
                pen.Width = 2f;
            }
            float x = s.x - ox, y = s.y - oy;
            gfx.DrawLine(pen, x, y, x - s.vx * 0.9f, y - s.vy * 0.9f);
        }
    }

    void Present()
    {
        SIZE size = new SIZE(); size.cx = W; size.cy = H;
        POINT src = new POINT();
        POINT dst = new POINT(); dst.x = mx - W/2; dst.y = my - H/2;
        BLENDFUNCTION bf = new BLENDFUNCTION();
        bf.BlendOp = 0; bf.BlendFlags = 0; bf.SourceConstantAlpha = 255; bf.AlphaFormat = 1;
        UpdateLayeredWindow(Handle, IntPtr.Zero, ref dst, ref size, memDc, ref src, 0, ref bf, 2);
    }

    // ===================================================================
    //  ARMAS NOVAS: AK-47, CATAPULTA DE FOGO AZUL, SPRAY
    //  (usam a mesma camada grande do arco; a mira segue o último
    //   movimento do mouse, igual ao arco)
    // ===================================================================
    bool UsesBigLayer()
    {
        return mode == WeaponMode.Bow || mode == WeaponMode.Ak ||
               mode == WeaponMode.Catapult || mode == WeaponMode.Spray;
    }

    List<Bullet> bullets = new List<Bullet>();
    List<Ball> balls = new List<Ball>();
    List<Burn> burns = new List<Burn>();
    List<Dot> dots = new List<Dot>();
    SolidBrush dotBr = new SolidBrush(Color.White);

    // botão apertado agora? (com o gancho ativo vale o estado engolido por ele)
    bool TriggerHeld()
    {
        if (hookId != IntPtr.Zero) return heldLeft || heldRight;
        return ((GetAsyncKeyState(1) & 0x8000) != 0) || ((GetAsyncKeyState(2) & 0x8000) != 0);
    }

    void InitAim()
    {
        EnsureBowDib();
        int fc = PickFacing();
        bowAim = (fc == 1) ? Deg(-8f) : Deg(188f);
        bowPrevMx = mx; bowPrevMy = my;
    }

    void TrackAim()
    {
        float mvx = mx - bowPrevMx, mvy = my - bowPrevMy;
        bowPrevMx = mx; bowPrevMy = my;
        if (mvx * mvx + mvy * mvy > 9f)
        {
            float target = (float)Math.Atan2(mvy, mvx);
            float diff = target - bowAim;
            while (diff > (float)Math.PI)  diff -= 2f * (float)Math.PI;
            while (diff < -(float)Math.PI) diff += 2f * (float)Math.PI;
            bowAim += diff * 0.18f;
        }
    }

    // base de desenho "(f, v)": f = para frente da arma, v = para cima na tela
    float gbx, gby, gca, gsa, gsg;
    void SetGunBasis(float cx, float cy, float ang)
    {
        gca = (float)Math.Cos(ang); gsa = (float)Math.Sin(ang);
        gsg = (gca >= 0f) ? -1f : 1f;
        gbx = cx; gby = cy;
    }
    PointF GP(float f, float v)
    {
        return new PointF(gbx + gca * f - gsg * gsa * v, gby + gsa * f + gsg * gca * v);
    }
    void GPoly(Color fill, Color edge, params float[] fv)
    {
        PointF[] pp = new PointF[fv.Length / 2];
        for (int i = 0; i < pp.Length; i++) pp[i] = GP(fv[i * 2], fv[i * 2 + 1]);
        using (SolidBrush br = new SolidBrush(fill)) gfx.FillPolygon(br, pp);
        pen.Color = edge; pen.Width = 1.2f;
        gfx.DrawPolygon(pen, pp);
    }
    void GLine(Color c, float w, float f0, float v0, float f1, float v1)
    {
        pen.Color = c; pen.Width = w;
        gfx.DrawLine(pen, GP(f0, v0), GP(f1, v1));
    }

    // ---------- Bugs: acertos das armas ----------
    int HitBugsSeg(float x0, float y0, float x1, float y1, float r)
    {
        if (bugs.Count == 0) return 0;
        int k = 0;
        foreach (BugForm b in bugs)
        {
            if (b.State == 0 && b.HitSeg(x0, y0, x1, y1, r))
            {
                b.Kill(x1 - x0, y1 - y0);
                k++;
            }
        }
        if (k > 0) PlaySquash();
        return k;
    }

    int HitBugsCircle(float x, float y, float r) { return HitBugsSeg(x, y, x, y, r); }

    void PoisonBugs(float x, float y, float r, Color c)
    {
        if (bugs.Count == 0) return;
        bool killed = false;
        foreach (BugForm b in bugs)
            if (b.State == 0 && b.PoisonHit(x, y, r, c)) killed = true;
        if (killed) PlaySquash();
    }

    // ===================================================================
    //  AK-47
    // ===================================================================
    int   akCd = 0, akFlash = 0;
    bool  akShotReq = false;
    float akKick = 0f;

    void InitAk()
    {
        InitAim();
        akCd = 0; akFlash = 0; akShotReq = false; akKick = 0f;
    }

    void StepAk()
    {
        TrackAim();
        if (akCd > 0) akCd--;
        if (akFlash > 0) akFlash--;
        if (akKick > 0f) akKick = Math.Max(0f, akKick - 0.16f);

        bool held = TriggerHeld();
        if ((held || akShotReq) && akCd <= 0)
        {
            akShotReq = false;
            FireBullet();
            akCd = 5;                      // ~12 tiros por segundo
        }
        StepBullets();
        StepParticles();
    }

    void FireBullet()
    {
        SetGunBasis(mx, my, bowAim);
        PointF m = GP(97f, 10f);
        float ang = bowAim + Rf(-0.03f, 0.03f);
        Bullet b = new Bullet();
        b.x = b.px = m.X; b.y = b.py = m.Y;
        b.vx = (float)Math.Cos(ang) * 48f; b.vy = (float)Math.Sin(ang) * 48f;
        bullets.Add(b);
        while (bullets.Count > 150) bullets.RemoveAt(0);
        akKick = 1f; akFlash = 2;

        if (sparks.Count < 450)
        {
            // cápsula ejetada
            PointF e = GP(4f, 15f);
            Spark c = new Spark();
            c.x = e.X; c.y = e.Y;
            c.vx = -gsg * gsa * 3f - gca * 1.2f + Rf(-0.6f, 0.6f);
            c.vy = gsg * gca * 3f - gsa * 1.2f - 2.2f + Rf(-0.6f, 0.4f);
            c.max = c.life = 22 + rnd.Next(10);
            c.custom = true; c.drag = 0.96f; c.g = 0.36f;
            sparks.Add(c);
            for (int i = 0; i < 3; i++)
            {
                Spark s = new Spark();
                s.x = m.X; s.y = m.Y;
                s.vx = gca * Rf(2f, 7f) + Rf(-1.5f, 1.5f);
                s.vy = gsa * Rf(2f, 7f) + Rf(-1.5f, 1.5f);
                s.max = s.life = 6 + rnd.Next(6);
                s.hot = true; s.custom = true; s.drag = 0.9f; s.g = 0.05f;
                sparks.Add(s);
            }
        }
        PlayAk();
    }

    void StepBullets()
    {
        Rectangle vs = SystemInformation.VirtualScreen;
        for (int i = bullets.Count - 1; i >= 0; i--)
        {
            Bullet b = bullets[i];
            b.px = b.x; b.py = b.y;
            b.x += b.vx; b.y += b.vy;
            b.age++;
            if (HitBugsSeg(b.px, b.py, b.x, b.y, 4f) > 0)
            {
                for (int k = 0; k < 6; k++)
                {
                    Spark s = new Spark();
                    s.x = b.x; s.y = b.y;
                    s.vx = Rf(-4f, 4f); s.vy = Rf(-4f, 3f);
                    s.max = s.life = 8 + rnd.Next(8);
                    s.hot = true; s.custom = true; s.drag = 0.9f; s.g = 0.2f;
                    sparks.Add(s);
                }
                bullets.RemoveAt(i);
                continue;
            }
            if (b.x < vs.Left - 100 || b.x > vs.Right + 100 || b.y < vs.Top - 100 || b.y > vs.Bottom + 100 || b.age > 160)
                bullets.RemoveAt(i);
        }
    }

    void DrawAkScene(float ox, float oy)
    {
        foreach (Bullet b in bullets)
        {
            float x = b.x - ox, y = b.y - oy;
            float tx = x - b.vx * 0.95f, ty = y - b.vy * 0.95f;
            pen.Color = Color.FromArgb(70, 255, 170, 60);  pen.Width = 7f;
            gfx.DrawLine(pen, tx, ty, x, y);
            pen.Color = Color.FromArgb(230, 255, 210, 90); pen.Width = 3f;
            gfx.DrawLine(pen, tx, ty, x, y);
            pen.Color = Color.FromArgb(255, 255, 255, 235); pen.Width = 1.4f;
            gfx.DrawLine(pen, x - b.vx * 0.4f, y - b.vy * 0.4f, x, y);
        }
        DrawAkBody(ox, oy);
        DrawEffects(ox, oy);
    }

    void DrawAkBody(float ox, float oy)
    {
        float kick = akKick * 7f;
        float ca = (float)Math.Cos(bowAim), sa = (float)Math.Sin(bowAim);
        SetGunBasis(mx - ox - ca * kick, my - oy - sa * kick, bowAim);

        Color wood = Color.FromArgb(255, 142, 88, 44), woodD = Color.FromArgb(255, 62, 36, 16);
        Color steel = Color.FromArgb(255, 66, 70, 78), steelD = Color.FromArgb(255, 18, 18, 22);
        Color poly = Color.FromArgb(255, 36, 36, 40);

        // coronha
        GPoly(wood, woodD, -14f, 16f, -52f, 13f, -52f, 0f, -32f, 3f, -14f, 6f);
        GPoly(steel, steelD, -52f, 13f, -55f, 13f, -55f, 0f, -52f, 0f);
        GLine(Color.FromArgb(90, 255, 220, 160), 1.2f, -48f, 11f, -20f, 14f);
        // carregador curvo
        GPoly(steel, steelD, 10f, 5f, 22f, 5f, 30f, -12f, 35f, -22f, 25f, -25f, 17f, -12f);
        GLine(Color.FromArgb(120, 20, 20, 24), 1f, 14f, 0f, 24f, -3f);
        GLine(Color.FromArgb(120, 20, 20, 24), 1f, 19f, -8f, 28f, -11f);
        // punho
        GPoly(poly, steelD, -3f, 6f, 6f, 6f, 3f, -12f, -6f, -10f);
        // caixa da culatra + tampa
        GPoly(steel, steelD, -14f, 4f, 26f, 4f, 26f, 17f, -14f, 17f);
        GPoly(Color.FromArgb(255, 98, 104, 114), steelD, -12f, 14.5f, 24f, 14.5f, 24f, 17f, -12f, 17f);
        // guarda-mão de madeira
        GPoly(wood, woodD, 26f, 2f, 54f, 2f, 54f, 13f, 26f, 13f);
        GPoly(wood, woodD, 30f, 13f, 56f, 13f, 56f, 19f, 30f, 19f);
        // cano, tubo de gás, massa de mira, quebra-chamas
        GPoly(steel, steelD, 54f, 8f, 90f, 8f, 90f, 12f, 54f, 12f);
        GPoly(steel, steelD, 56f, 19f, 78f, 19f, 78f, 22f, 56f, 22f);
        GPoly(steel, steelD, 76f, 10f, 82f, 10f, 82f, 24f, 76f, 24f);
        GPoly(steel, steelD, 79f, 24f, 81f, 24f, 81f, 29f, 79f, 29f);
        GPoly(Color.FromArgb(255, 52, 54, 60), steelD, 88f, 6f, 97f, 6f, 97f, 14f, 88f, 14f);
        // guarda-mato do gatilho
        GLine(steelD, 2f, 6f, 4f, 8f, -3f);
        GLine(steelD, 2f, 8f, -3f, 16f, -3f);
        GLine(steelD, 2f, 16f, -3f, 17f, 4f);

        // clarão do tiro
        if (akFlash > 0)
        {
            PointF m = GP(98f, 10f);
            using (SolidBrush glow = new SolidBrush(Color.FromArgb(80, 255, 190, 60)))
                gfx.FillEllipse(glow, m.X - 28f, m.Y - 28f, 56f, 56f);
            PointF[] star = new PointF[16];
            for (int i = 0; i < 16; i++)
            {
                double a = i * Math.PI / 8.0;
                float r = (i % 2 == 0) ? 15f + Rf(0f, 12f) : 5f;
                star[i] = new PointF(m.X + (float)Math.Cos(a) * r, m.Y + (float)Math.Sin(a) * r);
            }
            using (SolidBrush br = new SolidBrush(Color.FromArgb(235, 255, 190, 50))) gfx.FillPolygon(br, star);
            using (SolidBrush br2 = new SolidBrush(Color.FromArgb(255, 255, 250, 220)))
                gfx.FillEllipse(br2, m.X - 6f, m.Y - 6f, 12f, 12f);
            // jato na direção do cano
            pen.Color = Color.FromArgb(220, 255, 220, 100); pen.Width = 4f;
            gfx.DrawLine(pen, m.X, m.Y, m.X + gca * 30f, m.Y + gsa * 30f);
        }

        DrawHotspot(ox, oy);
    }

    // ===================================================================
    //  CATAPULTA DE FOGO AZUL
    // ===================================================================
    int   catF = -1, catFacing = 1;
    float catArm = 138f, catRoll = 0f, catPulse = 0f;
    bool  catBall = true;
    const float CAT_PY = -50f, CAT_ARM = 54f, CAT_TAIL = 18f, CAT_REST = 138f;

    void InitCatapult()
    {
        InitAim();
        catF = -1; catArm = CAT_REST; catRoll = 0f; catBall = true;
        catFacing = PickFacing();
    }

    void StartCatapult()
    {
        if (catF >= 0) return;
        catF = 0;
    }

    void StepCatapult()
    {
        catRoll += (mx - bowPrevMx) * 0.1f;
        TrackAim();
        float cs = (float)Math.Cos(bowAim);
        if (Math.Abs(cs) > 0.25f) catFacing = (cs >= 0f) ? 1 : -1;

        if (catF >= 0)
        {
            int f = catF;
            if (f < 10)       catArm = Mix(CAT_REST, 116f, Sm(f / 9f));
            else if (f < 18)  catArm = Mix(116f, 322f, EAcc((f - 10) / 8f));
            else if (f < 23)  catArm = 322f - 12f * Sm((f - 18) / 5f);
            else              catArm = Mix(310f, CAT_REST, Sm((f - 23) / 28f));
            if (f == 12) PlayCatapult();
            if (f == 17) { LaunchBall(); catBall = false; }
            if (f == 46) catBall = true;
            catF++;
            if (catF > 52) { catF = -1; catArm = CAT_REST; catBall = true; }
        }
        else
        {
            catPulse += 0.05f;
            catArm = CAT_REST + 1.2f * (float)Math.Sin(catPulse);
        }

        StepBalls();
        StepBurns();
        StepParticles();
    }

    // centro da bola/caçamba (coordenadas relativas ao chão da catapulta, virada para a direita)
    void CatBucket(float armDeg, out float cx, out float cy)
    {
        float a = Deg(armDeg);
        cx = CAT_ARM * (float)Math.Cos(a) + 8f * (float)Math.Cos(a + (float)Math.PI / 2f);
        cy = CAT_PY + CAT_ARM * (float)Math.Sin(a) + 8f * (float)Math.Sin(a + (float)Math.PI / 2f);
    }

    void LaunchBall()
    {
        float bcx, bcy;
        CatBucket(catArm, out bcx, out bcy);
        Rectangle vs = SystemInformation.VirtualScreen;
        float ac = (float)Math.Cos(bowAim), asn = (float)Math.Sin(bowAim);
        float sx = mx + catFacing * bcx, sy = my;

        // mira assistida: pega o bug mais perto da linha de tiro
        BugForm best = null; float bestDiff = 0.5f;
        foreach (BugForm bg in bugs)
        {
            if (bg.State != 0) continue;
            float dx = bg.X - sx, dy = bg.Y - sy;
            float d = (float)Math.Sqrt(dx * dx + dy * dy);
            if (d < 90f) continue;
            float cosv = (dx * ac + dy * asn) / d;
            if (cosv > 1f) cosv = 1f;
            if (cosv < -1f) cosv = -1f;
            float diff = (float)Math.Acos(cosv);
            if (diff < bestDiff) { bestDiff = diff; best = bg; }
        }

        float tx, ty; int dur;
        if (best != null)
        {
            float dx = best.X - sx, dy = best.Y - sy;
            float d = (float)Math.Sqrt(dx * dx + dy * dy);
            dur = (int)Math.Max(32f, Math.Min(64f, 32f + d / 22f));
            tx = best.X + best.VX * dur; ty = best.Y + best.VY * dur;
        }
        else
        {
            tx = sx + ac * 520f; ty = sy + asn * 520f; dur = 46;
        }
        if (tx < vs.Left + 20) tx = vs.Left + 20;
        if (tx > vs.Right - 20) tx = vs.Right - 20;
        if (ty < vs.Top + 20) ty = vs.Top + 20;
        if (ty > vs.Bottom - 20) ty = vs.Bottom - 20;

        Ball b = new Ball();
        b.sx = sx; b.sy = sy; b.tx = tx; b.ty = ty;
        b.z0 = -bcy; b.dur = dur; b.age = 0;
        float gd = (float)Math.Sqrt((tx - sx) * (tx - sx) + (ty - sy) * (ty - sy));
        b.peak = 90f + gd * 0.18f;
        balls.Add(b);
    }

    float BallU(Ball b) { return Clamp01(b.age / (float)b.dur); }
    float BallZ(Ball b) { float u = BallU(b); return b.z0 * (1f - u) + 4f * b.peak * u * (1f - u); }
    float BallX(Ball b) { return Mix(b.sx, b.tx, BallU(b)); }
    float BallY(Ball b) { return Mix(b.sy, b.ty, BallU(b)) - BallZ(b); }

    void EmitBlueFire(float x, float y, float vx, float vy, int life)
    {
        if (fires.Count > 650) return;
        FireParticle fp = new FireParticle();
        fp.x = x; fp.y = y; fp.vx = vx; fp.vy = vy;
        fp.max = fp.life = life; fp.blue = true;
        fires.Add(fp);
    }

    void StepBalls()
    {
        for (int i = balls.Count - 1; i >= 0; i--)
        {
            Ball b = balls[i];
            b.age++;
            float x = BallX(b), y = BallY(b);
            for (int k = 0; k < 3; k++)
                EmitBlueFire(x + Rf(-5f, 5f), y + Rf(-5f, 5f), Rf(-1.2f, 1.2f), Rf(-2.5f, 0.5f), 10 + rnd.Next(10));
            if (sparks.Count < 450 && rnd.NextDouble() < 0.6)
            {
                Spark s = new Spark();
                s.x = x; s.y = y; s.vx = Rf(-1.5f, 1.5f); s.vy = Rf(-1.5f, 1.5f);
                s.max = s.life = 14 + rnd.Next(10);
                s.hot = true; s.blue = true; s.custom = true; s.drag = 0.95f; s.g = 0.08f;
                sparks.Add(s);
            }
            if (b.age >= b.dur)
            {
                DoBlueBlast(b.tx, b.ty);
                balls.RemoveAt(i);
            }
        }
    }

    void DoBlueBlast(float x, float y)
    {
        rings.Add(new Ring { x = x, y = y, age = 0 });
        HitBugsCircle(x, y, 92f);
        for (int i = 0; i < 46; i++)
        {
            double ang = rnd.NextDouble() * Math.PI * 2;
            double spd = 1 + rnd.NextDouble() * 7;
            EmitBlueFire(x, y, (float)(Math.Cos(ang) * spd), (float)(Math.Sin(ang) * spd) - 1.5f, 16 + rnd.Next(16));
        }
        for (int i = 0; i < 28; i++)
        {
            double ang = rnd.NextDouble() * Math.PI * 2;
            double spd = 2 + rnd.NextDouble() * 9;
            Spark s = new Spark();
            s.x = x; s.y = y;
            s.vx = (float)(Math.Cos(ang) * spd); s.vy = (float)(Math.Sin(ang) * spd) - 1f;
            s.max = s.life = 18 + rnd.Next(22);
            s.hot = true; s.blue = true; s.custom = true; s.drag = 0.93f; s.g = 0.2f;
            sparks.Add(s);
        }
        burns.Add(new Burn { x = x, y = y, life = 90 });
        PlayBoom();
    }

    void StepBurns()
    {
        for (int i = burns.Count - 1; i >= 0; i--)
        {
            Burn bn = burns[i];
            bn.life--;
            float k = bn.life / 90f;
            for (int j = 0; j < 2; j++)
                EmitBlueFire(bn.x + Rf(-26f, 26f) * (0.4f + k), bn.y + Rf(-6f, 6f), Rf(-0.5f, 0.5f), Rf(-4.2f, -1.2f), 12 + rnd.Next(12));
            if (bn.life > 30) HitBugsSeg(bn.x - 26f, bn.y, bn.x + 26f, bn.y, 14f);
            if (bn.life <= 0) burns.RemoveAt(i);
        }
    }

    // bola de fogo azul. (dx, dy) = direção do movimento (as chamas vão para trás)
    void DrawBlueBall(float x, float y, float rad, float dx, float dy)
    {
        using (SolidBrush g1 = new SolidBrush(Color.FromArgb(38, 40, 90, 255)))
            gfx.FillEllipse(g1, x - rad * 2.6f, y - rad * 2.6f, rad * 5.2f, rad * 5.2f);
        using (SolidBrush g2 = new SolidBrush(Color.FromArgb(90, 60, 140, 255)))
            gfx.FillEllipse(g2, x - rad * 1.7f, y - rad * 1.7f, rad * 3.4f, rad * 3.4f);

        float baseAng = (float)Math.Atan2(-dy, -dx);
        for (int k = 0; k < 5; k++)
        {
            float ang = baseAng + (k - 2) * 0.42f + Rf(-0.12f, 0.12f);
            float len = rad * (1.6f + Rf(0f, 1.2f));
            float ex = x + (float)Math.Cos(ang) * len, ey = y + (float)Math.Sin(ang) * len;
            pen.Color = Color.FromArgb(150, 50, 120, 255); pen.Width = rad * 1.1f;
            gfx.DrawLine(pen, x, y, ex, ey);
            pen.Color = Color.FromArgb(190, 130, 210, 255); pen.Width = rad * 0.55f;
            gfx.DrawLine(pen, x, y, x + (ex - x) * 0.7f, y + (ey - y) * 0.7f);
        }
        using (SolidBrush b3 = new SolidBrush(Color.FromArgb(235, 90, 170, 255)))
            gfx.FillEllipse(b3, x - rad, y - rad, rad * 2f, rad * 2f);
        using (SolidBrush b4 = new SolidBrush(Color.FromArgb(255, 215, 245, 255)))
            gfx.FillEllipse(b4, x - rad * 0.55f, y - rad * 0.55f, rad * 1.1f, rad * 1.1f);
    }

    float cbx, cby; int cbs = 1;
    PointF CP(float x, float y) { return new PointF(cbx + cbs * x, cby + y); }

    void DrawCatScene(float ox, float oy)
    {
        // brasas azuis no chão
        foreach (Burn bn in burns)
        {
            float k = Clamp01(bn.life / 90f);
            float x = bn.x - ox, y = bn.y - oy;
            using (SolidBrush gl = new SolidBrush(Color.FromArgb(Cl(70 * k), 40, 110, 255)))
                gfx.FillEllipse(gl, x - 52f, y - 16f, 104f, 32f);
            using (SolidBrush gl2 = new SolidBrush(Color.FromArgb(Cl(110 * k), 120, 200, 255)))
                gfx.FillEllipse(gl2, x - 26f, y - 8f, 52f, 16f);
        }
        DrawFireParticles(ox, oy);

        foreach (Ball b in balls)
        {
            float u = BallU(b);
            float gx = Mix(b.sx, b.tx, u) - ox, gy = Mix(b.sy, b.ty, u) - oy;
            float z = BallZ(b);
            float sh = 1f - Clamp01(z / 260f) * 0.6f;
            using (SolidBrush shb = new SolidBrush(Color.FromArgb(Cl(70 * sh), 0, 0, 0)))
                gfx.FillEllipse(shb, gx - 16f * sh, gy - 5f * sh, 32f * sh, 10f * sh);
            // alvo no chão
            pen.Color = Color.FromArgb(Cl(150 * u), 120, 200, 255); pen.Width = 1.6f;
            float tx = b.tx - ox, ty = b.ty - oy, tr = 40f - 22f * u;
            gfx.DrawEllipse(pen, tx - tr, ty - tr * 0.4f, tr * 2f, tr * 0.8f);
            float nx = BallX(b) - ox, ny = BallY(b) - oy;
            float vx = (b.tx - b.sx) / b.dur, vy = (b.ty - b.sy) / b.dur;
            DrawBlueBall(nx, ny, 13f, vx, vy);
        }

        DrawCatBody(ox, oy);
        DrawEffects(ox, oy);
    }

    void CatLine(Color c, float w, float x0, float y0, float x1, float y1)
    {
        pen.Color = c; pen.Width = w;
        gfx.DrawLine(pen, CP(x0, y0), CP(x1, y1));
    }
    void CatPoly(Color fill, Color edge, params float[] fv)
    {
        PointF[] pp = new PointF[fv.Length / 2];
        for (int i = 0; i < pp.Length; i++) pp[i] = CP(fv[i * 2], fv[i * 2 + 1]);
        using (SolidBrush br = new SolidBrush(fill)) gfx.FillPolygon(br, pp);
        pen.Color = edge; pen.Width = 1.3f;
        gfx.DrawPolygon(pen, pp);
    }

    void DrawCatBody(float ox, float oy)
    {
        cbx = mx - ox; cby = my - oy; cbs = catFacing;
        Color wood = Color.FromArgb(255, 150, 98, 48), woodD = Color.FromArgb(255, 58, 34, 14);
        Color woodM = Color.FromArgb(255, 112, 72, 34);

        using (SolidBrush sh = new SolidBrush(Color.FromArgb(60, 0, 0, 0)))
            gfx.FillEllipse(sh, cbx - 50f, cby - 5f, 100f, 12f);

        // rodas
        float[] wxs = new float[] { -30f, 22f };
        foreach (float wx in wxs)
        {
            PointF c = CP(wx, -9f);
            using (SolidBrush wb = new SolidBrush(woodM)) gfx.FillEllipse(wb, c.X - 10f, c.Y - 10f, 20f, 20f);
            pen.Color = woodD; pen.Width = 2.2f;
            gfx.DrawEllipse(pen, c.X - 10f, c.Y - 10f, 20f, 20f);
            for (int k = 0; k < 6; k++)
            {
                float a = catRoll * cbs + k * (float)Math.PI / 3f;
                pen.Color = Color.FromArgb(255, 70, 44, 20); pen.Width = 1.6f;
                gfx.DrawLine(pen, c.X, c.Y, c.X + (float)Math.Cos(a) * 9f, c.Y + (float)Math.Sin(a) * 9f);
            }
            using (SolidBrush hub = new SolidBrush(Color.FromArgb(255, 150, 154, 164))) gfx.FillEllipse(hub, c.X - 3f, c.Y - 3f, 6f, 6f);
        }

        // viga base, pernas em A, travessa e batente
        CatPoly(wood, woodD, -42f, -25f, 38f, -25f, 38f, -14f, -42f, -14f);
        CatLine(woodD, 8.5f, -24f, -24f, 0f, CAT_PY); CatLine(wood, 5.5f, -24f, -24f, 0f, CAT_PY);
        CatLine(woodD, 8.5f, 24f, -24f, 0f, CAT_PY);  CatLine(wood, 5.5f, 24f, -24f, 0f, CAT_PY);
        CatLine(woodD, 5f, -12f, -36f, 12f, -36f);    CatLine(woodM, 3f, -12f, -36f, 12f, -36f);
        CatLine(woodD, 6f, 32f, -25f, 32f, -46f);     CatLine(wood, 3.5f, 32f, -25f, 32f, -46f);
        CatLine(woodD, 5f, 26f, -46f, 38f, -46f);

        // braço
        float arm = Deg(catArm);
        float dx = (float)Math.Cos(arm), dy = (float)Math.Sin(arm);
        float ex = CAT_ARM * dx, ey = CAT_PY + CAT_ARM * dy;
        float sx = -CAT_TAIL * dx, sy = CAT_PY - CAT_TAIL * dy;
        CatLine(woodD, 9f, sx, sy, ex, ey);
        CatLine(wood, 6f, sx, sy, ex, ey);
        CatLine(Color.FromArgb(110, 255, 230, 180), 1.4f, sx, sy - 1.5f, ex, ey - 1.5f);

        // contrapeso de pedra
        PointF cw = CP(sx - dx * 5f, sy - dy * 5f);
        using (SolidBrush st = new SolidBrush(Color.FromArgb(255, 112, 114, 122))) gfx.FillEllipse(st, cw.X - 9f, cw.Y - 9f, 18f, 18f);
        pen.Color = Color.FromArgb(255, 30, 30, 36); pen.Width = 1.6f;
        gfx.DrawEllipse(pen, cw.X - 9f, cw.Y - 9f, 18f, 18f);
        using (SolidBrush st2 = new SolidBrush(Color.FromArgb(120, 255, 255, 255))) gfx.FillEllipse(st2, cw.X - 5f, cw.Y - 6f, 6f, 4f);

        // caçamba
        float ox2 = (float)Math.Cos(arm + Math.PI / 2), oy2 = (float)Math.Sin(arm + Math.PI / 2);
        CatPoly(woodM, woodD,
                ex - dx * 9f, ey - dy * 9f,
                ex + dx * 9f, ey + dy * 9f,
                ex + dx * 9f + ox2 * 9f, ey + dy * 9f + oy2 * 9f,
                ex - dx * 9f + ox2 * 9f, ey - dy * 9f + oy2 * 9f);
        CatLine(Color.FromArgb(255, 150, 154, 164), 2f, ex - dx * 9f + ox2 * 9f, ey - dy * 9f + oy2 * 9f, ex + dx * 9f + ox2 * 9f, ey + dy * 9f + oy2 * 9f);

        // bola de fogo azul esperando na caçamba
        if (catBall)
        {
            float bcx, bcy;
            CatBucket(catArm, out bcx, out bcy);
            PointF bp = CP(bcx, bcy);
            DrawBlueBall(bp.X, bp.Y, 10f + 1.2f * (float)Math.Sin(catPulse * 6f), 0f, 1f);
        }

        // eixo
        PointF ax = CP(0f, CAT_PY);
        using (SolidBrush axb = new SolidBrush(Color.FromArgb(255, 140, 144, 154))) gfx.FillEllipse(axb, ax.X - 4.5f, ax.Y - 4.5f, 9f, 9f);
        pen.Color = Color.FromArgb(255, 30, 30, 36); pen.Width = 1.2f;
        gfx.DrawEllipse(pen, ax.X - 4.5f, ax.Y - 4.5f, 9f, 9f);

        DrawHotspot(ox, oy);
    }

    // ===================================================================
    //  SPRAY (a tinta fica um tempo na tela e depois some)
    // ===================================================================
    static readonly int[] SpPal = new int[] {
        0xE8352B, 0x2E9BFF, 0x35D06A, 0xFFD23A, 0xF04FD0, 0xFFFFFF, 0xFF8A1F, 0x25E0D0
    };
    int   spCol = 0, spStill = 0;
    bool  spWasHeld = false, hissOn = false;
    float spPrevX, spPrevY;

    static Color PalColor(int c, int a) { return Color.FromArgb(a, (c >> 16) & 255, (c >> 8) & 255, c & 255); }

    void StartHiss()
    {
        if (!soundOn || hissOn) return;
        try { hissPlayer.PlayLooping(); hissOn = true; } catch { }
    }
    void StopHiss()
    {
        if (!hissOn) return;
        try { hissPlayer.Stop(); } catch { }
        hissOn = false;
    }

    void InitSpray()
    {
        InitAim();
        spCol = SpPal.Length - 1; spWasHeld = false; spStill = 0;
    }

    void AddDot(float x, float y, float r, int life, int kind)
    {
        Dot d = new Dot();
        d.x = x; d.y = y; d.r = r;
        d.life = d.max = life; d.col = SpPal[spCol]; d.kind = kind;
        dots.Add(d);
        while (dots.Count > 2000) dots.RemoveAt(0);
    }

    void StepSpray()
    {
        bool held = TriggerHeld();
        if (held && !spWasHeld)
        {
            spCol = (spCol + 1) % SpPal.Length;       // cada apertada troca a cor
            spPrevX = mx; spPrevY = my; spStill = 0;
            StartHiss();
        }
        if (!held && spWasHeld) StopHiss();
        spWasHeld = held;

        if (held)
        {
            float dx = mx - spPrevX, dy = my - spPrevY;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);
            int n = (int)(dist / 3f) + 2;
            for (int i = 0; i < n; i++)
            {
                float t = (i + 1f) / n;
                float x = spPrevX + dx * t, y = spPrevY + dy * t;
                AddDot(x + Rf(-5f, 5f), y + Rf(-5f, 5f), Rf(4f, 8.5f), 330, 0);
                if (rnd.NextDouble() < 0.4) AddDot(x + Rf(-2f, 2f), y + Rf(-2f, 2f), Rf(1.6f, 3f), 330, 3);
            }
            spPrevX = mx; spPrevY = my;
            if (dist < 2f) spStill++; else spStill = 0;
            if (spStill > 14 && spStill % 6 == 0) AddDot(mx + Rf(-3f, 3f), my, 3.2f, 46, 2);

            for (int m = 0; m < 3; m++)
            {
                Dot d = new Dot();
                d.x = mx; d.y = my; d.r = Rf(5f, 10f);
                d.vx = -0.5f * Rf(0.5f, 3.5f) + Rf(-1f, 1f); d.vy = 0.866f * Rf(0.5f, 3.5f) + Rf(-1f, 1f);
                d.life = d.max = 12; d.col = SpPal[spCol]; d.kind = 1;
                dots.Add(d);
            }
            PoisonBugs(mx, my, 26f, PalColor(SpPal[spCol], 255));
        }

        StepDots();
        StepParticles();
    }

    void StepDots()
    {
        for (int i = dots.Count - 1; i >= 0; i--)
        {
            Dot d = dots[i];
            d.life--;
            if (d.kind == 1) { d.x += d.vx; d.y += d.vy; d.vx *= 0.9f; d.vy *= 0.9f; }
            else if (d.kind == 2)
            {
                d.y += 0.4f + 1.3f * (d.life / (float)d.max);
                if (dots.Count < 1990) AddDot(d.x, d.y, 1.8f, 240, 3);
            }
            if (d.life <= 0) dots.RemoveAt(i);
        }
    }

    void DrawDots(float ox, float oy)
    {
        foreach (Dot d in dots)
        {
            float x = d.x - ox, y = d.y - oy;
            float fade = (d.life < 90) ? d.life / 90f : 1f;
            int a; float r = d.r;
            if (d.kind == 1) { a = Cl(60f * d.life / d.max); r = d.r * (1.5f - 0.5f * d.life / d.max); }
            else if (d.kind == 2) { a = 230; }
            else if (d.kind == 3) { a = Cl(125f * fade); }
            else { a = Cl(44f * fade); }
            dotBr.Color = PalColor(d.col, a);
            gfx.FillEllipse(dotBr, x - r, y - r, r * 2f, r * 2f);
        }
    }

    void FillPts(Color fill, Color edge, params PointF[] p)
    {
        using (SolidBrush br = new SolidBrush(fill)) gfx.FillPolygon(br, p);
        pen.Color = edge; pen.Width = 1.3f;
        gfx.DrawPolygon(pen, p);
    }

    void DrawSprayScene(float ox, float oy)
    {
        DrawDots(ox, oy);
        DrawSprayCan(ox, oy);
        DrawEffects(ox, oy);
    }

    void DrawSprayCan(float ox, float oy)
    {
        float gx = mx - ox, gy = my - oy;
        if (spWasHeld) { gx += Rf(-1.2f, 1.2f); gy += Rf(-1.2f, 1.2f); }
        const float ax = 0.5f, ay = -0.866f, qx = 0.866f, qy = 0.5f;
        Color paint = PalColor(SpPal[spCol < 0 ? 0 : spCol], 255);
        Color edge = Color.FromArgb(255, 24, 26, 32);

        // corpo
        FillPts(Color.FromArgb(255, 198, 204, 214), edge,
            SP(gx, gy, ax, ay, qx, qy, 18f, -11f), SP(gx, gy, ax, ay, qx, qy, 66f, -11f),
            SP(gx, gy, ax, ay, qx, qy, 66f, 11f),  SP(gx, gy, ax, ay, qx, qy, 18f, 11f));
        // rótulo colorido
        FillPts(paint, edge,
            SP(gx, gy, ax, ay, qx, qy, 27f, -11f), SP(gx, gy, ax, ay, qx, qy, 55f, -11f),
            SP(gx, gy, ax, ay, qx, qy, 55f, 11f),  SP(gx, gy, ax, ay, qx, qy, 27f, 11f));
        pen.Color = Color.FromArgb(150, 255, 255, 255); pen.Width = 2f;
        gfx.DrawLine(pen, SP(gx, gy, ax, ay, qx, qy, 20f, -6.5f), SP(gx, gy, ax, ay, qx, qy, 64f, -6.5f));
        // ombro, pescoço e botão
        FillPts(Color.FromArgb(255, 168, 174, 186), edge,
            SP(gx, gy, ax, ay, qx, qy, 10f, -4.5f), SP(gx, gy, ax, ay, qx, qy, 18f, -11f),
            SP(gx, gy, ax, ay, qx, qy, 18f, 11f),   SP(gx, gy, ax, ay, qx, qy, 10f, 4.5f));
        FillPts(Color.FromArgb(255, 40, 42, 48), edge,
            SP(gx, gy, ax, ay, qx, qy, 4f, -4.5f), SP(gx, gy, ax, ay, qx, qy, 10f, -4.5f),
            SP(gx, gy, ax, ay, qx, qy, 10f, 4.5f),  SP(gx, gy, ax, ay, qx, qy, 4f, 4.5f));
        FillPts(paint, edge,
            SP(gx, gy, ax, ay, qx, qy, 0f, -3f), SP(gx, gy, ax, ay, qx, qy, 4f, -3.5f),
            SP(gx, gy, ax, ay, qx, qy, 4f, 3.5f), SP(gx, gy, ax, ay, qx, qy, 0f, 3f));
        pen.Color = edge; pen.Width = 2f;
        gfx.DrawLine(pen, SP(gx, gy, ax, ay, qx, qy, 66f, -11f), SP(gx, gy, ax, ay, qx, qy, 66f, 11f));

        DrawHotspot(ox, oy);
    }

    static PointF SP(float gx, float gy, float ax, float ay, float qx, float qy, float t, float w)
    {
        return new PointF(gx + ax * t + qx * w, gy + ay * t + qy * w);
    }

    // ===================================================================
    //  BUGS NA TELA (Ctrl+Alt+E): 5 bugs, cada um numa janelinha própria
    // ===================================================================
    List<BugForm> bugs = new List<BugForm>();
    Stopwatch bugSw = new Stopwatch();
    double bugAcc = 0;

    void ToggleBugs()
    {
        if (bugs.Count > 0) ClearBugs(); else SpawnBugs(5);
        UpdateTray();
    }

    void SpawnBugs(int n)
    {
        Rectangle vs = SystemInformation.VirtualScreen;
        for (int i = 0; i < n; i++)
        {
            BugForm b = new BugForm(rnd, vs);
            b.Show();
            b.Render();
            bugs.Add(b);
        }
        bugSw.Restart(); bugAcc = 0;
    }

    void ClearBugs()
    {
        foreach (BugForm b in bugs)
        {
            try { b.Close(); b.Dispose(); } catch { }
        }
        bugs.Clear();
    }

    void StepBugsTimed()
    {
        if (bugs.Count == 0) return;
        bugAcc += bugSw.Elapsed.TotalMilliseconds;
        bugSw.Restart();
        if (bugAcc > 100) bugAcc = 100;
        int steps = 0;
        while (bugAcc >= STEP_MS && steps < 4)
        {
            foreach (BugForm b in bugs) b.Step();
            bugAcc -= STEP_MS;
            steps++;
        }
        if (steps > 0)
        {
            bool removed = false;
            for (int i = bugs.Count - 1; i >= 0; i--)
            {
                BugForm b = bugs[i];
                if (b.State == 2)
                {
                    try { b.Close(); b.Dispose(); } catch { }
                    bugs.RemoveAt(i);
                    removed = true;
                }
                else b.Render();
            }
            if (removed) UpdateTray();
        }
    }

    // ---------- Sons das armas novas ----------
    byte[] MakeAkWav()
    {
        int sr = 44100, n = (int)(sr * 0.24);
        double[] o = new double[n];
        double lp = 0, lp2 = 0, ph = 0, peak = 0.0001;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / sr;
            double x = rnd.NextDouble() * 2 - 1;
            double crack = x * Math.Exp(-t / 0.0035) * 1.1;
            lp += 0.18 * (x - lp);
            double body = lp * Math.Exp(-t / 0.03) * 1.8;
            ph += 2 * Math.PI * (60 + 110 * Math.Exp(-t / 0.02)) / sr;
            double thump = Math.Sin(ph) * Math.Exp(-t / 0.05) * 0.9;
            lp2 += 0.05 * (x - lp2);
            double tail = lp2 * Math.Exp(-t / 0.08) * 1.4;
            o[i] = crack + body + thump + tail;
            if (Math.Abs(o[i]) > peak) peak = Math.Abs(o[i]);
        }
        for (int i = 0; i < n; i++) o[i] = Math.Tanh(o[i] / peak * 1.8) * 0.95;
        return ToWav(o, sr);
    }

    byte[] MakeCatapultWav()
    {
        int sr = 44100, n = (int)(sr * 0.7);
        double[] o = new double[n];
        double ph = 0, ph2 = 0, lp = 0, lp2 = 0, peak = 0.0001;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / sr;
            double x = rnd.NextDouble() * 2 - 1;
            // rangido da madeira
            double creak = 0;
            if (t < 0.14)
            {
                ph += 2 * Math.PI * (210 - 700 * t * 0.8 + 40 * Math.Sin(t * 90)) / sr;
                double saw = ((ph / (2 * Math.PI)) % 1.0) * 2 - 1;
                creak = saw * 0.35 * Math.Min(1.0, t / 0.02) * (1 - t / 0.14);
            }
            // pancada do braço no batente
            double tt = t - 0.15;
            double hit = 0;
            if (tt >= 0)
            {
                ph2 += 2 * Math.PI * (55 + 120 * Math.Exp(-tt / 0.02)) / sr;
                hit = x * Math.Exp(-tt / 0.008) * 0.9 + Math.Sin(ph2) * Math.Exp(-tt / 0.07) * 1.1;
                lp += 0.06 * (x - lp);
                lp2 += 0.3 * (x - lp2);
                hit += (lp2 - lp) * Math.Exp(-tt / 0.12) * 1.2;          // whoosh da bola
            }
            o[i] = creak + hit;
            if (Math.Abs(o[i]) > peak) peak = Math.Abs(o[i]);
        }
        for (int i = 0; i < n; i++) o[i] = Math.Tanh(o[i] / peak * 1.5) * 0.95;
        return ToWav(o, sr);
    }

    byte[] MakeBoomWav()
    {
        int sr = 44100, n = (int)(sr * 0.9);
        double[] o = new double[n];
        double ph = 0, lp = 0, hp = 0, peak = 0.0001, cr = 0;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / sr;
            double x = rnd.NextDouble() * 2 - 1;
            ph += 2 * Math.PI * (34 + 70 * Math.Exp(-t / 0.08)) / sr;
            double boom = Math.Sin(ph) * Math.Exp(-t / 0.22) * 1.4;
            lp += 0.08 * (x - lp);
            double rumble = lp * Math.Exp(-t / 0.2) * 2.2;
            hp += 0.4 * (x - hp);
            double fizz = (x - hp) * Math.Exp(-t / 0.25) * 0.5;               // chiado do fogo azul
            if (rnd.NextDouble() < 0.002) cr = 1.0;
            cr *= 0.97;
            double crackle = (rnd.NextDouble() * 2 - 1) * cr * 0.3 * Math.Exp(-t / 0.5);
            double hit = x * Math.Exp(-t / 0.004);
            o[i] = boom + rumble + fizz + crackle + hit;
            if (Math.Abs(o[i]) > peak) peak = Math.Abs(o[i]);
        }
        for (int i = 0; i < n; i++) o[i] = Math.Tanh(o[i] / peak * 1.6) * 0.95;
        return ToWav(o, sr);
    }

    // chiado contínuo do spray (toca em loop enquanto o botão está apertado)
    byte[] MakeHissWav()
    {
        int sr = 22050, n = (int)(sr * 0.6);
        double[] o = new double[n];
        double lp = 0, mod = 0, modT = 0;
        for (int i = 0; i < n; i++)
        {
            double x = rnd.NextDouble() * 2 - 1;
            lp += 0.25 * (x - lp);
            modT += (rnd.NextDouble() - 0.5) * 0.02;
            mod += 0.02 * (modT - mod);
            double v = (x - lp) * (0.55 + 0.25 * Math.Sin(i * 0.004) + mod);
            double fade = Math.Min(1.0, Math.Min(i, n - 1 - i) / (sr * 0.03));
            o[i] = v * fade * 0.8;
        }
        return ToWav(o, sr);
    }

    byte[] MakeSquashWav()
    {
        int sr = 44100, n = (int)(sr * 0.22);
        double[] o = new double[n];
        double ph = 0, lp = 0, peak = 0.0001;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / sr;
            double x = rnd.NextDouble() * 2 - 1;
            lp += 0.12 * (x - lp);
            ph += 2 * Math.PI * (320 * Math.Exp(-t / 0.05) + 90) / sr;
            double pop = Math.Sin(ph) * Math.Exp(-t / 0.045);
            double splat = lp * Math.Exp(-t / 0.05) * 2.2;
            double wet = (rnd.NextDouble() < 0.02 ? (rnd.NextDouble() * 2 - 1) : 0) * Math.Exp(-t / 0.1) * 0.9;
            o[i] = pop + splat + wet;
            if (Math.Abs(o[i]) > peak) peak = Math.Abs(o[i]);
        }
        for (int i = 0; i < n; i++) o[i] = Math.Tanh(o[i] / peak * 1.5) * 0.9;
        return ToWav(o, sr);
    }

    void PlayAk()        { if (!soundOn) return; try { akPlayer.Play(); }     catch { } }
    void PlayCatapult()  { if (!soundOn) return; try { catPlayer.Play(); }    catch { } }
    void PlayBoom()      { if (!soundOn) return; try { boomPlayer.Play(); }   catch { } }
    void PlaySquash()    { if (!soundOn) return; if (hissOn) return; try { squashPlayer.Play(); } catch { } }


    // ---------- Helpers ----------
    static int Cl(float v) { int i = (int)v; return i < 0 ? 0 : (i > 255 ? 255 : i); }

    static Color Lerp(Color a, Color b, float t)
    {
        return Color.FromArgb(Cl(a.R+(b.R-a.R)*t), Cl(a.G+(b.G-a.G)*t), Cl(a.B+(b.B-a.B)*t));
    }

    static float WidthAt(int i)
    {
        float t = (float)i / (N - 1);
        return 1.2f + 4.8f * (float)Math.Pow(1 - t, 1.1);
    }

    // ---------- Sons ----------
    static byte[] ToWav(double[] s, int sr)
    {
        MemoryStream ms = new MemoryStream();
        BinaryWriter bw = new BinaryWriter(ms);
        int n = s.Length;
        bw.Write(new char[]{'R','I','F','F'});
        bw.Write(36 + n*2);
        bw.Write(new char[]{'W','A','V','E','f','m','t',' '});
        bw.Write(16); bw.Write((short)1); bw.Write((short)1);
        bw.Write(sr); bw.Write(sr*2); bw.Write((short)2); bw.Write((short)16);
        bw.Write(new char[]{'d','a','t','a'});
        bw.Write(n*2);
        for (int i = 0; i < n; i++)
        {
            double v = s[i];
            if (v >  1) v =  1;
            if (v < -1) v = -1;
            bw.Write((short)(v * 32767));
        }
        bw.Flush();
        return ms.ToArray();
    }

    byte[] MakeCrackWav()
    {
        int sr = 44100, n = (int)(sr * 0.6);
        double[] nz = new double[n];
        double[] o  = new double[n];
        for (int i = 0; i < n; i++) nz[i] = rnd.NextDouble()*2 - 1;
        double ph = 0, p1 = 0;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / sr;
            double d1 = nz[i] - (i > 0 ? nz[i-1] : 0);
            double d2 = d1 - p1; p1 = d1;
            double snap  = d2 * Math.Exp(-t/0.0045) * 0.9;
            double body  = d1 * Math.Exp(-t/0.025) * 0.35;
            double click = (t < 0.002) ? nz[i]*0.8 : 0;
            double f = 55 + 120*Math.Exp(-t/0.02);
            ph += 2*Math.PI*f/sr;
            double thump = Math.Sin(ph)*Math.Exp(-t/0.05)*0.45;
            o[i] = snap + body + click + thump;
        }
        double[] e = new double[n];
        int dA = (int)(sr*0.055), dB = (int)(sr*0.11);
        double peak = 0.0001;
        for (int i = 0; i < n; i++)
        {
            double v = o[i];
            if (i >= dA) v += o[i-dA]*0.22;
            if (i >= dB) v += o[i-dB]*0.10;
            e[i] = v;
            if (Math.Abs(v) > peak) peak = Math.Abs(v);
        }
        for (int i = 0; i < n; i++) e[i] = Math.Tanh(e[i]/peak*1.4)*0.95;
        return ToWav(e, sr);
    }

    byte[] MakeWhooshWav()
    {
        int sr = 44100, n = (int)(sr*0.28);
        double[] o = new double[n];
        double a = 0, b = 0, peak = 0.0001;
        for (int i = 0; i < n; i++)
        {
            double u = (double)i/n;
            double x = rnd.NextDouble()*2 - 1;
            a += (0.04 + 0.45*u)*(x-a);
            b += 0.03*(x-b);
            double env = Math.Sin(Math.PI*Math.Pow(u, 0.7));
            o[i] = (a-b)*env;
            if (Math.Abs(o[i]) > peak) peak = Math.Abs(o[i]);
        }
        for (int i = 0; i < n; i++) o[i] = o[i]/peak*0.55;
        return ToWav(o, sr);
    }

    // Pancada pesada de porrete: clack seco + madeira + "bum" grave + lascas
    byte[] MakeThudWav()
    {
        int sr = 44100, n = (int)(sr * 0.55);
        double[] o = new double[n];
        double peak = 0.0001;

        double ph1 = 0, ph2 = 0, ph3 = 0, ph4 = 0, phB = 0;
        double f1 = 820, f2 = 1640, f3 = 2600, f4 = 380;

        for (int i = 0; i < n; i++)
        {
            double t = (double)i / sr;
            double clack = (rnd.NextDouble() * 2 - 1) * Math.Exp(-t / 0.0025) * 1.1;

            ph1 += 2 * Math.PI * f1 / sr;
            ph2 += 2 * Math.PI * f2 / sr;
            ph3 += 2 * Math.PI * f3 / sr;
            ph4 += 2 * Math.PI * f4 / sr;
            double wood1 = Math.Sin(ph1) * Math.Exp(-t / 0.055) * 0.60;
            double wood2 = Math.Sin(ph2) * Math.Exp(-t / 0.030) * 0.30;
            double wood3 = Math.Sin(ph3) * Math.Exp(-t / 0.015) * 0.15;
            double wood4 = Math.Sin(ph4) * Math.Exp(-t / 0.080) * 0.25;

            double fb = 46 + 70 * Math.Exp(-t / 0.05);
            phB += 2 * Math.PI * fb / sr;
            double boom = Math.Sin(phB) * Math.Exp(-t / 0.11) * 1.1;

            double crunch = (rnd.NextDouble() * 2 - 1) * Math.Exp(-t / 0.03) * 0.35;

            o[i] = clack + wood1 + wood2 + wood3 + wood4 + boom + crunch;
            if (Math.Abs(o[i]) > peak) peak = Math.Abs(o[i]);
        }
        for (int i = 0; i < n; i++)
            o[i] = Math.Tanh(o[i] / peak * 1.4) * 0.95;
        return ToWav(o, sr);
    }

    // metal batendo em metal: parciais inarmônicos de aço, decaimento longo,
    // e um "tchk" brilhante no instante da batida
    void AddMetalStrike(double[] o, int sr, double t0, double f0, double amp, double decay)
    {
        int start = (int)(t0 * sr);
        int len = (int)(decay * 5 * sr);
        double[] mul = new double[] { 1.00, 1.59, 2.14, 2.65, 3.51, 4.23, 5.87 };
        double[] am  = new double[] { 1.00, 0.80, 0.65, 0.50, 0.35, 0.22, 0.12 };
        double lp = 0;
        for (int i = 0; i < len; i++)
        {
            int idx = start + i;
            if (idx >= o.Length) break;
            double t = (double)i / sr;
            double v = 0;
            for (int k = 0; k < 7; k++)
                v += Math.Sin(2 * Math.PI * f0 * mul[k] * t) * am[k] * Math.Exp(-t / (decay / (1 + k * 0.35)));
            double nz = rnd.NextDouble() * 2 - 1;
            lp += 0.6 * (nz - lp);
            v += (nz - lp) * Math.Exp(-t / 0.003) * 1.6;
            o[idx] += v * amp;
        }
    }

    // batida da corrente: metal contra metal (golpe duplo + toque grave + eco)
    byte[] MakeClankWav()
    {
        int sr = 44100, n = (int)(sr * 0.9);
        double[] o = new double[n];
        AddMetalStrike(o, sr, 0.000, 720,  1.0, 0.16);
        AddMetalStrike(o, sr, 0.014, 1080, 0.8, 0.12);
        AddMetalStrike(o, sr, 0.030, 460,  0.7, 0.20);

        double ph = 0;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / sr;
            double fq = 70 + 60 * Math.Exp(-t / 0.03);
            ph += 2 * Math.PI * fq / sr;
            o[i] += Math.Sin(ph) * Math.Exp(-t / 0.06) * 0.6;
        }

        double[] e = new double[n];
        int dA = (int)(sr * 0.06), dB = (int)(sr * 0.13);
        for (int i = 0; i < n; i++)
        {
            double v = o[i];
            if (i >= dA) v += o[i - dA] * 0.25;
            if (i >= dB) v += o[i - dB] * 0.12;
            e[i] = v;
        }
        double peak = 0.0001;
        for (int i = 0; i < n; i++) if (Math.Abs(e[i]) > peak) peak = Math.Abs(e[i]);
        for (int i = 0; i < n; i++) e[i] = Math.Tanh(e[i] / peak * 1.7) * 0.95;
        return ToWav(e, sr);
    }


    // disparo do arco: estalo da corda + sopro da flecha em chamas
    byte[] MakeShootWav()
    {
        int sr = 44100, n = (int)(sr * 0.55);
        double[] o = new double[n];
        double a = 0, b = 0, cr = 0, peak = 0.0001;
        double f0 = 170;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / sr;
            double str = 0;
            for (int h = 1; h <= 6; h++)
                str += Math.Sin(2 * Math.PI * f0 * h * t) / h * Math.Exp(-t / (0.22 / (1 + 0.35 * h)));
            double slap = (rnd.NextDouble() * 2 - 1) * Math.Exp(-t / 0.004) * 0.9;
            double x = rnd.NextDouble() * 2 - 1;
            a += (0.05 + 0.4 * Math.Min(1.0, t / 0.3)) * (x - a);
            b += 0.03 * (x - b);
            double env = Math.Exp(-t / 0.18) * (1 - Math.Exp(-t / 0.01));
            double whoosh = (a - b) * env * 1.6;
            if (rnd.NextDouble() < 0.0007) cr = 1.0;
            cr *= 0.985;
            double crackle = (rnd.NextDouble() * 2 - 1) * cr * 0.25 * (t > 0.06 ? 1.0 : 0.0);
            o[i] = str * 0.9 + slap + whoosh + crackle;
            if (Math.Abs(o[i]) > peak) peak = Math.Abs(o[i]);
        }
        for (int i = 0; i < n; i++) o[i] = Math.Tanh(o[i] / peak * 1.5) * 0.95;
        return ToWav(o, sr);
    }

    // flecha cravando na borda da tela: "toc" de madeira + estalos de fogo
    byte[] MakeStickWav()
    {
        int sr = 44100, n = (int)(sr * 0.35);
        double[] o = new double[n];
        double ph = 0, cr = 0, peak = 0.0001;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / sr;
            double fq = 140 + 180 * Math.Exp(-t / 0.02);
            ph += 2 * Math.PI * fq / sr;
            double thock = Math.Sin(ph) * Math.Exp(-t / 0.05);
            double hit = (rnd.NextDouble() * 2 - 1) * Math.Exp(-t / 0.004) * 0.8;
            if (rnd.NextDouble() < 0.0012) cr = 1.0;
            cr *= 0.98;
            double crackle = (rnd.NextDouble() * 2 - 1) * cr * 0.3;
            o[i] = thock + hit + crackle;
            if (Math.Abs(o[i]) > peak) peak = Math.Abs(o[i]);
        }
        for (int i = 0; i < n; i++) o[i] = Math.Tanh(o[i] / peak * 1.4) * 0.9;
        return ToWav(o, sr);
    }

    // Fsssh de lâmina cortando o ar — sibilante, rápido e afiado
    byte[] MakeSlashWav()
    {
        int sr = 44100, n = (int)(sr * 0.22);
        double[] o = new double[n];
        double a = 0, b = 0, peak = 0.0001;
        for (int i = 0; i < n; i++)
        {
            double u  = (double)i / n;
            double x  = rnd.NextDouble() * 2 - 1;
            // filtro passa-alto agressivo: sibilante metálico
            a += (0.08 + 0.55 * (1 - u)) * (x - a);
            b += 0.015 * (x - b);
            // envelope: ataque brevíssimo, queda rápida mas com pequeno tail
            double env = Math.Pow(1 - u, 1.6) * Math.Sin(Math.PI * Math.Pow(u, 0.18));
            // tonalidade de lâmina: sutil sine em ~3400 Hz que decai rápido
            double sing = Math.Sin(2 * Math.PI * 3400 * i / sr) * Math.Exp(-u / 0.04) * 0.18;
            o[i] = (a - b) * env + sing;
            if (Math.Abs(o[i]) > peak) peak = Math.Abs(o[i]);
        }
        for (int i = 0; i < n; i++) o[i] = Math.Tanh(o[i] / peak * 1.4) * 0.75;
        return ToWav(o, sr);
    }

    // Clinck metálico de aço — impacto de lâmina, ressonância cristalina
    byte[] MakeClangWav()
    {
        int sr = 44100, n = (int)(sr * 0.55);
        double[] o = new double[n];
        // múltiplos parciais inarmônicos de aço
        double[] freqs = { 1340, 2210, 3480, 4820, 680 };
        double[] amps  = { 0.55, 0.35, 0.22, 0.14, 0.40 };
        double[] decays= { 0.18, 0.10, 0.07, 0.05, 0.25 };
        double[] phases= new double[freqs.Length];
        double peak = 0.0001;

        for (int i = 0; i < n; i++)
        {
            double t = (double)i / sr;
            double v = 0;
            for (int p = 0; p < freqs.Length; p++)
            {
                phases[p] += 2 * Math.PI * freqs[p] / sr;
                v += Math.Sin(phases[p]) * amps[p] * Math.Exp(-t / decays[p]);
            }
            // ataque percussivo (transiente de metal)
            double hit = (rnd.NextDouble() * 2 - 1) * Math.Exp(-t / 0.0018) * 0.9;
            o[i] = v + hit;
            if (Math.Abs(o[i]) > peak) peak = Math.Abs(o[i]);
        }
        for (int i = 0; i < n; i++) o[i] = Math.Tanh(o[i] / peak * 1.5) * 0.88;
        return ToWav(o, sr);
    }

    void PlayCrack()       { if (!soundOn) return; try { crackPlayer.Play(); }      catch { } }
    void PlayWhoosh()      { if (!soundOn) return; try { whooshPlayer.Play(); }     catch { } }
    void PlayThud()        { if (!soundOn) return; try { thudPlayer.Play(); }       catch { } }
    void PlaySlash()       { if (!soundOn) return; try { slashPlayer.Play(); }      catch { } }
    void PlayClang()       { if (!soundOn) return; try { clangPlayer.Play(); }      catch { } }
    void PlayClank()       { if (!soundOn) return; try { clankPlayer.Play(); }      catch { } }
    void PlayShoot()       { if (!soundOn) return; try { shootPlayer.Play(); }      catch { } }
    void PlayStick()       { if (!soundOn) return; try { stickPlayer.Play(); }      catch { } }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        RemoveMouseHook();
        UnregisterHotKey(Handle, HOTKEY_ID);
        UnregisterHotKey(Handle, HOTKEY_ID + 1);
        UnregisterHotKey(Handle, HOTKEY_ID + 2);
        UnregisterHotKey(Handle, HOTKEY_ID + 3);
        ClearBugs();
        RestoreCursor();
        base.OnFormClosed(e);
    }

    [STAThread]
    static void Main(string[] args)
    {
        foreach (string a in args)
        {
            string l = a.ToLower();
            if (l == "/restaurar") { RestoreCursorStatic(); return; }
            if (l == "/boot") bootLaunch = true;
        }
        bool created;
        System.Threading.Mutex m = new System.Threading.Mutex(true, "WhipCursorSingleInstance", out created);
        if (!created) return;

        SetProcessDPIAware();
        Application.EnableVisualStyles();
        Application.Run(new Whip());
        GC.KeepAlive(m);
    }
}

// ===================================================================
//  Um bug na tela: janela própria, transparente e "atravessável" pelo mouse.
//  Anda sozinho; qualquer arma que o acerte o esmaga.
// ===================================================================
public class BugForm : Form
{
    const int S = 150;

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

    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
    [DllImport("user32.dll", SetLastError = true)]
    static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
        IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr o);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr o);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")]
    static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER pbmi, uint iUsage,
        out IntPtr ppvBits, IntPtr hSection, uint dwOffset);

    class Drop { public float x, y, vx, vy, r; public Color c; }

    public float X, Y, VX, VY, Ang, Speed;
    public int State;            // 0 vivo, 1 morrendo, 2 pronto para remover
    public int Poison;           // veneno do spray
    Color tint = Color.Transparent;

    Random rnd;
    Rectangle bounds;
    int age, dieF, pauseF, turnT;
    float tgtAng, wantSpeed, legPh;
    IntPtr memDc, hDib;
    Bitmap bmp;
    Graphics g;
    Pen pen;
    List<Drop> drops = new List<Drop>();
    float[] bx = new float[9], by = new float[9], br = new float[9];

    public BugForm(Random r, Rectangle vs)
    {
        rnd = r;
        bounds = new Rectangle(vs.Left + 50, vs.Top + 50, Math.Max(100, vs.Width - 100), Math.Max(100, vs.Height - 100));
        X = bounds.Left + (float)rnd.NextDouble() * bounds.Width;
        Y = bounds.Top + (float)rnd.NextDouble() * bounds.Height;
        Ang = tgtAng = (float)(rnd.NextDouble() * Math.PI * 2);
        wantSpeed = Speed = 1.4f + (float)rnd.NextDouble() * 1.6f;
        turnT = 20 + rnd.Next(40);
        legPh = (float)(rnd.NextDouble() * 6.0);

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar   = false;
        TopMost         = true;
        StartPosition   = FormStartPosition.Manual;
        Location        = new Point(-32000, -32000);
        Size            = new Size(S, S);

        IntPtr sdc = GetDC(IntPtr.Zero);
        memDc = CreateCompatibleDC(sdc);
        BITMAPINFOHEADER bi = new BITMAPINFOHEADER();
        bi.biSize = 40; bi.biWidth = S; bi.biHeight = -S;
        bi.biPlanes = 1; bi.biBitCount = 32; bi.biCompression = 0;
        IntPtr bits;
        hDib = CreateDIBSection(sdc, ref bi, 0, out bits, IntPtr.Zero, 0);
        ReleaseDC(IntPtr.Zero, sdc);
        SelectObject(memDc, hDib);
        bmp = new Bitmap(S, S, S * 4, PixelFormat.Format32bppPArgb, bits);
        g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        pen = new Pen(Color.Black);
        pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round; pen.LineJoin = LineJoin.Round;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x80000 | 0x20 | 0x80 | 0x8 | 0x08000000;   // layered, click-through, toolwindow, topmost, noactivate
            return cp;
        }
    }

    protected override bool ShowWithoutActivation { get { return true; } }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        try
        {
            if (g != null) g.Dispose();
            if (bmp != null) bmp.Dispose();
            if (hDib != IntPtr.Zero) DeleteObject(hDib);
            if (memDc != IntPtr.Zero) DeleteDC(memDc);
            if (pen != null) pen.Dispose();
        }
        catch { }
        g = null; hDib = IntPtr.Zero; memDc = IntPtr.Zero;
        base.OnFormClosed(e);
    }

    // ---------- Lógica ----------
    public void Step()
    {
        age++;
        if (State == 1) { StepDeath(); return; }
        if (State != 0) return;

        if (Poison > 0 && age % 3 == 0) Poison--;
        float boost = 1f + Poison * 0.045f;          // intoxicado corre mais

        if (pauseF > 0) { pauseF--; Speed *= 0.8f; }
        else
        {
            turnT--;
            if (turnT <= 0)
            {
                tgtAng = Ang + ((float)rnd.NextDouble() * 2f - 1f) * 1.3f;
                turnT = 18 + rnd.Next(50);
                wantSpeed = 1.2f + (float)rnd.NextDouble() * 2.4f;
                if (rnd.NextDouble() < 0.14) pauseF = 18 + rnd.Next(40);
            }
            Speed += (wantSpeed - Speed) * 0.1f;
        }

        // saiu da área: volta para o meio
        if (X < bounds.Left || X > bounds.Right || Y < bounds.Top || Y > bounds.Bottom)
        {
            tgtAng = (float)Math.Atan2(bounds.Top + bounds.Height / 2f - Y, bounds.Left + bounds.Width / 2f - X);
            turnT = 25; pauseF = 0;
        }

        float diff = tgtAng - Ang;
        while (diff > (float)Math.PI)  diff -= 2f * (float)Math.PI;
        while (diff < -(float)Math.PI) diff += 2f * (float)Math.PI;
        if (diff > 0.09f) diff = 0.09f;
        if (diff < -0.09f) diff = -0.09f;
        Ang += diff;

        VX = (float)Math.Cos(Ang) * Speed * boost;
        VY = (float)Math.Sin(Ang) * Speed * boost;
        X += VX; Y += VY;
        legPh += 0.10f + Speed * boost * 0.22f;
    }

    public bool HitSeg(float x0, float y0, float x1, float y1, float r)
    {
        float vx = x1 - x0, vy = y1 - y0;
        float wx = X - x0, wy = Y - y0;
        float l2 = vx * vx + vy * vy;
        float t = (l2 > 0.0001f) ? (wx * vx + wy * vy) / l2 : 0f;
        if (t < 0f) t = 0f;
        if (t > 1f) t = 1f;
        float px = x0 + vx * t - X, py = y0 + vy * t - Y;
        float rr = r + 19f;
        return px * px + py * py <= rr * rr;
    }

    // devolve true se o veneno matou
    public bool PoisonHit(float x, float y, float r, Color c)
    {
        float dx = X - x, dy = Y - y;
        float rr = r + 19f;
        if (dx * dx + dy * dy > rr * rr) return false;
        Poison += 2;
        tint = c;
        if (Poison >= 40) { Kill(0f, 0f); return true; }
        return false;
    }

    public void Kill(float dx, float dy)
    {
        if (State != 0) return;
        State = 1; dieF = 0;
        float l = (float)Math.Sqrt(dx * dx + dy * dy);
        if (l > 0.01f) { dx /= l; dy /= l; } else { dx = 0f; dy = 0f; }

        for (int i = 0; i < bx.Length; i++)
        {
            bx[i] = (i == 0) ? 0f : (float)(rnd.NextDouble() * 26 - 13);
            by[i] = (i == 0) ? 0f : (float)(rnd.NextDouble() * 26 - 13);
            br[i] = (i == 0) ? 15f : 4f + (float)rnd.NextDouble() * 8f;
        }
        bool tinted = tint.A > 0 && Poison > 0;
        for (int i = 0; i < 28; i++)
        {
            double a = rnd.NextDouble() * Math.PI * 2;
            double sp = 1.5 + rnd.NextDouble() * 6.5;
            Drop d = new Drop();
            d.x = 0f; d.y = 0f;
            d.vx = (float)(Math.Cos(a) * sp) + dx * 2.5f;
            d.vy = (float)(Math.Sin(a) * sp) + dy * 2.5f;
            d.r = 1.8f + (float)rnd.NextDouble() * 3.8f;
            d.c = (tinted && i % 2 == 0) ? tint : Color.FromArgb(70 + rnd.Next(70), 150 + rnd.Next(60), 30 + rnd.Next(40));
            drops.Add(d);
        }
    }

    void StepDeath()
    {
        dieF++;
        foreach (Drop d in drops)
        {
            d.x += d.vx; d.y += d.vy;
            d.vx *= 0.9f; d.vy = d.vy * 0.9f + 0.25f;
        }
        if (dieF >= 46) State = 2;
    }

    // ---------- Desenho ----------
    public void Render()
    {
        if (g == null) return;
        g.Clear(Color.Transparent);
        g.ResetTransform();
        if (State == 1) DrawDeath(); else if (State == 0) DrawBug();

        POINT dst = new POINT(); dst.x = (int)(X - S / 2f); dst.y = (int)(Y - S / 2f);
        SIZE size = new SIZE(); size.cx = S; size.cy = S;
        POINT src = new POINT();
        BLENDFUNCTION bf = new BLENDFUNCTION();
        bf.BlendOp = 0; bf.BlendFlags = 0; bf.SourceConstantAlpha = 255; bf.AlphaFormat = 1;
        UpdateLayeredWindow(Handle, IntPtr.Zero, ref dst, ref size, memDc, ref src, 0, ref bf, 2);
    }

    static Color Blend(Color a, Color b, float t)
    {
        return Color.FromArgb(255, (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
    }

    void DrawBug()
    {
        float sc = (age < 16) ? 0.3f + 0.7f * (age / 16f) : 1f;
        Color body = Color.FromArgb(255, 96, 192, 60);
        Color stripe = Color.FromArgb(255, 54, 130, 40);
        Color dark = Color.FromArgb(255, 24, 62, 22);
        if (Poison > 0 && tint.A > 0)
        {
            float k = Math.Min(1f, Poison / 30f) * 0.7f;
            body = Blend(body, tint, k);
            stripe = Blend(stripe, tint, k * 0.6f);
        }

        g.TranslateTransform(S / 2f, S / 2f);
        g.RotateTransform(Ang * 57.29578f);
        g.ScaleTransform(sc, sc);

        using (SolidBrush sh = new SolidBrush(Color.FromArgb(48, 0, 0, 0)))
            g.FillEllipse(sh, -26f, -4f, 62f, 24f);

        // pernas (marcha em tripé)
        pen.Color = dark; pen.Width = 2.4f;
        for (int k = 0; k < 3; k++)
        {
            for (int sd = -1; sd <= 1; sd += 2)
            {
                float ph = legPh + (((k + (sd > 0 ? 1 : 0)) % 2 == 0) ? 0f : (float)Math.PI);
                float sw = (float)Math.Sin(ph);
                float lf = Math.Max(0f, (float)Math.Cos(ph));
                float bf = 7f - k * 7f;
                float kneeF = bf + ((k == 0) ? 7f : (k == 1 ? 1f : -8f)) + sw * 3f;
                float footF = bf + ((k == 0) ? 12f : (k == 1 ? 3f : -13f)) + sw * 6f;
                float kneeV = sd * (12f + lf * 2f);
                float footV = sd * (20f - lf * 1.5f);
                g.DrawLine(pen, bf, sd * 3f, kneeF, kneeV);
                g.DrawLine(pen, kneeF, kneeV, footF, footV);
            }
        }

        // antenas
        pen.Width = 1.6f;
        for (int sd = -1; sd <= 1; sd += 2)
        {
            float wig = (float)Math.Sin(age * 0.3 + sd) * 2.5f;
            g.DrawBezier(pen, 19f, sd * 2.5f, 26f, sd * (5f + wig), 31f, sd * (9f + wig * 2f), 38f, sd * (8f + wig * 3f));
        }

        // abdômen
        using (SolidBrush b1 = new SolidBrush(body)) g.FillEllipse(b1, -26f, -10f, 26f, 20f);
        pen.Color = stripe; pen.Width = 2f;
        for (int i = 0; i < 3; i++)
        {
            float xs = -20f + i * 6f;
            float u = (xs + 13f) / 13f;
            float hh = 10f * (float)Math.Sqrt(Math.Max(0.0, 1.0 - u * u));
            g.DrawLine(pen, xs, -hh + 1f, xs, hh - 1f);
        }
        pen.Color = dark; pen.Width = 1.6f;
        g.DrawEllipse(pen, -26f, -10f, 26f, 20f);
        using (SolidBrush hl = new SolidBrush(Color.FromArgb(80, 255, 255, 255)))
            g.FillEllipse(hl, -21f, -8f, 12f, 5f);

        // tórax
        using (SolidBrush b2 = new SolidBrush(Blend(body, Color.White, 0.08f))) g.FillEllipse(b2, -3f, -7f, 16f, 14f);
        pen.Color = dark; g.DrawEllipse(pen, -3f, -7f, 16f, 14f);

        // cabeça
        using (SolidBrush b3 = new SolidBrush(Blend(body, dark, 0.2f))) g.FillEllipse(b3, 11f, -6f, 12f, 12f);
        pen.Color = dark; g.DrawEllipse(pen, 11f, -6f, 12f, 12f);
        pen.Width = 1.4f;
        g.DrawLine(pen, 22f, -2f, 26f, -3.5f);
        g.DrawLine(pen, 22f, 2f, 26f, 3.5f);

        // olhos
        for (int sd = -1; sd <= 1; sd += 2)
        {
            g.FillEllipse(Brushes.White, 16.5f, sd * 4.6f - 2.5f, 5f, 5f);
            g.FillEllipse(Brushes.Black, 18.4f, sd * 4.6f - 1.2f, 2.4f, 2.4f);
        }

        g.ResetTransform();
    }

    void DrawDeath()
    {
        float u = dieF / 46f;
        float cx = S / 2f, cy = S / 2f;
        float grow = Math.Min(1f, dieF / 6f);

        // mancha
        int sa = (int)(215 * (1f - u * u));
        if (sa < 0) sa = 0;
        Color stain = (Poison > 0 && tint.A > 0) ? Blend(Color.FromArgb(255, 80, 150, 40), tint, 0.5f) : Color.FromArgb(255, 80, 150, 40);
        for (int i = 0; i < bx.Length; i++)
        {
            float r = br[i] * (0.6f + 0.4f * grow);
            using (SolidBrush sb = new SolidBrush(Color.FromArgb(sa, stain.R, stain.G, stain.B)))
                g.FillEllipse(sb, cx + bx[i] - r, cy + by[i] - r, r * 2f, r * 2f);
        }
        using (SolidBrush sb2 = new SolidBrush(Color.FromArgb((int)(sa * 0.6f), 40, 90, 24)))
            g.FillEllipse(sb2, cx - 7f, cy - 6f, 14f, 12f);

        // clarão do impacto
        if (dieF < 9)
        {
            float rr = dieF * 5f;
            pen.Color = Color.FromArgb((int)(220 * (1f - dieF / 9f)), 230, 255, 180);
            pen.Width = 3f * (1f - dieF / 9f) + 0.6f;
            g.DrawEllipse(pen, cx - rr, cy - rr, rr * 2f, rr * 2f);
        }

        // gotas voando
        foreach (Drop d in drops)
        {
            float r = d.r * (1f - u * 0.5f);
            int da = (int)(240 * (1f - u));
            if (da < 0) da = 0;
            using (SolidBrush db = new SolidBrush(Color.FromArgb(da, d.c.R, d.c.G, d.c.B)))
                g.FillEllipse(db, cx + d.x - r, cy + d.y - r, r * 2f, r * 2f);
        }
    }
}
