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
        if (tipSpeed > 9f) HitBugsSeg(tip.px, tip.py, tip.x, tip.y, 9f, 2);

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
        HitBugsSeg(x - 18f, y, x + 18f, y, 26f, 2);
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

}
