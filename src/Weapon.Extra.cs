using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

public class Flm { public float x, y, vx, vy; public int life, max, age; }
public class Gren { public float sx, sy, tx, ty, x, y, rot; public int age, dur, fuse; public bool landed; }
public class Blast { public float x, y; public int age; }

// Lança-chamas e granada
public partial class Whip
{
    // ===================================================================
    //  LANÇA-CHAMAS: segure o botão. O combustível acaba em ~4 s e
    //  recarrega sozinho quando solta. Queimar também machuca bugs.
    // ===================================================================
    List<Flm> flames = new List<Flm>();
    const float FL_MAX = 240f;
    float flFuel = FL_MAX;
    bool flLock = false, flameOn = false;
    Snd flamePlayer;

    void InitFlame()
    {
        InitAim();
        flFuel = FL_MAX; flLock = false;
    }

    void StopFlameSound()
    {
        if (!flameOn) return;
        try { if (flamePlayer != null) flamePlayer.Stop(); } catch { }
        flameOn = false;
    }

    void StepFlame()
    {
        TrackAim();
        bool held = TriggerHeld();
        bool fire = held && !flLock && flFuel > 0f;
        if (fire)
        {
            flFuel -= 1f;
            if (flFuel <= 0f) { flFuel = 0f; flLock = true; }
            SetGunBasis(wx, wy, bowAim);
            PointF m = GP(88f, 9f);
            for (int i = 0; i < 3; i++)
            {
                float ang = bowAim + Rf(-0.13f, 0.13f);
                float spd = Rf(9f, 14f);
                Flm f = new Flm();
                f.x = m.X; f.y = m.Y;
                f.vx = (float)Math.Cos(ang) * spd; f.vy = (float)Math.Sin(ang) * spd;
                f.max = f.life = 24 + rnd.Next(14);
                flames.Add(f);
            }
            while (flames.Count > 260) flames.RemoveAt(0);
            if (!flameOn && soundOn && flamePlayer != null) { try { flamePlayer.PlayLooping(); flameOn = true; } catch { } }
        }
        else
        {
            flFuel = Math.Min(FL_MAX, flFuel + 1.4f);
            if (flLock && flFuel >= 90f) flLock = false;
            StopFlameSound();
        }

        for (int i = flames.Count - 1; i >= 0; i--)
        {
            Flm f = flames[i];
            f.x += f.vx; f.y += f.vy;
            f.vx *= 0.955f; f.vy = f.vy * 0.955f - 0.08f;
            f.age++; f.life--;
            if (f.age % 3 == 0) HitBugsCircle(f.x, f.y, 9f + f.age * 0.9f, 1);
            if (f.life <= 0) flames.RemoveAt(i);
        }
        StepParticles();
    }

    void DrawFlameScene(float ox, float oy)
    {
        foreach (Flm f in flames)
        {
            float t = f.age / (float)(f.age + f.life);
            float size = 8f + f.age * 1.15f;
            float x = f.x - ox, y = f.y - oy;
            int a = Cl(190 * (1f - t));
            Color outer = Tc((t < 0.55f) ? Color.FromArgb(a / 2, 255, 120, 20) : Color.FromArgb(a / 2, 120, 40, 20));
            using (SolidBrush b0 = new SolidBrush(outer)) gfx.FillEllipse(b0, x - size, y - size, size * 2f, size * 2f);
            Color mid = Tc((t < 0.25f) ? Color.FromArgb(a, 255, 235, 150) : (t < 0.6f ? Color.FromArgb(a, 255, 160, 40) : Color.FromArgb(a, 210, 70, 30)));
            float s2 = size * 0.62f;
            using (SolidBrush b1 = new SolidBrush(mid)) gfx.FillEllipse(b1, x - s2, y - s2, s2 * 2f, s2 * 2f);
            if (t < 0.3f)
            {
                float s3 = size * 0.3f;
                using (SolidBrush b2 = new SolidBrush(Color.FromArgb(a, 255, 255, 240))) gfx.FillEllipse(b2, x - s3, y - s3, s3 * 2f, s3 * 2f);
            }
        }
        DrawFlameBody(ox, oy);
        DrawEffects(ox, oy);
    }

    void DrawFlameBody(float ox, float oy)
    {
        SetGunBasis(wx - ox, wy - oy, bowAim);
        Color steel = Color.FromArgb(255, 70, 74, 82), steelD = Color.FromArgb(255, 18, 18, 22);
        Color red = Color.FromArgb(255, 190, 40, 36), redD = Color.FromArgb(255, 80, 14, 14);
        // tanques de combustível
        GPoly(red, redD, -46f, 18f, -8f, 18f, -8f, 2f, -46f, 2f);
        GPoly(Color.FromArgb(255, 220, 190, 60), redD, -34f, 18f, -30f, 18f, -30f, 2f, -34f, 2f);
        GPoly(steel, steelD, -8f, 12f, 0f, 12f, 0f, 8f, -8f, 8f);
        // punho + cano + bico
        GPoly(Color.FromArgb(255, 40, 40, 44), steelD, 6f, 6f, 16f, 6f, 12f, -12f, 3f, -10f);
        GPoly(steel, steelD, 0f, 14f, 24f, 14f, 24f, 4f, 0f, 4f);
        GPoly(steel, steelD, 24f, 11f, 76f, 11f, 76f, 7f, 24f, 7f);
        GPoly(Color.FromArgb(255, 110, 114, 122), steelD, 40f, 14f, 60f, 14f, 60f, 4f, 40f, 4f);
        GPoly(steel, steelD, 76f, 13f, 86f, 15f, 86f, 3f, 76f, 5f);
        // chama-piloto
        PointF pl = GP(88f, 9f);
        using (SolidBrush pb = new SolidBrush(Color.FromArgb(200, 255, 190, 60))) gfx.FillEllipse(pb, pl.X - 3f, pl.Y - 4f, 6f, 8f);
        // combustível
        Color fc = flLock ? Color.FromArgb(255, 220, 60, 50) : Color.FromArgb(255, 255, 170, 40);
        DrawBar(wx - ox - 30f, wy - oy - 66f, 60f, 6f, flFuel / FL_MAX, fc);
        if (flLock) DrawSmallText("superaqueceu", wx - ox, wy - oy - 84f, Color.FromArgb(255, 255, 120, 90));
    }

    // ===================================================================
    //  GRANADA: o clique lança, ela cai, pisca e explode (dano 3 em área)
    // ===================================================================
    List<Gren> grens = new List<Gren>();
    List<Blast> blasts = new List<Blast>();
    int grCd = 0;
    bool grReq = false;
    public int slowF = 0;         // quadros restantes de câmera lenta dos bugs

    void InitGren()
    {
        InitAim();
        grCd = 0; grReq = false;
    }

    void ThrowGren()
    {
        SetGunBasis(wx, wy, bowAim);
        PointF m = GP(40f, 6f);
        Rectangle vs = SystemInformation.VirtualScreen;
        float tx, ty;
        if (aimCursor) { tx = mx; ty = my; }
        else { tx = m.X + (float)Math.Cos(bowAim) * 340f; ty = m.Y + (float)Math.Sin(bowAim) * 340f; }
        tx = Math.Max(vs.Left + 20, Math.Min(vs.Right - 20, tx));
        ty = Math.Max(vs.Top + 20, Math.Min(vs.Bottom - 20, ty));
        Gren g = new Gren();
        g.sx = g.x = m.X; g.sy = g.y = m.Y; g.tx = tx; g.ty = ty;
        g.dur = 34; g.fuse = 42;
        grens.Add(g);
        grCd = 38;
        PlayWhoosh();
    }

    void StepGren()
    {
        TrackAim();
        if (grCd > 0) grCd--;
        if (grReq && grCd <= 0) ThrowGren();
        grReq = false;

        for (int i = grens.Count - 1; i >= 0; i--)
        {
            Gren g = grens[i];
            g.rot += 0.45f;
            if (!g.landed)
            {
                g.age++;
                float t = Math.Min(1f, g.age / (float)g.dur);
                g.x = g.sx + (g.tx - g.sx) * t;
                g.y = g.sy + (g.ty - g.sy) * t - (float)Math.Sin(Math.PI * t) * 150f;
                if (g.age >= g.dur) { g.landed = true; g.x = g.tx; g.y = g.ty; PlayClank(); }
            }
            else
            {
                g.fuse--;
                if (g.fuse <= 0)
                {
                    ExplodeGren(g.x, g.y);
                    grens.RemoveAt(i);
                }
            }
        }
        for (int i = blasts.Count - 1; i >= 0; i--)
        {
            blasts[i].age++;
            if (blasts[i].age > 18) blasts.RemoveAt(i);
        }
        StepParticles();
    }

    void ExplodeGren(float x, float y)
    {
        blasts.Add(new Blast { x = x, y = y, age = 0 });
        HitBugsCircle(x, y, 115f, 3);
        AddScorchDecal(x, y, false);
        for (int i = 0; i < 36; i++)
        {
            double ang = rnd.NextDouble() * Math.PI * 2;
            double spd = 2 + rnd.NextDouble() * 9;
            Spark s = new Spark();
            s.x = x; s.y = y;
            s.vx = (float)(Math.Cos(ang) * spd); s.vy = (float)(Math.Sin(ang) * spd) - 1f;
            s.max = s.life = 16 + rnd.Next(22);
            s.hot = true; s.custom = true; s.drag = 0.93f; s.g = 0.22f;
            sparks.Add(s);
        }
        OnBigExplosion();
        PlayBoom();
    }

    // explosão grande: câmera lenta curta nos bugs
    void OnBigExplosion() { slowF = 22; }

    void DrawGrenScene(float ox, float oy)
    {
        foreach (Blast b in blasts)
        {
            float x = b.x - ox, y = b.y - oy;
            float f = b.age / 18f;
            float rad = 20f + b.age * 6.5f;
            if (b.age < 5)
                using (SolidBrush fl = new SolidBrush(Color.FromArgb(Cl(210 * (1f - b.age / 5f)), 255, 230, 150)))
                    gfx.FillEllipse(fl, x - 60f, y - 60f, 120f, 120f);
            pen.Color = Tc(Color.FromArgb(Cl(230 * (1f - f)), 255, 150, 40)); pen.Width = 5f * (1f - f) + 0.8f;
            gfx.DrawEllipse(pen, x - rad, y - rad, rad * 2f, rad * 2f);
            pen.Color = Color.FromArgb(Cl(120 * (1f - f)), 255, 220, 120); pen.Width = 2f;
            gfx.DrawEllipse(pen, x - rad * 0.7f, y - rad * 0.7f, rad * 1.4f, rad * 1.4f);
        }
        foreach (Gren g in grens)
        {
            float x = g.x - ox, y = g.y - oy;
            if (!g.landed)
            {
                float t = Math.Min(1f, g.age / (float)g.dur);
                float gx = g.sx + (g.tx - g.sx) * t - ox, gy = g.sy + (g.ty - g.sy) * t - oy;
                using (SolidBrush sh = new SolidBrush(Color.FromArgb(60, 0, 0, 0))) gfx.FillEllipse(sh, gx - 8f, gy - 3f, 16f, 6f);
            }
            bool blink = g.landed && g.fuse < 24 && (g.fuse / 3) % 2 == 0;
            float c = (float)Math.Cos(g.rot), s = (float)Math.Sin(g.rot);
            using (SolidBrush body = new SolidBrush(blink ? Color.FromArgb(255, 220, 60, 50) : Color.FromArgb(255, 70, 96, 52)))
                gfx.FillEllipse(body, x - 8f, y - 10f, 16f, 20f);
            pen.Color = Color.FromArgb(255, 24, 36, 18); pen.Width = 1.4f;
            gfx.DrawEllipse(pen, x - 8f, y - 10f, 16f, 20f);
            gfx.DrawLine(pen, x - 7f, y - 3f, x + 7f, y - 3f);
            gfx.DrawLine(pen, x - 7f, y + 3f, x + 7f, y + 3f);
            // tampa e alavanca girando
            pen.Color = Color.FromArgb(255, 150, 150, 156); pen.Width = 2.4f;
            gfx.DrawLine(pen, x, y - 10f, x + c * 9f, y - 10f + s * 4f);
            pen.Width = 1.6f;
            gfx.DrawEllipse(pen, x + c * 9f - 3f, y - 10f + s * 4f - 3f, 6f, 6f);
        }
        DrawGrenBody(ox, oy);
        DrawEffects(ox, oy);
    }

    void DrawGrenBody(float ox, float oy)
    {
        SetGunBasis(wx - ox, wy - oy, bowAim);
        Color g1 = Color.FromArgb(255, 78, 104, 60), g2 = Color.FromArgb(255, 26, 38, 20);
        GPoly(Color.FromArgb(255, 40, 40, 44), g2, 4f, 6f, 14f, 6f, 10f, -12f, 1f, -10f);
        GPoly(g1, g2, 0f, 14f, 50f, 14f, 50f, 5f, 0f, 5f);
        GPoly(Color.FromArgb(255, 56, 76, 44), g2, 50f, 17f, 62f, 17f, 62f, 2f, 50f, 2f);
        GLine(Color.FromArgb(255, 200, 200, 120), 2f, 18f, 14f, 18f, 5f);
        // munição: barra de recarga
        if (grCd > 0) DrawBar(wx - ox - 24f, wy - oy - 50f, 48f, 5f, 1f - grCd / 38f, Color.FromArgb(255, 150, 200, 90));
    }

    byte[] MakeFlameWav()
    {
        int sr = 22050, n = sr;      // 1 s para repetir
        double[] o = new double[n];
        Random r = new Random(7);
        double lp = 0, lp2 = 0;
        for (int i = 0; i < n; i++)
        {
            double w = r.NextDouble() * 2 - 1;
            lp += (w - lp) * 0.22; lp2 += (lp - lp2) * 0.3;
            double flick = 0.75 + 0.25 * Math.Sin(i * 2 * Math.PI * 9 / sr);
            o[i] = lp2 * 1.6 * flick * 0.55;
        }
        // emenda suave para o loop
        int fade = 800;
        for (int i = 0; i < fade; i++)
        {
            double k = i / (double)fade;
            o[i] = o[i] * k + o[n - fade + i] * (1 - k) * 0;
            o[n - 1 - i] *= k;
        }
        return ToWav(o, sr);
    }
}
