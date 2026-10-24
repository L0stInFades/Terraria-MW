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

public sealed class ArsenalSelfTest : ModSystem
{
    private readonly List<string> passed = new();
    public override bool IsLoadingEnabled(Mod mod) => new[] { "-arsenaltest", "-arsenalreload", "-arsenalprepare", "-arsenalcompat" }.Any(Program.LaunchParameters.ContainsKey);
    private void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); passed.Add(message); }
    public override void PostWorldLoad()
    {
        if (!Main.dedServ) return;
        string flag = new[] { "-arsenaltest", "-arsenalreload", "-arsenalprepare", "-arsenalcompat" }.First(Program.LaunchParameters.ContainsKey);
        string report = Program.LaunchParameters[flag];
        try
        {
            Main.myPlayer = 0;
            if (flag == "-arsenalreload") Reload();
            else if (flag == "-arsenalcompat") Compatibility();
            else if (flag == "-arsenalprepare") Prepare();
            else Run();
            WorldFile.SaveWorld();
            File.WriteAllText(report, JsonSerializer.Serialize(new { success = true, checks = passed, world = Main.worldName, crate = new { SupplyWorld.Home.X, SupplyWorld.Home.Y } }, new JsonSerializerOptions { WriteIndented = true }));
            Environment.Exit(0);
        }
        catch (Exception exception)
        {
            File.WriteAllText(report, JsonSerializer.Serialize(new { success = false, checks = passed, error = exception.ToString() }, new JsonSerializerOptions { WriteIndented = true }));
            Environment.Exit(1);
        }
    }
    private Player NewPlayer(string name)
    {
        Player player = new() { name = name, whoAmI = 0, active = true, statLife = 100, statLifeMax = 100, statMana = 20, statManaMax = 20 };
        Main.player[0] = player;
        player.Bottom = new Vector2((Main.spawnTileX + 2) * 16, Main.spawnTileY * 16);
        return player;
    }
    private void MakeHome()
    {
        int x = Main.spawnTileX - 18, floor = Main.spawnTileY + 3;
        for (int dx = 0; dx <= 36; dx++)
            for (int dy = -12; dy <= 2; dy++)
            {
                Tile tile = Main.tile[x + dx, floor + dy];
                tile.ClearEverything();
                if (dy == 0 || dy == -12 || dx == 0 || dx == 36)
                { tile.HasTile = true; tile.TileType = TileID.WoodBlock; }
                else if (dy < 0) tile.WallType = WallID.Wood;
            }
        for (int dy = -3; dy < 0; dy++) Main.tile[x + 36, floor + dy].ClearTile();
        WorldGen.PlaceTile(x + 36, floor - 1, TileID.ClosedDoor);
        WorldGen.PlaceTile(x + 7, floor - 4, TileID.Torches);
        WorldGen.PlaceObject(x + 13, floor - 1, TileID.WorkBenches);
        WorldGen.PlaceTile(x + 16, floor - 1, TileID.Chairs);
        WorldGen.PlaceObject(x + 23, floor - 1, TileID.Beds);
        SupplyWorld.Home = Point.Zero;
        SupplyWorld.EnsureCrate();
        Check(SupplyWorld.Exists(SupplyWorld.Home.X, SupplyWorld.Home.Y), "automatic supply crate is physically placed");
        Check(Main.tile[SupplyWorld.Home.X, SupplyWorld.Home.Y].WallType == WallID.Wood, "automatic crate prefers the actual house interior");
    }
    private void Prepare()
    {
        MakeHome();
        Player player = NewPlayer("新兵");
        player.inventory[0] = ArsenalPlayer.Starter().First();
        player.inventory[1] = new Item(ItemID.CopperPickaxe);
        player.inventory[2] = new Item(ItemID.CopperAxe);
        player.inventory[3] = new Item(ItemID.Wood, 200);
        player.inventory[54] = ArsenalPlayer.Starter().Last();
        player.GetModPlayer<ArsenalPlayer>().LoadData(new TagCompound { ["starterGranted"] = true });
        if (!File.Exists(Path.Combine(Main.PlayerPath, player.name + ".plr"))) SavePlayer(player);
        Check(File.Exists(Path.Combine(Main.PlayerPath, player.name + ".plr")), "playable starter player saved in its separate profile");
    }
    private void Run()
    {
        int previousMode = Main.netMode;
        Main.netMode = NetmodeID.SinglePlayer;
        Main.gameMenu = false;
        try
        {
            Check(Catalog.Data.Guns.Length == 24 && Catalog.Data.Ammo.Length == 19 && Catalog.Data.Ammo.Select(a => a.Caliber).Distinct().Count() == 10, "24 weapons, 19 ammo variants, 10 calibers");
            foreach (GunSpec spec in Catalog.Data.Guns)
            {
                Item item = new(spec.Type);
                Check(item.ModItem is ArsenalGun gun && ReferenceEquals(gun.Spec, spec) && item.damage == spec.Damage &&
                    (Mod.FileExists("Assets/Guns/" + spec.Id + ".png") || Mod.FileExists("Assets/Guns/" + spec.Id + ".rawimg")), spec.Id + " has working native registration, stats and sprite");
            }
            Check(!Main.recipe.Take(Recipe.numRecipes).Any(r => Catalog.Data.Ammo.Any(a => a.Type == r.createItem.type)), "all ammunition has no crafting recipe");
            Player player = NewPlayer("ArsenalTester");
            ArsenalPlayer state = player.GetModPlayer<ArsenalPlayer>();
            Item[] starter = state.AddStartingItems(false).ToArray();
            Check(starter.Length == 2 && starter[0].ModItem is ArsenalGun g && g.Spec.Id == "USP45" && g.Rounds == 12 && starter[1].stack == 600, "real new-player hook grants a loaded USP and 600 rounds");
            Check(!state.AddStartingItems(true).Any(), "mediumcore death does not mint duplicate starter guns");
            player.inventory[0] = starter[0]; player.inventory[54] = starter[1]; player.selectedItem = 0;
            ArsenalGun usp = (ArsenalGun)player.HeldItem.ModItem;
            int fmj = usp.LoadedAmmo;
            Check(usp.Item.damage + Catalog.Data.Ammo.First(a => a.Type == fmj).Damage >= 1000, "starter USP uses the globally increased damage before equipment bonuses");
            usp.Rounds = 2;
            Check(state.BeginReload() && state.ReloadTicks == 65 && player.inventory[54].stack == 600, "reload starts a real timer without consuming ammunition early");
            for (int i = 0; i < 65; i++) state.PostUpdate();
            Check(usp.Rounds == 12 && player.inventory[54].stack == 590 && state.ReloadTicks == 0, "completed reload transfers exactly ten rounds");
            usp.Rounds = 1; state.BeginReload(); player.selectedItem = 1; state.PostUpdate();
            Check(state.ReloadTicks == 0 && usp.Rounds == 1 && player.inventory[54].stack == 590, "weapon switching cancels reload with no ammunition loss");
            player.selectedItem = 0;
            int jhp = Catalog.Data.Ammo.First(a => a.Id == "FortyFiveJHP").Type;
            player.inventory[54] = new Item(jhp, 20); player.inventory[4] = new Item(fmj, 4);
            Check(state.Reserve(usp).type == fmj, "tactical reload preserves the loaded ammunition type");
            usp.Rounds = 0;
            Check(state.Reserve(usp).type == jhp, "empty magazine selects ammunition slots before inventory");
            state.BeginReload(); state.FinishReload();
            Check(usp.Rounds == 12 && usp.LoadedAmmo == jhp && player.inventory[54].stack == 8, "reload switches to priority JHP only when the magazine is empty");
            usp.Rounds = 0; player.inventory[54] = new Item(jhp, 3); player.inventory[4].TurnToAir();
            state.BeginReload(); state.FinishReload();
            Check(usp.Rounds == 3 && player.inventory[54].IsAir, "short reserves produce a partial magazine without negative stacks");
            usp.Rounds = 0;
            Check(!state.BeginReload() && !usp.CanUseItem(player), "empty gun with no compatible ammunition cannot shoot");

            MakeHome();
            Point home = SupplyWorld.Home;
            SupplyWorld.EnsureCrate();
            Check(SupplyWorld.Home == home, "rechecking a home does not duplicate or move its crate");
            player.Center = home.ToVector2() * 16;
            NPC.downedBoss1 = NPC.downedBoss2 = NPC.downedBoss3 = NPC.downedMechBossAny = NPC.downedPlantBoss = Main.hardMode = false;
            Check(SupplyService.Claim(player, home.X, home.Y, true, 23) == 2, "supply rejects a locked Barrett at the grant boundary");
            state.LastClaim = 0;
            Check(SupplyService.Claim(player, home.X, home.Y, false, 2) == 1 && Count(player, fmj) == 600, "home crate grants 600 rounds free without crafting");
            Check(SupplyService.Claim(player, -10, -10, false, 2) == 4, "out-of-world and remote supply requests are rejected safely");
            state.LastClaim = 0;
            Check(SupplyService.Claim(player, home.X, home.Y, false, 255) == 0, "invalid content indices are rejected");
            foreach (int slot in ArsenalPlayer.InventorySlots) player.inventory[slot] = new Item(ItemID.DirtBlock, 9999);
            string full = string.Join(";", player.inventory.Select(i => i.type + ":" + i.stack));
            Check(SupplyService.Grant(player, fmj, 600) == 0 && full == string.Join(";", player.inventory.Select(i => i.type + ":" + i.stack)), "full inventory is preserved exactly without overwrites");
            foreach (Item item in player.inventory) item.TurnToAir();
            player.inventory[0] = new Item(Catalog.Data.Guns[0].Type);
            usp = (ArsenalGun)player.HeldItem.ModItem;
            usp.LoadedAmmo = fmj; usp.Rounds = 12;
            Ballistics(player, usp);
            foreach (GunSpec spec in Catalog.Data.Guns)
            {
                ArsenalGun weapon = (ArsenalGun)new Item(spec.Type).ModItem;
                weapon.Rounds = Math.Min(5, spec.Magazine);
                weapon.LoadedAmmo = Catalog.Data.Ammo.First(a => a.Caliber == spec.Caliber).Type;
                using MemoryStream stream = new(); weapon.NetSend(new BinaryWriter(stream)); stream.Position = 0;
                ArsenalGun copy = (ArsenalGun)new Item(spec.Type).ModItem; copy.NetReceive(new BinaryReader(stream));
                Check(copy.Rounds == weapon.Rounds && copy.LoadedAmmo == weapon.LoadedAmmo, spec.Id + " magazine survives network serialization");
            }
            usp.Rounds = 5; usp.LoadedAmmo = jhp;
            SavePlayer(player);
            Check(File.Exists(Path.Combine(Main.PlayerPath, player.name + ".plr")), "native player file with a partial JHP magazine is saved");
        }
        finally { Main.netMode = previousMode; }
    }
    private void Ballistics(Player player, ArsenalGun usp)
    {
        Main.netMode = NetmodeID.Server;
        int x = Main.spawnTileX + 65, y = Main.spawnTileY - 25;
        for (int dx = -5; dx < 80; dx++) for (int dy = -10; dy < 15; dy++) Main.tile[x + dx, y + dy].ClearEverything();
        player.Center = new Vector2(x * 16, y * 16);
        int npcIndex = NPC.NewNPC(new EntitySource_Misc("ArsenalAcceptance"), (x + 12) * 16, y * 16, NPCID.BlueSlime);
        NPC target = Main.npc[npcIndex]; target.Center = new Vector2((x + 12) * 16, y * 16); target.noGravity = true;
        EntitySource_ItemUse_WithAmmo source = new(player, usp.Item, usp.LoadedAmmo);
        int before = usp.Rounds;
        usp.Shoot(player, source, player.Center, new Vector2(26, 0), ModContent.ProjectileType<BallisticRound>(), player.GetWeaponDamage(usp.Item), 3);
        Projectile bullet = Main.projectile.First(p => p.active && p.ModProjectile is BallisticRound);
        // Align the small fixture with the emitted ray; the held muzzle is above the player's center.
        target.Center = bullet.Center + bullet.velocity.SafeNormalize(Vector2.UnitX) * 160;
        for (int tick = 0; tick < 30 && bullet.active; tick++) bullet.Update(bullet.whoAmI);
        Check(usp.Rounds == before - 1 && (!target.active || target.life <= 0), "actual USP shot consumes one magazine round and kills an early slime through native collision and damage");
        foreach (Projectile p in Main.projectile) p.active = false;
        int apIndex = Array.FindIndex(Catalog.Data.Ammo, a => a.Id == "Bmg50AP");
        int id = Projectile.NewProjectile(source, player.Center, new Vector2(58f / BallisticRound.Updates, 0), ModContent.ProjectileType<BallisticRound>(), 668, 10, 0, apIndex);
        bullet = Main.projectile[id];
        Check(bullet.ArmorPenetration == 60 && bullet.penetrate == 5 && bullet.extraUpdates == 7, "AP projectile has real armor penetration, bounded target penetration and eight collision updates");
        float initialY = bullet.velocity.Y;
        for (int tick = 0; tick < 5 && bullet.active; tick++) bullet.Update(id);
        Check(bullet.velocity.Y > initialY && bullet.velocity.X < 58f / BallisticRound.Updates, "native projectile updates apply gravity and drag");
        bullet.active = false;
        for (int dy = -8; dy <= 8; dy++) { Tile tile = Main.tile[x + 8, y + dy]; tile.HasTile = true; tile.TileType = TileID.Stone; }
        id = Projectile.NewProjectile(source, player.Center, new Vector2(58f / BallisticRound.Updates, 0), ModContent.ProjectileType<BallisticRound>(), 668, 10, 0, apIndex);
        bullet = Main.projectile[id];
        for (int tick = 0; tick < 25 && bullet.active; tick++) bullet.Update(id);
        Check(!bullet.active && bullet.Center.X < (x + 10) * 16, "fast AP round stops at a one-tile stone wall instead of tunneling through");
        for (int dy = -8; dy <= 8; dy++) Main.tile[x + 8, y + dy].ClearTile();
        foreach (int npcType in new[] { NPCID.SkeletronHead, NPCID.ArmoredSkeleton })
        {
            foreach (NPC npc in Main.npc) npc.active = false;
            npcIndex = NPC.NewNPC(new EntitySource_Misc("ArsenalDamageCalibration"), (x + 12) * 16, y * 16, npcType);
            target = Main.npc[npcIndex]; target.Center = new Vector2((x + 12) * 16, y * 16);
            target.noGravity = true;
            int shots = 0;
            while (target.active && target.life > 0 && shots < 90)
            {
                usp.Rounds = 12;
                usp.Shoot(player, source, player.Center, new Vector2(26, 0), ModContent.ProjectileType<BallisticRound>(), player.GetWeaponDamage(usp.Item), 3);
                bullet = Main.projectile.First(p => p.active && p.ModProjectile is BallisticRound);
                for (int tick = 0; tick < 25 && bullet.active; tick++) bullet.Update(bullet.whoAmI);
                shots++;
            }
            float seconds = (shots * 14 + Math.Max(0, (shots - 1) / 12) * 65) / 60f;
            Check(!target.active && (npcType == NPCID.SkeletronHead ? seconds < 30 : shots <= 12),
                npcType == NPCID.SkeletronHead ? $"unmodified USP damage defeats native stationary Skeletron in {shots} rounds / {seconds:0.0}s firing-and-reload budget" :
                $"unmodified USP damage defeats a native hardmode Armored Skeleton within one magazine ({shots} rounds)");
        }
        Main.netMode = NetmodeID.SinglePlayer;
    }
    private void Reload()
    {
        Player player = Player.LoadPlayer(Path.Combine(Main.PlayerPath, "ArsenalTester.plr"), false).Player;
        Check(player.inventory[0].ModItem is ArsenalGun gun && gun.Rounds == 5 && gun.Ammo.Id == "FortyFiveJHP", "separate process restores exact gun, partial magazine and JHP identity");
        TagCompound state = new(); player.GetModPlayer<ArsenalPlayer>().SaveData(state);
        Check(state.GetBool("starterGranted"), "starter grant receipt persists, preventing repeat gifts");
        Check(SupplyWorld.Exists(SupplyWorld.Home.X, SupplyWorld.Home.Y), "world file reload restores the home supply crate");
    }
    private void Compatibility()
    {
        Check(ModLoader.TryGetMod("ModernEngineering", out _), "ModernEngineering and ModernArsenal load together");
        SupplyWorld.EnsureCrate();
        Check(SupplyWorld.Exists(SupplyWorld.Home.X, SupplyWorld.Home.Y), "existing engineering world receives an actual supply crate");
        Check(Main.wallHouse[Main.tile[SupplyWorld.Home.X, SupplyWorld.Home.Y].WallType], "engineering home crate is placed against an existing safe interior wall");
    }
    private static int Count(Player p, int type) => p.inventory.Where(i => i.type == type).Sum(i => i.stack);
    private static void SavePlayer(Player player)
    {
        Directory.CreateDirectory(Main.PlayerPath);
        PlayerFileData data = new(Path.Combine(Main.PlayerPath, player.name + ".plr"), false)
        { Player = player, Metadata = FileMetadata.FromCurrentSettings(FileType.Player) };
        typeof(Player).GetMethod("InternalSavePlayerFile", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { data });
    }
}
