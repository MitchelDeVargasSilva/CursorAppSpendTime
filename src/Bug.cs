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
//  Um bug na tela: janela própria, transparente e "atravessável" pelo mouse.
//  Tipos: 0 normal, 1 corredor (foge do cursor), 2 tanque (aguenta golpes),
//  3 explosivo (explode ao morrer e leva os vizinhos junto).
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
    public int Kind;             // 0 normal, 1 corredor, 2 tanque, 3 explosivo
    public float BodyScale = 1f;
    public int Hp = 1, MaxHp = 1;
    public bool Boom;            // explosivo morreu: o jogo dispara a explosão
    public string Pop = "";      // pontos que sobem ao morrer
    Color tint = Color.Transparent;

    Random rnd;
    Rectangle bounds;
    int age, dieF, pauseF, turnT, hitCd, hurt;
    float tgtAng, wantSpeed, legPh, speedMul;
    bool fleeing;
    IntPtr memDc, hDib;
    Bitmap bmp;
    Graphics g;
    Pen pen;
    List<Drop> drops = new List<Drop>();
    float[] bx = new float[9], by = new float[9], br = new float[9];
    static Font popFont;

    public bool Frozen;
    public int PoisonLimit { get { return Kind == 4 ? 400 : (Kind == 2 ? 100 : 40); } }

    public BugForm(Random r, Rectangle vs, int kind, float spMul, int hp = 0)
    {
        rnd = r;
        Kind = kind;
        BodyScale = (kind == 1) ? 0.8f : (kind == 2 ? 1.55f : (kind == 3 ? 1.1f : (kind == 4 ? 1.7f : 1f)));
        Hp = MaxHp = (kind == 2) ? 4 : (kind == 4 ? Math.Max(1, hp) : 1);
        speedMul = spMul * ((kind == 1) ? 2.0f : (kind == 2 ? 0.6f : (kind == 3 ? 0.9f : (kind == 4 ? 0.75f : 1f))));

        bounds = new Rectangle(vs.Left + 50, vs.Top + 50, Math.Max(100, vs.Width - 100), Math.Max(100, vs.Height - 100));
        X = bounds.Left + (float)rnd.NextDouble() * bounds.Width;
        Y = bounds.Top + (float)rnd.NextDouble() * bounds.Height;
        Ang = tgtAng = (float)(rnd.NextDouble() * Math.PI * 2);
        wantSpeed = Speed = (1.4f + (float)rnd.NextDouble() * 1.6f) * speedMul;
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
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        pen = new Pen(Color.Black);
        pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round; pen.LineJoin = LineJoin.Round;
        if (popFont == null)
        {
            try { popFont = new Font("Segoe UI", 12f, FontStyle.Bold); }
            catch { popFont = new Font(FontFamily.GenericSansSerif, 12f, FontStyle.Bold); }
        }
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
    public void Step(int cmx, int cmy, bool flee)
    {
        age++;
        if (hitCd > 0) hitCd--;
        if (hurt > 0) hurt--;
        if (State == 1) { StepDeath(); return; }
        if (State != 0) return;
        if (Frozen) return;

        if (Poison > 0 && age % 3 == 0) Poison--;
        float boost = 1f + Poison * 0.045f;          // intoxicado corre mais

        // corredor: foge do cursor quando a arma está ligada
        fleeing = false;
        if (Kind == 1 && flee)
        {
            float fx = X - cmx, fy = Y - cmy;
            if (fx * fx + fy * fy < 190f * 190f)
            {
                tgtAng = (float)Math.Atan2(fy, fx) + ((float)rnd.NextDouble() - 0.5f) * 0.6f;
                turnT = 8; pauseF = 0; fleeing = true;
                boost *= 1.6f;
            }
        }

        if (pauseF > 0) { pauseF--; Speed *= 0.8f; }
        else
        {
            turnT--;
            if (turnT <= 0)
            {
                tgtAng = Ang + ((float)rnd.NextDouble() * 2f - 1f) * 1.3f;
                turnT = 18 + rnd.Next(50);
                wantSpeed = (1.2f + (float)rnd.NextDouble() * 2.4f) * speedMul;
                if (Kind != 1 && rnd.NextDouble() < 0.14) pauseF = 18 + rnd.Next(40);
            }
            Speed += (wantSpeed - Speed) * 0.1f;
        }

        // saiu da área: volta para o meio
        if (X < bounds.Left || X > bounds.Right || Y < bounds.Top || Y > bounds.Bottom)
        {
            tgtAng = (float)Math.Atan2(bounds.Top + bounds.Height / 2f - Y, bounds.Left + bounds.Width / 2f - X);
            turnT = 25; pauseF = 0;
        }

        float maxTurn = (Kind == 1) ? 0.17f : 0.09f;
        float diff = tgtAng - Ang;
        while (diff > (float)Math.PI)  diff -= 2f * (float)Math.PI;
        while (diff < -(float)Math.PI) diff += 2f * (float)Math.PI;
        if (diff > maxTurn) diff = maxTurn;
        if (diff < -maxTurn) diff = -maxTurn;
        Ang += diff;

        VX = (float)Math.Cos(Ang) * Speed * boost;
        VY = (float)Math.Sin(Ang) * Speed * boost;
        X += VX; Y += VY;
        legPh += 0.10f + Speed * boost * 0.22f / Math.Max(0.8f, BodyScale);
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
        float rr = r + 19f * BodyScale;
        return px * px + py * py <= rr * rr;
    }

    // dano: 0 ignorado (recuperando), 1 acertou mas aguentou, 2 morreu
    public int Damage(float dx, float dy, int dmg, bool force)
    {
        if (State != 0) return 0;
        if (hitCd > 0 && !force) return 0;
        Hp -= dmg;
        hitCd = 8; hurt = 6;
        if (Hp <= 0) { Kill(dx, dy); return 2; }
        return 1;
    }

    // devolve true se o veneno matou
    public bool PoisonHit(float x, float y, float r, Color c)
    {
        float dx = X - x, dy = Y - y;
        float rr = r + 19f * BodyScale;
        if (dx * dx + dy * dy > rr * rr) return false;
        Poison += 2;
        tint = c;
        if (Poison >= PoisonLimit) { Kill(0f, 0f); return true; }
        return false;
    }

    void Kill(float dx, float dy)
    {
        if (State != 0) return;
        State = 1; dieF = 0;
        if (Kind == 3) Boom = true;
        float l = (float)Math.Sqrt(dx * dx + dy * dy);
        if (l > 0.01f) { dx /= l; dy /= l; } else { dx = 0f; dy = 0f; }

        for (int i = 0; i < bx.Length; i++)
        {
            bx[i] = (i == 0) ? 0f : (float)(rnd.NextDouble() * 26 - 13) * BodyScale;
            by[i] = (i == 0) ? 0f : (float)(rnd.NextDouble() * 26 - 13) * BodyScale;
            br[i] = ((i == 0) ? 15f : 4f + (float)rnd.NextDouble() * 8f) * BodyScale;
        }
        bool tinted = tint.A > 0 && Poison > 0;
        int nd = (Kind == 3) ? 44 : 28;
        for (int i = 0; i < nd; i++)
        {
            double a = rnd.NextDouble() * Math.PI * 2;
            double sp = 1.5 + rnd.NextDouble() * (Kind == 3 ? 9.0 : 6.5);
            Drop d = new Drop();
            d.x = 0f; d.y = 0f;
            d.vx = (float)(Math.Cos(a) * sp) + dx * 2.5f;
            d.vy = (float)(Math.Sin(a) * sp) + dy * 2.5f;
            d.r = (1.8f + (float)rnd.NextDouble() * 3.8f) * (Kind == 2 ? 1.4f : 1f);
            if (Kind == 3) d.c = Color.FromArgb(255, 200 + rnd.Next(55), 60 + rnd.Next(120), 20);
            else if (tinted && i % 2 == 0) d.c = tint;
            else d.c = BodyColor(Kind, 0.6f);
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
        if (t < 0f) t = 0f;
        if (t > 1f) t = 1f;
        return Color.FromArgb(255, (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
    }

    static Color BodyColor(int kind, float lighten)
    {
        Color c;
        if (kind == 1)      c = Color.FromArgb(255, 240, 176, 48);
        else if (kind == 2) c = Color.FromArgb(255, 96, 104, 200);
        else if (kind == 3) c = Color.FromArgb(255, 176, 38, 38);
        else if (kind == 4) c = Color.FromArgb(255, 150, 70, 200);
        else                c = Color.FromArgb(255, 96, 192, 60);
        if (lighten < 1f) c = Blend(c, Color.Black, (1f - lighten) * 0.25f);
        return c;
    }

    void DrawBug()
    {
        float sc = ((age < 16) ? 0.3f + 0.7f * (age / 16f) : 1f) * BodyScale;
        Color body = BodyColor(Kind, 1f);
        Color stripe, dark;
        if (Kind == 1)      { stripe = Color.FromArgb(255, 206, 112, 22); dark = Color.FromArgb(255, 84, 46, 10); }
        else if (Kind == 2) { stripe = Color.FromArgb(255, 54, 58, 142);  dark = Color.FromArgb(255, 20, 22, 62); }
        else if (Kind == 3)
        {
            float pulse = 0.5f + 0.5f * (float)Math.Sin(age * 0.35);
            stripe = Blend(Color.FromArgb(255, 90, 12, 12), Color.FromArgb(255, 255, 210, 50), pulse);
            dark = Color.FromArgb(255, 52, 8, 8);
        }
        else if (Kind == 4) { stripe = Color.FromArgb(255, 90, 30, 140);  dark = Color.FromArgb(255, 40, 12, 66); }
        else                { stripe = Color.FromArgb(255, 54, 130, 40);  dark = Color.FromArgb(255, 24, 62, 22); }

        if (Poison > 0 && tint.A > 0)
        {
            float k = Math.Min(1f, Poison / (PoisonLimit * 0.75f)) * 0.7f;
            body = Blend(body, tint, k);
            stripe = Blend(stripe, tint, k * 0.6f);
        }
        if (Frozen) { body = Blend(body, Color.FromArgb(255, 150, 210, 255), 0.65f); stripe = Blend(stripe, Color.FromArgb(255, 200, 235, 255), 0.5f); }
        if (hurt > 0) { body = Blend(body, Color.White, 0.7f); stripe = Blend(stripe, Color.White, 0.5f); }

        g.TranslateTransform(S / 2f, S / 2f);
        g.RotateTransform(Ang * 57.29578f);
        g.ScaleTransform(sc, sc);

        using (SolidBrush sh = new SolidBrush(Color.FromArgb(48, 0, 0, 0)))
            g.FillEllipse(sh, -26f, -4f, 62f, 24f);

        // sprite personalizado (bug.png ao lado do exe)
        if (Sprite != null)
        {
            using (Pen rp = new Pen(Color.FromArgb(200, body), 3f)) g.DrawEllipse(rp, -26f, -26f, 52f, 52f);
            g.DrawImage(Sprite, -24f, -24f, 48f, 48f);
            g.ResetTransform();
            DrawHpBar();
            return;
        }

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

        // tanque: placas de armadura
        if (Kind == 2)
        {
            pen.Color = Color.FromArgb(200, 200, 205, 240); pen.Width = 2.2f;
            g.DrawArc(pen, -24f, -8f, 22f, 16f, 200f, 140f);
            g.DrawArc(pen, -2f, -6f, 14f, 12f, 200f, 140f);
        }
        // explosivo: pavio brilhante
        if (Kind == 3)
        {
            float pl = 0.5f + 0.5f * (float)Math.Sin(age * 0.35);
            using (SolidBrush gl = new SolidBrush(Color.FromArgb((int)(60 + 120 * pl), 255, 200, 40)))
                g.FillEllipse(gl, -18f, -14f, 30f, 28f);
        }

        // cabeça
        using (SolidBrush b3 = new SolidBrush(Blend(body, dark, 0.2f))) g.FillEllipse(b3, 11f, -6f, 12f, 12f);
        pen.Color = dark; pen.Width = 1.6f; g.DrawEllipse(pen, 11f, -6f, 12f, 12f);
        pen.Width = 1.4f;
        g.DrawLine(pen, 22f, -2f, 26f, -3.5f);
        g.DrawLine(pen, 22f, 2f, 26f, 3.5f);

        // chefe: coroa dourada
        if (Kind == 4)
        {
            PointF[] cr = new PointF[] { new PointF(10f, -7f), new PointF(10f, -15f), new PointF(14f, -10f), new PointF(18f, -17f), new PointF(22f, -10f), new PointF(24f, -15f), new PointF(24f, -7f) };
            using (SolidBrush gb = new SolidBrush(Color.FromArgb(255, 245, 200, 60))) g.FillPolygon(gb, cr);
            pen.Color = Color.FromArgb(255, 130, 90, 10); pen.Width = 1.2f; g.DrawPolygon(pen, cr);
        }

        // olhos
        for (int sd = -1; sd <= 1; sd += 2)
        {
            g.FillEllipse(Brushes.White, 16.5f, sd * 4.6f - 2.5f, 5f, 5f);
            g.FillEllipse((Kind == 3) ? Brushes.Red : Brushes.Black, 18.4f, sd * 4.6f - 1.2f, 2.4f, 2.4f);
        }

        g.ResetTransform();

        DrawHpBar();
    }

    void DrawHpBar()
    {
        if ((Kind == 2 || Kind == 4) && Hp < MaxHp)
        {
            float bw = (Kind == 4) ? 70f : 40f, bh = 5f, bx0 = S / 2f - bw / 2f, by0 = S / 2f - ((Kind == 4) ? 70f : 46f);
            using (SolidBrush bb = new SolidBrush(Color.FromArgb(170, 0, 0, 0))) g.FillRectangle(bb, bx0 - 1f, by0 - 1f, bw + 2f, bh + 2f);
            using (SolidBrush bf2 = new SolidBrush(Color.FromArgb(255, 220, 60, 60))) g.FillRectangle(bf2, bx0, by0, bw * Hp / MaxHp, bh);
        }
    }

    static Image sprite; static bool spriteTried;
    static Image Sprite
    {
        get
        {
            if (!spriteTried)
            {
                spriteTried = true;
                try
                {
                    string f = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bug.png");
                    if (System.IO.File.Exists(f)) sprite = new Bitmap(f);
                }
                catch { sprite = null; }
            }
            return sprite;
        }
    }

    void DrawDeath()
    {
        float u = dieF / 46f;
        float cx = S / 2f, cy = S / 2f;
        float grow = Math.Min(1f, dieF / 6f);

        // mancha
        int sa = (int)(215 * (1f - u * u));
        if (sa < 0) sa = 0;
        Color stain = (Poison > 0 && tint.A > 0) ? Blend(BodyColor(Kind, 0.7f), tint, 0.5f) : BodyColor(Kind, 0.7f);
        if (Kind == 3) stain = Color.FromArgb(255, 40, 20, 14);
        for (int i = 0; i < bx.Length; i++)
        {
            float r = br[i] * (0.6f + 0.4f * grow);
            using (SolidBrush sb = new SolidBrush(Color.FromArgb(sa, stain.R, stain.G, stain.B)))
                g.FillEllipse(sb, cx + bx[i] - r, cy + by[i] - r, r * 2f, r * 2f);
        }

        // clarão do impacto (o explosivo faz uma bola de fogo)
        if (Kind == 3 && dieF < 16)
        {
            float f = dieF / 16f;
            float rr = 8f + f * 62f;
            using (SolidBrush fb = new SolidBrush(Color.FromArgb((int)(190 * (1f - f)), 255, 150, 30)))
                g.FillEllipse(fb, cx - rr, cy - rr, rr * 2f, rr * 2f);
            using (SolidBrush fb2 = new SolidBrush(Color.FromArgb((int)(230 * (1f - f)), 255, 240, 160)))
                g.FillEllipse(fb2, cx - rr * 0.55f, cy - rr * 0.55f, rr * 1.1f, rr * 1.1f);
            pen.Color = Color.FromArgb((int)(220 * (1f - f)), 255, 220, 120);
            pen.Width = 3f;
            g.DrawEllipse(pen, cx - rr * 1.1f, cy - rr * 1.1f, rr * 2.2f, rr * 2.2f);
        }
        else if (dieF < 9)
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

        // pontos subindo
        if (Pop.Length > 0 && popFont != null)
        {
            float py = cy - 16f - dieF * 0.9f;
            int pa = (dieF < 28) ? 255 : (int)(255 * (1f - (dieF - 28) / 18f));
            if (pa < 0) pa = 0;
            SizeF ts = g.MeasureString(Pop, popFont);
            using (SolidBrush shb = new SolidBrush(Color.FromArgb(pa, 0, 0, 0)))
                g.DrawString(Pop, popFont, shb, cx - ts.Width / 2f + 1.2f, py + 1.2f);
            using (SolidBrush tb = new SolidBrush(Color.FromArgb(pa, 255, 232, 90)))
                g.DrawString(Pop, popFont, tb, cx - ts.Width / 2f, py);
        }
    }
}
