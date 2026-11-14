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
        wxInit = false; UpdateWeaponPos();
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
        // a mira segue a direção do movimento do mouse (ou o cursor, no modo mira no cursor)
        UpdateWeaponPos();
        float mvx = mx - bowPrevMx, mvy = my - bowPrevMy;
        bowPrevMx = mx; bowPrevMy = my;
        if (aimCursor) bowAim = (float)Math.Atan2(my - wy, mx - wx);
        else if (mvx * mvx + mvy * mvy > 9f)
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
                EmitArrowFire(wx + ca * hn, wy + sa * hn, 0f, 0f, 1);
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
        ar.x = wx + (float)Math.Cos(bowAim) * cf;
        ar.y = wy + (float)Math.Sin(bowAim) * cf;
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
                HitBugsSeg(ohx, ohy, hx, hy, 11f, 2);
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
        AddScorchDecal(x, y, false);
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
        if (!UsesBigLayer()) Grow(ref minX, ref maxX, ref minY, ref maxY, mx, my, 280f);
        else if (mode != WeaponMode.Bow) Grow(ref minX, ref maxX, ref minY, ref maxY, mx, my, 150f);
        if (mode == WeaponMode.Bow || mode == WeaponMode.Ak || mode == WeaponMode.Catapult || mode == WeaponMode.Flame || mode == WeaponMode.Grenade) Grow(ref minX, ref maxX, ref minY, ref maxY, wx, wy, 170f);
        foreach (Flm fm in flames) Grow(ref minX, ref maxX, ref minY, ref maxY, fm.x, fm.y, 8f + fm.age * 1.2f + 6f);
        foreach (Gren gr in grens) Grow(ref minX, ref maxX, ref minY, ref maxY, gr.x, gr.y, 30f);
        foreach (Blast bl in blasts) Grow(ref minX, ref maxX, ref minY, ref maxY, bl.x, bl.y, 160f);
        foreach (Shard shd in shards) Grow(ref minX, ref maxX, ref minY, ref maxY, shd.x, shd.y, 20f);
        foreach (Decal dcl in decals) Grow(ref minX, ref maxX, ref minY, ref maxY, dcl.x, dcl.y, 75f);

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
        DrawDecals(ox, oy);
        if (!UsesBigLayer()) { DrawLegacyScene(ox, oy); return; }
        if (mode == WeaponMode.Ak)       { DrawAkScene(ox, oy); return; }
        if (mode == WeaponMode.Catapult) { DrawCatScene(ox, oy); return; }
        if (mode == WeaponMode.Spray)    { DrawSprayScene(ox, oy); return; }
        if (mode == WeaponMode.Flame)    { DrawFlameScene(ox, oy); return; }
        if (mode == WeaponMode.Grenade)  { DrawGrenScene(ox, oy); return; }
        DrawFireParticles(ox, oy);
        foreach (Arrow ar in arrows)
        {
            float tx = ar.x - ar.ux * 26f - ox, ty = ar.y - ar.uy * 26f - oy;
            float spd = ar.isStuck ? 0f : (float)Math.Sqrt(ar.vx * ar.vx + ar.vy * ar.vy);
            DrawArrow(tx, ty, ar.ux, ar.uy, 52f, spd);
        }
        DrawBowBody(ox, oy);
        if (aimCursor) DrawHotspot(ox, oy);
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
        float gx = wx - ox, gy = wy - oy;            // empunhadura = cursor (ou arma fixa)
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

}
