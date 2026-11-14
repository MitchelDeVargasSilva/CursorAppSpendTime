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
    //  MODO ESPADA  (combo de 3 golpes: corte descendente, corte
    //  ascendente e estocada; o braço gira no pulso, sem "chicotear")
    // ===================================================================
    const float SWORD_LEN = 96f;
    const float GUARD_W   = 17f;
    const float GRIP_LEN  = 22f;

    int   swF = -1, swType = 0, swCombo = 0, swFacing = 1, swIdle = 0;
    float swAng, swReach = 1f, swHand = 0f;
    float swFromAng, swFromReach = 1f, swFromHand = 0f;
    float swPrevTipX, swPrevTipY, swPulse = 0f;
    bool  swStrikeNow = false, swStrikePrev = false;

    void InitSword()
    {
        swFacing = PickFacing();
        swAng = Deg(-60f); swReach = 1f; swHand = 0f;
        swF = -1; swType = 0; swCombo = 0; swIdle = 0; swPulse = 0f;
        swPrevTipX = mx; swPrevTipY = my;
    }

    // só aceita novo golpe parado ou já na fase de recuperação (combo fluido)
    bool SwordCanChain()
    {
        int rec = (swType == 1) ? 11 : 13;
        return swF >= rec;
    }

    void StartSwordSlash()
    {
        if (swF >= 0 && !SwordCanChain()) return;
        if (swF < 0) swFacing = PickFacing();
        if (swIdle > 50) swCombo = 0;
        swType  = swCombo;
        swCombo = (swCombo + 1) % 3;
        swFromAng = swAng; swFromReach = swReach; swFromHand = swHand;
        swF = 0;
        swIdle = 0;
    }

    // Pose da espada em cada quadro: preparar -> cortar -> segurar -> recuperar
    void SwordPose(int type, int f, out float ang, out float reach, out float hand)
    {
        float rest = Deg(-60f);
        ang = rest; reach = 1f; hand = 0f;

        if (type == 0)   // corte descendente (de cima para baixo, na diagonal)
        {
            float aw = Deg(-125f), ae = Deg(55f);
            if (f < 6)       { float u = EDec((f + 1) / 6f); ang = Mix(swFromAng, aw, u); reach = Mix(swFromReach, 1f, u); hand = Mix(swFromHand, -6f, u); }
            else if (f < 11) { float u = EAcc((f - 5) / 5f); ang = Mix(aw, ae, u); hand = Mix(-6f, 10f, u); }
            else if (f < 13) { ang = ae + Deg(3f) * (f - 10); hand = 10f; }
            else             { float u = Sm((f - 12) / 12f); ang = Mix(ae + Deg(6f), rest, u); hand = Mix(10f, 0f, u); }
        }
        else if (type == 1)   // corte ascendente (reverso, de baixo para cima)
        {
            float aw = Deg(35f), ae = Deg(-140f);
            if (f < 4)       { float u = EDec((f + 1) / 4f); ang = Mix(swFromAng, aw, u); reach = Mix(swFromReach, 1f, u); hand = Mix(swFromHand, -4f, u); }
            else if (f < 9)  { float u = EAcc((f - 3) / 5f); ang = Mix(aw, ae, u); hand = Mix(-4f, 8f, u); }
            else if (f < 11) { ang = ae - Deg(3f) * (f - 8); hand = 8f; }
            else             { float u = Sm((f - 10) / 12f); ang = Mix(ae - Deg(6f), rest, u); hand = Mix(8f, 0f, u); }
        }
        else   // estocada (a lâmina avança em linha reta)
        {
            float a0 = Deg(-14f), a1 = Deg(0f);
            if (f < 6)       { float u = EDec((f + 1) / 6f); ang = Mix(swFromAng, a0, u); reach = Mix(swFromReach, 0.84f, u); hand = Mix(swFromHand, -7f, u); }
            else if (f < 10) { float u = EAcc((f - 5) / 4f); ang = Mix(a0, a1, u); reach = Mix(0.84f, 1.2f, u); hand = Mix(-7f, 16f, u); }
            else if (f < 13) { ang = a1; reach = 1.2f; hand = 16f; }
            else             { float u = Sm((f - 12) / 10f); ang = Mix(a1, rest, u); reach = Mix(1.2f, 1f, u); hand = Mix(16f, 0f, u); }
        }
    }

    void StepSword()
    {
        float rest = Deg(-60f);
        bool strike = false, impact = false;

        if (swF >= 0)
        {
            SwordPose(swType, swF, out swAng, out swReach, out swHand);
            int sStart = (swType == 1) ? 5 : 7;                         // começa o corte (som + rastro)
            int sEnd   = (swType == 0) ? 10 : (swType == 1 ? 8 : 9);    // fim do corte (impacto)
            int sLast  = (swType == 0) ? 24 : 22;
            if (swF == sStart) PlaySlash();
            strike = (swF >= sStart && swF <= sEnd);
            impact = (swF == sEnd);
            swF++;
            if (swF > sLast) { swF = -1; swIdle = 0; }
        }
        else
        {
            swIdle++;
            swPulse += 0.06f;
            swAng = rest + Deg(2.5f) * (float)Math.Sin(swPulse);
            swReach = 1f; swHand = 0f;
        }

        float ca = (float)Math.Cos(swAng) * swFacing, sa = (float)Math.Sin(swAng);
        float hx = mx + swFacing * swHand, hy = my;
        float tipX = hx + ca * SWORD_LEN * swReach;
        float tipY = hy + sa * SWORD_LEN * swReach;
        float svx = tipX - swPrevTipX, svy = tipY - swPrevTipY;

        // rastro em crescente
        for (int i = trails.Count - 1; i >= 0; i--)
        {
            trails[i].age++;
            if (trails[i].age > 10) trails.RemoveAt(i);
        }
        if (strike)
        {
            Trail tr = new Trail();
            tr.tx = tipX; tr.ty = tipY;
            tr.bx = hx + ca * SWORD_LEN * swReach * 0.42f;
            tr.by = hy + sa * SWORD_LEN * swReach * 0.42f;
            tr.age = 0;
            tr.start = !swStrikePrev;
            trails.Add(tr);

            // faíscas de aço rasgando o ar
            for (int i = 0; i < 2; i++)
            {
                float kk = 0.55f + (float)rnd.NextDouble() * 0.45f;
                AddSteelSpark(hx + ca * SWORD_LEN * swReach * kk, hy + sa * SWORD_LEN * swReach * kk,
                              svx * 0.12f + Rf(-1.5f, 1.5f), svy * 0.12f + Rf(-1.5f, 1.5f) - 0.4f);
            }
        }
        if (strike)
        {
            HitBugsSeg(hx + ca * SWORD_LEN * swReach * 0.35f, hy + sa * SWORD_LEN * swReach * 0.35f, tipX, tipY, 11f, 2);
            HitBugsSeg(swPrevTipX, swPrevTipY, tipX, tipY, 14f, 2);
        }
        swStrikePrev = strike;
        swStrikeNow  = strike;

        if (impact) DoSwordImpact(tipX, tipY, svx, svy);

        swPrevTipX = tipX; swPrevTipY = tipY;
        StepParticles();
    }

    void AddSteelSpark(float x, float y, float vx, float vy)
    {
        if (sparks.Count > 400) return;
        Spark s = new Spark();
        s.x = x; s.y = y; s.vx = vx; s.vy = vy;
        s.max = s.life = 8 + rnd.Next(10);
        s.custom = true; s.drag = 0.90f; s.g = 0.25f;
        sparks.Add(s);
    }

    void DoSwordImpact(float x, float y, float vx, float vy)
    {
        rings.Add(new Ring { x = x, y = y, age = 0 });
        float sp = (float)Math.Sqrt(vx * vx + vy * vy);
        if (sp < 0.01f) sp = 1f;
        float ux = vx / sp, uy = vy / sp;
        AddSlashDecal(x, y, ux, uy);
        for (int i = 0; i < 22; i++)
        {
            double ang = rnd.NextDouble() * Math.PI * 2;
            float spd = 2f + (float)rnd.NextDouble() * 8f;
            // as faíscas saem mais na direção do corte
            AddSteelSpark(x, y,
                ux * spd * 0.9f + (float)Math.Cos(ang) * spd * 0.55f,
                uy * spd * 0.9f + (float)Math.Sin(ang) * spd * 0.55f);
        }
        PlayClang();
    }

}
