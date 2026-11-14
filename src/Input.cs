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
    //  BLOQUEIO DE CLIQUES: com a arma ligada, o clique do mouse só dispara
    //  a habilidade e NÃO chega na tela/programa por baixo (vale em qualquer tela).
    //  Segurar SHIFT deixa o clique passar normalmente (saída de emergência).
    // ===================================================================
    delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData; public uint flags; public uint time; public IntPtr dwExtraInfo; }

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)] static extern IntPtr GetModuleHandle(string name);
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(POINT p);
    [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);

    LowLevelMouseProc mouseProc;
    IntPtr hookId = IntPtr.Zero;
    volatile bool clickPending = false;
    bool passLeft = false, passRight = false;
    volatile bool heldLeft = false, heldRight = false;
    uint ownPid = 0;

    void InstallMouseHook()
    {
        if (hookId != IntPtr.Zero) return;
        clickPending = false; passLeft = false; passRight = false; heldLeft = false; heldRight = false;
        try
        {
            ownPid = (uint)Process.GetCurrentProcess().Id;
            mouseProc = MouseHookProc;
            using (Process cur = Process.GetCurrentProcess())
            using (ProcessModule mod = cur.MainModule)
                hookId = SetWindowsHookEx(14, mouseProc, GetModuleHandle(mod.ModuleName), 0);
        }
        catch { hookId = IntPtr.Zero; }
    }

    void RemoveMouseHook()
    {
        if (hookId != IntPtr.Zero)
        {
            try { UnhookWindowsHookEx(hookId); } catch { }
            hookId = IntPtr.Zero;
        }
        clickPending = false; heldLeft = false; heldRight = false;
    }

    // true se o clique caiu numa janela deste programa (ex.: menu da bandeja)
    bool IsOwnWindowAt(IntPtr lParam)
    {
        try
        {
            MSLLHOOKSTRUCT ms = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
            IntPtr hw = WindowFromPoint(ms.pt);
            if (hw == IntPtr.Zero || hw == Handle) return false;
            if (Control.FromHandle(hw) is BugForm) return false;     // bug nunca bloqueia a arma
            if (GetAncestor(hw, 2) == Handle) return false;      // a camada da arma nunca conta
            uint pid;
            GetWindowThreadProcessId(hw, out pid);
            return pid == ownPid;
        }
        catch { return false; }
    }

    IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && enabled)
        {
            int msg = wParam.ToInt32();
            if (msg == 0x0201 || msg == 0x0204)           // botão esquerdo / direito apertado
            {
                bool shift = (GetAsyncKeyState(0x10) & 0x8000) != 0;
                bool pass = shift || IsOwnWindowAt(lParam);
                if (msg == 0x0201) passLeft = pass; else passRight = pass;
                if (!pass)
                {
                    if (msg == 0x0201) heldLeft = true; else heldRight = true;
                    clickPending = true;                   // dispara a habilidade
                    return (IntPtr)1;                      // e engole o clique
                }
            }
            else if (msg == 0x020A && cfgWheel)            // roda do mouse: troca de arma
            {
                bool shift = (GetAsyncKeyState(0x10) & 0x8000) != 0;
                if (!shift && !IsOwnWindowAt(lParam))
                {
                    MSLLHOOKSTRUCT ws = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                    short dl = (short)((ws.mouseData >> 16) & 0xFFFF);
                    wheelPend = (dl > 0) ? 1 : -1;
                    return (IntPtr)1;
                }
            }
            else if (msg == 0x0202 || msg == 0x0205)      // soltou
            {
                bool pass = (msg == 0x0202) ? passLeft : passRight;
                if (msg == 0x0202) { passLeft = false; heldLeft = false; } else { passRight = false; heldRight = false; }
                if (!pass) return (IntPtr)1;
            }
        }
        return CallNextHookEx(hookId, nCode, wParam, lParam);
    }

    void StepParticles()
    {
        for (int s = sparks.Count - 1; s >= 0; s--)
        {
            Spark sp = sparks[s];
            sp.x += sp.vx; sp.y += sp.vy;
            if (sp.custom)
            {
                sp.vx *= sp.drag;
                sp.vy  = sp.vy * sp.drag + sp.g;
            }
            else
            {
                sp.vx *= sp.isFire ? 0.88f : 0.92f;
                sp.vy  = sp.vy * (sp.isFire ? 0.88f : 0.92f) + (sp.isFire ? 0.15f : 0.3f);
            }
            sp.life--;
            if (sp.life <= 0) sparks.RemoveAt(s);
        }
        for (int r = rings.Count - 1; r >= 0; r--)
        {
            rings[r].age++;
            if (rings[r].age > 14) rings.RemoveAt(r);
        }
        for (int f = fires.Count - 1; f >= 0; f--)
        {
            FireParticle fp = fires[f];
            fp.x += fp.vx; fp.y += fp.vy;
            fp.vx *= 0.90f;
            fp.vy  = fp.vy * 0.90f - 0.1f;
            fp.life--;
            if (fp.life <= 0) fires.RemoveAt(f);
        }
        for (int dc = decals.Count - 1; dc >= 0; dc--)
        {
            decals[dc].life--;
            if (decals[dc].life <= 0) decals.RemoveAt(dc);
        }
        for (int s = shards.Count - 1; s >= 0; s--)
        {
            Shard sh = shards[s];
            sh.x   += sh.vx;
            sh.y   += sh.vy;
            sh.vx  *= 0.93f;
            sh.vy   = sh.vy * 0.93f + 0.45f;
            sh.rot += sh.rotV;
            sh.life--;
            if (sh.life <= 0) shards.RemoveAt(s);
        }
    }

}
