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
    //  CATAPULTA DE FOGO AZUL  (recarga lenta, com barra embaixo)
    // ===================================================================
    int   catF = -1, catFacing = 1;
    float catArm = 138f, catRoll = 0f, catPulse = 0f, catPrevWx = 0f;
    bool  catBall = true;
    const float CAT_PY = -50f, CAT_ARM = 54f, CAT_TAIL = 18f, CAT_REST = 138f;
    const int CAT_END = 78, CAT_LAUNCH = 17;

    void InitCatapult()
    {
        InitAim();
        catF = -1; catArm = CAT_REST; catRoll = 0f; catBall = true; catPrevWx = wx;
        catFacing = PickFacing();
    }

    void StartCatapult()
    {
        if (catF >= 0) return;
        catF = 0;
    }

    void StepCatapult()
    {
        TrackAim();
        catRoll += (wx - catPrevWx) * 0.1f;
        catPrevWx = wx;
        float cs = (float)Math.Cos(bowAim);
        if (Math.Abs(cs) > 0.25f) catFacing = (cs >= 0f) ? 1 : -1;

        if (catF >= 0)
        {
            int f = catF;
            if (f < 10)       catArm = Mix(CAT_REST, 116f, Sm(f / 9f));
            else if (f < 18)  catArm = Mix(116f, 322f, EAcc((f - 10) / 8f));
            else if (f < 23)  catArm = 322f - 12f * Sm((f - 18) / 5f);
            else              catArm = Mix(310f, CAT_REST, Sm((f - 23) / (float)(CAT_END - 24 - 1)));
            if (f == 12) PlayCatapult();
            if (f == CAT_LAUNCH) { LaunchBall(); catBall = false; }
            if (f == CAT_END - 6) catBall = true;
            catF++;
            if (catF > CAT_END) { catF = -1; catArm = CAT_REST; catBall = true; }
        }
        else
        {
            catPulse += 0.05f;
            catArm = CAT_REST + 1.2f * (float)Math.Sin(catPulse);
        }

        StepBalls();
        StepBurns();
        StepParticles();
    }

    // centro da bola/caçamba (coordenadas relativas ao chão da catapulta, virada para a direita)
    void CatBucket(float armDeg, out float cx, out float cy)
    {
        float a = Deg(armDeg);
        cx = CAT_ARM * (float)Math.Cos(a) + 8f * (float)Math.Cos(a + (float)Math.PI / 2f);
        cy = CAT_PY + CAT_ARM * (float)Math.Sin(a) + 8f * (float)Math.Sin(a + (float)Math.PI / 2f);
    }

    void LaunchBall()
    {
        float bcx, bcy;
        CatBucket(catArm, out bcx, out bcy);
        Rectangle vs = SystemInformation.VirtualScreen;
        float ac = (float)Math.Cos(bowAim), asn = (float)Math.Sin(bowAim);
        float sx = wx + catFacing * bcx, sy = wy;

        float tx, ty; int dur;
        if (aimCursor)
        {
            // cai exatamente onde está a mira
            tx = mx; ty = my;
            float d = (float)Math.Sqrt((tx - sx) * (tx - sx) + (ty - sy) * (ty - sy));
            dur = (int)Math.Max(32f, Math.Min(64f, 32f + d / 22f));
        }
        else
        {
            // mira assistida: pega o bug mais perto da linha de tiro
            BugForm best = null; float bestDiff = 0.5f;
            foreach (BugForm bg in bugs)
            {
                if (bg.State != 0) continue;
                float dx = bg.X - sx, dy = bg.Y - sy;
                float d = (float)Math.Sqrt(dx * dx + dy * dy);
                if (d < 90f) continue;
                float cosv = (dx * ac + dy * asn) / d;
                if (cosv > 1f) cosv = 1f;
                if (cosv < -1f) cosv = -1f;
                float diff = (float)Math.Acos(cosv);
                if (diff < bestDiff) { bestDiff = diff; best = bg; }
            }
            if (best != null)
            {
                float dx = best.X - sx, dy = best.Y - sy;
                float d = (float)Math.Sqrt(dx * dx + dy * dy);
                dur = (int)Math.Max(32f, Math.Min(64f, 32f + d / 22f));
                tx = best.X + best.VX * dur; ty = best.Y + best.VY * dur;
            }
            else
            {
                tx = sx + ac * 520f; ty = sy + asn * 520f; dur = 46;
            }
        }
        if (tx < vs.Left + 20) tx = vs.Left + 20;
        if (tx > vs.Right - 20) tx = vs.Right - 20;
        if (ty < vs.Top + 20) ty = vs.Top + 20;
        if (ty > vs.Bottom - 20) ty = vs.Bottom - 20;

        Ball b = new Ball();
        b.sx = sx; b.sy = sy; b.tx = tx; b.ty = ty;
        b.z0 = -bcy; b.dur = dur; b.age = 0;
        float gd = (float)Math.Sqrt((tx - sx) * (tx - sx) + (ty - sy) * (ty - sy));
        b.peak = 90f + gd * 0.18f;
        balls.Add(b);
    }

    float BallU(Ball b) { return Clamp01(b.age / (float)b.dur); }
    float BallZ(Ball b) { float u = BallU(b); return b.z0 * (1f - u) + 4f * b.peak * u * (1f - u); }
    float BallX(Ball b) { return Mix(b.sx, b.tx, BallU(b)); }
    float BallY(Ball b) { return Mix(b.sy, b.ty, BallU(b)) - BallZ(b); }

    void EmitBlueFire(float x, float y, float vx, float vy, int life)
    {
        if (fires.Count > 650) return;
        FireParticle fp = new FireParticle();
        fp.x = x; fp.y = y; fp.vx = vx; fp.vy = vy;
        fp.max = fp.life = life; fp.blue = true;
        fires.Add(fp);
    }

    void StepBalls()
    {
        for (int i = balls.Count - 1; i >= 0; i--)
        {
            Ball b = balls[i];
            b.age++;
            float x = BallX(b), y = BallY(b);
            for (int k = 0; k < 3; k++)
                EmitBlueFire(x + Rf(-5f, 5f), y + Rf(-5f, 5f), Rf(-1.2f, 1.2f), Rf(-2.5f, 0.5f), 10 + rnd.Next(10));
            if (sparks.Count < 450 && rnd.NextDouble() < 0.6)
            {
                Spark s = new Spark();
                s.x = x; s.y = y; s.vx = Rf(-1.5f, 1.5f); s.vy = Rf(-1.5f, 1.5f);
                s.max = s.life = 14 + rnd.Next(10);
                s.hot = true; s.blue = true; s.custom = true; s.drag = 0.95f; s.g = 0.08f;
                sparks.Add(s);
            }
            if (b.age >= b.dur)
            {
                DoBlueBlast(b.tx, b.ty);
                balls.RemoveAt(i);
            }
        }
    }

    void DoBlueBlast(float x, float y)
    {
        rings.Add(new Ring { x = x, y = y, age = 0 });
        HitBugsCircle(x, y, 92f, 4);
        AddScorchDecal(x, y, true);
        for (int i = 0; i < 46; i++)
        {
            double ang = rnd.NextDouble() * Math.PI * 2;
            double spd = 1 + rnd.NextDouble() * 7;
            EmitBlueFire(x, y, (float)(Math.Cos(ang) * spd), (float)(Math.Sin(ang) * spd) - 1.5f, 16 + rnd.Next(16));
        }
        for (int i = 0; i < 28; i++)
        {
            double ang = rnd.NextDouble() * Math.PI * 2;
            double spd = 2 + rnd.NextDouble() * 9;
            Spark s = new Spark();
            s.x = x; s.y = y;
            s.vx = (float)(Math.Cos(ang) * spd); s.vy = (float)(Math.Sin(ang) * spd) - 1f;
            s.max = s.life = 18 + rnd.Next(22);
            s.hot = true; s.blue = true; s.custom = true; s.drag = 0.93f; s.g = 0.2f;
            sparks.Add(s);
        }
        burns.Add(new Burn { x = x, y = y, life = 90 });
        PlayBoom();
    }

    void StepBurns()
    {
        for (int i = burns.Count - 1; i >= 0; i--)
        {
            Burn bn = burns[i];
            bn.life--;
            float k = bn.life / 90f;
            for (int j = 0; j < 2; j++)
                EmitBlueFire(bn.x + Rf(-26f, 26f) * (0.4f + k), bn.y + Rf(-6f, 6f), Rf(-0.5f, 0.5f), Rf(-4.2f, -1.2f), 12 + rnd.Next(12));
            if (bn.life > 30) HitBugsSeg(bn.x - 26f, bn.y, bn.x + 26f, bn.y, 14f);
            if (bn.life <= 0) burns.RemoveAt(i);
        }
    }

    // bola de fogo azul. (dx, dy) = direção do movimento (as chamas vão para trás)
    void DrawBlueBall(float x, float y, float rad, float dx, float dy)
    {
        using (SolidBrush g1 = new SolidBrush(Color.FromArgb(38, 40, 90, 255)))
            gfx.FillEllipse(g1, x - rad * 2.6f, y - rad * 2.6f, rad * 5.2f, rad * 5.2f);
        using (SolidBrush g2 = new SolidBrush(Color.FromArgb(90, 60, 140, 255)))
            gfx.FillEllipse(g2, x - rad * 1.7f, y - rad * 1.7f, rad * 3.4f, rad * 3.4f);

        float baseAng = (float)Math.Atan2(-dy, -dx);
        for (int k = 0; k < 5; k++)
        {
            float ang = baseAng + (k - 2) * 0.42f + Rf(-0.12f, 0.12f);
            float len = rad * (1.6f + Rf(0f, 1.2f));
            float ex = x + (float)Math.Cos(ang) * len, ey = y + (float)Math.Sin(ang) * len;
            pen.Color = Color.FromArgb(150, 50, 120, 255); pen.Width = rad * 1.1f;
            gfx.DrawLine(pen, x, y, ex, ey);
            pen.Color = Color.FromArgb(190, 130, 210, 255); pen.Width = rad * 0.55f;
            gfx.DrawLine(pen, x, y, x + (ex - x) * 0.7f, y + (ey - y) * 0.7f);
        }
        using (SolidBrush b3 = new SolidBrush(Color.FromArgb(235, 90, 170, 255)))
            gfx.FillEllipse(b3, x - rad, y - rad, rad * 2f, rad * 2f);
        using (SolidBrush b4 = new SolidBrush(Color.FromArgb(255, 215, 245, 255)))
            gfx.FillEllipse(b4, x - rad * 0.55f, y - rad * 0.55f, rad * 1.1f, rad * 1.1f);
    }

    float cbx, cby; int cbs = 1;
    PointF CP(float x, float y) { return new PointF(cbx + cbs * x, cby + y); }

    void DrawCatScene(float ox, float oy)
    {
        // brasas azuis no chão
        foreach (Burn bn in burns)
        {
            float k = Clamp01(bn.life / 90f);
            float x = bn.x - ox, y = bn.y - oy;
            using (SolidBrush gl = new SolidBrush(Color.FromArgb(Cl(70 * k), 40, 110, 255)))
                gfx.FillEllipse(gl, x - 52f, y - 16f, 104f, 32f);
            using (SolidBrush gl2 = new SolidBrush(Color.FromArgb(Cl(110 * k), 120, 200, 255)))
                gfx.FillEllipse(gl2, x - 26f, y - 8f, 52f, 16f);
        }
        DrawFireParticles(ox, oy);

        foreach (Ball b in balls)
        {
            float u = BallU(b);
            float gx = Mix(b.sx, b.tx, u) - ox, gy = Mix(b.sy, b.ty, u) - oy;
            float z = BallZ(b);
            float sh = 1f - Clamp01(z / 260f) * 0.6f;
            using (SolidBrush shb = new SolidBrush(Color.FromArgb(Cl(70 * sh), 0, 0, 0)))
                gfx.FillEllipse(shb, gx - 16f * sh, gy - 5f * sh, 32f * sh, 10f * sh);
            // alvo no chão
            pen.Color = Color.FromArgb(Cl(150 * u), 120, 200, 255); pen.Width = 1.6f;
            float tx = b.tx - ox, ty = b.ty - oy, tr = 40f - 22f * u;
            gfx.DrawEllipse(pen, tx - tr, ty - tr * 0.4f, tr * 2f, tr * 0.8f);
            float nx = BallX(b) - ox, ny = BallY(b) - oy;
            float vx = (b.tx - b.sx) / b.dur, vy = (b.ty - b.sy) / b.dur;
            DrawBlueBall(nx, ny, 13f, vx, vy);
        }

        DrawCatBody(ox, oy);
        DrawEffects(ox, oy);
    }

    void CatLine(Color c, float w, float x0, float y0, float x1, float y1)
    {
        pen.Color = c; pen.Width = w;
        gfx.DrawLine(pen, CP(x0, y0), CP(x1, y1));
    }
    void CatPoly(Color fill, Color edge, params float[] fv)
    {
        PointF[] pp = new PointF[fv.Length / 2];
        for (int i = 0; i < pp.Length; i++) pp[i] = CP(fv[i * 2], fv[i * 2 + 1]);
        using (SolidBrush br = new SolidBrush(fill)) gfx.FillPolygon(br, pp);
        pen.Color = edge; pen.Width = 1.3f;
        gfx.DrawPolygon(pen, pp);
    }

    void DrawCatBody(float ox, float oy)
    {
        cbx = wx - ox; cby = wy - oy; cbs = catFacing;
        Color wood = Color.FromArgb(255, 150, 98, 48), woodD = Color.FromArgb(255, 58, 34, 14);
        Color woodM = Color.FromArgb(255, 112, 72, 34);

        using (SolidBrush sh = new SolidBrush(Color.FromArgb(60, 0, 0, 0)))
            gfx.FillEllipse(sh, cbx - 50f, cby - 5f, 100f, 12f);

        // rodas
        float[] wxs = new float[] { -30f, 22f };
        foreach (float wxx in wxs)
        {
            PointF c = CP(wxx, -9f);
            using (SolidBrush wb = new SolidBrush(woodM)) gfx.FillEllipse(wb, c.X - 10f, c.Y - 10f, 20f, 20f);
            pen.Color = woodD; pen.Width = 2.2f;
            gfx.DrawEllipse(pen, c.X - 10f, c.Y - 10f, 20f, 20f);
            for (int k = 0; k < 6; k++)
            {
                float a = catRoll * cbs + k * (float)Math.PI / 3f;
                pen.Color = Color.FromArgb(255, 70, 44, 20); pen.Width = 1.6f;
                gfx.DrawLine(pen, c.X, c.Y, c.X + (float)Math.Cos(a) * 9f, c.Y + (float)Math.Sin(a) * 9f);
            }
            using (SolidBrush hub = new SolidBrush(Color.FromArgb(255, 150, 154, 164))) gfx.FillEllipse(hub, c.X - 3f, c.Y - 3f, 6f, 6f);
        }

        // viga base, pernas em A, travessa e batente
        CatPoly(wood, woodD, -42f, -25f, 38f, -25f, 38f, -14f, -42f, -14f);
        CatLine(woodD, 8.5f, -24f, -24f, 0f, CAT_PY); CatLine(wood, 5.5f, -24f, -24f, 0f, CAT_PY);
        CatLine(woodD, 8.5f, 24f, -24f, 0f, CAT_PY);  CatLine(wood, 5.5f, 24f, -24f, 0f, CAT_PY);
        CatLine(woodD, 5f, -12f, -36f, 12f, -36f);    CatLine(woodM, 3f, -12f, -36f, 12f, -36f);
        CatLine(woodD, 6f, 32f, -25f, 32f, -46f);     CatLine(wood, 3.5f, 32f, -25f, 32f, -46f);
        CatLine(woodD, 5f, 26f, -46f, 38f, -46f);

        // braço
        float arm = Deg(catArm);
        float dx = (float)Math.Cos(arm), dy = (float)Math.Sin(arm);
        float ex = CAT_ARM * dx, ey = CAT_PY + CAT_ARM * dy;
        float sx = -CAT_TAIL * dx, sy = CAT_PY - CAT_TAIL * dy;
        CatLine(woodD, 9f, sx, sy, ex, ey);
        CatLine(wood, 6f, sx, sy, ex, ey);
        CatLine(Color.FromArgb(110, 255, 230, 180), 1.4f, sx, sy - 1.5f, ex, ey - 1.5f);

        // contrapeso de pedra
        PointF cw = CP(sx - dx * 5f, sy - dy * 5f);
        using (SolidBrush st = new SolidBrush(Color.FromArgb(255, 112, 114, 122))) gfx.FillEllipse(st, cw.X - 9f, cw.Y - 9f, 18f, 18f);
        pen.Color = Color.FromArgb(255, 30, 30, 36); pen.Width = 1.6f;
        gfx.DrawEllipse(pen, cw.X - 9f, cw.Y - 9f, 18f, 18f);
        using (SolidBrush st2 = new SolidBrush(Color.FromArgb(120, 255, 255, 255))) gfx.FillEllipse(st2, cw.X - 5f, cw.Y - 6f, 6f, 4f);

        // caçamba
        float ox2 = (float)Math.Cos(arm + Math.PI / 2), oy2 = (float)Math.Sin(arm + Math.PI / 2);
        CatPoly(woodM, woodD,
                ex - dx * 9f, ey - dy * 9f,
                ex + dx * 9f, ey + dy * 9f,
                ex + dx * 9f + ox2 * 9f, ey + dy * 9f + oy2 * 9f,
                ex - dx * 9f + ox2 * 9f, ey - dy * 9f + oy2 * 9f);
        CatLine(Color.FromArgb(255, 150, 154, 164), 2f, ex - dx * 9f + ox2 * 9f, ey - dy * 9f + oy2 * 9f, ex + dx * 9f + ox2 * 9f, ey + dy * 9f + oy2 * 9f);

        // bola de fogo azul esperando na caçamba
        if (catBall)
        {
            float bcx, bcy;
            CatBucket(catArm, out bcx, out bcy);
            PointF bp = CP(bcx, bcy);
            DrawBlueBall(bp.X, bp.Y, 10f + 1.2f * (float)Math.Sin(catPulse * 6f), 0f, 1f);
        }

        // eixo
        PointF axp = CP(0f, CAT_PY);
        using (SolidBrush axb = new SolidBrush(Color.FromArgb(255, 140, 144, 154))) gfx.FillEllipse(axb, axp.X - 4.5f, axp.Y - 4.5f, 9f, 9f);
        pen.Color = Color.FromArgb(255, 30, 30, 36); pen.Width = 1.2f;
        gfx.DrawEllipse(pen, axp.X - 4.5f, axp.Y - 4.5f, 9f, 9f);

        // barra de recarga: enche enquanto o braço volta
        if (catF >= CAT_LAUNCH)
        {
            float frac = (catF - CAT_LAUNCH) / (float)(CAT_END - CAT_LAUNCH);
            DrawBar(cbx - 34f, cby + 10f, 68f, 6f, frac, Color.FromArgb(255, 90, 170, 255));
            DrawSmallText("CARREGANDO", cbx, cby + 18f, Color.FromArgb(255, 150, 205, 255));
        }
        else
        {
            DrawBar(cbx - 34f, cby + 10f, 68f, 6f, 1f, Color.FromArgb(255, 90, 220, 120));
        }

        DrawHotspot(ox, oy);
    }

}
