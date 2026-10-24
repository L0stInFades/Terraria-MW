using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace ModernArsenal.Verification;

public sealed class MillionHealthAudit : ModSystem
{
    public override bool IsLoadingEnabled(Mod mod) => Program.LaunchParameters.ContainsKey("-arsenalmillion");
    public override void PostWorldLoad()
    {
        if (!Main.dedServ) return;
        string path = Program.LaunchParameters["-arsenalmillion"];
        try
        {
            Main.netMode = NetmodeID.SinglePlayer; Main.myPlayer = 0; Main.gameMenu = false;
            Main.GameViewMatrix ??= new Terraria.Graphics.SpriteViewMatrix(null);
            List<object> results = new();
            results.Add(Measure("Vector", 95));
            results.Add(Measure("BarrettM82", 2450));
            foreach (GunSpec gun in Catalog.Data.Guns.Where(g => g.Tier >= 3)) results.Add(Measure(gun.Id));
            File.WriteAllText(path, JsonSerializer.Serialize(new { success = true, initialHealth = 1005000, defense = 84,
                distancePixels = 600, intentionalMisses = "Every second shot aims 1800 pixels below the target; every shot still consumes magazine ammunition.",
                note = "Native ItemCheck, projectile movement, armor, range falloff, critical hits, collision and every reload. Full-health stationary calibration target; boss movement and phase invulnerability are not simulated.", results }, new JsonSerializerOptions { WriteIndented = true }));
            Environment.Exit(0);
        }
        catch (Exception exception)
        {
            File.WriteAllText(path, JsonSerializer.Serialize(new { success = false, error = exception.ToString() }));
            Environment.Exit(1);
        }
    }
    private static object Measure(string id, int oldDamage = 0)
    {
        foreach (NPC npc in Main.npc) npc.active = false;
        foreach (Projectile projectile in Main.projectile) projectile.active = false;
        Player player = new() { name = "MillionHealthAudit", whoAmI = 0, active = true, statLife = 100, statLifeMax = 100 };
        Main.player[0] = player;
        player.Center = new Vector2(Main.spawnTileX * 16, 800);
        Main.screenPosition = player.Center - new Vector2(500, 400);
        player.selectedItem = 0; player.releaseUseItem = true; player.autoReuseAllWeapons = true;
        GunSpec spec = Catalog.Data.Guns.Single(g => g.Id == id);
        player.inventory[0] = new Item(spec.Type);
        ArsenalGun gun = (ArsenalGun)player.HeldItem.ModItem;
        AmmoSpec ammo = Catalog.Data.Ammo.First(a => a.Caliber == spec.Caliber);
        player.inventory[54] = new Item(ammo.Type, 9999);
        gun.LoadedAmmo = ammo.Type; gun.Rounds = spec.Magazine;
        if (oldDamage > 0) player.HeldItem.damage = oldDamage;
        int npcId = NPC.NewNPC(new EntitySource_Misc("FullHealthCalibration"), (int)player.Center.X + 600, (int)player.Center.Y, NPCID.SkeletronHead);
        NPC target = Main.npc[npcId];
        target.Center = player.Center + new Vector2(600, 0);
        target.life = target.lifeMax = 1005000; target.defense = target.defDefense = 84;
        target.aiStyle = -1; target.noGravity = true; target.dontTakeDamage = target.immortal = false;
        target.velocity = Vector2.Zero;
        int shots = 0, reloadFrames = 0, maxPayload = 0, tick = 0;
        double maxRestoredDamage = 0;
        ArsenalPlayer state = player.GetModPlayer<ArsenalPlayer>();
        int limit = oldDamage > 0 ? 1800 : 3600;
        for (; tick < limit && target.active && target.life > 0; tick++)
        {
            Main.mouseX = 1100; Main.mouseY = 400 + (shots % 2 == 0 ? 0 : 1800);
            int rounds = gun.Rounds;
            player.controlUseItem = player.channel = true;
            player.ItemCheck();
            if (gun.Rounds < rounds) shots += rounds - gun.Rounds;
            foreach (Projectile projectile in Main.projectile)
            {
                if (!projectile.active || projectile.ModProjectile is not BallisticRound round) continue;
                maxPayload = Math.Max(maxPayload, projectile.damage);
                maxRestoredDamage = Math.Max(maxRestoredDamage, projectile.damage * round.DamageScale);
                Main.netMode = NetmodeID.Server;
                projectile.Update(projectile.whoAmI);
                Main.netMode = NetmodeID.SinglePlayer;
            }
            if (state.ReloadTicks > 0) reloadFrames++;
            state.PostUpdate();
        }
        int damage = 1005000 - Math.Max(0, target.life);
        bool killed = !target.active || target.life <= 0;
        if (damage <= 0 || maxPayload > 30000) throw new InvalidOperationException(id + " has invalid native damage or network payload");
        if (oldDamage == 0 && (!killed || tick > (id == "BarrettM82" ? 1500 : 3600)))
            throw new InvalidOperationException(id + " failed the full-health, armored, half-miss benchmark: " + damage + " damage in " + tick + " ticks");
        return new { id, version = oldDamage > 0 ? "before" : "1.2.0", weaponDamage = player.HeldItem.damage, ammo = ammo.Id,
            killed, seconds = Math.Round(tick / 60.0, 2), shots, reloadSeconds = Math.Round(reloadFrames / 60.0, 2),
            effectiveDps = Math.Round(damage * 60.0 / tick, 2), damage, maxPayload, maxRestoredDamage };
    }
}
