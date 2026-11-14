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
    //  POSIÇÃO DA ARMA / MIRA NO CURSOR
    //  Normal: a arma fica no cursor e a mira segue o último movimento.
    //  "Mira no cursor": arco, AK-47 e catapulta ficam fixos embaixo da
    //  tela (seguindo o cursor na horizontal) e miram para o cursor.
    // ===================================================================
    float wx, wy;
    bool  wxInit = false;

    void UpdateWeaponPos()
    {
        if (!aimCursor) { wx = mx; wy = my; wxInit = false; return; }
        Rectangle b = Screen.FromPoint(new Point(mx, my)).Bounds;
        float tx = Math.Max(b.Left + 90, Math.Min(b.Right - 90, mx));
        if (!wxInit) { wx = tx; wxInit = true; }
        else wx += (tx - wx) * 0.12f;
        wy = b.Bottom - 70;
    }

}
