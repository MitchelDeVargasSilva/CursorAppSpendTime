using System;
class Tests
{
    static int fails = 0, total = 0;
    static void Eq(object a, object b, string what)
    {
        total++;
        if (!object.Equals(a, b)) { fails++; Console.WriteLine("FALHOU: " + what + " esperado=" + b + " obtido=" + a); }
    }
    static void Main()
    {
        Eq(GameLogic.IsBossPhase(5), true, "chefe fase 5");
        Eq(GameLogic.IsBossPhase(4), false, "sem chefe fase 4");
        Eq(GameLogic.IsBossPhase(0), false, "fase 0");
        int[] k1 = GameLogic.Compose(1, 5);
        Eq(k1.Length, 5, "fase 1 tem 5");
        foreach (int k in k1) Eq(k, 0, "fase 1 só normal");
        int[] k4 = GameLogic.Compose(4, 5);
        int bombers = 0; foreach (int k in k4) if (k == 3) bombers++;
        Eq(bombers, 1, "fase 4 tem 1 explosivo");
        int[] k5 = GameLogic.Compose(5, 5);
        Eq(k5[k5.Length - 1], 4, "chefe no fim da fase 5");
        Eq(GameLogic.Compose(30, 10).Length <= 11, true, "limite de bugs");
        Eq(GameLogic.Compose(1, 99).Length, 10, "clamp de bugs");
        Eq(GameLogic.NextCombo(1, true), 2, "combo sobe");
        Eq(GameLogic.NextCombo(5, true), 5, "combo máx 5");
        Eq(GameLogic.NextCombo(4, false), 1, "combo reinicia");
        Eq(GameLogic.TimeBonus(0), 400, "bônus máximo");
        Eq(GameLogic.TimeBonus(60 * 60), 0, "sem bônus");
        Eq(GameLogic.PointsFor(2, 3), 900, "tanque x3");
        Eq(GameLogic.PointsFor(4, 0), 2000, "chefe combo 0");
        Eq(GameLogic.FmtTime(60 * 75), "01:15", "formato do tempo");
        Eq(GameLogic.SpeedFor(1, 2), 1f, "velocidade base");
        Eq(GameLogic.SpeedFor(99, 3), 2.2f * 1.3f, "velocidade máx");
        Eq(GameLogic.BossHp(5) < GameLogic.BossHp(10), true, "chefe fica mais forte");
        uint m, v;
        Eq(GameLogic.ParseHotkey("Ctrl+Alt+W", out m, out v), true, "atalho válido");
        Eq(m, 3u, "mods ctrl+alt"); Eq(v, (uint)'W', "vk W");
        Eq(GameLogic.ParseHotkey("Ctrl+Shift+F9", out m, out v), true, "F9");
        Eq(v, 0x78u, "vk F9");
        Eq(GameLogic.ParseHotkey("W", out m, out v), false, "sem modificador");
        Eq(GameLogic.ParseHotkey("Ctrl+Alt+", out m, out v), false, "vazio");
        Eq(GameLogic.ParseHotkey("Ctrl+F13", out m, out v), false, "F13 inválida");
        Eq(GameLogic.ParseHotkey(null, out m, out v), false, "null");
        Random r = new Random(1);
        Eq(GameLogic.PickKind(0, r), 0, "início só normal");
        bool isNew; int mask = GameLogic.Unlock(0, GameLogic.AchBoss, out isNew);
        Eq(isNew, true, "conquista nova"); mask = GameLogic.Unlock(mask, GameLogic.AchBoss, out isNew);
        Eq(isNew, false, "conquista repetida");
        int b, d, l; GameLogic.Profile("dificil", out b, out d, out l);
        Eq(d, 3, "perfil difícil");
        Console.WriteLine(fails == 0 ? "OK: " + total + " testes passaram" : "FALHAS: " + fails + " de " + total);
        Environment.Exit(fails == 0 ? 0 : 1);
    }
}
