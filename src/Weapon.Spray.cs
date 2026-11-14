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
    //  SPRAY (a tinta fica um tempo na tela e depois some)
    // ===================================================================
    static readonly int[] SpPal = new int[] {
        0xE8352B, 0x2E9BFF, 0x35D06A, 0xFFD23A, 0xF04FD0, 0xFFFFFF, 0xFF8A1F, 0x25E0D0
    };
    int   spCol = 0, spStill = 0;
    bool  spWasHeld = false, hissOn = false;
    float spPrevX, spPrevY;
    float spShake = 0f, spShakePh = 0f, spLastDx = 0f;
    int   spPrevMx = 0, spRattleCd = 0;

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
        spShake = 0f; spShakePh = 0f; spLastDx = 0f; spPrevMx = mx; spRattleCd = 0;
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

        // chacoalhar a lata: mexer o mouse rápido de um lado para o outro
        int mdx = mx - spPrevMx; spPrevMx = mx;
        if (spRattleCd > 0) spRattleCd--;
        if (!held && Math.Abs(mdx) > 7)
        {
            float sg = (mdx > 0) ? 1f : -1f;
            if (spLastDx != 0f && sg != spLastDx) spShake = Math.Min(6f, spShake + 1f);
            spLastDx = sg;
        }
        else if (Math.Abs(mdx) < 2) spLastDx = 0f;
        if (spShake > 0f)
        {
            spShake = Math.Max(0f, spShake - 0.05f);
            spShakePh += 1.1f;
            if (!held && spShake >= 2f && spRattleCd <= 0) { PlayRattle(); spRattleCd = 16; }
        }
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
                d.vx = -Rf(1f, 4f); d.vy = Rf(-1.2f, 1.2f);
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
        float tiltDeg = spWasHeld ? 20f : 12f;
        // chacoalhando: a lata balança de verdade
        float sh = Clamp01(spShake / 3f);
        if (sh > 0f && !spWasHeld)
        {
            tiltDeg += 14f * sh * (float)Math.Sin(spShakePh);
            gx += 4f * sh * (float)Math.Sin(spShakePh * 1.7f);
            gy += 2f * sh * (float)Math.Cos(spShakePh * 2.3f);
        }
        if (spWasHeld) { gx += Rf(-0.8f, 0.8f); gy += Rf(-0.8f, 0.8f); }

        float th = Deg(tiltDeg);
        float ax = (float)Math.Sin(th), ay = (float)Math.Cos(th);      // eixo: do bico para o fundo (para baixo)
        float qx = ay, qy = -ax;                                       // para a direita da lata
        float nx = gx + 10f, ny = gy + 1f;                             // centro do bico
        Color paint = PalColor(SpPal[spCol < 0 ? 0 : spCol], 255);
        Color edge = Color.FromArgb(255, 24, 26, 32);

        // corpo de alumínio
        FillPts(Color.FromArgb(255, 200, 206, 216), edge,
            SP(nx, ny, ax, ay, qx, qy, 22f, -11f), SP(nx, ny, ax, ay, qx, qy, 72f, -11f),
            SP(nx, ny, ax, ay, qx, qy, 72f, 11f),  SP(nx, ny, ax, ay, qx, qy, 22f, 11f));
        // rótulo colorido
        FillPts(paint, edge,
            SP(nx, ny, ax, ay, qx, qy, 32f, -11f), SP(nx, ny, ax, ay, qx, qy, 62f, -11f),
            SP(nx, ny, ax, ay, qx, qy, 62f, 11f),  SP(nx, ny, ax, ay, qx, qy, 32f, 11f));
        pen.Color = Color.FromArgb(150, 255, 255, 255); pen.Width = 2f;
        gfx.DrawLine(pen, SP(nx, ny, ax, ay, qx, qy, 24f, -6.5f), SP(nx, ny, ax, ay, qx, qy, 70f, -6.5f));
        // ombro (afina para cima), pescoço e tampa do bico
        FillPts(Color.FromArgb(255, 170, 176, 188), edge,
            SP(nx, ny, ax, ay, qx, qy, 12f, -5f), SP(nx, ny, ax, ay, qx, qy, 22f, -11f),
            SP(nx, ny, ax, ay, qx, qy, 22f, 11f), SP(nx, ny, ax, ay, qx, qy, 12f, 5f));
        FillPts(Color.FromArgb(255, 44, 46, 52), edge,
            SP(nx, ny, ax, ay, qx, qy, 5f, -5f), SP(nx, ny, ax, ay, qx, qy, 12f, -5f),
            SP(nx, ny, ax, ay, qx, qy, 12f, 5f),  SP(nx, ny, ax, ay, qx, qy, 5f, 5f));
        // botão/bico, com a saída apontando para o cursor (a tinta sai dali)
        FillPts(paint, edge,
            SP(nx, ny, ax, ay, qx, qy, -1f, -4f), SP(nx, ny, ax, ay, qx, qy, 5f, -5f),
            SP(nx, ny, ax, ay, qx, qy, 5f, 5f),   SP(nx, ny, ax, ay, qx, qy, -1f, 4f));
        pen.Color = edge; pen.Width = 3f;
        gfx.DrawLine(pen, nx - 2f, ny - 1f, gx + 1f, gy);
        pen.Color = Color.FromArgb(255, 70, 72, 80); pen.Width = 1.4f;
        gfx.DrawLine(pen, nx - 2f, ny - 1f, gx + 1f, gy);
        // aro do fundo
        pen.Color = edge; pen.Width = 2f;
        gfx.DrawLine(pen, SP(nx, ny, ax, ay, qx, qy, 72f, -11f), SP(nx, ny, ax, ay, qx, qy, 72f, 11f));

        DrawHotspot(ox, oy);
    }

    static PointF SP(float gx, float gy, float ax, float ay, float qx, float qy, float t, float w)
    {
        return new PointF(gx + ax * t + qx * w, gy + ay * t + qy * w);
    }

}
