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
    //  MARCAS DE DANO NA TELA (rachaduras e queimados que somem aos poucos)
    // ===================================================================
    List<Decal> decals = new List<Decal>();

    void AddDecal(Decal d)
    {
        EnsureBowDib();
        if (gfxBow == null) return;
        decals.Add(d);
        while (decals.Count > 40) decals.RemoveAt(0);
    }

    void AddCrackDecal(float x, float y, float sc)
    {
        Decal d = new Decal();
        d.x = x; d.y = y; d.kind = 0; d.scale = sc; d.life = d.max = 420;
        List<float> seg = new List<float>();
        int n = 7 + rnd.Next(4);
        for (int i = 0; i < n; i++)
        {
            float ang = i * (float)Math.PI * 2f / n + Rf(-0.3f, 0.3f);
            float len = Rf(22f, 60f) * sc;
            int steps = 3 + rnd.Next(2);
            float px = (float)Math.Cos(ang) * 5f * sc, py = (float)Math.Sin(ang) * 5f * sc;
            for (int s = 0; s < steps; s++)
            {
                ang += Rf(-0.45f, 0.45f);
                float sl = len / steps;
                float nx = px + (float)Math.Cos(ang) * sl, ny = py + (float)Math.Sin(ang) * sl;
                seg.Add(px); seg.Add(py); seg.Add(nx); seg.Add(ny);
                if (rnd.NextDouble() < 0.35)
                {
                    float a2 = ang + Rf(-1.1f, 1.1f), l2 = sl * Rf(0.5f, 1.1f);
                    seg.Add(nx); seg.Add(ny);
                    seg.Add(nx + (float)Math.Cos(a2) * l2); seg.Add(ny + (float)Math.Sin(a2) * l2);
                }
                px = nx; py = ny;
            }
        }
        d.pts = seg.ToArray();
        AddDecal(d);
    }

    // talho da espada: dois riscos paralelos na direção do golpe
    void AddSlashDecal(float x, float y, float ux, float uy)
    {
        Decal d = new Decal();
        d.x = x; d.y = y; d.kind = 3; d.scale = 1f; d.life = d.max = 360;
        float len = Rf(34f, 52f), nx = -uy * 5f, ny = ux * 5f;
        d.pts = new float[] { -ux * len + nx, -uy * len + ny, ux * len * 0.4f + nx, uy * len * 0.4f + ny,
                              -ux * len * 0.7f - nx, -uy * len * 0.7f - ny, ux * len * 0.2f - nx, uy * len * 0.2f - ny };
        AddDecal(d);
    }

    void AddScorchDecal(float x, float y, bool blue)
    {
        Decal d = new Decal();
        d.x = x; d.y = y; d.kind = blue ? 1 : 2; d.scale = 1f;
        d.life = d.max = blue ? 480 : 300;
        d.pts = new float[0];
        AddDecal(d);
    }

    void DrawDecals(float ox, float oy)
    {
        foreach (Decal d in decals)
        {
            float fade = (d.life < 120) ? d.life / 120f : 1f;
            float x = d.x - ox, y = d.y - oy;
            if (d.kind == 0)
            {
                float sc = d.scale;
                using (SolidBrush dent = new SolidBrush(Color.FromArgb(Cl(150 * fade), 24, 20, 16)))
                    gfx.FillEllipse(dent, x - 6f * sc, y - 5f * sc, 12f * sc, 10f * sc);
                for (int i = 0; i + 3 < d.pts.Length; i += 4)
                {
                    pen.Color = Color.FromArgb(Cl(215 * fade), 30, 24, 18); pen.Width = 2.2f * Math.Max(0.6f, sc);
                    gfx.DrawLine(pen, x + d.pts[i], y + d.pts[i + 1], x + d.pts[i + 2], y + d.pts[i + 3]);
                    pen.Color = Color.FromArgb(Cl(90 * fade), 240, 236, 228); pen.Width = 1f;
                    gfx.DrawLine(pen, x + d.pts[i] + 1f, y + d.pts[i + 1] + 1f, x + d.pts[i + 2] + 1f, y + d.pts[i + 3] + 1f);
                }
            }
            else if (d.kind == 3)
            {
                for (int i = 0; i + 3 < d.pts.Length; i += 4)
                {
                    pen.Color = Color.FromArgb(Cl(230 * fade), 20, 16, 14); pen.Width = 2.6f;
                    gfx.DrawLine(pen, x + d.pts[i], y + d.pts[i + 1], x + d.pts[i + 2], y + d.pts[i + 3]);
                    pen.Color = Color.FromArgb(Cl(140 * fade), 235, 240, 250); pen.Width = 1f;
                    gfx.DrawLine(pen, x + d.pts[i] + 1.5f, y + d.pts[i + 1] + 1.5f, x + d.pts[i + 2] + 1.5f, y + d.pts[i + 3] + 1.5f);
                }
            }
            else
            {
                bool blue = (d.kind == 1);
                float R = blue ? 44f : 24f;
                int age = d.max - d.life;
                for (int k = 0; k < 4; k++)
                {
                    float rr = R * (1f - k * 0.2f);
                    Color c = blue ? Color.FromArgb(Cl((40 + k * 22) * fade), 8, 12, 30)
                                   : Color.FromArgb(Cl((40 + k * 22) * fade), 30, 14, 6);
                    using (SolidBrush sb = new SolidBrush(c))
                        gfx.FillEllipse(sb, x - rr, y - rr * 0.55f, rr * 2f, rr * 1.1f);
                }
                if (age < 160)
                {
                    float gl = 1f - age / 160f;
                    float fl = 0.7f + 0.3f * (float)Math.Sin(age * 0.4);
                    pen.Color = blue ? Color.FromArgb(Cl(150 * gl * fl * fade), 90, 180, 255)
                                     : Color.FromArgb(Cl(150 * gl * fl * fade), 255, 140, 40);
                    pen.Width = 2.5f;
                    gfx.DrawEllipse(pen, x - R * 0.8f, y - R * 0.45f, R * 1.6f, R * 0.9f);
                }
            }
        }
    }

}
