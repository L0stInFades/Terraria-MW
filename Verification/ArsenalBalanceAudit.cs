using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ModernArsenal.Verification;

public sealed class ArsenalBalanceAudit : ModSystem
{
    public override bool IsLoadingEnabled(Mod mod) => Program.LaunchParameters.ContainsKey("-arsenalbalance");
    public override void PostWorldLoad()
    {
        if (!Main.dedServ) return;
        string report = Program.LaunchParameters["-arsenalbalance"];
        try
        {
            Main.netMode = NetmodeID.SinglePlayer;
            Main.myPlayer = 0;
            Main.gameMenu = false;
            Main.GameViewMatrix ??= new Terraria.Graphics.SpriteViewMatrix(null);
            Main.mouseX = 1000; Main.mouseY = 400;
            var references = new[] {
                (0, ItemID.Minishark, ItemID.MusketBall), (0, ItemID.Musket, ItemID.MusketBall),
                (1, ItemID.PhoenixBlaster, ItemID.MusketBall), (1, ItemID.Boomstick, ItemID.MusketBall),
                (2, ItemID.StarCannon, ItemID.FallenStar),
                (3, ItemID.Megashark, ItemID.CrystalBullet), (3, ItemID.OnyxBlaster, ItemID.CrystalBullet),
                (3, ItemID.DaedalusStormbow, ItemID.HolyArrow),
                (4, ItemID.ChainGun, ItemID.CrystalBullet), (4, ItemID.TacticalShotgun, ItemID.CrystalBullet),
                (4, ItemID.SniperRifle, ItemID.CrystalBullet),
                (5, ItemID.SDMG, ItemID.MoonlordBullet), (5, ItemID.VortexBeater, ItemID.MoonlordBullet),
                (5, ItemID.Phantasm, ItemID.MoonlordArrow)
            };
            List<object> vanilla = new();
            foreach (var (tier, weapon, ammo) in references)
                vanilla.Add(Measure(new Item(weapon), new Item(ammo, 9999), tier));
            List<object> modern = new();
            foreach (GunSpec gun in Catalog.Data.Guns)
            {
                AmmoSpec ammo = Catalog.Data.Ammo.Where(a => a.Caliber == gun.Caliber && a.Tier <= gun.Tier).OrderByDescending(a => a.Damage).First();
                modern.Add(Measure(new Item(gun.Type), new Item(ammo.Type, 9999), gun.Tier));
            }
            File.WriteAllText(report, JsonSerializer.Serialize(new { success = true, seconds = 30, vanilla, modern,
                note = "Native ItemCheck and held-projectile AI; magazine reloads included. Raw expected damage counts full hits. Crystal fragmentation, Holy Arrow stars, and Phantasm/Luminite secondary hits are an explicit all-hit ceiling derived from the installed Projectile code." }, new JsonSerializerOptions { WriteIndented = true }));
            Environment.Exit(0);
        }
        catch (Exception exception)
        {
            File.WriteAllText(report, JsonSerializer.Serialize(new { success = false, error = exception.ToString() }));
            Environment.Exit(1);
        }
    }
    private static object Measure(Item weapon, Item ammunition, int tier)
    {
        foreach (Projectile projectile in Main.projectile) projectile.active = false;
        Player player = new() { name = "BalanceAudit", whoAmI = 0, active = true, statLife = 100, statLifeMax = 100 };
        Main.player[0] = player;
        player.Center = new Vector2(Main.spawnTileX * 16, (Main.spawnTileY - 10) * 16);
        Main.screenPosition = player.Center - new Vector2(500, 400);
        player.inventory[0] = weapon;
        player.inventory[54] = ammunition;
        player.selectedItem = 0;
        player.releaseUseItem = true;
        player.autoReuseAllWeapons = true;
        ArsenalGun gun = weapon.ModItem as ArsenalGun;
        if (gun != null) { gun.LoadedAmmo = ammunition.type; gun.Rounds = gun.Spec.Magazine; }
        double damage = 0;
        int bullets = 0, reloadFrames = 0;
        const int frames = 1800;
        for (int tick = 0; tick < frames; tick++)
        {
            player.controlUseItem = true;
            player.channel = true;
            player.ItemCheck();
            foreach (Projectile controller in Main.projectile.Where(p => p.active && p.aiStyle == 75).ToArray()) controller.Update(controller.whoAmI);
            foreach (Projectile projectile in Main.projectile)
            {
                if (!projectile.active || projectile.aiStyle == 75) continue;
                double weight = ammunition.type == ItemID.CrystalBullet && projectile.type == ProjectileID.CrystalBullet ? 2.5 :
                    weapon.type == ItemID.Phantasm ? 3.8 : ammunition.type == ItemID.HolyArrow ? 2.0 : 1.0;
                double scale = projectile.ModProjectile is BallisticRound round ? round.DamageScale : 1;
                damage += projectile.damage * scale * weight * (1 + projectile.CritChance / 100.0);
                bullets++;
                projectile.active = false;
            }
            Array.Clear(player.ownedProjectileCounts);
            foreach (Projectile projectile in Main.projectile)
                if (projectile.active && projectile.owner == 0) player.ownedProjectileCounts[projectile.type]++;
            ArsenalPlayer state = player.GetModPlayer<ArsenalPlayer>();
            if (state.ReloadTicks > 0) reloadFrames++;
            state.PostUpdate();
        }
        if (bullets == 0) throw new InvalidOperationException("Native firing loop produced no shots for " + weapon.Name);
        double? cycleDps = null;
        if (gun != null)
        {
            int perProjectile = player.GetWeaponDamage(weapon) + ammunition.damage;
            int pellets = gun.Spec.Pellets;
            if (((ArsenalAmmo)ammunition.ModItem).Spec.Kind == "Slug")
            { perProjectile = (int)(perProjectile * pellets * 0.78f); pellets = 1; }
            int burstPauses = gun.Spec.Burst > 1 ? (int)Math.Ceiling(gun.Spec.Magazine / (double)gun.Spec.Burst) * weapon.reuseDelay : 0;
            cycleDps = Math.Round(perProjectile * pellets * gun.Spec.Magazine * 60.0 * (1 + player.GetWeaponCrit(weapon) / 100.0) /
                (gun.Spec.Magazine * gun.Spec.Interval + burstPauses + gun.Spec.Reload), 2);
        }
        return new { id = weapon.ModItem?.Name ?? weapon.type.ToString(), weapon = weapon.Name, tier, weapon.damage, weapon.useTime, weapon.useAnimation, weapon.reuseDelay,
            crit = player.GetWeaponCrit(weapon), ammo = ammunition.Name, ammoDamage = ammunition.damage,
            magazine = gun?.Spec.Magazine ?? 0, reloadTicks = gun?.Spec.Reload ?? 0, bullets, reloadFrames,
            sustainDps = Math.Round(damage / (frames / 60.0), 2), cycleDps };
    }
}
