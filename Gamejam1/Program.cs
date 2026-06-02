using Raylib_cs;
using System;
using System.Collections.Generic;
using System.Numerics;

class Game
{
    const int ScreenW = 1000;
    const int ScreenH = 700;

    // ---------- Wapens ----------
    enum Rarity { Common, Uncommon, Rare, Epic, Legendary }

    class Weapon
    {
        public string Name = "";
        public Rarity Rarity;
        public float Damage;
        public float FireRate;     // seconden tussen schoten
        public float BulletSpeed;
        public int BulletsPerShot;
        public float Spread;       // graden

        public Color RarityColor()
        {
            switch (Rarity)
            {
                case Rarity.Common:    return new Color(160, 160, 160, 255);
                case Rarity.Uncommon:  return new Color(76, 204, 76, 255);
                case Rarity.Rare:      return new Color(76, 128, 255, 255);
                case Rarity.Epic:      return new Color(178, 76, 255, 255);
                case Rarity.Legendary: return new Color(255, 178, 25, 255);
            }
            return Color.White;
        }
    }

    // De loot-pool: alle wapens die uit een chest kunnen komen
    static List<Weapon> WeaponPool = new List<Weapon>
    {
        new Weapon { Name = "Pistol",       Rarity = Rarity.Common,    Damage = 10, FireRate = 0.35f, BulletSpeed = 500, BulletsPerShot = 1, Spread = 2 },
        new Weapon { Name = "SMG",          Rarity = Rarity.Common,    Damage = 8,  FireRate = 0.12f, BulletSpeed = 550, BulletsPerShot = 1, Spread = 6 },
        new Weapon { Name = "Rifle",        Rarity = Rarity.Uncommon,  Damage = 18, FireRate = 0.30f, BulletSpeed = 650, BulletsPerShot = 1, Spread = 1 },
        new Weapon { Name = "Shotgun",      Rarity = Rarity.Uncommon,  Damage = 7,  FireRate = 0.70f, BulletSpeed = 480, BulletsPerShot = 6, Spread = 18 },
        new Weapon { Name = "Burst Rifle",  Rarity = Rarity.Rare,      Damage = 22, FireRate = 0.25f, BulletSpeed = 700, BulletsPerShot = 2, Spread = 3 },
        new Weapon { Name = "Heavy MG",     Rarity = Rarity.Rare,      Damage = 14, FireRate = 0.08f, BulletSpeed = 600, BulletsPerShot = 1, Spread = 7 },
        new Weapon { Name = "Plasma Gun",   Rarity = Rarity.Epic,      Damage = 40, FireRate = 0.40f, BulletSpeed = 750, BulletsPerShot = 1, Spread = 0 },
        new Weapon { Name = "Scatter Epic", Rarity = Rarity.Epic,      Damage = 12, FireRate = 0.55f, BulletSpeed = 600, BulletsPerShot = 10, Spread = 22 },
        new Weapon { Name = "Railgun",      Rarity = Rarity.Legendary, Damage = 90, FireRate = 0.60f, BulletSpeed = 1000, BulletsPerShot = 1, Spread = 0 },
        new Weapon { Name = "Doom Cannon",  Rarity = Rarity.Legendary, Damage = 30, FireRate = 0.06f, BulletSpeed = 800, BulletsPerShot = 1, Spread = 4 },
    };

    // ---------- Spelobjecten ----------
    class Bullet { public Vector2 Pos; public Vector2 Vel; public float Damage; public bool Alive = true; }
    class Enemy  { public Vector2 Pos; public float Health; public float MaxHealth; public float Speed; public int Coins; }
    class Chest  { public Vector2 Pos; public int Cost = 50; }

    static Random rng = new Random();

    // ---------- Game state ----------
    static Vector2 playerPos = new Vector2(ScreenW / 2f, ScreenH / 2f);
    static float playerSpeed = 260f;
    static int playerHealth = 100;
    static Weapon currentWeapon = null!;
    static int coins = 0;
    static float fireCooldown = 0f;

    static List<Bullet> bullets = new List<Bullet>();
    static List<Enemy> enemies = new List<Enemy>();
    static List<Chest> chests = new List<Chest>();

    static float enemySpawnTimer = 0f;
    static float chestSpawnTimer = 0f;

    // ---------- Case-opening animatie ----------
    static bool caseOpen = false;
    static List<Weapon> caseStrip = new List<Weapon>();
    static float caseScroll = 0f;       // huidige x-offset van de strip
    static float caseTargetScroll = 0f; // eindpositie
    static float caseTimer = 0f;
    static float caseDuration = 4.5f;
    static int caseWinIndex = 0;
    static bool caseFinished = false;
    static float caseResultTimer = 0f;
    const int SlotWidth = 140;

    static void Main()
    {
        Raylib.InitWindow(ScreenW, ScreenH, "Top-Down Shooter");
        Raylib.SetTargetFPS(60);

        currentWeapon = WeaponPool[0]; // start met de Pistol

        while (!Raylib.WindowShouldClose())
        {
            float dt = Raylib.GetFrameTime();

            if (caseOpen) UpdateCase(dt);
            else if (playerHealth > 0) UpdateGame(dt);

            Draw();
        }

        Raylib.CloseWindow();
    }

    // ---------- Gameplay update ----------
    static void UpdateGame(float dt)
    {
        // Beweging (WASD)
        Vector2 move = Vector2.Zero;
        if (Raylib.IsKeyDown(KeyboardKey.W)) move.Y -= 1;
        if (Raylib.IsKeyDown(KeyboardKey.S)) move.Y += 1;
        if (Raylib.IsKeyDown(KeyboardKey.A)) move.X -= 1;
        if (Raylib.IsKeyDown(KeyboardKey.D)) move.X += 1;
        if (move.LengthSquared() > 0) move = Vector2.Normalize(move);
        playerPos += move * playerSpeed * dt;

        // Binnen scherm houden
        playerPos.X = Math.Clamp(playerPos.X, 20, ScreenW - 20);
        playerPos.Y = Math.Clamp(playerPos.Y, 20, ScreenH - 20);

        // Richten naar muis
        Vector2 mouse = Raylib.GetMousePosition();
        Vector2 aimDir = mouse - playerPos;
        if (aimDir.LengthSquared() > 0) aimDir = Vector2.Normalize(aimDir);

        // Schieten (linkermuis ingedrukt)
        fireCooldown -= dt;
        if (Raylib.IsMouseButtonDown(MouseButton.Left) && fireCooldown <= 0f)
        {
            Shoot(aimDir);
            fireCooldown = currentWeapon.FireRate;
        }

        // Chest openen (E)
        if (Raylib.IsKeyPressed(KeyboardKey.E))
        {
            for (int i = chests.Count - 1; i >= 0; i--)
            {
                if (Vector2.Distance(playerPos, chests[i].Pos) < 50 && coins >= chests[i].Cost)
                {
                    coins -= chests[i].Cost;
                    chests.RemoveAt(i);
                    StartCase();
                    break;
                }
            }
        }

        // Bullets bewegen
        foreach (var b in bullets)
        {
            b.Pos += b.Vel * dt;
            if (b.Pos.X < -50 || b.Pos.X > ScreenW + 50 || b.Pos.Y < -50 || b.Pos.Y > ScreenH + 50)
                b.Alive = false;
        }

        // Enemies bewegen naar speler
        foreach (var e in enemies)
        {
            Vector2 dir = playerPos - e.Pos;
            if (dir.LengthSquared() > 0) dir = Vector2.Normalize(dir);
            e.Pos += dir * e.Speed * dt;

            // Speler raken
            if (Vector2.Distance(e.Pos, playerPos) < 26)
                playerHealth -= 1; // langzame schade bij contact
        }

        // Bullet vs enemy
        foreach (var b in bullets)
        {
            if (!b.Alive) continue;
            foreach (var e in enemies)
            {
                if (e.Health <= 0) continue;
                if (Vector2.Distance(b.Pos, e.Pos) < 20)
                {
                    e.Health -= b.Damage;
                    b.Alive = false;
                    if (e.Health <= 0) coins += e.Coins;
                    break;
                }
            }
        }

        // Opruimen
        bullets.RemoveAll(b => !b.Alive);
        enemies.RemoveAll(e => e.Health <= 0);

        // Spawnen
        enemySpawnTimer -= dt;
        if (enemySpawnTimer <= 0f)
        {
            SpawnEnemy();
            enemySpawnTimer = Math.Max(0.4f, 1.6f - coins * 0.002f); // sneller naarmate je rijker wordt
        }

        chestSpawnTimer -= dt;
        if (chestSpawnTimer <= 0f && chests.Count < 3)
        {
            chests.Add(new Chest
            {
                Pos = new Vector2(rng.Next(60, ScreenW - 60), rng.Next(60, ScreenH - 60))
            });
            chestSpawnTimer = 12f;
        }
    }

    static void Shoot(Vector2 dir)
    {
        float baseAngle = MathF.Atan2(dir.Y, dir.X);
        for (int i = 0; i < currentWeapon.BulletsPerShot; i++)
        {
            float spreadRad = (float)((rng.NextDouble() * 2 - 1) * currentWeapon.Spread * Math.PI / 180.0);
            float a = baseAngle + spreadRad;
            Vector2 vel = new Vector2(MathF.Cos(a), MathF.Sin(a)) * currentWeapon.BulletSpeed;
            bullets.Add(new Bullet { Pos = playerPos, Vel = vel, Damage = currentWeapon.Damage });
        }
    }

    static void SpawnEnemy()
    {
        // Spawn aan een willekeurige rand
        Vector2 pos;
        int side = rng.Next(4);
        switch (side)
        {
            case 0: pos = new Vector2(rng.Next(ScreenW), -30); break;
            case 1: pos = new Vector2(rng.Next(ScreenW), ScreenH + 30); break;
            case 2: pos = new Vector2(-30, rng.Next(ScreenH)); break;
            default: pos = new Vector2(ScreenW + 30, rng.Next(ScreenH)); break;
        }
        float hp = 30 + coins * 0.05f;
        enemies.Add(new Enemy { Pos = pos, Health = hp, MaxHealth = hp, Speed = rng.Next(70, 130), Coins = 5 });
    }

    // ---------- Case opening ----------
    static void StartCase()
    {
        caseOpen = true;
        caseFinished = false;
        caseTimer = 0f;
        caseResultTimer = 0f;
        caseScroll = 0f;

        // Bepaal de winst via gewogen rarity
        Weapon won = RollWeapon();

        // Bouw de strip met willekeurige wapens, zet de winst op een vaste plek
        caseStrip.Clear();
        int total = 50;
        caseWinIndex = total - 6;
        for (int i = 0; i < total; i++)
            caseStrip.Add(i == caseWinIndex ? won : RollWeapon());

        // Eindpositie: zorg dat caseWinIndex onder de marker (midden) valt
        float markerX = ScreenW / 2f;
        float slotCenter = caseWinIndex * SlotWidth + SlotWidth / 2f;
        // kleine random offset binnen het slot voor realisme
        float offset = (float)((rng.NextDouble() * 2 - 1) * SlotWidth * 0.3f);
        caseTargetScroll = markerX - slotCenter + offset;
    }

    static void UpdateCase(float dt)
    {
        if (!caseFinished)
        {
            caseTimer += dt;
            float t = Math.Clamp(caseTimer / caseDuration, 0f, 1f);
            float eased = 1f - MathF.Pow(1f - t, 3f); // ease-out cubic
            caseScroll = eased * caseTargetScroll;

            if (t >= 1f)
            {
                caseScroll = caseTargetScroll;
                caseFinished = true;
            }
        }
        else
        {
            caseResultTimer += dt;
            // Na de animatie: wapen uitrusten als het beter is
            if (caseResultTimer > 0.01f && caseResultTimer < 0.05f)
            {
                Weapon won = caseStrip[caseWinIndex];
                if (won.Damage * (1f / won.FireRate) * won.BulletsPerShot
                    > currentWeapon.Damage * (1f / currentWeapon.FireRate) * currentWeapon.BulletsPerShot)
                {
                    currentWeapon = won;
                }
            }
            // Sluiten met spatie of na 3 seconden
            if (Raylib.IsKeyPressed(KeyboardKey.Space) || caseResultTimer > 3f)
                caseOpen = false;
        }
    }

    static Weapon RollWeapon()
    {
        double roll = rng.NextDouble();
        Rarity target;
        if (roll < 0.50) target = Rarity.Common;
        else if (roll < 0.78) target = Rarity.Uncommon;
        else if (roll < 0.92) target = Rarity.Rare;
        else if (roll < 0.985) target = Rarity.Epic;
        else target = Rarity.Legendary;

        var matches = WeaponPool.FindAll(w => w.Rarity == target);
        if (matches.Count == 0) return WeaponPool[rng.Next(WeaponPool.Count)];
        return matches[rng.Next(matches.Count)];
    }

    // ---------- Tekenen ----------
    static void Draw()
    {
        Raylib.BeginDrawing();
        Raylib.ClearBackground(new Color(28, 28, 36, 255));

        // Chests
        foreach (var c in chests)
        {
            Raylib.DrawRectangle((int)c.Pos.X - 18, (int)c.Pos.Y - 14, 36, 28, new Color(180, 140, 40, 255));
            Raylib.DrawRectangleLines((int)c.Pos.X - 18, (int)c.Pos.Y - 14, 36, 28, Color.Yellow);
            if (Vector2.Distance(playerPos, c.Pos) < 50)
            {
                string txt = coins >= c.Cost ? "[E] Open (50)" : "Te weinig coins";
                Raylib.DrawText(txt, (int)c.Pos.X - 40, (int)c.Pos.Y - 36, 16, Color.White);
            }
        }

        // Enemies
        foreach (var e in enemies)
        {
            Raylib.DrawCircleV(e.Pos, 16, new Color(220, 60, 60, 255));
            // health bar
            float w = 32 * (e.Health / e.MaxHealth);
            Raylib.DrawRectangle((int)e.Pos.X - 16, (int)e.Pos.Y - 26, 32, 4, Color.DarkGray);
            Raylib.DrawRectangle((int)e.Pos.X - 16, (int)e.Pos.Y - 26, (int)w, 4, Color.Green);
        }

        // Bullets
        foreach (var b in bullets)
            Raylib.DrawCircleV(b.Pos, 4, currentWeapon.RarityColor());

        // Speler
        Raylib.DrawCircleV(playerPos, 16, new Color(80, 160, 255, 255));
        // loop-richting
        Vector2 mouse = Raylib.GetMousePosition();
        Vector2 aim = mouse - playerPos;
        if (aim.LengthSquared() > 0) aim = Vector2.Normalize(aim);
        Raylib.DrawLineEx(playerPos, playerPos + aim * 26, 4, Color.White);

        // HUD
        Raylib.DrawText($"Coins: {coins}", 14, 14, 24, Color.Yellow);
        Raylib.DrawText($"HP: {Math.Max(0, playerHealth)}", 14, 44, 24, Color.White);
        Raylib.DrawText($"Wapen: {currentWeapon.Name}", 14, 74, 22, currentWeapon.RarityColor());

        if (playerHealth <= 0)
        {
            Raylib.DrawText("GAME OVER", ScreenW / 2 - 130, ScreenH / 2 - 30, 60, Color.Red);
        }

        if (caseOpen) DrawCase();

        Raylib.EndDrawing();
    }

    static void DrawCase()
    {
        // donkere overlay
        Raylib.DrawRectangle(0, 0, ScreenW, ScreenH, new Color(0, 0, 0, 200));

        int stripY = ScreenH / 2 - 60;
        int stripH = 120;

        // achtergrond van de strip
        Raylib.DrawRectangle(0, stripY, ScreenW, stripH, new Color(20, 20, 28, 255));

        // de items
        for (int i = 0; i < caseStrip.Count; i++)
        {
            float x = caseScroll + i * SlotWidth;
            if (x + SlotWidth < 0 || x > ScreenW) continue; // buiten beeld

            Weapon w = caseStrip[i];
            Color col = w.RarityColor();
            int pad = 8;
            Raylib.DrawRectangle((int)x + pad, stripY + pad, SlotWidth - pad * 2, stripH - pad * 2,
                new Color(col.R, col.G, col.B, (byte)60));
            Raylib.DrawRectangleLines((int)x + pad, stripY + pad, SlotWidth - pad * 2, stripH - pad * 2, col);

            // naam gecentreerd
            int fontSize = 16;
            int tw = Raylib.MeasureText(w.Name, fontSize);
            Raylib.DrawText(w.Name, (int)(x + SlotWidth / 2 - tw / 2), stripY + stripH / 2 - 8, fontSize, col);
        }

        // marker in het midden
        Raylib.DrawRectangle(ScreenW / 2 - 2, stripY - 10, 4, stripH + 20, Color.Red);
        Raylib.DrawTriangle(
            new Vector2(ScreenW / 2 - 10, stripY - 10),
            new Vector2(ScreenW / 2 + 10, stripY - 10),
            new Vector2(ScreenW / 2, stripY + 4),
            Color.Red);

        // resultaat
        if (caseFinished)
        {
            Weapon won = caseStrip[caseWinIndex];
            string txt = $"Je kreeg: {won.Name} ({won.Rarity})";
            int fs = 30;
            int tw = Raylib.MeasureText(txt, fs);
            Raylib.DrawText(txt, ScreenW / 2 - tw / 2, stripY + stripH + 30, fs, won.RarityColor());
            Raylib.DrawText("[Spatie] om door te gaan", ScreenW / 2 - 120, stripY + stripH + 70, 18, Color.LightGray);
        }
    }
}
