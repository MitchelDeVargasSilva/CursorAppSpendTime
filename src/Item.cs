using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// Item solto por um bug: passe o cursor por cima para pegar.
// Tipos: 0 munição/recarga, 1 gelo (congela os bugs), 2 fúria (pontos em dobro)
public class ItemForm : Form
{
    const int S = 72;
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

    public float X, Y;
    public int Kind;
    public int Life = 480;
    int age;
    IntPtr memDc, hDib;
    Bitmap bmp;
    Graphics g;
    static Font font;

    public ItemForm(float x, float y, int kind)
    {
        X = x; Y = y; Kind = kind;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false; TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(-32000, -32000);
        Size = new Size(S, S);
        IntPtr sdc = GetDC(IntPtr.Zero);
        memDc = CreateCompatibleDC(sdc);
        BITMAPINFOHEADER bi = new BITMAPINFOHEADER();
        bi.biSize = 40; bi.biWidth = S; bi.biHeight = -S; bi.biPlanes = 1; bi.biBitCount = 32;
        IntPtr bits;
        hDib = CreateDIBSection(sdc, ref bi, 0, out bits, IntPtr.Zero, 0);
        ReleaseDC(IntPtr.Zero, sdc);
        SelectObject(memDc, hDib);
        bmp = new Bitmap(S, S, S * 4, PixelFormat.Format32bppPArgb, bits);
        g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        if (font == null)
        {
            try { font = new Font("Segoe UI", 10f, FontStyle.Bold); }
            catch { font = new Font(FontFamily.GenericSansSerif, 10f, FontStyle.Bold); }
        }
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

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        try
        {
            if (g != null) g.Dispose();
            if (bmp != null) bmp.Dispose();
            if (hDib != IntPtr.Zero) DeleteObject(hDib);
            if (memDc != IntPtr.Zero) DeleteDC(memDc);
        }
        catch { }
        g = null; hDib = IntPtr.Zero; memDc = IntPtr.Zero;
        base.OnFormClosed(e);
    }

    public bool Dead { get { return Life <= 0; } }
    public void Step() { age++; Life--; }

    public static string Label(int kind, bool en)
    {
        if (kind == 0) return en ? "AMMO" : "MUN";
        if (kind == 1) return en ? "FREEZE" : "GELO";
        return "x2";
    }

    public void Render(bool en)
    {
        if (g == null) return;
        g.Clear(Color.Transparent);
        if (Life < 120 && (Life / 6) % 2 == 0) { Push(); return; }
        float bob = (float)Math.Sin(age * 0.12) * 3f;
        Color c = (Kind == 0) ? Color.FromArgb(255, 96, 200, 90) : (Kind == 1 ? Color.FromArgb(255, 110, 190, 255) : Color.FromArgb(255, 255, 170, 40));
        float cx = S / 2f, cy = S / 2f + bob;
        using (SolidBrush glow = new SolidBrush(Color.FromArgb(70, c))) g.FillEllipse(glow, cx - 30f, cy - 30f, 60f, 60f);
        using (SolidBrush b = new SolidBrush(Color.FromArgb(235, 24, 28, 36))) g.FillEllipse(b, cx - 22f, cy - 22f, 44f, 44f);
        using (Pen p = new Pen(c, 3f)) g.DrawEllipse(p, cx - 22f, cy - 22f, 44f, 44f);
        string t = Label(Kind, en);
        SizeF ts = g.MeasureString(t, font);
        using (SolidBrush tb = new SolidBrush(c)) g.DrawString(t, font, tb, cx - ts.Width / 2f, cy - ts.Height / 2f);
        Push();
    }

    void Push()
    {
        POINT dst = new POINT(); dst.x = (int)(X - S / 2f); dst.y = (int)(Y - S / 2f);
        SIZE size = new SIZE(); size.cx = S; size.cy = S;
        POINT src = new POINT();
        BLENDFUNCTION bf = new BLENDFUNCTION();
        bf.BlendOp = 0; bf.BlendFlags = 0; bf.SourceConstantAlpha = 255; bf.AlphaFormat = 1;
        UpdateLayeredWindow(Handle, IntPtr.Zero, ref dst, ref size, memDc, ref src, 0, ref bf, 2);
    }
}
