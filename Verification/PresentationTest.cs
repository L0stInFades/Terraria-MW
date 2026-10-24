using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.UI;

namespace ModernArsenal.Verification;

public sealed class PresentationTest : ModSystem
{
    private readonly List<string> checks = new();
    private int ticks, reserve, dark;
    private bool finished;
    private string capture;
    private int captureFrames;
    internal static bool Fire, Walk;
    internal static Vector2 Aim = new(220, 0);
    internal static int DrawnType, GunDraws, Frame, BeamEntries;
    internal static float BeamLength;
    private Player Player => Main.LocalPlayer;
    private ArsenalGun Gun => Player.HeldItem.ModItem as ArsenalGun;
    private string Report => Program.LaunchParameters["-arsenalpresentation"];
    public override bool IsLoadingEnabled(Mod mod) => !Main.dedServ && Program.LaunchParameters.ContainsKey("-arsenalpresentation");
    public override void Load() => Main.OnPostDraw += AfterDraw;
    public override void Unload() { Main.OnPostDraw -= AfterDraw; Fire = Walk = false; }
    private void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); checks.Add(name); }
    private void Equip(string id)
    {
        GunSpec spec = Catalog.Data.Guns.First(g => g.Id == id);
        Player.inventory[0] = new Item(spec.Type); Player.selectedItem = 0;
        Gun.LoadedAmmo = Catalog.Data.Ammo.First(a => a.Caliber == spec.Caliber).Type;
        Gun.Rounds = spec.Magazine; DrawnType = GunDraws = 0;
    }
    private void Click(string id)
    {
        UIElement control = GunsmithSystem.Current.Controls[id];
        control.LeftClick(new UIMouseEvent(control, control.GetDimensions().Center()));
    }
    private void PressModify()
    {
        PlayerInput.Triggers.JustPressed.KeyStatus["ModernArsenal/ModifyGun"] = true;
        PlayerLoader.ProcessTriggers(Player, PlayerInput.Triggers.Current);
        PlayerInput.Triggers.JustPressed.KeyStatus["ModernArsenal/ModifyGun"] = false;
    }
    public override void PostUpdateEverything()
    {
        if (Main.gameMenu || finished) return;
        try
        {
            ticks++;
            Main.autoPause = false; Main.mapStyle = 0;
            Player.GetModPlayer<ArsenalSandboxPlayer>().GodEnabled = true;
            if (ticks == 45)
            {
                Main.dayTime = true; Main.time = 27000;
                Main.playerInventory = false;
                Player.Bottom = new Vector2((SupplyWorld.Home.X + 8) * 16, (SupplyWorld.Home.Y + 2) * 16);
                for (int i = 45; i < 50; i++) Player.inventory[i].TurnToAir();
            }
            if (ticks >= 60 && ticks < 396)
            {
                int index = (ticks - 60) / 14, frame = (ticks - 60) % 14;
                if (frame == 0)
                {
                    Equip(Catalog.Data.Guns[index].Id);
                    Aim = new Vector2(index % 2 == 0 ? 220 : -220, index % 3 == 0 ? -45 : 20);
                    Walk = index % 5 == 3;
                }
                if (frame == 9)
                {
                    Check(DrawnType == Gun.Type && GunDraws == 1 && Frame == 0 && Player.itemAnimation == 0 && Gun.Rounds == Gun.Spec.Magazine,
                        Gun.Spec.Id + " is drawn once while idle without firing, ammunition use or a fake use animation");
                    if (Walk) Check(Math.Abs(Player.velocity.X) > 0.01f, Gun.Spec.Id + " remains held during real player movement");
                    if (index == 7) capture = "idle-m4a1.png";
                    if (index == 22) capture = "idle-intervention.png";
                }
            }
            if (ticks == 396)
            {
                Walk = false; Gun.Rounds = 0;
            }
            if (ticks == 399) Check(DrawnType == Gun.Type && GunDraws == 1 && Player.itemAnimation == 0, "an empty magazine does not hide the selected gun");
            if (ticks == 400)
            {
                Equip("SPAS12"); Aim = new Vector2(220, 0);
                Player.inventory[54] = new Item(Gun.LoadedAmmo, 1000);
                Fire = true;
            }
            if (ticks == 401) Fire = false;
            if (ticks == 420)
            {
                Check(Gun.Rounds == Gun.Spec.Magazine - 1 && Frame > 0, "SPAS-12 fires one shell and renders its pump cycle");
                capture = "spas-pump.png";
            }
            if (ticks == 445)
            {
                Check(Frame == 0 && GunDraws == 1, "a completed firing cycle returns to a visible idle gun");
                reserve = Player.inventory[54].stack;
                Check(Player.GetModPlayer<ArsenalPlayer>().BeginReload(), "manual reload starts after the firing animation finishes");
            }
            if (ticks == 450)
            {
                Check(Player.GetModPlayer<ArsenalPlayer>().ReloadTicks > 0 && GunDraws == 1 && Frame == 0, "reloading retains the gun and does not play a stale firing cycle");
                Player.selectedItem = 1;
            }
            if (ticks == 454) Check(Player.GetModPlayer<ArsenalPlayer>().ReloadTicks == 0 && Player.inventory[54].stack == reserve, "switching guns cancels reload without consuming ammunition");
            if (ticks == 460)
            {
                Equip("M4A1"); Player.inventory[54] = new Item(Gun.LoadedAmmo, 1000);
                Player.armor[0].TurnToAir();
            }
            if (ticks == 470)
            {
                PressModify(); Check(GunsmithSystem.IsOpen, "registered V key opens the gunsmith for the selected gun");
                Click("suppressor"); Click("flashlight"); Click("laser"); Fire = true;
            }
            if (ticks == 480)
            {
                Fire = false;
                Check(Gun.Rounds == Gun.Spec.Magazine && Player.itemAnimation == 0, "holding fire inside the gunsmith cannot fire or consume ammunition");
                Check(Gun.Has(GunAttachments.Suppressor) && Gun.Has(GunAttachments.Flashlight) && Gun.Has(GunAttachments.Laser), "three real UI controls apply their individual attachments");
                Check(Gun.Item.UseSound.HasValue && Gun.Item.UseSound.Value.Volume < 0.3f, "suppressor attenuates the actual weapon report");
                Check(BeamEntries >= 2, "visible aiming laser produces a beam and impact dot in the native draw cache");
                Click("laser");
            }
            if (ticks == 486)
            {
                Check(Gun.Has(GunAttachments.Infrared) && !Player.nightVision && BeamEntries == 0, "IR replaces visible laser and stays invisible without night vision");
                Click("nightvision");
                int slot = Array.FindIndex(Player.inventory, i => i.type == ItemID.NightVisionHelmet);
                Check(slot >= 0, "the gunsmith supplies the vanilla night vision helmet");
                Player.armor[0] = Player.inventory[slot].Clone(); Player.inventory[slot].TurnToAir();
            }
            if (ticks == 496)
            {
                Check(Player.nightVision && BeamEntries >= 2, "equipped native night vision makes IR aiming visible");
                capture = "gunsmith.png";
            }
            if (ticks == 505)
            {
                PressModify(); Check(!GunsmithSystem.IsOpen, "V closes the gunsmith");
                Player.armor[0].TurnToAir();
                int x = Main.spawnTileX + 50, floor = Main.spawnTileY + 3;
                for (int dx = -5; dx < 70; dx++) for (int dy = -20; dy <= 1; dy++)
                {
                    Tile tile = Main.tile[x + dx, floor + dy]; tile.ClearEverything();
                    if (dy == 0) { tile.HasTile = true; tile.TileType = TileID.Stone; }
                }
                Player.Bottom = new Vector2(x * 16, floor * 16);
                Main.dayTime = false; Main.time = 18000;
                Gun.Attachments &= ~GunAttachments.Flashlight;
            }
            if (ticks == 535)
            {
                Color color = Lighting.GetColor((int)Player.Center.X / 16 + 8, (int)Player.Center.Y / 16);
                dark = color.R + color.G + color.B;
                Gun.Attachments |= GunAttachments.Flashlight;
            }
            if (ticks == 565) capture = "flashlight-night.png";
            if (ticks == 570)
            {
                Color color = Lighting.GetColor((int)Player.Center.X / 16 + 8, (int)Player.Center.Y / 16);
                Check(color.R + color.G + color.B > dark + 35, "the flashlight increases actual world lighting ahead of the held gun");
                Gun.Attachments = GunAttachments.Suppressor | GunAttachments.Laser;
                int x = (int)Player.Center.X / 16 + 25, y = (int)Player.Center.Y / 16;
                for (int dy = -7; dy <= 7; dy++) { Tile tile = Main.tile[x, y + dy]; tile.HasTile = true; tile.TileType = TileID.Stone; }
            }
            if (ticks == 590)
            {
                Check(BeamEntries >= 2 && BeamLength < 350, "laser beam stops at the actual wall");
                TagCompound tag = new(); Gun.SaveData(tag);
                ArsenalGun restored = (ArsenalGun)new Item(Gun.Type).ModItem; restored.LoadData(tag);
                Check(restored.Attachments == Gun.Attachments && restored.Rounds == Gun.Rounds, "save data retains independent attachments and the magazine");
                using MemoryStream stream = new(); Gun.NetSend(new BinaryWriter(stream)); stream.Position = 0; restored.NetReceive(new BinaryReader(stream));
                Check(restored.Attachments == Gun.Attachments, "native inventory synchronization carries attachments");
                Terraria.Player.SavePlayer(Main.ActivePlayerFileData);
                var saved = Terraria.Player.LoadPlayer(Main.ActivePlayerFileData.Path, false).Player;
                Check(saved.inventory[0].ModItem is ArsenalGun persisted && persisted.Attachments == Gun.Attachments,
                    "a real player-file save and reload preserves the configured gun attachments");
                PressModify(); Player.selectedItem = 1;
            }
            if (ticks == 595)
            {
                Check(!GunsmithSystem.IsOpen, "switching guns closes the gunsmith instead of modifying a different item");
            }
            if (ticks >= 610)
            {
                int start = 610;
                for (int index = 0; index < Catalog.Data.Guns.Length; index++)
                {
                    GunSpec spec = Catalog.Data.Guns[index];
                    int duration = Math.Max(spec.Interval * spec.Burst + (spec.Burst > 1 ? 18 : 0), spec.Animation.Cycle) + 18;
                    if (ticks >= start && ticks < start + duration)
                    {
                        int elapsed = ticks - start;
                        if (elapsed == 0)
                        {
                            Equip(spec.Id); Aim = new Vector2(220, 0);
                            Main.dayTime = true; Main.time = 27000;
                            Player.itemAnimation = Player.itemTime = Player.reuseDelay = 0;
                            Player.inventory[54] = new Item(Gun.LoadedAmmo, 1000);
                            foreach (Dust dust in Main.dust) if (dust.type == ModContent.DustType<SpentCasing>()) dust.active = false;
                            Fire = true;
                        }
                        if (elapsed == 1) Fire = false;
                        if (elapsed == spec.Animation.ActionDelay + 2)
                            Check(DrawnType == Gun.Type && GunDraws == 1 && Frame > 0,
                                spec.Id + " fires through the native player loop and renders its individual mechanical frames");
                        if (elapsed == spec.Animation.EjectDelay + 5)
                            Check(Main.dust.Any(d => d.active && d.type == ModContent.DustType<SpentCasing>()),
                                spec.Id + " ejects its configured case or shell at the scheduled mechanical point");
                        if (elapsed == duration - 3)
                            Check(Gun.Rounds == spec.Magazine - spec.Burst && Frame == 0 && GunDraws == 1,
                                spec.Id + " finishes the exact shot or burst and returns to visible idle without duplicate consumption");
                        if (index == 2 && elapsed == 2) capture = "desert-eagle-fire.png";
                        if (index == 23 && elapsed == 2) capture = "barrett-fire.png";
                        break;
                    }
                    start += duration;
                    if (index == Catalog.Data.Guns.Length - 1 && ticks >= start) break;
                }
                int ending = 610 + Catalog.Data.Guns.Sum(g => Math.Max(g.Interval * g.Burst + (g.Burst > 1 ? 18 : 0), g.Animation.Cycle) + 18);
                int step = ticks - ending;
                if (step == 0)
                {
                    Equip("M16A4"); Player.itemAnimation = Player.itemTime = Player.reuseDelay = 0;
                    Player.inventory[54] = new Item(Gun.LoadedAmmo, 1000); Fire = true;
                }
                if (step == 2)
                {
                    Fire = false;
                    PlayerInput.Triggers.JustPressed.KeyStatus["ModernArsenal/Reload"] = true;
                    PlayerLoader.ProcessTriggers(Player, PlayerInput.Triggers.Current);
                    PlayerInput.Triggers.JustPressed.KeyStatus["ModernArsenal/Reload"] = false;
                    Check(Player.GetModPlayer<ArsenalPlayer>().ReloadTicks > 0 && Gun.Rounds == 29,
                        "R immediately interrupts a running three-round burst and begins reloading");
                }
                if (step == 12)
                    Check(Gun.Rounds == 29 && Player.inventory[54].stack == 1000 && Frame == 0,
                        "interrupted bursts cannot fire trailing rounds or debit reserve ammunition during reload");
                if (step == 105)
                {
                    Check(Gun.Rounds == 30 && Player.inventory[54].stack == 999 && Player.GetModPlayer<ArsenalPlayer>().ReloadTicks == 0,
                        "the interrupted burst reload completes with exactly one reserve round consumed");
                    Equip("M4A1");
                    int npcIndex = NPC.NewNPC(new EntitySource_Misc("CloseRangeAcceptance"), (int)Player.Center.X + 35, (int)Player.Center.Y, NPCID.Zombie);
                    NPC npc = Main.npc[npcIndex]; npc.Center = Player.Center + new Vector2(35, 0); npc.aiStyle = -1; npc.noGravity = true;
                    foreach (Projectile p in Main.projectile) p.active = false;
                    Gun.Shoot(Player, new EntitySource_ItemUse_WithAmmo(Player, Gun.Item, Gun.LoadedAmmo), Player.Center,
                        new Vector2(Gun.Spec.Speed, 0), ModContent.ProjectileType<BallisticRound>(), Gun.Spec.Damage, 3);
                    Projectile bullet = Main.projectile.First(p => p.active && p.ModProjectile is BallisticRound);
                    for (int tick = 0; tick < 15 && bullet.active; tick++) bullet.Update(bullet.whoAmI);
                    Check(!npc.active || npc.life <= 0, "the drawn barrel cannot cause close-range shots to spawn beyond an enemy");
                    finished = true; Write(true, null); Main.instance.Exit(); return;
                }
            }
            if (ticks % 60 == 0) Write(null, null);
        }
        catch (Exception exception) { finished = true; Fire = Walk = false; Write(false, exception.ToString()); Main.instance.Exit(); }
    }
    private void Write(bool? success, string error) => File.WriteAllText(Report, JsonSerializer.Serialize(new { success, ticks, checks, error }, new JsonSerializerOptions { WriteIndented = true }));
    private void AfterDraw(GameTime time)
    {
        if (capture == null || Main.gameMenu || finished || ++captureFrames < 2) return;
        captureFrames = 0;
        GraphicsDevice device = Main.instance.GraphicsDevice;
        int width = device.PresentationParameters.BackBufferWidth, height = device.PresentationParameters.BackBufferHeight;
        Color[] colors = new Color[width * height]; device.GetBackBufferData(colors);
        using Texture2D image = new(device, width, height); image.SetData(colors);
        using FileStream stream = File.Create(Path.Combine(Path.GetDirectoryName(Report), capture)); image.SaveAsPng(stream, width, height);
        capture = null;
    }
}

public sealed class PresentationProbe : ModPlayer
{
    public override bool IsLoadingEnabled(Mod mod) => !Main.dedServ && Program.LaunchParameters.ContainsKey("-arsenalpresentation");
    public override void SetControls()
    {
        Player.controlUseItem = PresentationTest.Fire;
        Player.controlLeft = Player.controlJump = Player.controlUseTile = false;
        Player.controlRight = PresentationTest.Walk;
        Main.mouseX = Main.screenWidth / 2 + (int)PresentationTest.Aim.X;
        Main.mouseY = Main.screenHeight / 2 + (int)PresentationTest.Aim.Y;
        if (!GunsmithSystem.IsOpen && !ArmorySystem.IsOpen) Player.mouseInterface = false;
    }
    public override void TransformDrawData(ref PlayerDrawSet info)
    {
        if (Player.whoAmI != Main.myPlayer || info.shadow != 0 || Player.HeldItem.ModItem is not ArsenalGun gun) return;
        Texture2D texture = ModContent.Request<Texture2D>("ModernArsenal/Assets/Animations/" + gun.Spec.Id).Value;
        DrawData[] matches = info.DrawDataCache.Where(d => ReferenceEquals(d.texture, texture)).ToArray();
        if (matches.Length == 0) return;
        PresentationTest.DrawnType = gun.Type; PresentationTest.GunDraws = matches.Length;
        PresentationTest.Frame = (matches[0].sourceRect?.Y ?? 0) / 46;
        var beams = info.DrawDataCache.Where(d => ReferenceEquals(d.texture, TextureAssets.MagicPixel.Value)).ToArray();
        PresentationTest.BeamEntries = beams.Length;
        PresentationTest.BeamLength = beams.Length == 0 ? 0 : beams.Max(d => d.scale.X);
    }
}
