using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ModernArsenal.Verification;

public sealed class FlightSetup : ModSystem
{
    private int ticks;
    private bool finished;
    public override bool IsLoadingEnabled(Mod mod) => !Main.dedServ && Program.LaunchParameters.ContainsKey("-arsenalflight");
    public override void PostUpdateEverything()
    {
        if (Main.gameMenu || finished) return;
        string path = Program.LaunchParameters["-arsenalflight"];
        Player player = Main.LocalPlayer;
        try
        {
            player.GetModPlayer<ArsenalSandboxPlayer>().GodEnabled = true; player.creativeGodMode = true;
            if (++ticks == 1)
            {
                int[] missing = new int[] { ItemID.LongRainbowTrailWings, ItemID.EmpressFlightBooster }
                    .Where(type => !player.armor.Take(8).Any(i => i.type == type)).ToArray();
                int[] slots = Enumerable.Range(3, 5).Reverse().Where(i => player.armor[i].IsAir).Take(missing.Length).ToArray();
                if (slots.Length != missing.Length) throw new InvalidOperationException("Not enough empty accessory slots; existing equipment was preserved.");
                for (int i = 0; i < missing.Length; i++)
                {
                    player.armor[slots[i]] = new Item(missing[i]);
                    player.hideVisibleAccessory[slots[i] - 3] = false;
                }
                return;
            }
            if (ticks < 5) return;
            if (player.wingsLogic != 45 || !player.empressBrooch || player.wingTimeMax <= 0)
                throw new InvalidOperationException("Native starboard and infinite-flight accessory effects did not activate.");
            Player.SavePlayer(Main.ActivePlayerFileData);
            Player restored = Player.LoadPlayer(Main.ActivePlayerFileData.Path, false).Player;
            if (!restored.armor.Take(8).Any(i => i.type == ItemID.LongRainbowTrailWings) ||
                !restored.armor.Take(8).Any(i => i.type == ItemID.EmpressFlightBooster))
                throw new InvalidOperationException("Flight equipment did not survive native player-file reload.");
            Main.autoPause = true; Main.playerInventory = true; finished = true;
            File.WriteAllText(path, JsonSerializer.Serialize(new { success = true, version = Mod.Version.ToString(), god = player.creativeGodMode,
                paused = Main.autoPause && Main.playerInventory, wings = player.wingsLogic, wingTimeMax = player.wingTimeMax,
                infiniteFlight = player.empressBrooch, saveReloaded = true,
                accessories = player.armor.Take(8).Select((item, slot) => new { slot, type = item.type, name = item.Name }).Where(i => i.type > 0).ToArray() }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception exception)
        {
            finished = true; Main.autoPause = true; Main.playerInventory = true;
            File.WriteAllText(path, JsonSerializer.Serialize(new { success = false, error = exception.ToString() }));
        }
    }
}
