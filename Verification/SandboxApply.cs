using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.IO;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ModernArsenal.Verification;

public sealed class SandboxApply : ModSystem
{
    private int ticks;
    private bool applied;
    private string[] originals;
    public override bool IsLoadingEnabled(Mod mod) => !Main.dedServ && (Program.LaunchParameters.ContainsKey("-arsenalsandbox") || Program.LaunchParameters.ContainsKey("-arsenalhandoff"));
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
    public override void PostUpdateEverything()
    {
        if (Main.gameMenu || applied) return;
        Player player = Main.LocalPlayer;
        if (Program.LaunchParameters.TryGetValue("-arsenalhandoff", out string handoff))
        {
            player.GetModPlayer<ArsenalSandboxPlayer>().GodEnabled = true;
            player.creativeGodMode = true;
            Main.autoPause = true; Main.playerInventory = true;
            Player.SavePlayer(Main.ActivePlayerFileData);
            applied = true;
            File.WriteAllText(handoff, JsonSerializer.Serialize(new { success = true, version = Mod.Version.ToString(),
                god = true, paused = Main.autoPause && Main.playerInventory, player = player.name,
                guns = player.inventory.Where(i => i.ModItem is ArsenalGun).Select(i => new { id = i.ModItem.Name, damage = i.damage, rounds = ((ArsenalGun)i.ModItem).Rounds }).ToArray() }, new JsonSerializerOptions { WriteIndented = true }));
            return;
        }
        try
        {
            ticks++;
            if (ticks == 30)
            {
                originals = player.inventory.Take(50).Where(i => !i.IsAir && i.ModItem is not ArsenalGun && i.ModItem is not ArsenalAmmo)
                    .Select(i => $"{i.type}:{i.stack}:{i.prefix}").ToArray();
                player.GetModPlayer<ArsenalSandboxPlayer>().GodEnabled = true;
                Check(ArsenalCommand.EquipAll(player), "Not enough free inventory space for the arsenal");
            }
            if (ticks != 60) return;
            Check(player.GetModPlayer<ArsenalSandboxPlayer>().GodEnabled && player.creativeGodMode, "Native god mode is not active");
            int life = player.statLife;
            player.immune = false;
            player.Hurt(PlayerDeathReason.ByCustomReason(NetworkText.FromLiteral("Sandbox invincibility verification")), 99999, 0);
            Check(!player.dead && player.statLife == life, "God mode failed native damage verification");
            foreach (string item in originals)
                Check(player.inventory.Take(50).Any(i => $"{i.type}:{i.stack}:{i.prefix}" == item), "An original inventory item was lost");
            foreach (GunSpec spec in Catalog.Data.Guns)
                Check(player.inventory.Take(50).Any(i => i.ModItem is ArsenalGun gun && gun.Spec.Id == spec.Id && gun.Rounds == spec.Magazine), "A loaded gun is missing: " + spec.Id);
            foreach (AmmoSpec ammo in Catalog.Data.Ammo)
                Check(ArsenalPlayer.InventorySlots.Sum(i => player.inventory[i].type == ammo.Type ? player.inventory[i].stack : 0) >= 600, "Ammo is missing: " + ammo.Id);
            Player.SavePlayer(Main.ActivePlayerFileData);
            Player restored = Player.LoadPlayer(Main.ActivePlayerFileData.Path, false).Player;
            Check(restored.GetModPlayer<ArsenalSandboxPlayer>().GodEnabled && Catalog.Data.Guns.All(spec => restored.inventory.Take(50).Any(i => i.type == spec.Type)), "Saved character did not retain god mode and every gun");
            Main.NewText("无敌已开启并保存；24 把枪已装满，19 种弹药各至少 600 发。", Color.LightGreen);
            Main.playerInventory = true;
            applied = true;
            Write(new { success = true, player = player.name, god = true, nativeDamageBlocked = true, savedAndRestored = true,
                guns = Catalog.Data.Guns.Select(g => g.Id).ToArray(), ammoTypes = Catalog.Data.Ammo.Length, originalItemsPreserved = originals.Length });
        }
        catch (Exception exception) { applied = true; Write(new { success = false, error = exception.ToString() }); }
    }
    private static void Write(object report) => File.WriteAllText(Program.LaunchParameters["-arsenalsandbox"], JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
}
