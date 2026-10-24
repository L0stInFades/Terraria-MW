using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;

namespace ModernArsenal.Verification;

public sealed class PhalanxClientTest : ModSystem
{
    private int tick, pillarStart, waves;
    private bool finished;
    private PhalanxEntity[] guns;
    private long[] counts;
    private NPC target;
    private NPC[] pillars;
    private readonly List<string> checks = new();
    private readonly List<int> defeated = new();
    private string capture;
    private string inventory;
    private Player Player => Main.LocalPlayer;
    private string Report => Program.LaunchParameters["-arsenalciwsclient"];
    public override bool IsLoadingEnabled(Mod mod) => !Main.dedServ && Program.LaunchParameters.ContainsKey("-arsenalciwsclient");
    public override void Load() => Main.OnPostDraw += AfterDraw;
    public override void Unload() => Main.OnPostDraw -= AfterDraw;
    private void Check(bool value, string label) { if (!value) throw new Exception(label); checks.Add(label); }
    private NPC Spawn(int type, Vector2 center, bool stationary)
    {
        int index = NPC.NewNPC(new EntitySource_Misc("CIWSClientVerification"), (int)center.X, (int)center.Y, type);
        if (index >= Main.maxNPCs) return null;
        NPC npc = Main.npc[index]; npc.Center = center; npc.velocity = Vector2.Zero; npc.timeLeft = 18000;
        if (stationary) { npc.aiStyle = -1; npc.noGravity = true; }
        return npc;
    }
    private void Use(bool alternate)
    {
        Player.altFunctionUse = alternate ? 2 : 0;
        Main.keyState = new KeyboardState();
        Player.HeldItem.ModItem.UseItem(Player);
        Player.altFunctionUse = 0;
    }
    public override void PostUpdateEverything()
    {
        if (Main.gameMenu || finished) return;
        try
        {
            tick++; Main.autoPause = false; Main.mapStyle = 0;
            Player.GetModPlayer<ArsenalSandboxPlayer>().GodEnabled = true;
            if (tick == 30)
            {
                Check(PhalanxArena.Equip(Player), "native client loads the platform arena and equips its remote");
                Main.playerInventory = false; Main.dayTime = true; Main.time = 27000;
                PhalanxWorld.Enabled = false; PhalanxWorld.Ammo = 0;
                guns = TileEntity.ByID.Values.OfType<PhalanxEntity>().ToArray();
                Check(guns.Length == 6, "all six restored tile entities exist in the real client");
                foreach (NPC npc in Main.npc) if (!npc.friendly) npc.active = false;
                target = Spawn(NPCID.EyeofCthulhu, Player.Center + new Vector2(300, -140), true);
                target.life = target.lifeMax = 200000000;
                inventory = string.Join(";", Player.inventory.Select(i => i.type + ":" + i.stack));
                Use(false);
                Check(PhalanxWorld.Enabled && Player.HeldItem.ModItem is PhalanxRemote, "remote item primary use turns the installation on");
            }
            if (tick == 120) counts = guns.Select(g => g.Shots).ToArray();
            if (tick == 600) capture = "ciws-ap-firing.png";
            if (tick == 720)
            {
                long[] emitted = guns.Select((g, i) => g.Shots - counts[i]).ToArray();
                Check(emitted.All(n => n == 750), "real client: each of six turrets fires exactly 750 projectiles in ten seconds");
                Check(target.life < target.lifeMax - 10000000, "continuous native collisions remove over ten million actual target health");
                Check(inventory == string.Join(";", Player.inventory.Select(i => i.type + ":" + i.stack)), "ten seconds of live automatic fire consumes no inventory ammunition");
                Use(false); counts = guns.Select(g => g.Shots).ToArray();
            }
            if (tick == 745) Check(!PhalanxWorld.Enabled && guns.Select((g, i) => g.Shots == counts[i]).All(v => v), "remote item power-off stops all six guns in real gameplay");
            if (tick == 750) { Use(true); Check(PhalanxWorld.Ammo == 1, "remote item alternate use selects HEI-T"); }
            if (tick == 780) Use(false);
            if (tick == 840)
            {
                Check(Main.projectile.Any(p => p.active && p.ModProjectile is PhalanxRound && p.ai[0] == 1), "HEI-T rounds fly in the real client after the ammo switch");
                capture = "ciws-he-firing.png";
            }
            if (tick == 900)
            {
                Use(false); target.active = false;
                foreach (Projectile projectile in Main.projectile) projectile.active = false;
            }
            if (tick == 930)
            {
                PhalanxWorld.Ammo = 0;
                NPC.downedTowerVortex = NPC.downedTowerStardust = NPC.downedTowerSolar = NPC.downedTowerNebula = false;
                NPC.ShieldStrengthTowerVortex = NPC.ShieldStrengthTowerStardust = NPC.ShieldStrengthTowerSolar = NPC.ShieldStrengthTowerNebula = 100;
                Vector2 center = Player.Center + new Vector2(0, -300);
                pillars = new[] {
                    Spawn(NPCID.LunarTowerVortex, center + new Vector2(600, 0), false),
                    Spawn(NPCID.LunarTowerStardust, center + new Vector2(200, 0), false),
                    Spawn(NPCID.LunarTowerSolar, center + new Vector2(-200, 0), false),
                    Spawn(NPCID.LunarTowerNebula, center + new Vector2(-600, 0), false)
                };
                Check(pillars.All(p => p != null), "four actual vanilla pillars spawn with full 100-kill shields");
                Use(false); pillarStart = tick;
            }
            if (pillars != null && tick > pillarStart)
            {
                NPC objective = pillars.FirstOrDefault(p => p.active);
                if (objective != null && tick % 10 == 0 && PhalanxTargeting.Shield(objective.type) > 0)
                {
                    int type = objective.type switch {
                        NPCID.LunarTowerVortex => NPCID.VortexRifleman,
                        NPCID.LunarTowerStardust => NPCID.StardustSoldier,
                        NPCID.LunarTowerSolar => NPCID.SolarCorite,
                        _ => NPCID.NebulaSoldier
                    };
                    for (int i = 0; i < 6; i++) Spawn(type, objective.Center + new Vector2(-160 + i * 55, 80), true);
                    waves++;
                }
                for (int i = 0; i < pillars.Length; i++)
                    if (!pillars[i].active && !defeated.Contains(pillars[i].type))
                    {
                        Check(i == defeated.Count, "real pillar defeat follows strategic order at step " + (i + 1));
                        defeated.Add(pillars[i].type);
                    }
                if (defeated.Count == 4)
                {
                    Check(NPC.downedTowerVortex && NPC.downedTowerStardust && NPC.downedTowerSolar && NPC.downedTowerNebula,
                        "four pillar shields are broken by native enemy deaths and all four pillars are actually defeated");
                    PhalanxWorld.Enabled = false; capture = "ciws-pillars-cleared.png";
                    finished = true; Write(true, null);
                }
                if (tick - pillarStart > 3600) throw new Exception("Four-pillar flow timed out: shields " + string.Join(",", pillars.Select(p => PhalanxTargeting.Shield(p.type))) + "; targets " + string.Join(",", guns.Select(g => g.Target)));
            }
        }
        catch (Exception exception) { finished = true; PhalanxWorld.Enabled = false; Write(false, exception.ToString()); Main.instance.Exit(); }
    }
    private void Write(bool success, string error) => File.WriteAllText(Report, JsonSerializer.Serialize(new { success, tick, checks,
        pillarFrames = pillarStart == 0 ? 0 : tick - pillarStart, waves, defeated, error }, new JsonSerializerOptions { WriteIndented = true }));
    private void AfterDraw(GameTime gameTime)
    {
        if (capture == null) return;
        GraphicsDevice device = Main.instance.GraphicsDevice;
        int width = device.PresentationParameters.BackBufferWidth, height = device.PresentationParameters.BackBufferHeight;
        Color[] pixels = new Color[width * height]; device.GetBackBufferData(pixels);
        using Texture2D texture = new(device, width, height); texture.SetData(pixels);
        using FileStream stream = File.Create(Path.Combine(Path.GetDirectoryName(Report), capture)); texture.SaveAsPng(stream, width, height);
        capture = null;
        if (finished) Main.instance.Exit();
    }
}

public sealed class PhalanxClientControls : ModPlayer
{
    public override bool IsLoadingEnabled(Mod mod) => !Main.dedServ && Program.LaunchParameters.ContainsKey("-arsenalciwsclient");
    public override void SetControls()
    {
        Player.controlUseItem = Player.controlUseTile = Player.controlLeft = Player.controlRight = Player.controlJump = false;
        Player.mouseInterface = false;
    }
}
