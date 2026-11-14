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

// ===================================================================
//  Painel de pontos (HUD): janela transparente no topo da tela
// ===================================================================
public class HudForm : Form
{
    public static Color Accent = Color.FromArgb(255, 255, 214, 70);
    const int HW = 680, HH = 210;

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

    IntPtr memDc, hDib;
    Bitmap bmp;
    Graphics g;
    Font f1, f2, fBig, fSub;

    public HudForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar   = false;
        TopMost         = true;
        StartPosition   = FormStartPosition.Manual;
        Location        = new Point(-32000, -32000);
        Size            = new Size(HW, HH);

        IntPtr sdc = GetDC(IntPtr.Zero);
        memDc = CreateCompatibleDC(sdc);
        BITMAPINFOHEADER bi = new BITMAPINFOHEADER();
        bi.biSize = 40; bi.biWidth = HW; bi.biHeight = -HH;
        bi.biPlanes = 1; bi.biBitCount = 32; bi.biCompression = 0;
        IntPtr bits;
        hDib = CreateDIBSection(sdc, ref bi, 0, out bits, IntPtr.Zero, 0);
        ReleaseDC(IntPtr.Zero, sdc);
        SelectObject(memDc, hDib);
        bmp = new Bitmap(HW, HH, HW * 4, PixelFormat.Format32bppPArgb, bits);
        g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        try
        {
            f1 = new Font("Segoe UI", 13f, FontStyle.Bold);
            f2 = new Font("Segoe UI", 10f, FontStyle.Regular);
            fBig = new Font("Segoe UI", 30f, FontStyle.Bold);
            fSub = new Font("Segoe UI", 13f, FontStyle.Bold);
        }
        catch
        {
            f1 = new Font(FontFamily.GenericSansSerif, 13f, FontStyle.Bold);
            f2 = new Font(FontFamily.GenericSansSerif, 10f, FontStyle.Regular);
            fBig = new Font(FontFamily.GenericSansSerif, 30f, FontStyle.Bold);
            fSub = new Font(FontFamily.GenericSansSerif, 13f, FontStyle.Bold);
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
            if (f1 != null) f1.Dispose();
            if (f2 != null) f2.Dispose();
            if (fBig != null) fBig.Dispose();
            if (fSub != null) fSub.Dispose();
        }
        catch { }
        g = null; hDib = IntPtr.Zero; memDc = IntPtr.Zero;
        base.OnFormClosed(e);
    }

    static GraphicsPath Round(RectangleF r, float rad)
    {
        GraphicsPath p = new GraphicsPath();
        float d = rad * 2f;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public void Draw(string line1, string line2, string banner, string sub, float bannerAlpha)
    {
        if (g == null) return;
        g.Clear(Color.Transparent);

        using (GraphicsPath gp = Round(new RectangleF(30f, 0f, 620f, 62f), 12f))
        {
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(165, 10, 12, 18))) g.FillPath(bg, gp);
            using (Pen bp = new Pen(Color.FromArgb(110, Accent.R, Accent.G, Accent.B), 1.4f)) g.DrawPath(bp, gp);
        }
        using (SolidBrush w = new SolidBrush(Color.FromArgb(255, 255, 255, 255))) g.DrawString(line1, f1, w, 46f, 7f);
        using (SolidBrush gr = new SolidBrush(Color.FromArgb(255, 200, 206, 216))) g.DrawString(line2, f2, gr, 46f, 35f);

        if (banner != null && banner.Length > 0 && bannerAlpha > 0f)
        {
            int a = (int)(255 * Math.Min(1f, bannerAlpha));
            SizeF ts = g.MeasureString(banner, fBig);
            float bx = (HW - ts.Width) / 2f, by = 86f;
            using (SolidBrush sh = new SolidBrush(Color.FromArgb(a * 3 / 4, 0, 0, 0)))
                g.DrawString(banner, fBig, sh, bx + 2f, by + 2f);
            using (SolidBrush gold = new SolidBrush(Color.FromArgb(a, Accent.R, Accent.G, Accent.B)))
                g.DrawString(banner, fBig, gold, bx, by);
            if (sub != null && sub.Length > 0)
            {
                SizeF ss = g.MeasureString(sub, fSub);
                float sx = (HW - ss.Width) / 2f, sy = by + ts.Height - 2f;
                using (SolidBrush sh2 = new SolidBrush(Color.FromArgb(a * 3 / 4, 0, 0, 0)))
                    g.DrawString(sub, fSub, sh2, sx + 1.5f, sy + 1.5f);
                using (SolidBrush wh = new SolidBrush(Color.FromArgb(a, 255, 255, 255)))
                    g.DrawString(sub, fSub, wh, sx, sy);
            }
        }

        Rectangle sb = Screen.PrimaryScreen.Bounds;
        POINT dst = new POINT(); dst.x = sb.Left + (sb.Width - HW) / 2; dst.y = sb.Top + 8;
        SIZE size = new SIZE(); size.cx = HW; size.cy = HH;
        POINT src = new POINT();
        BLENDFUNCTION bf = new BLENDFUNCTION();
        bf.BlendOp = 0; bf.BlendFlags = 0; bf.SourceConstantAlpha = 255; bf.AlphaFormat = 1;
        UpdateLayeredWindow(Handle, IntPtr.Zero, ref dst, ref size, memDc, ref src, 0, ref bf, 2);
    }
}
