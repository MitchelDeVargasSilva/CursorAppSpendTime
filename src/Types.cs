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

public class Pt { public float x, y, px, py; }
public class Spark
{
    public float x, y, vx, vy;
    public int life, max;
    public bool isFire, hot, custom, blue;
    public float drag = 0.92f, g = 0.3f;
}
public class Ring { public float x, y; public int age; }
public class FireParticle { public float x, y, vx, vy; public int life, max; public bool blue; }
public class Shard { public float x, y, vx, vy, rot, rotV, len, w; public int life, max; }
public class Trail { public float tx, ty, bx, by; public int age; public bool start; }
public class Arrow { public float x, y, vx, vy, ux, uy; public int age, stuck; public bool isStuck; }

// ---- Armas ----
public class Bullet { public float x, y, px, py, vx, vy; public int age; }
public class Ball { public float sx, sy, tx, ty, z0, peak; public int age, dur; }
public class Burn { public float x, y; public int life; }
public class Decal { public float x, y, scale; public int kind, life, max; public float[] pts; }
public class Dot { public float x, y, vx, vy, r; public int life, max, col, kind; }

public enum WeaponMode { Whip = 0, Mace = 1, Sword = 2, Chain = 3, Bow = 4, Ak = 5, Catapult = 6, Spray = 7, Flame = 8, Grenade = 9 }
