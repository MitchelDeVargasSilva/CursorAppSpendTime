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
    //  MODO PORRETE COM PREGOS  (pancada pesada de cima + tacada lateral)
    // ===================================================================
    const float MC_LEN = 88f;      // da mão até a ponta da cabeça

    int   mcF = -1, mcType = 0, mcCombo = 0, mcFacing = 1, mcIdle = 0, mcShake = 0;
    float mcAng, mcPrevAng, mcHand = 0f, mcFromAng, mcFromHand, mcPulse = 0f;

    static readonly float[] MC_S = new float[] { 0f, 0.28f, 0.50f, 0.66f, 0.80f, 0.92f, 1.0f };
    static readonly float[] MC_W = new float[] { 2.6f, 2.6f, 4.2f, 6.4f, 8.2f, 8.8f, 6.2f };
    static readonly float[] NAIL_S    = new float[] { 0.64f, 0.70f, 0.76f, 0.82f, 0.88f, 0.94f, 0.67f, 0.79f, 0.91f };
    static readonly int[]   NAIL_SIDE = new int[]   { 1, -1, 1, -1, 1, -1, -1, 1, -1 };
    static readonly float[] NAIL_TILT = new float[] { -0.35f, 0.30f, -0.10f, 0.35f, 0.15f, -0.30f, 0.10f, -0.25f, 0.20f };

    void InitMace()
    {
        mcFacing = PickFacing();
        mcAng = mcPrevAng = Deg(-78f);
        mcHand = 0f;
        mcF = -1; mcType = 0; mcCombo = 0; mcIdle = 0; mcShake = 0; mcPulse = 0f;
    }

    void StartMaceSwing()
    {
        // pesado: só aceita outro golpe na fase de recuperação
        if (mcF >= 0)
        {
            int rec = (mcType == 0) ? 25 : 17;
            if (mcF < rec) return;
        }
        if (mcF < 0) mcFacing = PickFacing();
        if (mcIdle > 60) mcCombo = 0;
        mcType  = mcCombo;
        mcCombo = (mcCombo + 1) % 2;
        mcFromAng = mcAng; mcFromHand = mcHand;
        mcF = 0;
        mcIdle = 0;
    }

    void MacePose(int type, int f, out float ang, out float hand)
    {
        float rest = Deg(-78f);
        ang = rest; hand = 0f;

        if (type == 0)   // pancada de cima: sobe por trás do ombro, pausa, e desce com tudo
        {
            if (f < 10)      { float u = EDec((f + 1) / 10f); ang = Mix(mcFromAng, Deg(-155f), u); hand = Mix(mcFromHand, -6f, u); }
            else if (f < 12) { ang = Deg(-155f) - Deg(4f) * (f - 9); hand = -8f; }
            else if (f < 17) { float u = EAcc((f - 11) / 5f); ang = Mix(Deg(-163f), Deg(62f), u); hand = Mix(-8f, 12f, u); }
            else if (f < 25) { int k = f - 16; ang = Deg(62f) - Deg(14f) * (float)(Math.Exp(-0.35 * k) * Math.Sin(1.2 * k)); hand = 12f; }
            else             { float u = Sm((f - 24) / 14f); ang = Mix(Deg(62f), rest, u); hand = Mix(12f, 0f, u); }
        }
        else   // tacada lateral: leva o porrete atrás e varre a frente de baixo para cima
        {
            if (f < 8)       { float u = EDec((f + 1) / 8f); ang = Mix(mcFromAng, Deg(125f), u); hand = Mix(mcFromHand, -8f, u); }
            else if (f < 10) { ang = Deg(125f) + Deg(4f) * (f - 7); hand = -10f; }
            else if (f < 15) { float u = EAcc((f - 9) / 5f); ang = Mix(Deg(133f), Deg(-85f), u); hand = Mix(-10f, 14f, u); }
            else if (f < 17) { ang = Deg(-85f) - Deg(4f) * (f - 14); hand = 14f; }
            else             { float u = Sm((f - 16) / 14f); ang = Mix(Deg(-93f), rest, u); hand = Mix(14f, 0f, u); }
        }
    }

    void StepMace()
    {
        float rest = Deg(-78f);
        mcPrevAng = mcAng;

        if (mcF >= 0)
        {
            MacePose(mcType, mcF, out mcAng, out mcHand);
            int whooshF = (mcType == 0) ? 12 : 10;
            int hitF    = (mcType == 0) ? 16 : 13;
            int lastF   = (mcType == 0) ? 38 : 30;
            if (mcF == whooshF) PlayWhoosh();
            if (mcF == hitF) DoMaceHit();
            mcF++;
            if (mcF > lastF) { mcF = -1; mcIdle = 0; }
        }
        else
        {
            mcIdle++;
            mcPulse += 0.05f;
            mcAng = rest + Deg(2f) * (float)Math.Sin(mcPulse);
            mcHand = 0f;
        }

        if (mcShake > 0) mcShake--;
        StepParticles();
    }

    void DoMaceHit()
    {
        float ca = (float)Math.Cos(mcAng) * mcFacing, sa = (float)Math.Sin(mcAng);
        float x = mx + mcFacing * mcHand + ca * MC_LEN * 0.97f;
        float y = my + sa * MC_LEN * 0.97f;

        rings.Add(new Ring { x = x, y = y, age = 0 });
        HitBugsSeg(x - 24f, y, x + 24f, y, 40f, 3);
        AddCrackDecal(x, y, 1f);

        // lascas de madeira
        int shardCount = 16 + rnd.Next(10);
        for (int i = 0; i < shardCount; i++)
        {
            Shard sh = new Shard();
            double ang = rnd.NextDouble() * Math.PI * 2;
            double spd = 2.5 + rnd.NextDouble() * 9;
            sh.x = x + (float)(rnd.NextDouble() - 0.5) * 6;
            sh.y = y + (float)(rnd.NextDouble() - 0.5) * 6;
            sh.vx = (float)(Math.Cos(ang) * spd);
            sh.vy = (float)(Math.Sin(ang) * spd) - (float)(rnd.NextDouble() * 3);
            sh.rot = (float)(rnd.NextDouble() * Math.PI * 2);
            sh.rotV = (float)(rnd.NextDouble() - 0.5) * 0.55f;
            sh.len = 4f + (float)(rnd.NextDouble() * 11);
            sh.w   = 1.2f + (float)(rnd.NextDouble() * 2.2);
            sh.max = sh.life = 22 + rnd.Next(20);
            shards.Add(sh);
        }

        // poeira
        for (int i = 0; i < 22; i++)
        {
            Spark s = new Spark();
            double ang = rnd.NextDouble() * Math.PI * 2;
            double spd = 1 + rnd.NextDouble() * 5;
            s.x = x; s.y = y;
            s.vx = (float)(Math.Cos(ang) * spd);
            s.vy = (float)(Math.Sin(ang) * spd) - 0.5f;
            s.max = s.life = 10 + rnd.Next(12);
            sparks.Add(s);
        }

        // faíscas dos pregos batendo
        for (int i = 0; i < 9; i++)
        {
            Spark s = new Spark();
            double ang = rnd.NextDouble() * Math.PI * 2;
            double spd = 2 + rnd.NextDouble() * 7;
            s.x = x; s.y = y;
            s.vx = (float)(Math.Cos(ang) * spd);
            s.vy = (float)(Math.Sin(ang) * spd) - 1f;
            s.max = s.life = 10 + rnd.Next(10);
            s.hot = true; s.custom = true; s.drag = 0.92f; s.g = 0.3f;
            sparks.Add(s);
        }

        mcShake = 7;
        PlayThud();
    }

}
