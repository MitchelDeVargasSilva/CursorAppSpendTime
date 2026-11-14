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
    //  AK-47  (pente de 30; recarrega sozinha ao acabar ou com a tecla R)
    // ===================================================================
    const int AK_MAG = 30, AK_RELOAD = 84;
    int   akCd = 0, akFlash = 0, akAmmo = AK_MAG, akReload = 0;
    bool  akShotReq = false, akRPrev = false;
    float akKick = 0f;

    void InitAk()
    {
        InitAim();
        akCd = 0; akFlash = 0; akShotReq = false; akKick = 0f; akAmmo = AK_MAG; akReload = 0;
        akRPrev = (GetAsyncKeyState(0x52) & 0x8000) != 0;
    }

    void StartAkReload()
    {
        akReload = AK_RELOAD; akShotReq = false;
        PlayReload();
    }

    void StepAk()
    {
        TrackAim();
        if (akCd > 0) akCd--;
        if (akFlash > 0) akFlash--;
        if (akKick > 0f) akKick = Math.Max(0f, akKick - 0.16f);

        bool held = TriggerHeld();
        bool rKey = (GetAsyncKeyState(0x52) & 0x8000) != 0;
        if (akReload > 0)
        {
            akReload--;
            akShotReq = false;
            if (akReload == 0) akAmmo = AK_MAG;
        }
        else
        {
            if (rKey && !akRPrev && akAmmo < AK_MAG) StartAkReload();
            else if ((held || akShotReq) && akCd <= 0)
            {
                akShotReq = false;
                if (akAmmo > 0)
                {
                    FireBullet();
                    akAmmo--;
                    akCd = 5;                      // ~12 tiros por segundo
                    if (akAmmo == 0) StartAkReload();
                }
                else StartAkReload();
            }
        }
        akRPrev = rKey;

        StepBullets();
        StepParticles();
    }

    void FireBullet()
    {
        SetGunBasis(wx, wy, bowAim);
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
            pen.Color = Tc(Color.FromArgb(70, 255, 170, 60));  pen.Width = 7f;
            gfx.DrawLine(pen, tx, ty, x, y);
            pen.Color = Tc(Color.FromArgb(230, 255, 210, 90)); pen.Width = 3f;
            gfx.DrawLine(pen, tx, ty, x, y);
            pen.Color = Color.FromArgb(255, 255, 255, 235); pen.Width = 1.4f;
            gfx.DrawLine(pen, x - b.vx * 0.4f, y - b.vy * 0.4f, x, y);
        }
        DrawAkBody(ox, oy);
        DrawEffects(ox, oy);
    }

    void GPolyOff(Color fill, Color edge, float dx, float dy, params float[] fv)
    {
        PointF[] pp = new PointF[fv.Length / 2];
        for (int i = 0; i < pp.Length; i++)
        {
            PointF q = GP(fv[i * 2], fv[i * 2 + 1]);
            pp[i] = new PointF(q.X + dx, q.Y + dy);
        }
        using (SolidBrush br = new SolidBrush(fill)) gfx.FillPolygon(br, pp);
        pen.Color = edge; pen.Width = 1.2f;
        gfx.DrawPolygon(pen, pp);
    }

    Font hudFont;
    void DrawSmallText(string s, float cx, float cy, Color c)
    {
        if (hudFont == null)
        {
            try { hudFont = new Font("Segoe UI", 9f, FontStyle.Bold); }
            catch { hudFont = new Font(FontFamily.GenericSansSerif, 9f, FontStyle.Bold); }
        }
        gfx.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        SizeF ts = gfx.MeasureString(s, hudFont);
        using (SolidBrush sh = new SolidBrush(Color.FromArgb(200, 0, 0, 0)))
            gfx.DrawString(s, hudFont, sh, cx - ts.Width / 2f + 1f, cy + 1f);
        using (SolidBrush br = new SolidBrush(c))
            gfx.DrawString(s, hudFont, br, cx - ts.Width / 2f, cy);
    }

    void DrawBar(float x, float y, float w, float h, float frac, Color fill)
    {
        using (SolidBrush bg = new SolidBrush(Color.FromArgb(170, 0, 0, 0)))
            gfx.FillRectangle(bg, x - 1f, y - 1f, w + 2f, h + 2f);
        using (SolidBrush fg = new SolidBrush(fill))
            gfx.FillRectangle(fg, x, y, w * Clamp01(frac), h);
    }

    void DrawAkBody(float ox, float oy)
    {
        float kick = akKick * 7f;
        float ca = (float)Math.Cos(bowAim), sa = (float)Math.Sin(bowAim);
        float sgn = (ca >= 0f) ? 1f : -1f;

        // recarregando: o cano abaixa e o pente sai e entra
        float p = (akReload > 0) ? (AK_RELOAD - akReload) / (float)AK_RELOAD : -1f;
        float tilt = (p >= 0f) ? 0.55f * (float)Math.Sin(Math.Min(1f, p / 0.95f) * Math.PI) : 0f;
        SetGunBasis(wx - ox - ca * kick, wy - oy - sa * kick, bowAim + sgn * tilt);

        float mdx = 0f, mdy = 0f; bool drawMag = true;
        if (p >= 0f)
        {
            if (p < 0.30f) { float q = p / 0.30f; mdy = q * q * 80f; mdx = sgn * q * 10f; }
            else if (p < 0.50f) drawMag = false;
            else if (p < 0.80f) { float q = (p - 0.5f) / 0.3f; mdy = (1f - q) * (1f - q) * 60f; }
        }

        Color wood = Color.FromArgb(255, 142, 88, 44), woodD = Color.FromArgb(255, 62, 36, 16);
        Color steel = Color.FromArgb(255, 66, 70, 78), steelD = Color.FromArgb(255, 18, 18, 22);
        Color poly = Color.FromArgb(255, 36, 36, 40);

        // coronha
        GPoly(wood, woodD, -14f, 16f, -52f, 13f, -52f, 0f, -32f, 3f, -14f, 6f);
        GPoly(steel, steelD, -52f, 13f, -55f, 13f, -55f, 0f, -52f, 0f);
        GLine(Color.FromArgb(90, 255, 220, 160), 1.2f, -48f, 11f, -20f, 14f);
        // carregador curvo
        if (drawMag)
        {
            GPolyOff(steel, steelD, mdx, mdy, 10f, 5f, 22f, 5f, 30f, -12f, 35f, -22f, 25f, -25f, 17f, -12f);
            pen.Color = Color.FromArgb(120, 20, 20, 24); pen.Width = 1f;
            PointF r1 = GP(14f, 0f), r2 = GP(24f, -3f), r3 = GP(19f, -8f), r4 = GP(28f, -11f);
            gfx.DrawLine(pen, r1.X + mdx, r1.Y + mdy, r2.X + mdx, r2.Y + mdy);
            gfx.DrawLine(pen, r3.X + mdx, r3.Y + mdy, r4.X + mdx, r4.Y + mdy);
        }
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
            pen.Color = Color.FromArgb(220, 255, 220, 100); pen.Width = 4f;
            gfx.DrawLine(pen, m.X, m.Y, m.X + gca * 30f, m.Y + gsa * 30f);
        }

        // munição
        float cx = wx - ox, cy = wy - oy;
        if (akReload > 0)
        {
            DrawSmallText("RECARREGANDO", cx, cy + 36f, Color.FromArgb(255, 255, 214, 80));
            DrawBar(cx - 32f, cy + 54f, 64f, 5f, p, Color.FromArgb(255, 255, 190, 60));
        }
        else
        {
            Color ac = (akAmmo <= 5) ? Color.FromArgb(255, 255, 90, 80) : Color.FromArgb(255, 255, 255, 255);
            DrawSmallText(string.Format("{0}/{1}", akAmmo, AK_MAG), cx, cy + 36f, ac);
            DrawBar(cx - 32f, cy + 54f, 64f, 4f, akAmmo / (float)AK_MAG, Color.FromArgb(255, 240, 200, 80));
        }

        DrawHotspot(ox, oy);
    }

}
