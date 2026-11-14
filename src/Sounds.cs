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
    // ---------- Sons novos ----------
    // pente saindo, caindo, entrando e ferrolho
    byte[] MakeReloadWav()
    {
        int sr = 44100, n = (int)(sr * 1.5);
        double[] o = new double[n];
        double[] ev = new double[] { 0.05, 0.30, 0.92, 1.22, 1.32 };
        double[] fr = new double[] { 1900, 150, 1100, 2400, 1500 };
        double[] am = new double[] { 0.7, 0.9, 1.0, 0.8, 0.9 };
        double peak = 0.0001;
        for (int e = 0; e < ev.Length; e++)
        {
            int s0 = (int)(ev[e] * sr);
            double ph = 0;
            for (int i = s0; i < n && i < s0 + sr / 4; i++)
            {
                double t = (double)(i - s0) / sr;
                double x = rnd.NextDouble() * 2 - 1;
                ph += 2 * Math.PI * fr[e] / sr;
                double env = Math.Exp(-t / (fr[e] < 300 ? 0.05 : 0.012));
                o[i] += (x * 0.6 + Math.Sin(ph) * 0.7 + Math.Sin(ph * 1.52) * 0.3) * env * am[e];
            }
        }
        for (int i = 0; i < n; i++) if (Math.Abs(o[i]) > peak) peak = Math.Abs(o[i]);
        for (int i = 0; i < n; i++) o[i] = Math.Tanh(o[i] / peak * 1.4) * 0.9;
        return ToWav(o, sr);
    }

    // bipe curto de início de fase
    byte[] MakeBeepWav()
    {
        int sr = 44100, n = (int)(sr * 0.28);
        double[] o = new double[n];
        double ph = 0;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / sr;
            double f = (t < 0.12) ? 660 : 990;
            ph += 2 * Math.PI * f / sr;
            double env = Math.Min(1.0, t / 0.005) * Math.Exp(-(t < 0.12 ? t : t - 0.12) / 0.07);
            o[i] = Math.Sin(ph) * env * 0.5;
        }
        return ToWav(o, sr);
    }

    void PlayReload()     { if (!soundOn) return; try { reloadPlayer.Play(); } catch { } }
    void PlayBannerBeep() { if (!soundOn || hissOn) return; try { beepPlayer.Play(); } catch { } }

    // ---------- Sons das armas novas ----------
    byte[] MakeAkWav()
    {
        int sr = 44100, n = (int)(sr * 0.24);
        double[] o = new double[n];
        double lp = 0, lp2 = 0, ph = 0, peak = 0.0001;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / sr;
            double x = rnd.NextDouble() * 2 - 1;
            double crack = x * Math.Exp(-t / 0.0035) * 1.1;
            lp += 0.18 * (x - lp);
            double body = lp * Math.Exp(-t / 0.03) * 1.8;
            ph += 2 * Math.PI * (60 + 110 * Math.Exp(-t / 0.02)) / sr;
            double thump = Math.Sin(ph) * Math.Exp(-t / 0.05) * 0.9;
            lp2 += 0.05 * (x - lp2);
            double tail = lp2 * Math.Exp(-t / 0.08) * 1.4;
            o[i] = crack + body + thump + tail;
            if (Math.Abs(o[i]) > peak) peak = Math.Abs(o[i]);
        }
        for (int i = 0; i < n; i++) o[i] = Math.Tanh(o[i] / peak * 1.8) * 0.95;
        return ToWav(o, sr);
    }

    byte[] MakeCatapultWav()
    {
        int sr = 44100, n = (int)(sr * 0.7);
        double[] o = new double[n];
        double ph = 0, ph2 = 0, lp = 0, lp2 = 0, peak = 0.0001;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / sr;
            double x = rnd.NextDouble() * 2 - 1;
            // rangido da madeira
            double creak = 0;
            if (t < 0.14)
            {
                ph += 2 * Math.PI * (210 - 700 * t * 0.8 + 40 * Math.Sin(t * 90)) / sr;
                double saw = ((ph / (2 * Math.PI)) % 1.0) * 2 - 1;
                creak = saw * 0.35 * Math.Min(1.0, t / 0.02) * (1 - t / 0.14);
            }
            // pancada do braço no batente
            double tt = t - 0.15;
            double hit = 0;
            if (tt >= 0)
            {
                ph2 += 2 * Math.PI * (55 + 120 * Math.Exp(-tt / 0.02)) / sr;
                hit = x * Math.Exp(-tt / 0.008) * 0.9 + Math.Sin(ph2) * Math.Exp(-tt / 0.07) * 1.1;
                lp += 0.06 * (x - lp);
                lp2 += 0.3 * (x - lp2);
                hit += (lp2 - lp) * Math.Exp(-tt / 0.12) * 1.2;          // whoosh da bola
            }
            o[i] = creak + hit;
            if (Math.Abs(o[i]) > peak) peak = Math.Abs(o[i]);
        }
        for (int i = 0; i < n; i++) o[i] = Math.Tanh(o[i] / peak * 1.5) * 0.95;
        return ToWav(o, sr);
    }

    byte[] MakeBoomWav()
    {
        int sr = 44100, n = (int)(sr * 0.9);
        double[] o = new double[n];
        double ph = 0, lp = 0, hp = 0, peak = 0.0001, cr = 0;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / sr;
            double x = rnd.NextDouble() * 2 - 1;
            ph += 2 * Math.PI * (34 + 70 * Math.Exp(-t / 0.08)) / sr;
            double boom = Math.Sin(ph) * Math.Exp(-t / 0.22) * 1.4;
            lp += 0.08 * (x - lp);
            double rumble = lp * Math.Exp(-t / 0.2) * 2.2;
            hp += 0.4 * (x - hp);
            double fizz = (x - hp) * Math.Exp(-t / 0.25) * 0.5;               // chiado do fogo azul
            if (rnd.NextDouble() < 0.002) cr = 1.0;
            cr *= 0.97;
            double crackle = (rnd.NextDouble() * 2 - 1) * cr * 0.3 * Math.Exp(-t / 0.5);
            double hit = x * Math.Exp(-t / 0.004);
            o[i] = boom + rumble + fizz + crackle + hit;
            if (Math.Abs(o[i]) > peak) peak = Math.Abs(o[i]);
        }
        for (int i = 0; i < n; i++) o[i] = Math.Tanh(o[i] / peak * 1.6) * 0.95;
        return ToWav(o, sr);
    }

    // chiado contínuo do spray (toca em loop enquanto o botão está apertado)
    byte[] MakeHissWav()
    {
        int sr = 22050, n = (int)(sr * 0.6);
        double[] o = new double[n];
        double lp = 0, mod = 0, modT = 0;
        for (int i = 0; i < n; i++)
        {
            double x = rnd.NextDouble() * 2 - 1;
            lp += 0.25 * (x - lp);
            modT += (rnd.NextDouble() - 0.5) * 0.02;
            mod += 0.02 * (modT - mod);
            double v = (x - lp) * (0.55 + 0.25 * Math.Sin(i * 0.004) + mod);
            double fade = Math.Min(1.0, Math.Min(i, n - 1 - i) / (sr * 0.03));
            o[i] = v * fade * 0.8;
        }
        return ToWav(o, sr);
    }

    byte[] MakeSquashWav()
    {
        int sr = 44100, n = (int)(sr * 0.22);
        double[] o = new double[n];
        double ph = 0, lp = 0, peak = 0.0001;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / sr;
            double x = rnd.NextDouble() * 2 - 1;
            lp += 0.12 * (x - lp);
            ph += 2 * Math.PI * (320 * Math.Exp(-t / 0.05) + 90) / sr;
            double pop = Math.Sin(ph) * Math.Exp(-t / 0.045);
            double splat = lp * Math.Exp(-t / 0.05) * 2.2;
            double wet = (rnd.NextDouble() < 0.02 ? (rnd.NextDouble() * 2 - 1) : 0) * Math.Exp(-t / 0.1) * 0.9;
            o[i] = pop + splat + wet;
            if (Math.Abs(o[i]) > peak) peak = Math.Abs(o[i]);
        }
        for (int i = 0; i < n; i++) o[i] = Math.Tanh(o[i] / peak * 1.5) * 0.9;
        return ToWav(o, sr);
    }

    // bolinha de metal batendo dentro da lata de spray
    byte[] MakeRattleWav()
    {
        int sr = 44100, n = (int)(sr * 0.3);
        double[] o = new double[n];
        double peak = 0.0001;
        int next = 0;
        double ph1 = 0, ph2 = 0, env = 0, f1 = 3600, f2 = 5200;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / sr;
            if (i >= next)
            {
                env = 0.5 + rnd.NextDouble() * 0.5;
                f1 = 2800 + rnd.NextDouble() * 1800; f2 = f1 * 1.46;
                next = i + (int)(sr * (0.012 + rnd.NextDouble() * 0.028));
            }
            env *= 0.9985 - 0.0 * t;
            env *= 0.9955;
            ph1 += 2 * Math.PI * f1 / sr; ph2 += 2 * Math.PI * f2 / sr;
            double x = (rnd.NextDouble() * 2 - 1) * 0.35;
            o[i] = (Math.Sin(ph1) * 0.7 + Math.Sin(ph2) * 0.4 + x) * env * (1.0 - t / 0.34);
            if (Math.Abs(o[i]) > peak) peak = Math.Abs(o[i]);
        }
        for (int i = 0; i < n; i++) o[i] = Math.Tanh(o[i] / peak * 1.4) * 0.8;
        return ToWav(o, sr);
    }

    void PlayRattle()    { if (!soundOn || hissOn) return; try { rattlePlayer.Play(); } catch { } }
    void PlayAk()        { if (!soundOn) return; try { akPlayer.Play(); }     catch { } }
    void PlayCatapult()  { if (!soundOn) return; try { catPlayer.Play(); }    catch { } }
    void PlayBoom()      { if (!soundOn) return; try { boomPlayer.Play(); }   catch { } }
    void PlaySquash()    { if (!soundOn) return; if (hissOn) return; try { squashPlayer.Play(); } catch { } }


    // ---------- Helpers ----------
    static int Cl(float v) { int i = (int)v; return i < 0 ? 0 : (i > 255 ? 255 : i); }

    static Color Lerp(Color a, Color b, float t)
    {
        return Color.FromArgb(Cl(a.R+(b.R-a.R)*t), Cl(a.G+(b.G-a.G)*t), Cl(a.B+(b.B-a.B)*t));
    }

    static float WidthAt(int i)
    {
        float t = (float)i / (N - 1);
        return 1.2f + 4.8f * (float)Math.Pow(1 - t, 1.1);
    }

    // ---------- Sons ----------
    static double Vol = 1.0;

    static byte[] ToWav(double[] s, int sr)
    {
        MemoryStream ms = new MemoryStream();
        BinaryWriter bw = new BinaryWriter(ms);
        int n = s.Length;
        bw.Write(new char[]{'R','I','F','F'});
        bw.Write(36 + n*2);
        bw.Write(new char[]{'W','A','V','E','f','m','t',' '});
        bw.Write(16); bw.Write((short)1); bw.Write((short)1);
        bw.Write(sr); bw.Write(sr*2); bw.Write((short)2); bw.Write((short)16);
        bw.Write(new char[]{'d','a','t','a'});
        bw.Write(n*2);
        for (int i = 0; i < n; i++)
        {
            double v = s[i] * Vol;
            if (v >  1) v =  1;
            if (v < -1) v = -1;
            bw.Write((short)(v * 32767));
        }
        bw.Flush();
        return ms.ToArray();
    }

    byte[] MakeCrackWav()
    {
        int sr = 44100, n = (int)(sr * 0.6);
        double[] nz = new double[n];
        double[] o  = new double[n];
        for (int i = 0; i < n; i++) nz[i] = rnd.NextDouble()*2 - 1;
        double ph = 0, p1 = 0;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / sr;
            double d1 = nz[i] - (i > 0 ? nz[i-1] : 0);
            double d2 = d1 - p1; p1 = d1;
            double snap  = d2 * Math.Exp(-t/0.0045) * 0.9;
            double body  = d1 * Math.Exp(-t/0.025) * 0.35;
            double click = (t < 0.002) ? nz[i]*0.8 : 0;
            double f = 55 + 120*Math.Exp(-t/0.02);
            ph += 2*Math.PI*f/sr;
            double thump = Math.Sin(ph)*Math.Exp(-t/0.05)*0.45;
            o[i] = snap + body + click + thump;
        }
        double[] e = new double[n];
        int dA = (int)(sr*0.055), dB = (int)(sr*0.11);
        double peak = 0.0001;
        for (int i = 0; i < n; i++)
        {
            double v = o[i];
            if (i >= dA) v += o[i-dA]*0.22;
            if (i >= dB) v += o[i-dB]*0.10;
            e[i] = v;
            if (Math.Abs(v) > peak) peak = Math.Abs(v);
        }
        for (int i = 0; i < n; i++) e[i] = Math.Tanh(e[i]/peak*1.4)*0.95;
        return ToWav(e, sr);
    }

    byte[] MakeWhooshWav()
    {
        int sr = 44100, n = (int)(sr*0.28);
        double[] o = new double[n];
        double a = 0, b = 0, peak = 0.0001;
        for (int i = 0; i < n; i++)
        {
            double u = (double)i/n;
            double x = rnd.NextDouble()*2 - 1;
            a += (0.04 + 0.45*u)*(x-a);
            b += 0.03*(x-b);
            double env = Math.Sin(Math.PI*Math.Pow(u, 0.7));
            o[i] = (a-b)*env;
            if (Math.Abs(o[i]) > peak) peak = Math.Abs(o[i]);
        }
        for (int i = 0; i < n; i++) o[i] = o[i]/peak*0.55;
        return ToWav(o, sr);
    }

    // Pancada pesada de porrete: clack seco + madeira + "bum" grave + lascas
    byte[] MakeThudWav()
    {
        int sr = 44100, n = (int)(sr * 0.55);
        double[] o = new double[n];
        double peak = 0.0001;

        double ph1 = 0, ph2 = 0, ph3 = 0, ph4 = 0, phB = 0;
        double f1 = 820, f2 = 1640, f3 = 2600, f4 = 380;

        for (int i = 0; i < n; i++)
        {
            double t = (double)i / sr;
            double clack = (rnd.NextDouble() * 2 - 1) * Math.Exp(-t / 0.0025) * 1.1;

            ph1 += 2 * Math.PI * f1 / sr;
            ph2 += 2 * Math.PI * f2 / sr;
            ph3 += 2 * Math.PI * f3 / sr;
            ph4 += 2 * Math.PI * f4 / sr;
            double wood1 = Math.Sin(ph1) * Math.Exp(-t / 0.055) * 0.60;
            double wood2 = Math.Sin(ph2) * Math.Exp(-t / 0.030) * 0.30;
            double wood3 = Math.Sin(ph3) * Math.Exp(-t / 0.015) * 0.15;
            double wood4 = Math.Sin(ph4) * Math.Exp(-t / 0.080) * 0.25;

            double fb = 46 + 70 * Math.Exp(-t / 0.05);
            phB += 2 * Math.PI * fb / sr;
            double boom = Math.Sin(phB) * Math.Exp(-t / 0.11) * 1.1;

            double crunch = (rnd.NextDouble() * 2 - 1) * Math.Exp(-t / 0.03) * 0.35;

            o[i] = clack + wood1 + wood2 + wood3 + wood4 + boom + crunch;
            if (Math.Abs(o[i]) > peak) peak = Math.Abs(o[i]);
        }
        for (int i = 0; i < n; i++)
            o[i] = Math.Tanh(o[i] / peak * 1.4) * 0.95;
        return ToWav(o, sr);
    }

    // metal batendo em metal: parciais inarmônicos de aço, decaimento longo,
    // e um "tchk" brilhante no instante da batida
    void AddMetalStrike(double[] o, int sr, double t0, double f0, double amp, double decay)
    {
        int start = (int)(t0 * sr);
        int len = (int)(decay * 5 * sr);
        double[] mul = new double[] { 1.00, 1.59, 2.14, 2.65, 3.51, 4.23, 5.87 };
        double[] am  = new double[] { 1.00, 0.80, 0.65, 0.50, 0.35, 0.22, 0.12 };
        double lp = 0;
        for (int i = 0; i < len; i++)
        {
            int idx = start + i;
            if (idx >= o.Length) break;
            double t = (double)i / sr;
            double v = 0;
            for (int k = 0; k < 7; k++)
                v += Math.Sin(2 * Math.PI * f0 * mul[k] * t) * am[k] * Math.Exp(-t / (decay / (1 + k * 0.35)));
            double nz = rnd.NextDouble() * 2 - 1;
            lp += 0.6 * (nz - lp);
            v += (nz - lp) * Math.Exp(-t / 0.003) * 1.6;
            o[idx] += v * amp;
        }
    }

    // batida da corrente: metal contra metal (golpe duplo + toque grave + eco)
    byte[] MakeClankWav()
    {
        int sr = 44100, n = (int)(sr * 0.9);
        double[] o = new double[n];
        AddMetalStrike(o, sr, 0.000, 720,  1.0, 0.16);
        AddMetalStrike(o, sr, 0.014, 1080, 0.8, 0.12);
        AddMetalStrike(o, sr, 0.030, 460,  0.7, 0.20);

        double ph = 0;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / sr;
            double fq = 70 + 60 * Math.Exp(-t / 0.03);
            ph += 2 * Math.PI * fq / sr;
            o[i] += Math.Sin(ph) * Math.Exp(-t / 0.06) * 0.6;
        }

        double[] e = new double[n];
        int dA = (int)(sr * 0.06), dB = (int)(sr * 0.13);
        for (int i = 0; i < n; i++)
        {
            double v = o[i];
            if (i >= dA) v += o[i - dA] * 0.25;
            if (i >= dB) v += o[i - dB] * 0.12;
            e[i] = v;
        }
        double peak = 0.0001;
        for (int i = 0; i < n; i++) if (Math.Abs(e[i]) > peak) peak = Math.Abs(e[i]);
        for (int i = 0; i < n; i++) e[i] = Math.Tanh(e[i] / peak * 1.7) * 0.95;
        return ToWav(e, sr);
    }


    // disparo do arco: estalo da corda + sopro da flecha em chamas
    byte[] MakeShootWav()
    {
        int sr = 44100, n = (int)(sr * 0.55);
        double[] o = new double[n];
        double a = 0, b = 0, cr = 0, peak = 0.0001;
        double f0 = 170;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / sr;
            double str = 0;
            for (int h = 1; h <= 6; h++)
                str += Math.Sin(2 * Math.PI * f0 * h * t) / h * Math.Exp(-t / (0.22 / (1 + 0.35 * h)));
            double slap = (rnd.NextDouble() * 2 - 1) * Math.Exp(-t / 0.004) * 0.9;
            double x = rnd.NextDouble() * 2 - 1;
            a += (0.05 + 0.4 * Math.Min(1.0, t / 0.3)) * (x - a);
            b += 0.03 * (x - b);
            double env = Math.Exp(-t / 0.18) * (1 - Math.Exp(-t / 0.01));
            double whoosh = (a - b) * env * 1.6;
            if (rnd.NextDouble() < 0.0007) cr = 1.0;
            cr *= 0.985;
            double crackle = (rnd.NextDouble() * 2 - 1) * cr * 0.25 * (t > 0.06 ? 1.0 : 0.0);
            o[i] = str * 0.9 + slap + whoosh + crackle;
            if (Math.Abs(o[i]) > peak) peak = Math.Abs(o[i]);
        }
        for (int i = 0; i < n; i++) o[i] = Math.Tanh(o[i] / peak * 1.5) * 0.95;
        return ToWav(o, sr);
    }

    // flecha cravando na borda da tela: "toc" de madeira + estalos de fogo
    byte[] MakeStickWav()
    {
        int sr = 44100, n = (int)(sr * 0.35);
        double[] o = new double[n];
        double ph = 0, cr = 0, peak = 0.0001;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / sr;
            double fq = 140 + 180 * Math.Exp(-t / 0.02);
            ph += 2 * Math.PI * fq / sr;
            double thock = Math.Sin(ph) * Math.Exp(-t / 0.05);
            double hit = (rnd.NextDouble() * 2 - 1) * Math.Exp(-t / 0.004) * 0.8;
            if (rnd.NextDouble() < 0.0012) cr = 1.0;
            cr *= 0.98;
            double crackle = (rnd.NextDouble() * 2 - 1) * cr * 0.3;
            o[i] = thock + hit + crackle;
            if (Math.Abs(o[i]) > peak) peak = Math.Abs(o[i]);
        }
        for (int i = 0; i < n; i++) o[i] = Math.Tanh(o[i] / peak * 1.4) * 0.9;
        return ToWav(o, sr);
    }

    // Fsssh de lâmina cortando o ar — sibilante, rápido e afiado
    byte[] MakeSlashWav()
    {
        int sr = 44100, n = (int)(sr * 0.22);
        double[] o = new double[n];
        double a = 0, b = 0, peak = 0.0001;
        for (int i = 0; i < n; i++)
        {
            double u  = (double)i / n;
            double x  = rnd.NextDouble() * 2 - 1;
            // filtro passa-alto agressivo: sibilante metálico
            a += (0.08 + 0.55 * (1 - u)) * (x - a);
            b += 0.015 * (x - b);
            // envelope: ataque brevíssimo, queda rápida mas com pequeno tail
            double env = Math.Pow(1 - u, 1.6) * Math.Sin(Math.PI * Math.Pow(u, 0.18));
            // tonalidade de lâmina: sutil sine em ~3400 Hz que decai rápido
            double sing = Math.Sin(2 * Math.PI * 3400 * i / sr) * Math.Exp(-u / 0.04) * 0.18;
            o[i] = (a - b) * env + sing;
            if (Math.Abs(o[i]) > peak) peak = Math.Abs(o[i]);
        }
        for (int i = 0; i < n; i++) o[i] = Math.Tanh(o[i] / peak * 1.4) * 0.75;
        return ToWav(o, sr);
    }

    // Clinck metálico de aço — impacto de lâmina, ressonância cristalina
    byte[] MakeClangWav()
    {
        int sr = 44100, n = (int)(sr * 0.55);
        double[] o = new double[n];
        // múltiplos parciais inarmônicos de aço
        double[] freqs = { 1340, 2210, 3480, 4820, 680 };
        double[] amps  = { 0.55, 0.35, 0.22, 0.14, 0.40 };
        double[] decays= { 0.18, 0.10, 0.07, 0.05, 0.25 };
        double[] phases= new double[freqs.Length];
        double peak = 0.0001;

        for (int i = 0; i < n; i++)
        {
            double t = (double)i / sr;
            double v = 0;
            for (int p = 0; p < freqs.Length; p++)
            {
                phases[p] += 2 * Math.PI * freqs[p] / sr;
                v += Math.Sin(phases[p]) * amps[p] * Math.Exp(-t / decays[p]);
            }
            // ataque percussivo (transiente de metal)
            double hit = (rnd.NextDouble() * 2 - 1) * Math.Exp(-t / 0.0018) * 0.9;
            o[i] = v + hit;
            if (Math.Abs(o[i]) > peak) peak = Math.Abs(o[i]);
        }
        for (int i = 0; i < n; i++) o[i] = Math.Tanh(o[i] / peak * 1.5) * 0.88;
        return ToWav(o, sr);
    }

    void PlayCrack()       { if (!soundOn) return; try { crackPlayer.Play(); }      catch { } }
    void PlayWhoosh()      { if (!soundOn) return; try { whooshPlayer.Play(); }     catch { } }
    void PlayThud()        { if (!soundOn) return; try { thudPlayer.Play(); }       catch { } }
    void PlaySlash()       { if (!soundOn) return; try { slashPlayer.Play(); }      catch { } }
    void PlayClang()       { if (!soundOn) return; try { clangPlayer.Play(); }      catch { } }
    void PlayClank()       { if (!soundOn) return; try { clankPlayer.Play(); }      catch { } }
    void PlayShoot()       { if (!soundOn) return; try { shootPlayer.Play(); }      catch { } }
    void PlayStick()       { if (!soundOn) return; try { stickPlayer.Play(); }      catch { } }
}
