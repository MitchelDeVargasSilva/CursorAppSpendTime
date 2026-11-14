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
    //  ARMAS NOVAS: AK-47, CATAPULTA DE FOGO AZUL, SPRAY
    //  (usam a mesma camada grande do arco; a mira segue o último
    //   movimento do mouse, igual ao arco)
    // ===================================================================
    bool UsesBigLayer()
    {
        return mode == WeaponMode.Bow || mode == WeaponMode.Ak ||
               mode == WeaponMode.Catapult || mode == WeaponMode.Spray ||
               mode == WeaponMode.Flame || mode == WeaponMode.Grenade;
    }

    List<Bullet> bullets = new List<Bullet>();
    List<Ball> balls = new List<Ball>();
    List<Burn> burns = new List<Burn>();
    List<Dot> dots = new List<Dot>();
    SolidBrush dotBr = new SolidBrush(Color.White);

    // botão apertado agora? (com o gancho ativo vale o estado engolido por ele)
    bool TriggerHeld()
    {
        if (hookId != IntPtr.Zero) return heldLeft || heldRight;
        return ((GetAsyncKeyState(1) & 0x8000) != 0) || ((GetAsyncKeyState(2) & 0x8000) != 0);
    }

    void InitAim()
    {
        EnsureBowDib();
        wxInit = false; UpdateWeaponPos();
        int fc = PickFacing();
        bowAim = (fc == 1) ? Deg(-8f) : Deg(188f);
        bowPrevMx = mx; bowPrevMy = my;
    }

    void TrackAim()
    {
        UpdateWeaponPos();
        float mvx = mx - bowPrevMx, mvy = my - bowPrevMy;
        bowPrevMx = mx; bowPrevMy = my;
        if (aimCursor) { bowAim = (float)Math.Atan2(my - wy, mx - wx); return; }
        if (mvx * mvx + mvy * mvy > 9f)
        {
            float target = (float)Math.Atan2(mvy, mvx);
            float diff = target - bowAim;
            while (diff > (float)Math.PI)  diff -= 2f * (float)Math.PI;
            while (diff < -(float)Math.PI) diff += 2f * (float)Math.PI;
            bowAim += diff * 0.18f;
        }
    }

    // base de desenho "(f, v)": f = para frente da arma, v = para cima na tela
    float gbx, gby, gca, gsa, gsg;
    void SetGunBasis(float cx, float cy, float ang)
    {
        gca = (float)Math.Cos(ang); gsa = (float)Math.Sin(ang);
        gsg = (gca >= 0f) ? -1f : 1f;
        gbx = cx; gby = cy;
    }
    PointF GP(float f, float v)
    {
        f *= cfgScale; v *= cfgScale;
        return new PointF(gbx + gca * f - gsg * gsa * v, gby + gsa * f + gsg * gca * v);
    }
    void GPoly(Color fill, Color edge, params float[] fv)
    {
        PointF[] pp = new PointF[fv.Length / 2];
        for (int i = 0; i < pp.Length; i++) pp[i] = GP(fv[i * 2], fv[i * 2 + 1]);
        using (SolidBrush br = new SolidBrush(fill)) gfx.FillPolygon(br, pp);
        pen.Color = edge; pen.Width = 1.2f;
        gfx.DrawPolygon(pen, pp);
    }
    void GLine(Color c, float w, float f0, float v0, float f1, float v1)
    {
        pen.Color = c; pen.Width = w;
        gfx.DrawLine(pen, GP(f0, v0), GP(f1, v1));
    }

    // ---------- Bugs: acertos das armas ----------
    int HitBugsSeg(float x0, float y0, float x1, float y1, float r, int dmg = 1)
    {
        if (bugs.Count == 0) return 0;
        int hits = 0; bool killed = false, tough = false;
        foreach (BugForm b in bugs)
        {
            if (b.State == 0 && b.HitSeg(x0, y0, x1, y1, r))
            {
                hits++;
                int res = b.Damage(x1 - x0, y1 - y0, dmg, false);
                if (res == 2) { killed = true; OnBugKilled(b); }
                else if (res == 1) tough = true;
            }
        }
        if (killed) PlaySquash(); else if (tough) PlayClang();
        return hits;
    }

    int HitBugsCircle(float x, float y, float r, int dmg = 1) { return HitBugsSeg(x, y, x, y, r, dmg); }

    void PoisonBugs(float x, float y, float r, Color c)
    {
        if (bugs.Count == 0) return;
        bool killed = false;
        foreach (BugForm b in bugs)
        {
            if (b.State == 0 && b.PoisonHit(x, y, r, c)) { killed = true; OnBugKilled(b); }
        }
        if (killed) PlaySquash();
    }

    void BugBoom(BugForm src)
    {
        PlayBoom();
        foreach (BugForm o in bugs)
        {
            if (o == src || o.State != 0) continue;
            float dx = o.X - src.X, dy = o.Y - src.Y;
            if (dx * dx + dy * dy <= 95f * 95f)
            {
                int res = o.Damage(dx, dy, (o.Kind == 2 || o.Kind == 4) ? 2 : 99, true);
                if (res == 2) OnBugKilled(o);
            }
        }
    }

}
