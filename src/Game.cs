using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

// Regras do jogo: fases com chefe, modo relógio, modo infinito, vidas, itens, conquistas.
public partial class Whip
{
    List<BugForm> bugs = new List<BugForm>();
    List<ItemForm> items = new List<ItemForm>();
    Stopwatch bugSw = new Stopwatch();
    double bugAcc = 0;
    HudForm hud;
    int  phase = 0, phaseKills = 0, phaseTotal = 0, phaseFrames = 0, bannerF = 0, endF = 0, bonusPts = 0;
    int  score = 0, combo = 0, comboT = 0, hudTick = 0;
    bool phaseCleared = false, bestDirty = false;
    int  bestScore = 0, totalKills = 0;
    int[] bests = new int[3];

    // modo / vidas / extras
    int  cfgMode = 0;                 // 0 fases, 1 relógio, 2 infinito
    int  cfgLives = 5;                // 0 = vidas infinitas
    bool cfgEn = false;               // idioma: false = português
    int  gm = 0, runKills = 0, livesLeft = 0, biteCd = 0, timeLeft = 0, spawnCd = 0, minionCd = 0;
    int  gameOverF = 0, hurtF = 0, freezeF = 0, frenzyF = 0, toastF = 0, ach = 0;
    bool gameOver = false, hitThisPhase = false;
    string toastText = "", overTitle = "";

    string L(string pt, string en) { return cfgEn ? en : pt; }

    void ToggleBugs()
    {
        if (phase > 0 || bugs.Count > 0) StopGame(); else StartGame();
        UpdateTray();
    }

    void StartGame()
    {
        gm = GameLogic.ClampInt(cfgMode, 0, 2);
        score = 0; combo = 0; comboT = 0; phase = 1; runKills = 0;
        livesLeft = cfgLives; biteCd = 0; gameOver = false; gameOverF = 0;
        freezeF = 0; frenzyF = 0; toastF = 0; hurtF = 0; bossesSpawned = 0;
        bestScore = bests[gm];
        if (gm == 0) StartPhase();
        else
        {
            timeLeft = 3600; spawnCd = 0; minionCd = 0;
            phaseTotal = 0; phaseKills = 0; phaseFrames = 0; phaseCleared = false;
            bannerF = 130; endF = 0; bonusPts = 0;
            bugSw.Restart(); bugAcc = 0;
            int n = Math.Min(10, cfgBugs);
            for (int i = 0; i < Math.Min(3, n); i++) SpawnBug(0, GameLogic.SpeedFor(1, cfgDiff), 0, false);
            PlayBannerBeep();
        }
    }

    void StopGame()
    {
        ClearBugs();
        ClearItems();
        phase = 0; phaseCleared = false; combo = 0; comboT = 0; gameOver = false; gameOverF = 0;
        freezeF = 0; frenzyF = 0; slowF = 0;
        if (hud != null)
        {
            try { hud.Close(); hud.Dispose(); } catch { }
            hud = null;
        }
        if (bestDirty) { bestDirty = false; SaveSettings(); }
    }

    BugForm SpawnBug(int kind, float sp, int hp, bool at, float ax = 0f, float ay = 0f)
    {
        Rectangle vs = SystemInformation.VirtualScreen;
        BugForm b = new BugForm(rnd, vs, kind, sp, hp);
        if (at) { b.X = ax; b.Y = ay; }
        b.Show();
        b.Render();
        bugs.Add(b);
        return b;
    }

    void StartPhase()
    {
        int[] kinds = GameLogic.Compose(phase, cfgBugs);
        float sp = GameLogic.SpeedFor(phase, cfgDiff);
        for (int i = 0; i < kinds.Length; i++)
            SpawnBug(kinds[i], sp, kinds[i] == GameLogic.KindBoss ? GameLogic.BossHp(phase) : 0, false);
        phaseTotal = kinds.Length; phaseKills = 0; phaseFrames = 0;
        phaseCleared = false; bannerF = 130; endF = 0; bonusPts = 0;
        minionCd = 270; hitThisPhase = false;
        bugSw.Restart(); bugAcc = 0;
        if (phase >= 5) UnlockAch(GameLogic.AchPhase5);
        PlayBannerBeep();
    }

    void ClearBugs()
    {
        foreach (BugForm b in bugs)
        {
            try { b.Close(); b.Dispose(); } catch { }
        }
        bugs.Clear();
    }

    void ClearItems()
    {
        foreach (ItemForm it in items) { try { it.Close(); it.Dispose(); } catch { } }
        items.Clear();
    }

    // ---------- conquistas ----------
    void UnlockAch(int flag)
    {
        bool isNew;
        int m = GameLogic.Unlock(ach, flag, out isNew);
        if (!isNew) return;
        ach = m;
        int idx = 0;
        while (idx < 6 && (1 << idx) != flag) idx++;
        toastText = L(GameLogic.AchNames[Math.Min(idx, 5)], GameLogic.AchNamesEn[Math.Min(idx, 5)]);
        toastF = 150;
        bestDirty = true;
    }

    // ---------- pontos, combo, itens e explosão ----------
    void OnBugKilled(BugForm b)
    {
        combo = GameLogic.NextCombo(combo, comboT > 0);
        comboT = 100;
        int pts = GameLogic.PointsFor(b.Kind, combo) * (frenzyF > 0 ? 2 : 1);
        score += pts; phaseKills++; totalKills++; runKills++;
        b.Pop = "+" + pts;
        if (score > bestScore) { bestScore = score; bests[gm] = score; bestDirty = true; }
        if (gm == 1) timeLeft = Math.Min(5400, timeLeft + 60);

        UnlockAch(GameLogic.AchFirst);
        if (combo >= 5) UnlockAch(GameLogic.AchCombo5);
        if (totalKills >= 100) UnlockAch(GameLogic.AchKills100);
        if (b.Kind == GameLogic.KindBoss) { UnlockAch(GameLogic.AchBoss); OnBigExplosion(); PlayBoom(); }
        else
        {
            double chance = (b.Kind == GameLogic.KindTank) ? 0.4 : 0.12;
            if (phase > 0 && rnd.NextDouble() < chance) SpawnItem(b.X, b.Y);
        }
        if (b.Boom) { b.Boom = false; BugBoom(b); }
    }

    void SpawnItem(float x, float y)
    {
        if (items.Count >= 3) return;
        Rectangle vs = SystemInformation.VirtualScreen;
        x = Math.Max(vs.Left + 50, Math.Min(vs.Right - 50, x));
        y = Math.Max(vs.Top + 50, Math.Min(vs.Bottom - 50, y));
        ItemForm it = new ItemForm(x, y, rnd.Next(3));
        it.Show();
        it.Render(cfgEn);
        items.Add(it);
    }

    void CollectItem(ItemForm it)
    {
        if (it.Kind == 0)
        {
            akAmmo = AK_MAG; akReload = 0; flFuel = FL_MAX; flLock = false; grCd = 0;
            score += 50;
            toastText = L("Munição reabastecida", "Ammo refilled");
        }
        else if (it.Kind == 1) { freezeF = 300; toastText = L("Bugs congelados!", "Bugs frozen!"); }
        else { frenzyF = 480; toastText = L("Pontos em dobro!", "Double points!"); }
        toastF = 110;
        PlayPickup();
    }

    void PlayPickup() { PlayReload(); }

    void BugBite()
    {
        livesLeft--;
        hitThisPhase = true;
        biteCd = 75; hurtF = 40; combo = 0;
        toastText = L("Mordida! Vidas: ", "Bitten! Lives: ") + Math.Max(0, livesLeft);
        toastF = 70;
        PlayThud();
        if (livesLeft <= 0) EndGame(L("FIM DE JOGO", "GAME OVER"));
    }

    void EndGame(string title)
    {
        if (gameOver) return;
        gameOver = true; gameOverF = 240; overTitle = title;
        ClearBugs(); ClearItems();
        if (score > bests[gm]) bests[gm] = score;
        if (score > bestScore) bestScore = score;
        bestDirty = true;
        SaveSettings();
    }

    // passo de 1/60 s da regra do jogo
    void GameTick()
    {
        if (phase == 0) return;
        if (gameOver)
        {
            gameOverF--;
            if (gameOverF <= 0) StopGame();
            return;
        }
        if (comboT > 0) { comboT--; if (comboT == 0) combo = 0; }
        if (bannerF > 0) bannerF--;
        if (toastF > 0) toastF--;
        if (hurtF > 0) hurtF--;
        if (biteCd > 0) biteCd--;
        if (freezeF > 0) freezeF--;
        if (frenzyF > 0) frenzyF--;
        foreach (BugForm b in bugs) b.Frozen = freezeF > 0;

        // itens: passar o cursor por cima pega
        for (int i = items.Count - 1; i >= 0; i--)
        {
            ItemForm it = items[i];
            it.Step();
            float dx = it.X - mx, dy = it.Y - my;
            bool got = enabled && dx * dx + dy * dy < 36f * 36f;
            if (got || it.Dead)
            {
                if (got) CollectItem(it);
                try { it.Close(); it.Dispose(); } catch { }
                items.RemoveAt(i);
            }
            else if ((it.Life % 3) == 0) it.Render(cfgEn);
        }

        // mordidas: bug encostando no cursor com a arma ligada tira uma vida
        if (cfgLives > 0 && enabled && biteCd <= 0 && !phaseCleared)
        {
            foreach (BugForm b in bugs)
            {
                if (b.State != 0 || b.Frozen) continue;
                float dx = b.X - mx, dy = b.Y - my;
                float r = 18f + 14f * b.BodyScale;
                if (dx * dx + dy * dy < r * r) { BugBite(); break; }
            }
            if (gameOver) return;
        }

        // chefe: de tempos em tempos chama bugs pequenos
        BugForm boss = null;
        foreach (BugForm b in bugs) if (b.Kind == GameLogic.KindBoss && b.State == 0) boss = b;
        if (boss != null && !phaseCleared)
        {
            minionCd--;
            if (minionCd <= 0)
            {
                minionCd = 270;
                float sp = GameLogic.SpeedFor(Math.Max(1, phase), cfgDiff);
                for (int k = 0; k < 2; k++) SpawnBug(GameLogic.KindNormal, sp, 0, true, boss.X + (k == 0 ? -40f : 40f), boss.Y);
                if (gm == 0) phaseTotal += 2;
            }
        }

        if (gm == 0) PhaseTick();
        else FreeTick();
    }

    void PhaseTick()
    {
        if (!phaseCleared)
        {
            phaseFrames++;
            if (phaseKills >= phaseTotal)
            {
                phaseCleared = true;
                endF = 170;
                bonusPts = GameLogic.TimeBonus(phaseFrames);
                score += bonusPts;
                if (!hitThisPhase && cfgLives > 0) UnlockAch(GameLogic.AchNoHit);
                if (score > bestScore) { bestScore = score; bests[gm] = score; bestDirty = true; }
                if (bestDirty) { bestDirty = false; SaveSettings(); }
            }
        }
        else
        {
            endF--;
            if (endF <= 0) { phase++; StartPhase(); }
        }
    }

    // relógio e infinito: bugs nascem continuamente
    void FreeTick()
    {
        phaseFrames++;
        if (gm == 1)
        {
            timeLeft--;
            if (timeLeft <= 0) { EndGame(L("TEMPO ESGOTADO", "TIME'S UP")); return; }
        }
        int alive = 0; bool bossAlive = false;
        foreach (BugForm b in bugs) { if (b.State == 0) alive++; if (b.Kind == GameLogic.KindBoss && b.State == 0) bossAlive = true; }
        int lvl = 1 + runKills / 8;
        int target = Math.Min(10, cfgBugs + runKills / 8);
        if (spawnCd > 0) spawnCd--;
        if (alive < target && spawnCd <= 0)
        {
            spawnCd = 40;
            float sp = GameLogic.SpeedFor(lvl, cfgDiff);
            if (gm == 2 && !bossAlive && runKills >= 30 && (runKills / 30) > bossesSpawned)
            {
                bossesSpawned = runKills / 30;
                SpawnBug(GameLogic.KindBoss, sp, GameLogic.BossHp(5 * bossesSpawned), false);
            }
            else SpawnBug(GameLogic.PickKind(runKills, rnd), sp, 0, false);
        }
    }
    int bossesSpawned = 0;

    void UpdateHud()
    {
        try
        {
            if (!cfgHud || phase == 0)
            {
                if (hud != null) { hud.Close(); hud.Dispose(); hud = null; }
                return;
            }
            if (hud == null) { hud = new HudForm(); hud.Show(); }
            string l1, l2;
            string lives = (cfgLives > 0) ? "     " + L("Vidas ", "Lives ") + Math.Max(0, livesLeft) : "";
            string fx = "";
            if (freezeF > 0) fx += "     " + L("GELO ", "FREEZE ") + (freezeF / 60 + 1) + "s";
            if (frenzyF > 0) fx += "     x2 " + (frenzyF / 60 + 1) + "s";
            if (gm == 0)
            {
                l1 = string.Format(L("FASE {0}     BUGS {1}/{2}     PONTOS {3}", "PHASE {0}     BUGS {1}/{2}     SCORE {3}"), phase, Math.Min(phaseKills, phaseTotal), phaseTotal, score);
                l2 = string.Format(L("Tempo {0}     Combo x{1}     Recorde {2}", "Time {0}     Combo x{1}     Best {2}"), GameLogic.FmtTime(phaseFrames), Math.Max(1, combo), bestScore) + lives + fx;
            }
            else
            {
                string name = (gm == 1) ? L("RELÓGIO", "TIME ATTACK") : L("INFINITO", "ENDLESS");
                string tm = (gm == 1) ? GameLogic.FmtTime(Math.Max(0, timeLeft)) : GameLogic.FmtTime(phaseFrames);
                l1 = string.Format(L("{0}     PONTOS {1}     ABATES {2}", "{0}     SCORE {1}     KILLS {2}"), name, score, runKills);
                l2 = string.Format(L("Tempo {0}     Combo x{1}     Recorde {2}", "Time {0}     Combo x{1}     Best {2}"), tm, Math.Max(1, combo), bestScore) + lives + fx;
            }
            string banner = "", sub = ""; float ba = 0f;
            if (gameOver)
            {
                banner = overTitle;
                sub = string.Format(L("{0} pontos  -  recorde {1}", "{0} points  -  best {1}"), score, bestScore);
                ba = Math.Min(1f, gameOverF / 30f + 0.0f);
                if (gameOverF > 210) ba = (240 - gameOverF) / 30f;
            }
            else if (phaseCleared && gm == 0)
            {
                banner = string.Format(L("FASE {0} CONCLUÍDA!", "PHASE {0} CLEARED!"), phase);
                sub = string.Format(L("Bônus de tempo +{0}", "Time bonus +{0}"), bonusPts);
                ba = Math.Min(1f, endF / 25f + 0.0f);
                if (endF > 140) ba = (170 - endF) / 30f;
            }
            else if (toastF > 0)
            {
                banner = toastText;
                sub = (hurtF > 0) ? "" : L("conquista / item", "achievement / item");
                if (toastText.Length > 0 && IsAchToast()) { sub = L("CONQUISTA DESBLOQUEADA", "ACHIEVEMENT UNLOCKED"); }
                else sub = "";
                ba = Math.Min(1f, toastF / 25f);
            }
            else if (bannerF > 0)
            {
                if (gm == 0 && GameLogic.IsBossPhase(phase)) { banner = string.Format(L("FASE {0}: CHEFE!", "PHASE {0}: BOSS!"), phase); sub = L("derrube o rei dos bugs", "take down the bug king"); }
                else if (gm == 0) { banner = string.Format(L("FASE {0}", "PHASE {0}"), phase); sub = string.Format(L("{0} bugs: mate todos!", "{0} bugs: kill them all!"), phaseTotal); }
                else { banner = (gm == 1) ? L("CONTRA O RELÓGIO", "TIME ATTACK") : L("MODO INFINITO", "ENDLESS MODE"); sub = (gm == 1) ? L("cada bug dá +1 s", "each bug gives +1 s") : L("sobreviva o máximo que puder", "survive as long as you can"); }
                ba = Math.Min(1f, bannerF / 30f);
            }
            hud.Draw(l1, l2, banner, sub, ba);
        }
        catch { }
    }

    bool IsAchToast()
    {
        for (int i = 0; i < 6; i++)
            if (toastText == GameLogic.AchNames[i] || toastText == GameLogic.AchNamesEn[i]) return true;
        return false;
    }

    void StepBugsTimed()
    {
        if (bugs.Count == 0 && phase == 0) return;
        bugAcc += bugSw.Elapsed.TotalMilliseconds;
        bugSw.Restart();
        if (bugAcc > 100) bugAcc = 100;
        int steps = 0;
        while (bugAcc >= STEP_MS && steps < 4)
        {
            bool slow = slowF > 0;
            if (slow) slowF--;
            if (!(slow && (steps & 1) == 1))
                for (int i = 0; i < bugs.Count; i++) bugs[i].Step(mx, my, enabled);
            GameTick();
            bugAcc -= STEP_MS;
            steps++;
        }
        if (steps > 0)
        {
            bool removed = false;
            for (int i = bugs.Count - 1; i >= 0; i--)
            {
                BugForm b = bugs[i];
                if (b.State == 2)
                {
                    try { b.Close(); b.Dispose(); } catch { }
                    bugs.RemoveAt(i);
                    removed = true;
                }
                else b.Render();
            }
            if (removed) UpdateTray();
            if (phase > 0 && (hud == null || (++hudTick % 3) == 0)) UpdateHud();
        }
    }
}
