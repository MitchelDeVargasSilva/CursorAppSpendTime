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
    //  RENDERIZAÇÃO
    // ===================================================================
    void Render()
    {
        if (UsesBigLayer() || (decals.Count > 0 && gfxBow != null)) { RenderBow(); return; }
        gfx.Clear(Color.Transparent);
        float ox = mx - W / 2f, oy = my - H / 2f;
        DrawLegacyScene(ox, oy);
        Present();
    }

    void DrawLegacyScene(float ox, float oy)
    {
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
    }

    // ponto de clique real (usado quando a "mão" da arma se desloca do cursor)
    void DrawHotspot(float ox, float oy)
    {
        float x = mx - ox, y = my - oy;
        if (aimCursor && (mode == WeaponMode.Bow || mode == WeaponMode.Ak || mode == WeaponMode.Catapult))
        {
            pen.Color = Color.FromArgb(235, 255, 70, 60); pen.Width = 2f;
            gfx.DrawEllipse(pen, x - 11f, y - 11f, 22f, 22f);
            gfx.DrawLine(pen, x - 18f, y, x - 6f, y); gfx.DrawLine(pen, x + 6f, y, x + 18f, y);
            gfx.DrawLine(pen, x, y - 18f, x, y - 6f); gfx.DrawLine(pen, x, y + 6f, x, y + 18f);
            gfx.FillEllipse(Brushes.White, x - 1.5f, y - 1.5f, 3f, 3f);
            return;
        }
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

}
