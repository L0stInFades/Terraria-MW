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
using Terraria.UI;

namespace ModernArsenal.Verification;

public sealed class ArsenalClientTest : ModSystem
{
    private readonly List<string> passed = new();
    private int ticks, targetIndex, reserveBefore;
    private string capture;
    private int captureFrames;
    private bool finished;
    internal static bool Fire;
    private string Report => Program.LaunchParameters["-arsenalclient"];
    private Player Player => Main.LocalPlayer;
    private ArsenalPlayer State => Player.GetModPlayer<ArsenalPlayer>();
    public override bool IsLoadingEnabled(Mod mod) => !Main.dedServ && Program.LaunchParameters.ContainsKey("-arsenalclient");
    public override void Load() => Main.OnPostDraw += AfterDraw;
    public override void Unload() { Main.OnPostDraw -= AfterDraw; Fire = false; }
    private void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); passed.Add(message); }
    private void Click(string key)
    {
        UIElement element = ArmorySystem.Current.Controls[key];
        element.LeftClick(new UIMouseEvent(element, element.GetDimensions().Center()));
    }
    public override void PostUpdateEverything()
    {
        if (Main.gameMenu || finished) return;
        try
        {
            ticks++;
            Main.autoPause = false;
            Main.dayTime = true; Main.time = 27000;
            Player.immune = true; Player.immuneTime = 60;
            if (ticks == 90)
            {
                Check(Player.inventory[0].ModItem is ArsenalGun start && start.Rounds == 12, "playable profile enters with a loaded starter USP");
                Check(Player.inventory.Sum(i => i.type == Catalog.Data.Ammo[2].Type ? i.stack : 0) == 600, "entering the prepared profile does not duplicate the starter package");
                foreach (GunSpec gun in Catalog.Data.Guns)
                {
                    Main.instance.LoadItem(gun.Type);
                    Check(TextureAssets.Item[gun.Type].Value.Width >= 48, gun.Id + " sprite loads in the real renderer");
                }
                Check(SupplyWorld.Exists(SupplyWorld.Home.X, SupplyWorld.Home.Y), "client loads the actual home crate tile");
                Player.Bottom = new Vector2((SupplyWorld.Home.X + 4) * 16, (SupplyWorld.Home.Y + 2) * 16);
            }
            if (ticks == 110)
            {
                ModContent.GetInstance<SupplyTile>().RightClick(SupplyWorld.Home.X, SupplyWorld.Home.Y);
                Check(ArmorySystem.IsOpen && ArmorySystem.Current.Controls.Keys.Any(k => k.StartsWith("claim")), "right-clicking the real crate opens a populated weapons catalogue");
                capture = "armory-weapons.png";
            }
            if (ticks == 135)
            {
                Click("ammo");
                reserveBefore = Player.inventory.Where(i => i.type == Catalog.Data.Ammo[2].Type).Sum(i => i.stack);
                Click("claim2");
                Check(Player.inventory.Where(i => i.type == Catalog.Data.Ammo[2].Type).Sum(i => i.stack) == reserveBefore + 600, "real ammo card click delivers 600 rounds");
                capture = "armory-ammo.png";
            }
            if (ticks == 160)
            {
                Click("next"); Check(ArmorySystem.Current.Page == 1, "catalogue next-page button reaches additional ammunition");
                for (int i = 0; i < 10; i++) Click("next");
                Check(ArmorySystem.Current.Page >= 2, "last page contains late-tier ammunition");
                for (int i = 0; i < 10; i++) Click("previous");
                Check(ArmorySystem.Current.Page == 0, "previous-page button clamps at the first page");
                Click("filter");
                Check(ArmorySystem.Current.Controls.Keys.Count(k => k.StartsWith("claim")) == 2, "held USP caliber filter shows exactly its FMJ and JHP variants");
                Click("guide"); Check(ArmorySystem.Current.Tab == 2, "in-game field guide opens through its real tab handler");
                Click("close"); Check(!ArmorySystem.IsOpen, "close button releases the armory panel");
                Player.selectedItem = 0;
                Player.Center = new Vector2((Main.spawnTileX + 55) * 16, (Main.spawnTileY - 6) * 16);
                for (int x = Main.spawnTileX + 50; x < Main.spawnTileX + 90; x++)
                    for (int y = Main.spawnTileY - 15; y <= Main.spawnTileY + 3; y++)
                    {
                        Main.tile[x, y].ClearEverything();
                        if (y == Main.spawnTileY + 3) { Tile tile = Main.tile[x,y]; tile.HasTile = true; tile.TileType = TileID.Stone; }
                    }
                Player.Bottom = new Vector2((Main.spawnTileX + 55) * 16, (Main.spawnTileY + 3) * 16);
                targetIndex = NPC.NewNPC(new EntitySource_Misc("ArsenalClientAcceptance"), (int)Player.Center.X + 220, (int)Player.Center.Y, NPCID.Zombie);
                NPC target = Main.npc[targetIndex]; target.Center = Player.Center + new Vector2(220, 0);
                target.aiStyle = -1; target.damage = 0; target.noGravity = true; target.velocity = Vector2.Zero;
            }
            if (ticks == 190) Fire = true;
            if (ticks == 220)
            {
                Fire = false;
                Check(((ArsenalGun)Player.HeldItem.ModItem).Rounds < 12, "native player item-use consumes magazine rounds while firing");
                Check(!Main.npc[targetIndex].active || Main.npc[targetIndex].life <= 0, "native client gunfire kills a real early-game zombie");
                capture = "usp-field.png";
            }
            if (ticks == 235)
            {
                ArsenalGun usp = (ArsenalGun)Player.HeldItem.ModItem; usp.Rounds = 3;
                reserveBefore = Player.inventory.Where(i => i.type == usp.LoadedAmmo).Sum(i => i.stack);
                string key = "ModernArsenal/Reload";
                PlayerInput.Triggers.JustPressed.KeyStatus[key] = true;
                PlayerLoader.ProcessTriggers(Player, PlayerInput.Triggers.Current);
                PlayerInput.Triggers.JustPressed.KeyStatus[key] = false;
                Check(State.ReloadTicks > 0, "registered R hotkey starts reload through native trigger processing");
                capture = "usp-reload.png";
            }
            if (ticks == 315)
            {
                ArsenalGun usp = (ArsenalGun)Player.HeldItem.ModItem;
                Check(usp.Rounds == 12 && State.ReloadTicks == 0 && Player.inventory.Where(i => i.type == usp.LoadedAmmo).Sum(i => i.stack) == reserveBefore - 9, "real frame updates finish reload and deduct exactly nine reserve rounds");
                Item burst = new(Catalog.Data.Guns.First(g => g.Id == "M16A4").Type);
                ArsenalGun gun = (ArsenalGun)burst.ModItem;
                gun.Rounds = 30; gun.LoadedAmmo = Catalog.Data.Ammo.First(a => a.Id == "Nato556").Type;
                Player.inventory[0] = burst; Player.itemAnimation = Player.itemTime = 0;
                Fire = true;
            }
            if (ticks == 316) Fire = false;
            if (ticks == 350)
            {
                Check(((ArsenalGun)Player.HeldItem.ModItem).Rounds == 27, "one native trigger press fires exactly three M16A4 rounds");
                foreach (GunSpec spec in Catalog.Data.Guns)
                {
                    foreach (Projectile p in Main.projectile) p.active = false;
                    ArsenalGun gun = (ArsenalGun)new Item(spec.Type).ModItem;
                    gun.LoadedAmmo = Catalog.Data.Ammo.First(a => a.Caliber == spec.Caliber).Type; gun.Rounds = spec.Magazine;
                    gun.Shoot(Player, new EntitySource_ItemUse_WithAmmo(Player, gun.Item, gun.LoadedAmmo), Player.Center,
                        new Vector2(spec.Speed, 0), ModContent.ProjectileType<BallisticRound>(), spec.Damage, spec.Knockback);
                    Check(Main.projectile.Count(p => p.active && p.ModProjectile is BallisticRound) == spec.Pellets && gun.Rounds == spec.Magazine - 1, spec.Id + " fires its correct native projectile pattern");
                }
                foreach (Projectile p in Main.projectile) p.active = false;
                ArsenalGun shotgun = (ArsenalGun)new Item(Catalog.Data.Guns.First(g => g.Id == "SPAS12").Type).ModItem;
                shotgun.LoadedAmmo = Catalog.Data.Ammo.First(a => a.Id == "Slug12").Type; shotgun.Rounds = 8;
                shotgun.Shoot(Player, new EntitySource_ItemUse_WithAmmo(Player, shotgun.Item, shotgun.LoadedAmmo), Player.Center, new Vector2(28, 0), ModContent.ProjectileType<BallisticRound>(), 51, 8);
                Check(Main.projectile.Count(p => p.active && p.ModProjectile is BallisticRound) == 1 && Main.projectile.First(p => p.active && p.ModProjectile is BallisticRound).damage > 400, "slug ammunition converts a shotgun into one heavy projectile");
                Player.inventory[0] = ArsenalPlayer.Starter().First();
                Player.Bottom = new Vector2((SupplyWorld.Home.X + 4) * 16, (SupplyWorld.Home.Y + 2) * 16);
                Main.UIScale = 1.5f;
                ArmorySystem.Open(SupplyWorld.Home.X, SupplyWorld.Home.Y);
                ArmorySystem.Current.Recalculate();
                UIElement container = ArmorySystem.Current.Controls["guns"].Parent;
                Rectangle bounds = container.GetDimensions().ToRectangle();
                Check(bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= Main.screenWidth / Main.UIScale + 2 && bounds.Bottom <= Main.screenHeight / Main.UIScale + 2, "armory fits the real client viewport at 150 percent UI scale");
                capture = "armory-scaled.png";
            }
            if (ticks == 390)
            {
                Player.Center += new Vector2(1000, 0);
                ModContent.GetInstance<ArmorySystem>().UpdateUI(new GameTime());
                Check(!ArmorySystem.IsOpen, "walking away closes the crate and prevents remote access");
                finished = true; Write(true, null); Main.instance.Exit(); return;
            }
            if (ticks % 30 == 0) Write(null, null);
        }
        catch (Exception exception) { finished = true; Fire = false; Write(false, exception.ToString()); Main.instance.Exit(); }
    }
    private void Write(bool? success, string error) => File.WriteAllText(Report,
        JsonSerializer.Serialize(new { success, ticks, checks = passed, error }, new JsonSerializerOptions { WriteIndented = true }));
    private void AfterDraw(GameTime gameTime)
    {
        if (capture == null || Main.gameMenu || finished || ++captureFrames < 3) return;
        captureFrames = 0;
        try
        {
            GraphicsDevice device = Main.instance.GraphicsDevice;
            int width = device.PresentationParameters.BackBufferWidth, height = device.PresentationParameters.BackBufferHeight;
            Color[] pixels = new Color[width * height]; device.GetBackBufferData(pixels);
            using Texture2D shot = new(device, width, height); shot.SetData(pixels);
            using FileStream file = File.Create(Path.Combine(Path.GetDirectoryName(Report), capture));
            shot.SaveAsPng(file, width, height); capture = null;
        }
        catch (Exception exception) { finished = true; Write(false, exception.ToString()); Main.instance.Exit(); }
    }
}

public sealed class ArsenalTestInput : ModPlayer
{
    public override bool IsLoadingEnabled(Mod mod) => !Main.dedServ && Program.LaunchParameters.ContainsKey("-arsenalclient");
    public override void SetControls()
    {
        Player.controlUseItem = ArsenalClientTest.Fire;
        Player.controlLeft = Player.controlRight = Player.controlJump = Player.controlUseTile = false;
        Main.mouseX = Main.screenWidth / 2 + 220; Main.mouseY = Main.screenHeight / 2;
    }
}
