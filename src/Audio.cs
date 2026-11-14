using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Threading;

// Mixer de áudio por software em cima do waveOut: vários sons ao mesmo tempo.
// Se o waveOut falhar, cai no SoundPlayer (um som por vez).
public static class Mixer
{
    const int SR = 44100, BUF = 1024, NBUF = 4;
    const int WHDR_DONE = 1;

    [StructLayout(LayoutKind.Sequential)]
    struct WAVEFORMATEX
    {
        public ushort wFormatTag, nChannels; public uint nSamplesPerSec, nAvgBytesPerSec;
        public ushort nBlockAlign, wBitsPerSample, cbSize;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct WAVEHDR
    {
        public IntPtr lpData; public uint dwBufferLength, dwBytesRecorded; public IntPtr dwUser;
        public uint dwFlags, dwLoops; public IntPtr lpNext, reserved;
    }
    [DllImport("winmm.dll")] static extern int waveOutOpen(out IntPtr h, int dev, ref WAVEFORMATEX f, IntPtr cb, IntPtr inst, int flags);
    [DllImport("winmm.dll")] static extern int waveOutPrepareHeader(IntPtr h, IntPtr hdr, int size);
    [DllImport("winmm.dll")] static extern int waveOutUnprepareHeader(IntPtr h, IntPtr hdr, int size);
    [DllImport("winmm.dll")] static extern int waveOutWrite(IntPtr h, IntPtr hdr, int size);
    [DllImport("winmm.dll")] static extern int waveOutReset(IntPtr h);
    [DllImport("winmm.dll")] static extern int waveOutClose(IntPtr h);

    class Voice { public short[] d; public int pos; public bool loop; public bool dead; public object owner; }

    static readonly List<Voice> voices = new List<Voice>();
    static readonly object lk = new object();
    static IntPtr hWave = IntPtr.Zero;
    static IntPtr[] hdrs = new IntPtr[NBUF];
    static IntPtr[] bufs = new IntPtr[NBUF];
    static Thread th;
    static volatile bool run;
    public static bool Ok;
    public static double Master = 1.0;

    public static void Init()
    {
        if (Ok) return;
        try
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) return;
            WAVEFORMATEX f = new WAVEFORMATEX();
            f.wFormatTag = 1; f.nChannels = 1; f.nSamplesPerSec = SR; f.wBitsPerSample = 16;
            f.nBlockAlign = 2; f.nAvgBytesPerSec = SR * 2; f.cbSize = 0;
            if (waveOutOpen(out hWave, -1, ref f, IntPtr.Zero, IntPtr.Zero, 0) != 0) return;
            int hs = Marshal.SizeOf(typeof(WAVEHDR));
            for (int i = 0; i < NBUF; i++)
            {
                bufs[i] = Marshal.AllocHGlobal(BUF * 2);
                hdrs[i] = Marshal.AllocHGlobal(hs);
                WAVEHDR h = new WAVEHDR();
                h.lpData = bufs[i]; h.dwBufferLength = (uint)(BUF * 2);
                Marshal.StructureToPtr(h, hdrs[i], false);
                waveOutPrepareHeader(hWave, hdrs[i], hs);
                Fill(i);
                waveOutWrite(hWave, hdrs[i], hs);
            }
            run = true;
            th = new Thread(Loop); th.IsBackground = true; th.Priority = ThreadPriority.AboveNormal;
            th.Start();
            Ok = true;
        }
        catch { Ok = false; }
    }

    public static void Shutdown()
    {
        run = false;
        try { if (hWave != IntPtr.Zero) { waveOutReset(hWave); waveOutClose(hWave); hWave = IntPtr.Zero; } } catch { }
        Ok = false;
    }

    static void Loop()
    {
        int hs = Marshal.SizeOf(typeof(WAVEHDR));
        while (run)
        {
            try
            {
                for (int i = 0; i < NBUF; i++)
                {
                    int flags = Marshal.ReadInt32(hdrs[i], IntPtr.Size + 8 + IntPtr.Size);
                    if ((flags & WHDR_DONE) != 0)
                    {
                        Fill(i);
                        waveOutWrite(hWave, hdrs[i], hs);
                    }
                }
            }
            catch { }
            Thread.Sleep(4);
        }
    }

    static void Fill(int i)
    {
        short[] mix = new short[BUF];
        lock (lk)
        {
            for (int n = 0; n < BUF; n++)
            {
                int acc = 0;
                for (int v = 0; v < voices.Count; v++)
                {
                    Voice vo = voices[v];
                    if (vo.dead) continue;
                    if (vo.pos >= vo.d.Length)
                    {
                        if (vo.loop) vo.pos = 0; else { vo.dead = true; continue; }
                    }
                    acc += vo.d[vo.pos++];
                }
                if (acc > 32767) acc = 32767; else if (acc < -32768) acc = -32768;
                mix[n] = (short)acc;
            }
            voices.RemoveAll(delegate(Voice x) { return x.dead; });
        }
        Marshal.Copy(mix, 0, bufs[i], BUF);
    }

    public static void Play(short[] d, bool loop, object owner)
    {
        lock (lk)
        {
            if (voices.Count > 24) return;
            Voice v = new Voice(); v.d = d; v.loop = loop; v.owner = owner;
            voices.Add(v);
        }
    }

    public static void Stop(object owner)
    {
        lock (lk) { foreach (Voice v in voices) if (v.owner == owner) v.dead = true; }
    }

    // WAV (PCM 8/16 bits, mono/estéreo, qualquer taxa) -> 16 bits mono 44,1 kHz
    public static short[] WavToPcm(byte[] w)
    {
        int ch = 1, sr = SR, bits = 16, dataOff = -1, dataLen = 0;
        int p = 12;
        while (p + 8 <= w.Length)
        {
            string id = System.Text.Encoding.ASCII.GetString(w, p, 4);
            int len = BitConverter.ToInt32(w, p + 4);
            if (id == "fmt ")
            {
                ch = BitConverter.ToInt16(w, p + 10);
                sr = BitConverter.ToInt32(w, p + 12);
                bits = BitConverter.ToInt16(w, p + 22);
            }
            else if (id == "data") { dataOff = p + 8; dataLen = Math.Min(len, w.Length - dataOff); break; }
            p += 8 + len + (len & 1);
        }
        if (dataOff < 0 || ch < 1 || (bits != 16 && bits != 8)) return new short[0];
        int bps = bits / 8;
        int frames = dataLen / (bps * ch);
        double[] mono = new double[frames];
        for (int i = 0; i < frames; i++)
        {
            double s = 0;
            for (int c = 0; c < ch; c++)
            {
                int o = dataOff + (i * ch + c) * bps;
                s += (bits == 16) ? BitConverter.ToInt16(w, o) / 32768.0 : (w[o] - 128) / 128.0;
            }
            mono[i] = s / ch;
        }
        int outN = (int)((long)frames * SR / sr);
        short[] r = new short[outN];
        for (int i = 0; i < outN; i++)
        {
            double src = (double)i * sr / SR;
            int i0 = (int)src; double fr = src - i0;
            double a = mono[Math.Min(i0, frames - 1)], b = mono[Math.Min(i0 + 1, frames - 1)];
            double v = (a + (b - a) * fr) * 32767;
            r[i] = (short)Math.Max(-32768, Math.Min(32767, v));
        }
        return r;
    }
}

// Mesma "cara" do SoundPlayer (Play / PlayLooping / Stop / Load), mas mixando.
public class Snd
{
    short[] pcm; SoundPlayer fallback; byte[] raw;
    public Snd(byte[] wav)
    {
        raw = wav;
        try { pcm = Mixer.WavToPcm(wav); } catch { pcm = null; }
    }

    // procura Sons\<nome>.wav ao lado do exe; se existir, substitui o som gerado
    public static Snd Make(string name, byte[] generated)
    {
        try
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sons");
            string f = Path.Combine(dir, name + ".wav");
            if (File.Exists(f))
            {
                byte[] b = File.ReadAllBytes(f);
                Snd s = new Snd(b);
                if (s.pcm != null && s.pcm.Length > 0)
                {
                    for (int i = 0; i < s.pcm.Length; i++) s.pcm[i] = (short)(s.pcm[i] * Mixer.Master);
                    return s;
                }
            }
        }
        catch { }
        return new Snd(generated);
    }

    public void Load() { }

    void EnsureFallback()
    {
        if (fallback == null) { fallback = new SoundPlayer(new MemoryStream(raw)); try { fallback.Load(); } catch { } }
    }

    public void Play()
    {
        if (Mixer.Ok && pcm != null && pcm.Length > 0) Mixer.Play(pcm, false, this);
        else { EnsureFallback(); fallback.Play(); }
    }

    public void PlayLooping()
    {
        if (Mixer.Ok && pcm != null && pcm.Length > 0) { Mixer.Stop(this); Mixer.Play(pcm, true, this); }
        else { EnsureFallback(); fallback.PlayLooping(); }
    }

    public void Stop()
    {
        if (Mixer.Ok) Mixer.Stop(this);
        else if (fallback != null) fallback.Stop();
    }
}
