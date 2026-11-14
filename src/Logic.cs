using System;
using System.Collections.Generic;

// Regras puras do jogo (sem Windows Forms): podem ser testadas em qualquer sistema.
public static class GameLogic
{
    public const int KindNormal = 0, KindRunner = 1, KindTank = 2, KindBomber = 3, KindBoss = 4;

    public static bool IsBossPhase(int phase) { return phase > 0 && phase % 5 == 0; }

    public static int ClampInt(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }

    // lista de tipos de bug de uma fase (o chefe vem no fim da lista)
    public static int[] Compose(int phase, int baseBugs)
    {
        if (phase < 1) phase = 1;
        baseBugs = ClampInt(baseBugs, 1, 10);
        int n = Math.Min(10, baseBugs + (phase - 1));
        int runners = (phase >= 2) ? Math.Min(3, phase / 2) : 0;
        int tanks   = (phase >= 3) ? Math.Min(2, (phase - 1) / 2) : 0;
        int bombers = (phase >= 4) ? Math.Min(2, (phase - 2) / 2) : 0;
        bool boss = IsBossPhase(phase);
        if (boss) n = Math.Max(2, n / 2);
        List<int> k = new List<int>();
        for (int i = 0; i < runners && k.Count < n; i++) k.Add(KindRunner);
        for (int i = 0; i < tanks && k.Count < n; i++) k.Add(KindTank);
        for (int i = 0; i < bombers && k.Count < n; i++) k.Add(KindBomber);
        while (k.Count < n) k.Add(KindNormal);
        if (boss) k.Add(KindBoss);
        return k.ToArray();
    }

    public static float SpeedFor(int phase, int diff)
    {
        float d = (diff == 1) ? 0.8f : (diff == 3 ? 1.3f : 1f);
        return Math.Min(2.2f, 1f + 0.1f * (phase - 1)) * d;
    }

    public static int BaseScore(int kind)
    {
        switch (kind)
        {
            case KindRunner: return 150;
            case KindTank: return 300;
            case KindBomber: return 200;
            case KindBoss: return 2000;
            default: return 100;
        }
    }

    public static int BossHp(int phase) { return 12 + 4 * (phase / 5); }

    // combo: acerto dentro da janela sobe (máx. 5), senão recomeça em 1
    public static int NextCombo(int combo, bool windowOpen)
    {
        return windowOpen ? Math.Min(5, combo + 1) : 1;
    }

    // bônus por terminar rápido: 10 pontos por segundo abaixo de 40 s
    public static int TimeBonus(int frames)
    {
        return Math.Max(0, 40 - frames / 60) * 10;
    }

    public static int PointsFor(int kind, int combo) { return BaseScore(kind) * Math.Max(1, combo); }

    public static string FmtTime(int frames)
    {
        int s = frames / 60;
        return string.Format("{0:00}:{1:00}", s / 60, s % 60);
    }

    // sorteio do bug de modo infinito / relógio conforme o total de mortes
    public static int PickKind(int kills, Random rnd)
    {
        int r = rnd.Next(100);
        if (kills < 5) return KindNormal;
        if (kills < 15) return r < 25 ? KindRunner : KindNormal;
        if (r < 15) return KindBomber;
        if (r < 35) return KindTank;
        if (r < 60) return KindRunner;
        return KindNormal;
    }

    public static bool ParseHotkey(string s, out uint mod, out uint vk)
    {
        mod = 0; vk = 0;
        if (s == null) return false;
        string[] parts = s.Split(new char[] { '+' });
        foreach (string p in parts)
        {
            string t = p.Trim().ToLowerInvariant();
            if (t.Length == 0) return false;
            if (t == "ctrl" || t == "control") mod |= 0x0002;
            else if (t == "alt") mod |= 0x0001;
            else if (t == "shift") mod |= 0x0004;
            else if (t == "win") mod |= 0x0008;
            else if (t == "space") vk = 0x20;
            else if (t.Length == 1 && ((t[0] >= 'a' && t[0] <= 'z') || (t[0] >= '0' && t[0] <= '9')))
                vk = (uint)char.ToUpperInvariant(t[0]);
            else if (t[0] == 'f' && t.Length <= 3)
            {
                int n;
                if (int.TryParse(t.Substring(1), out n) && n >= 1 && n <= 12) vk = (uint)(0x70 + n - 1);
                else return false;
            }
            else return false;
        }
        return vk != 0 && mod != 0;
    }

    // conquistas (máscara de bits)
    public const int AchFirst = 1, AchCombo5 = 2, AchPhase5 = 4, AchBoss = 8, AchKills100 = 16, AchNoHit = 32;
    public static readonly string[] AchNames = new string[] { "Primeiro sangue", "Combo x5", "Fase 5", "Matador de chefe", "100 bugs", "Fase sem dano" };
    public static readonly string[] AchNamesEn = new string[] { "First blood", "Combo x5", "Phase 5", "Boss slayer", "100 bugs", "Flawless phase" };

    public static int Unlock(int mask, int flag, out bool isNew)
    {
        isNew = (mask & flag) == 0;
        return mask | flag;
    }

    // perfis de dificuldade: bugs iniciais, dificuldade, vidas
    public static void Profile(string name, out int bugs, out int diff, out int lives)
    {
        switch (name)
        {
            case "casual": bugs = 3; diff = 1; lives = 8; break;
            case "dificil": bugs = 7; diff = 3; lives = 3; break;
            default: bugs = 5; diff = 2; lives = 5; break;
        }
    }
}
