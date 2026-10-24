using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Terraria;
using Terraria.DataStructures;
using Terraria.IO;
using Terraria.ModLoader;

namespace ModernArsenal.Verification;

public sealed class PhalanxSetup : ModSystem
{
    private bool finished;
    public override bool IsLoadingEnabled(Mod mod) => !Main.dedServ && Program.LaunchParameters.ContainsKey("-arsenalciwssetup");
    public override void PostUpdateEverything()
    {
        if (Main.gameMenu || finished) return;
        string path = Program.LaunchParameters["-arsenalciwssetup"];
        try
        {
            Player player = Main.LocalPlayer;
            player.GetModPlayer<ArsenalSandboxPlayer>().GodEnabled = true; player.creativeGodMode = true;
            if (!PhalanxArena.Equip(player)) throw new InvalidOperationException("Could not build or equip the CIWS position");
            PhalanxWorld.Enabled = false;
            WorldFile.SaveWorld(); Player.SavePlayer(Main.ActivePlayerFileData);
            Main.autoPause = true; Main.playerInventory = true; finished = true;
            File.WriteAllText(path, JsonSerializer.Serialize(new { success = true, version = Mod.Version.ToString(),
                god = player.creativeGodMode, paused = Main.autoPause && Main.playerInventory, remoteSlot = player.selectedItem,
                remote = player.HeldItem.ModItem?.Name, arenas = PhalanxWorld.Arenas.Select(p => new { x = p.X, floor = p.Y }).ToArray(),
                turrets = TileEntity.ByID.Values.OfType<PhalanxEntity>().Select(g => new { x = g.Position.X, y = g.Position.Y }).ToArray(),
                originalGuns = player.inventory.Where(i => i.ModItem is ArsenalGun).Select(i => new { id = i.ModItem.Name, rounds = ((ArsenalGun)i.ModItem).Rounds }).ToArray() }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception exception)
        {
            finished = true;
            File.WriteAllText(path, JsonSerializer.Serialize(new { success = false, error = exception.ToString() }));
        }
    }
}
