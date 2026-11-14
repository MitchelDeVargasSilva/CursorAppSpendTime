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
    //  MODO CHICOTE DE CORRENTE  (elos de aço, peso com espinhos,
    //  barulho de corrente e rastro de faíscas)
    // ===================================================================
    int   chF = -1, chType = 0, chCombo = 0, chFacing = 1, chRattleCd = 0;
    bool  chSlammed = false;
    float chGx = 0.64f, chGy = -0.77f, chTipSpeed = 0f;

    void InitChain()
    {
        chFacing = PickFacing();
        chF = -1; chType = 0; chCombo = 0; chRattleCd = 0; chSlammed = false;
        float gx = chFacing * 0.64f, gy = -0.77f;
        chGx = gx; chGy = gy;
        float sx = mx + gx * CGRIP, sy = my + gy * CGRIP;
        for (int i = 0; i < CN; i++)
        {
            cpts[i].x = cpts[i].px = sx;
            cpts[i].y = cpts[i].py = sy + i * CSEG;
        }
    }

    void StartChainThrow()
    {
        if (chF >= 0 && chF <= 28) return;
        chFacing  = PickFacing();
        chType    = chCombo;
        chCombo   = (chCombo + 1) % 2;      // alterna: chicotada / giro
        chF       = 0;
        chSlammed = false;
    }

    // giro: o cabo dá duas voltas e depois solta a corrente
    static double ChainSpinAngle(int f)
    {
        double rest = -50.0 * Math.PI / 180.0;
        if (f < 0 || f >= 22) return rest;
        double u = f / 22.0;
        u = u * u * (3.0 - 2.0 * u);
        return rest + 4.0 * Math.PI * u;
    }

    void EmitChainSpark(float x, float y, float vx, float vy)
    {
        if (sparks.Count > 450) return;
        Spark s = new Spark();
        s.x = x; s.y = y; s.vx = vx; s.vy = vy;
        s.max = s.life = 16 + rnd.Next(22);
        s.hot = true; s.custom = true; s.drag = 0.95f; s.g = 0.28f;
        sparks.Add(s);
    }

    void StepChain()
    {
        double th;
        if (chF < 0) th = -50.0 * Math.PI / 180.0;
        else th = (chType == 0) ? HandleAngle(chF) : ChainSpinAngle(chF);
        float gx = (float)(chFacing * Math.Cos(th));
        float gy = (float)Math.Sin(th);
        chGx = gx; chGy = gy;

        cpts[0].x = cpts[0].px = mx + gx * CGRIP;
        cpts[0].y = cpts[0].py = my + gy * CGRIP;

        if (chF >= 0)
        {
            if (chType == 0)
            {
                if (chF == 9) PlayWhoosh();
                if (chF >= 11 && chF <= 17)
                {
                    for (int i = 1; i < CN; i++)
                    {
                        float k1 = (float)i / CN; k1 *= k1;
                        cpts[i].px -= chFacing * 5.0f * k1;
                    }
                }
            }
            else
            {
                if (chF == 1) PlayWhoosh();
                if (chF >= 2 && chF <= 19)
                {
                    // força tangencial: faz a corrente girar ao redor da mão
                    for (int i = 3; i < CN; i++)
                    {
                        float rx = cpts[i].x - mx, ry = cpts[i].y - my;
                        float rl = (float)Math.Sqrt(rx * rx + ry * ry);
                        if (rl < 1f) continue;
                        float k2 = (float)i / CN;
                        cpts[i].px -= (-ry / rl) * chFacing * 1.1f * k2;
                        cpts[i].py -= ( rx / rl) * chFacing * 1.1f * k2;
                    }
                }
                if (chF >= 20 && chF <= 22)
                {
                    for (int i = 1; i < CN; i++)
                    {
                        float k3 = (float)i / CN; k3 *= k3;
                        cpts[i].px -= chFacing * 5.0f * k3;
                    }
                }
            }
        }

        // integração (corrente pesada: gravidade alta, pouco amortecimento)
        for (int i = 1; i < CN; i++)
        {
            Pt pt = cpts[i];
            float ivx = (pt.x - pt.px) * 0.992f;
            float ivy = (pt.y - pt.py) * 0.992f + 0.55f;
            pt.px = pt.x; pt.py = pt.y;
            pt.x += ivx; pt.y += ivy;
        }

        // pequena rigidez na base
        for (int i = 1; i <= 3; i++)
        {
            float dxs, dys;
            if (i == 1) { dxs = gx; dys = gy; }
            else
            {
                dxs = cpts[i - 1].x - cpts[i - 2].x;
                dys = cpts[i - 1].y - cpts[i - 2].y;
                float l = (float)Math.Sqrt(dxs * dxs + dys * dys);
                if (l < 0.0001f) l = 1f;
                dxs /= l; dys /= l;
            }
            float tx = cpts[i - 1].x + dxs * CSEG, ty = cpts[i - 1].y + dys * CSEG;
            float kk = (i == 1) ? 0.45f : 0.16f * (1f - (i - 2) / 3f);
            cpts[i].x += (tx - cpts[i].x) * kk;
            cpts[i].y += (ty - cpts[i].y) * kk;
        }

        // restrições de distância entre elos
        for (int it = 0; it < 20; it++)
        {
            for (int i = 1; i < CN; i++)
            {
                Pt a = cpts[i - 1], b = cpts[i];
                float dx = b.x - a.x, dy = b.y - a.y;
                float d = (float)Math.Sqrt(dx * dx + dy * dy);
                if (d < 0.0001f) d = 0.0001f;
                float diff = (d - CSEG) / d;
                float wa = cinv[i - 1], wb = cinv[i], ws = wa + wb;
                a.x += dx * diff * wa / ws; a.y += dy * diff * wa / ws;
                b.x -= dx * diff * wb / ws; b.y -= dy * diff * wb / ws;
            }
        }

        // energia da corrente (para o barulho) e velocidade da ponta
        float en = 0f;
        for (int i = 1; i < CN; i++)
        {
            float ddx = cpts[i].x - cpts[i].px, ddy = cpts[i].y - cpts[i].py;
            en += (float)Math.Sqrt(ddx * ddx + ddy * ddy);
        }
        en /= (CN - 1);

        Pt tip = cpts[CN - 1];
        float tvx = tip.x - tip.px, tvy = tip.y - tip.py;
        chTipSpeed = (float)Math.Sqrt(tvx * tvx + tvy * tvy);
        if (chTipSpeed > 8f) HitBugsSeg(tip.px, tip.py, tip.x, tip.y, 13f);

        // faíscas: resquícios saindo da ponta e dos elos quando em movimento
        if (chTipSpeed > 7f)
        {
            int cnt = (int)(chTipSpeed / 5f);
            if (cnt > 6) cnt = 6;
            for (int c = 0; c < cnt; c++)
                EmitChainSpark(tip.x, tip.y, tvx * 0.18f + Rf(-2f, 2f), tvy * 0.18f + Rf(-2.5f, 1.5f));
        }
        if (chF >= 6 && en > 3f && rnd.NextDouble() < 0.6)
        {
            int li = CN / 2 + rnd.Next(CN / 2);
            float lvx = cpts[li].x - cpts[li].px, lvy = cpts[li].y - cpts[li].py;
            EmitChainSpark(cpts[li].x, cpts[li].y, lvx * 0.15f + Rf(-1.5f, 1.5f), lvy * 0.15f + Rf(-2f, 1f));
        }

        // movimento: mesmo "whoosh" do chicote normal (só em balançadas fortes)
        if (chRattleCd > 0) chRattleCd--;
        if (chF < 0 && chTipSpeed > 14f && chRattleCd <= 0)
        {
            PlayWhoosh();
            chRattleCd = 30;
        }

        // batida no fim do golpe
        if (chF >= 0)
        {
            float ex = tip.x - mx, ey = tip.y - my;
            float dist = (float)Math.Sqrt(ex * ex + ey * ey);
            float full = CGRIP + (CN - 1) * CSEG;
            bool ready;
            if (chType == 0) ready = (chF >= 14 && dist > 0.78f * full) || chF >= 24;
            else             ready = chF >= 23 && (dist > 0.78f * full || chF >= 28);
            if (!chSlammed && ready)
            {
                chSlammed = true;
                DoChainSlam(tip.x, tip.y);
            }
            chF++;
            if (chF > 36) chF = -1;
        }

        StepParticles();
    }

    void DoChainSlam(float x, float y)
    {
        HitBugsSeg(x - 20f, y, x + 20f, y, 34f, 2);
        AddCrackDecal(x, y, 0.55f);
        rings.Add(new Ring { x = x, y = y, age = 0 });
        for (int i = 0; i < 34; i++)
        {
            double ang = rnd.NextDouble() * Math.PI * 2;
            double spd = 3 + rnd.NextDouble() * 10;
            Spark s = new Spark();
            s.x = x; s.y = y;
            s.vx = (float)(Math.Cos(ang) * spd);
            s.vy = (float)(Math.Sin(ang) * spd) - 1.5f;
            s.max = s.life = 20 + rnd.Next(26);
            s.hot = true; s.custom = true; s.drag = 0.94f; s.g = 0.3f;
            sparks.Add(s);
        }
        PlayClank();
    }

}
