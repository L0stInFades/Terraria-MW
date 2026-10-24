using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace ModernArsenal.Verification;

public sealed class PhalanxTest : ModSystem
{
    private readonly List<string> checks = new();
    private void Check(bool value, string text) { if (!value) throw new InvalidOperationException(text); checks.Add(text); }
    public override bool IsLoadingEnabled(Mod mod) => Program.LaunchParameters.ContainsKey("-arsenalciwstest") || Program.LaunchParameters.ContainsKey("-arsenalciwsreload");
    public override void PostWorldLoad()
    {
        if (!Main.dedServ) return;
        string flag = Program.LaunchParameters.ContainsKey("-arsenalciwstest") ? "-arsenalciwstest" : "-arsenalciwsreload";
        try
        {
            Main.netMode = NetmodeID.Server; Main.myPlayer = 0;
            if (flag == "-arsenalciwsreload") Reload(); else Run();
            WorldFile.SaveWorld();
            File.WriteAllText(Program.LaunchParameters[flag], JsonSerializer.Serialize(new { success = true, checks,
                arenas = PhalanxWorld.Arenas.Select(a => new { x = a.X, floor = a.Y }).ToArray(),
                turrets = TileEntity.ByID.Values.Count(t => t is PhalanxEntity) }, new JsonSerializerOptions { WriteIndented = true }));
            Environment.Exit(0);
        }
        catch (Exception exception)
        {
            File.WriteAllText(Program.LaunchParameters[flag], JsonSerializer.Serialize(new { success = false, checks, error = exception.ToString() }, new JsonSerializerOptions { WriteIndented = true }));
            Environment.Exit(1);
        }
    }
    private Player Player(int index)
    {
        Player player = new() { name = index == 0 ? "CIWSTester" : "Operator" + index, whoAmI = index, active = true, statLife = 500, statLifeMax = 500 };
        Main.player[index] = player;
        player.Center = new Vector2(Main.spawnTileX * 16, Main.spawnTileY * 16);
        return player;
    }
    private NPC Spawn(int type, Vector2 center)
    {
        int id = NPC.NewNPC(new EntitySource_Misc("CIWSVerification"), (int)center.X, (int)center.Y, type);
        if (id >= Main.maxNPCs) throw new Exception("No free NPC slot in test");
        NPC npc = Main.npc[id]; npc.Center = center; npc.aiStyle = -1; npc.velocity = Vector2.Zero;
        npc.life = npc.lifeMax = 200000000; npc.damage = 0; npc.noGravity = true;
        npc.dontTakeDamage = npc.immortal = false; npc.chaseable = true;
        return npc;
    }
    private static void ClearNPCs() { foreach (NPC npc in Main.npc) npc.active = false; }
    private static void ClearRounds() { foreach (Projectile projectile in Main.projectile) projectile.active = false; }
    private void Run()
    {
        Player player = Player(0);
        for (int i = 0; i < 50; i++) player.inventory[i] = new Item(ItemID.Wood, i + 1);
        string[] old = player.inventory.Take(50).Select(i => i.type + ":" + i.stack).ToArray();
        Check(PhalanxArena.Equip(player), "six-row arena and remote equipment can be prepared in a real existing world");
        Point16 arena = PhalanxWorld.Arenas.Single();
        Check(PhalanxArena.Exists(arena) && player.inventory[9].ModItem is PhalanxRemote, "remote is equipped in hotbar 10 even with a full inventory");
        int chest = PhalanxArena.ControlChest(arena);
        Check(chest >= 0 && old.All(value => player.inventory.Take(50).Concat(Main.chest[chest].item).Any(i => i.type + ":" + i.stack == value)), "all fifty original item stacks survive remote delivery");
        Check(Main.chest[chest].item.Any(i => i.ModItem is PhalanxTurret && i.stack == 8), "control locker contains eight spare placeable turrets");
        var guns = TileEntity.ByID.Values.OfType<PhalanxEntity>().ToArray();
        Check(guns.Length == 6 && guns.All(g => g.IsTileValidForEntity(g.Position.X, g.Position.Y)), "six real turret tile entities are anchored on the generated platforms");
        for (int row = 0; row < PhalanxArena.Rows; row++)
            Check(Enumerable.Range(0, PhalanxArena.Width).Count(dx => Main.tile[arena.X + dx, arena.Y - row * PhalanxArena.Spacing].HasTile) == PhalanxArena.Width,
                "arena row " + (row + 1) + " spans 240 tiles, including the rope corridors and solid fittings");
        Check(PhalanxArena.Build(player.Center) == arena && TileEntity.ByID.Values.Count(t => t is PhalanxEntity) == 6, "repeated deployment near the same position reuses the arena without duplicates");
        Check(!PhalanxArena.ClearSite(Main.spawnTileX - 10, Main.spawnTileY + 3), "occupied terrain is rejected before construction");

        ClearNPCs(); ClearRounds();
        Vector2 origin = guns[0].Pivot;
        NPC target = Spawn(NPCID.EyeofCthulhu, origin + new Vector2(500, -40));
        Check(ReferenceEquals(PhalanxTargeting.Acquire(origin), target), "turret acquires a visible hostile NPC within the same update");
        PhalanxWorld.Enabled = true; PhalanxWorld.Ammo = 0;
        string ammunition = string.Join(";", player.inventory.Select(i => i.type + ":" + i.stack));
        long before = guns[0].Shots;
        for (int tick = 0; tick < 600; tick++) { guns[0].Update(); ClearRounds(); }
        Check(guns[0].Shots - before == 750, "one turret emits exactly 750 real rounds in 600 game ticks: 4500 RPM");
        long[] counters = guns.Select(g => g.Shots).ToArray();
        for (int tick = 0; tick < 60; tick++) { foreach (PhalanxEntity gun in guns) gun.Update(); ClearRounds(); }
        Check(guns.Select((g, i) => g.Shots - counters[i]).All(n => n == 75), "all six turrets independently sustain 75 rounds per second");
        Check(ammunition == string.Join(";", player.inventory.Select(i => i.type + ":" + i.stack)), "automatic fire consumes no inventory ammunition or other items");
        PhalanxWorld.Enabled = false; before = guns[0].Shots;
        for (int tick = 0; tick < 60; tick++) guns[0].Update();
        Check(guns[0].Shots == before && guns[0].Target == -1, "power-off immediately stops new rounds and clears the target");
        PhalanxWorld.Enabled = true; guns[0].Update();
        Projectile ap = Main.projectile.First(p => p.active && p.ModProjectile is PhalanxRound);
        Check(ap.penetrate == 6 && ap.ArmorPenetration == 100 && ap.ai[0] == 0, "APDS uses real six-target penetration and 100 armor penetration");
        PhalanxWorld.Ammo = 1; guns[0].Update();
        Check(ap.ai[0] == 0 && Main.projectile.Any(p => p.active && p.ModProjectile is PhalanxRound && p.ai[0] == 1 && p.penetrate == 1), "switching ammunition changes new rounds while existing AP rounds keep their type");
        ClearRounds(); PhalanxWorld.Ammo = 0;

        target.active = false;
        NPC replacement = Spawn(NPCID.EyeofCthulhu, origin + new Vector2(-300, -30));
        guns[0].Update(); Check(guns[0].Target == replacement.whoAmI, "a despawned target is replaced on the next game tick");
        Vector2 lead = PhalanxTargeting.Aim(origin, replacement); replacement.velocity = new Vector2(0, 15);
        Check(PhalanxTargeting.Aim(origin, replacement).Y > lead.Y, "aim leads a moving target instead of shooting only its old position");
        ClearNPCs();
        NPC guide = Spawn(NPCID.Guide, origin + new Vector2(30, -20)); guide.friendly = guide.townNPC = true;
        NPC clone = Spawn(NPCID.CultistBossClone, origin + new Vector2(60, -20));
        NPC eye = Spawn(NPCID.MoonLordFreeEye, origin + new Vector2(70, -20));
        Check(PhalanxTargeting.Acquire(origin) == null, "town NPCs, cultist clones and invulnerable free eyes are never selected");
        ClearNPCs();
        NPC real = Spawn(NPCID.CultistBoss, origin + new Vector2(400, -30));
        Spawn(NPCID.CultistBossClone, origin + new Vector2(50, -20));
        Check(ReferenceEquals(PhalanxTargeting.Acquire(origin), real), "cultist fight selects the real boss despite a nearer clone");
        ClearNPCs();
        NPC core = Spawn(NPCID.MoonLordCore, origin + new Vector2(500, -30)); core.dontTakeDamage = true;
        NPC hand = Spawn(NPCID.MoonLordHand, origin + new Vector2(300, -30));
        Check(ReferenceEquals(PhalanxTargeting.Acquire(origin), hand), "closed moon-lord core is skipped in favor of an exposed eye");
        core.dontTakeDamage = false;
        Check(ReferenceEquals(PhalanxTargeting.Acquire(origin), core), "exposed moon-lord heart becomes the first target");
        ClearNPCs();
        NPC head = Spawn(NPCID.SolarCrawltipedeHead, origin + new Vector2(40, -20));
        NPC tail = Spawn(NPCID.SolarCrawltipedeTail, origin + new Vector2(300, -20));
        Check(ReferenceEquals(PhalanxTargeting.Acquire(origin), tail), "solar crawltipede is engaged through its vulnerable tail");
        ClearNPCs();
        NPC green = Spawn(NPCID.Spazmatism, origin + new Vector2(600, -20));
        Spawn(NPCID.Retinazer, origin + new Vector2(50, -20));
        Check(ReferenceEquals(PhalanxTargeting.Acquire(origin), green), "Twins are focused in sequence with Spazmatism first");
        ClearNPCs();
        foreach ((int boss, int priority) in new[] { (NPCID.SkeletronHead, NPCID.SkeletronHand), (NPCID.Plantera, NPCID.PlanterasTentacle), (NPCID.SkeletronPrime, NPCID.PrimeLaser) })
        {
            Spawn(boss, origin + new Vector2(90, -20)); NPC part = Spawn(priority, origin + new Vector2(450, -20));
            Check(ReferenceEquals(PhalanxTargeting.Acquire(origin), part), "boss part priority is applied for NPC " + boss); ClearNPCs();
        }
        Pillars(origin);
        CollisionAndDamage(origin);

        PhalanxWorld.Enabled = false;
        Check(PhalanxWorld.Command(player, 0) && PhalanxWorld.Enabled, "remote power command is accepted for a real equipped controller");
        Check(!PhalanxWorld.Command(player, 0), "remote requests are rate limited");
        Player second = Player(1); second.inventory[0] = new Item(ModContent.ItemType<PhalanxRemote>());
        byte mode = PhalanxWorld.Ammo;
        Check(PhalanxWorld.Command(second, 1) && PhalanxWorld.Ammo != mode, "remote ammunition command cycles the whole installation");
        Check(!PhalanxWorld.Command(Player(2), 0) && !PhalanxWorld.Command(second, 255), "missing controllers and invalid network operations are rejected");
        PhalanxWorld.Enabled = false; PhalanxWorld.Ammo = 1;
        TagCompound tag = new(); ModContent.GetInstance<PhalanxWorld>().SaveWorldData(tag);
        PhalanxWorld.Enabled = true; PhalanxWorld.Ammo = 0;
        ModContent.GetInstance<PhalanxWorld>().LoadWorldData(tag);
        Check(!PhalanxWorld.Enabled && PhalanxWorld.Ammo == 1 && PhalanxWorld.Arenas.Count == 1, "world settings and the arena position survive save tags");
        using MemoryStream memory = new(); using BinaryWriter writer = new(memory, System.Text.Encoding.UTF8, true);
        ModContent.GetInstance<PhalanxWorld>().NetSend(writer); memory.Position = 0;
        using BinaryReader reader = new(memory); ModContent.GetInstance<PhalanxWorld>().NetReceive(reader);
        Check(!PhalanxWorld.Enabled && PhalanxWorld.Ammo == 1, "world synchronization carries power, ammunition and arena layout");
        PlayerFileData file = new(Path.Combine(Main.PlayerPath, "CIWSTester.plr"), false) { Player = player, Metadata = FileMetadata.FromCurrentSettings(FileType.Player) };
        typeof(Terraria.Player).GetMethod("InternalSavePlayerFile", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { file });
        Check(File.Exists(file.Path), "native player file saves the equipped remote");
    }
    private void Pillars(Vector2 origin)
    {
        ClearNPCs();
        NPC vortex = Spawn(NPCID.LunarTowerVortex, origin + new Vector2(600, -160));
        NPC stardust = Spawn(NPCID.LunarTowerStardust, origin + new Vector2(400, -160));
        NPC solar = Spawn(NPCID.LunarTowerSolar, origin + new Vector2(200, -160));
        NPC nebula = Spawn(NPCID.LunarTowerNebula, origin + new Vector2(100, -160));
        NPC.ShieldStrengthTowerVortex = NPC.ShieldStrengthTowerStardust = NPC.ShieldStrengthTowerSolar = NPC.ShieldStrengthTowerNebula = 100;
        Check(PhalanxTargeting.PriorityPillar(origin) == vortex && PhalanxTargeting.Acquire(origin) == null, "four shielded pillars are never shot; Vortex is the first strategic objective");
        foreach ((NPC pillar, int mobType) in new[] { (vortex, (int)NPCID.VortexRifleman), (stardust, (int)NPCID.StardustCellBig), (solar, (int)NPCID.SolarCorite), (nebula, (int)NPCID.NebulaBeast) })
        {
            NPC mob = Spawn(mobType, origin + new Vector2(300, -30));
            Spawn(NPCID.StardustCellSmall, origin + new Vector2(15, -20));
            Check(PhalanxTargeting.Acquire(origin) == mob, "pillar " + pillar.type + " selects a shield-credit enemy before non-credit small cells");
            switch (pillar.type)
            {
                case NPCID.LunarTowerVortex: NPC.ShieldStrengthTowerVortex = 0; break;
                case NPCID.LunarTowerStardust: NPC.ShieldStrengthTowerStardust = 0; break;
                case NPCID.LunarTowerSolar: NPC.ShieldStrengthTowerSolar = 0; break;
                case NPCID.LunarTowerNebula: NPC.ShieldStrengthTowerNebula = 0; break;
            }
            Check(PhalanxTargeting.Acquire(origin) == pillar, "pillar " + pillar.type + " is focused as soon as its shield is down");
            pillar.active = mob.active = false;
            foreach (NPC npc in Main.npc) if (npc.type == NPCID.StardustCellSmall) npc.active = false;
        }
        Check(PhalanxTargeting.PriorityPillar(origin) == null, "four-pillar order advances through all four completed objectives");
        ClearNPCs();
    }
    private void CollisionAndDamage(Vector2 origin)
    {
        ClearNPCs(); ClearRounds();
        NPC target = Spawn(NPCID.BlueSlime, origin + new Vector2(220, 0));
        target.life = target.lifeMax = 5000000; target.defense = 84;
        int id = Projectile.NewProjectile(new EntitySource_Misc("CIWSCollision"), origin, new Vector2(100 / 8f, 0), ModContent.ProjectileType<PhalanxRound>(), 18000, 0, 0);
        for (int i = 0; i < 8 && Main.projectile[id].active; i++) Main.projectile[id].Update(id);
        Check(target.life < target.lifeMax - 15000, "APDS deals native collision damage to an armored hostile target");
        ClearRounds();
        int wallX = (int)(origin.X / 16) + 5, wallY = (int)(origin.Y / 16);
        for (int y = wallY - 8; y <= wallY + 8; y++) { Tile tile = Main.tile[wallX, y]; tile.HasTile = true; tile.TileType = TileID.Stone; }
        Check(PhalanxTargeting.Acquire(origin) == null, "solid cover blocks target acquisition");
        id = Projectile.NewProjectile(new EntitySource_Misc("CIWSWall"), origin, new Vector2(100 / 8f, 0), ModContent.ProjectileType<PhalanxRound>(), 14000, 0, 0, 1);
        for (int i = 0; i < 8 && Main.projectile[id].active; i++) Main.projectile[id].Update(id);
        Check(!Main.projectile[id].active && Main.projectile.Any(p => p.active && p.ModProjectile is PhalanxBlast), "HEI-T detonates at solid cover instead of tunneling through it");
        Check(Enumerable.Range(wallY - 8, 17).All(y => Main.tile[wallX, y].HasTile && Main.tile[wallX, y].TileType == TileID.Stone), "explosions preserve every block in the test wall");
        for (int y = wallY - 8; y <= wallY + 8; y++) Main.tile[wallX, y].ClearTile();
        ClearRounds(); target.Center = origin + new Vector2(40, -8); target.life = target.lifeMax = 5000000;
        NPC adjacent = Spawn(NPCID.BlueSlime, origin + new Vector2(75, -8)); adjacent.life = adjacent.lifeMax = 5000000;
        NPC guide = Spawn(NPCID.Guide, origin + new Vector2(50, -8)); guide.friendly = guide.townNPC = true;
        id = Projectile.NewProjectile(new EntitySource_Misc("CIWSBlast"), target.Center, Vector2.Zero, ModContent.ProjectileType<PhalanxBlast>(), 8400, 0, 0);
        Main.projectile[id].Update(id);
        Check(target.life < target.lifeMax && adjacent.life < adjacent.lifeMax && guide.life == guide.lifeMax, "HE blast damages multiple hostile NPCs and preserves a nearby town NPC");
        ClearRounds(); ClearNPCs();
    }
    private void Reload()
    {
        Check(PhalanxWorld.Arenas.Count == 1 && !PhalanxWorld.Enabled && PhalanxWorld.Ammo == 1, "new server process restores arena, power-off and HE ammunition");
        Check(TileEntity.ByID.Values.Count(t => t is PhalanxEntity) == 6, "new process restores all six real turret tile entities");
        Point16 arena = PhalanxWorld.Arenas[0]; int chest = PhalanxArena.ControlChest(arena);
        Check(chest >= 0 && Main.chest[chest].item.Any(i => i.ModItem is PhalanxTurret && i.stack == 8), "control locker and spare turrets survive real world-file reload");
        Player saved = Terraria.Player.LoadPlayer(Path.Combine(Main.PlayerPath, "CIWSTester.plr"), false).Player;
        Check(saved.inventory[9].ModItem is PhalanxRemote, "native player-file reload retains the controller in hotbar 10");
    }
}
