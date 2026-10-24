using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.ObjectData;
using Terraria.UI;

namespace ModernArsenal;

public sealed class PhalanxTurret : ModItem
{
    public override string Texture => "ModernArsenal/Assets/CIWS/TurretItem";
    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(ModContent.TileType<PhalanxTile>());
        Item.width = 52; Item.height = 58; Item.maxStack = 99; Item.value = 0; Item.rare = ItemRarityID.Red;
    }
    public override void ModifyTooltips(List<TooltipLine> tooltips) => tooltips.Add(new TooltipLine(Mod, "CIWS",
        Catalog.Text("放在连续 5 格平台或实心地面上\n遥控器控制 · 4500 发/分 · 无限弹药", "Place on 5 consecutive platforms or solid blocks\nRemote controlled · 4500 RPM · Infinite ammunition")));
}

public sealed class PhalanxRemote : ModItem
{
    public override string Texture => "ModernArsenal/Assets/CIWS/Remote";
    public override void SetDefaults()
    {
        Item.width = 24; Item.height = 34; Item.maxStack = 1; Item.value = 0; Item.rare = ItemRarityID.Red;
        Item.useStyle = ItemUseStyleID.HoldUp; Item.useTime = Item.useAnimation = 20; Item.noMelee = true;
        Item.UseSound = SoundID.MenuTick;
    }
    public override bool AltFunctionUse(Player player) => true;
    public override bool CanUseItem(Player player) => !ArmorySystem.IsOpen && !GunsmithSystem.IsOpen && !Main.drawingPlayerChat;
    public override bool? UseItem(Player player)
    {
        if (player.whoAmI != Main.myPlayer) return true;
        bool alternate = player.altFunctionUse == 2;
        byte operation = (byte)(alternate && (Main.keyState.IsKeyDown(Keys.LeftShift) || Main.keyState.IsKeyDown(Keys.RightShift)) ? 2 : alternate ? 1 : 0);
        PhalanxWorld.Request(player, operation);
        return true;
    }
    public override void ModifyTooltips(List<TooltipLine> tooltips) => tooltips.Add(new TooltipLine(Mod, "Controls",
        Catalog.Text("左键：所有密集阵开/关\n右键：穿甲 / 高爆曳光\nShift+右键：部署标准平台战位\n不消耗背包弹药；/arsenal ciws 可补领炮塔与遥控器",
        "Left click: toggle all CIWS\nRight click: APDS / HEI-T\nShift + right click: deploy a platform position\nNo inventory ammo needed; /arsenal ciws supplies the equipment")));
}

public sealed class PhalanxTile : ModTile
{
    internal const int Width = 5, Height = 6;
    public override string Texture => "ModernArsenal/Assets/CIWS/Tile";
    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true; Main.tileLavaDeath[Type] = false; Main.tileLighted[Type] = true;
        TileID.Sets.DisableSmartCursor[Type] = true;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style3x4);
        TileObjectData.newTile.Width = Width; TileObjectData.newTile.Height = Height;
        TileObjectData.newTile.CoordinateHeights = Enumerable.Repeat(16, Height).ToArray();
        TileObjectData.newTile.Origin = new Point16(2, Height - 1);
        TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, Width, 0);
        TileObjectData.newTile.LavaDeath = false;
        TileObjectData.newTile.HookPostPlaceMyPlayer = ModContent.GetInstance<PhalanxEntity>().Generic_HookPostPlaceMyPlayer;
        TileObjectData.addTile(Type);
        RegisterItemDrop(ModContent.ItemType<PhalanxTurret>());
        AddMapEntry(new Color(185, 207, 207), CreateMapEntryName());
        DustType = DustID.Iron; HitSound = SoundID.Tink;
    }
    public override bool CanExplode(int i, int j) => false;
    public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;
    public override bool RightClick(int i, int j)
    {
        Main.NewText(Catalog.Text("持遥控器：左键开关，右键切弹；/arsenal ciws 补领。", "Hold the remote: left toggles power, right changes ammo; /arsenal ciws supplies it."), Color.LightCyan);
        return true;
    }
    public override void MouseOver(int i, int j)
    {
        Main.LocalPlayer.noThrow = 2; Main.LocalPlayer.cursorItemIconEnabled = true;
        Main.LocalPlayer.cursorItemIconID = ModContent.ItemType<PhalanxRemote>();
    }
    public override void KillMultiTile(int i, int j, int frameX, int frameY) => ModContent.GetInstance<PhalanxEntity>().Kill(i, j);
    public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
    {
        if (Main.tile[i, j].TileFrameX == 36 && Main.tile[i, j].TileFrameY == 0)
        { r = PhalanxWorld.Enabled ? 0.1f : 0.2f; g = PhalanxWorld.Enabled ? 0.35f : 0.04f; b = PhalanxWorld.Enabled ? 0.4f : 0.02f; }
    }
    public override void PostDraw(int i, int j, SpriteBatch spriteBatch)
    {
        Tile tile = Main.tile[i, j];
        if (tile.TileFrameX != 0 || tile.TileFrameY != 0) return;
        TileEntity.ByPosition.TryGetValue(new Point16(i, j), out TileEntity raw);
        PhalanxEntity entity = raw as PhalanxEntity;
        float angle = entity?.Angle ?? 0;
        Vector2 zero = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
        Vector2 pivot = new Vector2(i * 16 + 40, j * 16 + 60) - Main.screenPosition + zero;
        Color light = Lighting.GetColor(i + 2, j + 3);
        bool left = Math.Cos(angle) < 0;
        Texture2D head = ModContent.Request<Texture2D>("ModernArsenal/Assets/CIWS/Head").Value;
        spriteBatch.Draw(head, pivot - new Vector2(32, 58), null, light, 0, Vector2.Zero, 1, left ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0);
        Texture2D barrels = ModContent.Request<Texture2D>("ModernArsenal/Assets/CIWS/Barrels").Value;
        int frame = PhalanxWorld.Enabled && entity?.Target >= 0 ? (int)(Main.GameUpdateCount % 4) : 0;
        spriteBatch.Draw(barrels, pivot, new Rectangle(0, frame * 18, 68, 18), light, angle, new Vector2(8, 9), 1, left ? SpriteEffects.FlipVertically : SpriteEffects.None, 0);
        Color led = PhalanxWorld.Enabled ? Color.Cyan : Color.IndianRed;
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)pivot.X - 2, (int)pivot.Y - 53, 4, 4), new Rectangle(0, 0, 1, 1), led);
        if (PhalanxWorld.Enabled && entity?.Target >= 0)
        {
            Vector2 muzzle = pivot + new Vector2(58, 0).RotatedBy(angle);
            Texture2D flash = ModContent.Request<Texture2D>("ModernArsenal/Assets/Flashes/M249").Value;
            spriteBatch.Draw(flash, muzzle, null, Color.White * 0.85f, angle, new Vector2(0, flash.Height / 2f), 0.7f + frame * 0.08f, SpriteEffects.None, 0);
        }
    }
}

public sealed class PhalanxEntity : ModTileEntity
{
    internal int Target = -1;
    internal float Angle;
    internal long Shots;
    private int budget, ticks;
    internal Vector2 Pivot => new(Position.X * 16 + 40, Position.Y * 16 + 60);
    public override bool IsTileValidForEntity(int x, int y)
    {
        Tile tile = Main.tile[x, y];
        return tile.HasTile && tile.TileType == ModContent.TileType<PhalanxTile>() && tile.TileFrameX == 0 && tile.TileFrameY == 0;
    }
    public override void Update()
    {
        if (!PhalanxWorld.Enabled) { budget = 0; SetTarget(-1); return; }
        NPC target = PhalanxTargeting.Acquire(Pivot);
        if (target == null) { budget = 0; SetTarget(-1); return; }
        Vector2 aim = PhalanxTargeting.Aim(Pivot, target);
        Angle = aim.ToRotation();
        SetTarget(target.whoAmI);
        budget += 75;
        int fired = 0;
        while (budget >= 60)
        {
            budget -= 60;
            if (!PhalanxTargeting.Hostile(target))
            {
                target = PhalanxTargeting.Acquire(Pivot);
                if (target == null) { budget = 0; SetTarget(-1); break; }
                aim = PhalanxTargeting.Aim(Pivot, target); Angle = aim.ToRotation();
                SetTarget(target.whoAmI);
            }
            Vector2 muzzle = Vector2.DistanceSquared(Pivot, target.Center) < 80 * 80 ? Pivot + aim * 4 : Pivot + aim * 58;
            if (!Collision.CanHitLine(Pivot, 1, 1, muzzle, 1, 1)) continue;
            int damage = PhalanxWorld.Ammo == 0 ? 18000 : 14000;
            int id = Projectile.NewProjectile(new EntitySource_TileEntity(this), muzzle, aim * (PhalanxTargeting.Speed / 8), ModContent.ProjectileType<PhalanxRound>(), damage, 1, Main.myPlayer, PhalanxWorld.Ammo);
            if (id < Main.maxProjectiles) { Shots++; fired++; }
        }
        if (Main.netMode == NetmodeID.Server && ++ticks % 10 == 0) NetMessage.SendData(MessageID.TileEntitySharing, -1, -1, null, ID, Position.X, Position.Y);
        if (!Main.dedServ && fired > 0)
        {
            Lighting.AddLight(Pivot + aim * 58, 0.5f, 0.35f, 0.12f);
            if (++ticks % 6 == 0) SoundEngine.PlaySound(SoundID.Item11 with { Volume = 0.25f, Pitch = 0.35f, MaxInstances = 3 }, Pivot);
        }
    }
    private void SetTarget(int value)
    {
        if (Target == value) return;
        Target = value;
        if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.TileEntitySharing, -1, -1, null, ID, Position.X, Position.Y);
    }
    public override void SaveData(TagCompound tag) => tag["angle"] = Angle;
    public override void LoadData(TagCompound tag) { Angle = tag.GetFloat("angle"); Target = -1; }
    public override void NetSend(BinaryWriter writer) { writer.Write(Target); writer.Write(Angle); writer.Write(Shots); }
    public override void NetReceive(BinaryReader reader)
    {
        int target = reader.ReadInt32(); float angle = reader.ReadSingle(); Shots = Math.Max(0, reader.ReadInt64());
        Target = target >= -1 && target < Main.maxNPCs ? target : -1; Angle = float.IsFinite(angle) ? angle : 0;
    }
}

public sealed class PhalanxRound : ModProjectile
{
    public override string Texture => "ModernArsenal/Assets/Round";
    internal bool Explosive => Projectile.ai[0] == 1;
    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 4; Projectile.friendly = true; Projectile.DamageType = DamageClass.Ranged;
        Projectile.extraUpdates = 7; Projectile.timeLeft = 480; Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = -1;
    }
    public override void OnSpawn(IEntitySource source)
    { Projectile.penetrate = Explosive ? 1 : 6; Projectile.ArmorPenetration = Explosive ? 30 : 100; }
    public override bool? CanHitNPC(NPC target) => PhalanxTargeting.Hostile(target) ? null : false;
    public override bool CanHitPvp(Player target) => false;
    public override bool TileCollideStyle(ref int width, ref int height, ref bool fallThrough, ref Vector2 hitboxCenterFrac) { fallThrough = true; return true; }
    public override void AI()
    {
        Projectile.ai[1] += Projectile.velocity.Length(); Projectile.rotation = Projectile.velocity.ToRotation();
        if (Projectile.ai[1] > PhalanxTargeting.Range + 120) Projectile.Kill();
    }
    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    { if (!Explosive) Projectile.damage = (int)(Projectile.damage * 0.9f); }
    public override bool PreDraw(ref Color lightColor)
    {
        Main.EntitySpriteDraw(TextureAssets.MagicPixel.Value, Projectile.Center - Main.screenPosition, new Rectangle(0, 0, 1, 1),
            Explosive ? Color.OrangeRed : Color.LightGoldenrodYellow, Projectile.rotation, new Vector2(1, 0.5f), new Vector2(26, 2), SpriteEffects.None);
        return false;
    }
    public override void OnKill(int timeLeft)
    {
        if (Explosive && Main.netMode != NetmodeID.MultiplayerClient)
            Projectile.NewProjectile(Projectile.GetSource_Death(), Projectile.Center, Vector2.Zero, ModContent.ProjectileType<PhalanxBlast>(), (int)(Projectile.damage * 0.6f), 2, Projectile.owner);
        if (!Main.dedServ && Explosive)
            for (int i = 0; i < 6; i++) Dust.NewDustPerfect(Projectile.Center, DustID.Torch, Main.rand.NextVector2Circular(4, 4), 100, default, 1.2f).noGravity = true;
    }
}

public sealed class PhalanxBlast : ModProjectile
{
    public override string Texture => "ModernArsenal/Assets/Round";
    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 144; Projectile.friendly = true; Projectile.DamageType = DamageClass.Ranged;
        Projectile.tileCollide = false; Projectile.penetrate = -1; Projectile.timeLeft = 2;
        Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = -1; Projectile.ArmorPenetration = 30;
    }
    public override bool? CanHitNPC(NPC target) => PhalanxTargeting.Hostile(target) && Collision.CanHitLine(Projectile.Center, 1, 1, target.position, target.width, target.height) ? null : false;
    public override bool CanHitPvp(Player target) => false;
    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) => Vector2.DistanceSquared(Projectile.Center, targetHitbox.ClosestPointInRect(Projectile.Center)) <= 72 * 72;
    public override bool PreDraw(ref Color lightColor) => false;
}

public sealed class PhalanxHud : ModSystem
{
    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int index = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
        layers.Insert(index < 0 ? layers.Count : index, new LegacyGameInterfaceLayer("ModernArsenal: CIWS", () =>
        {
            if (Main.gameMenu || Main.LocalPlayer.HeldItem.ModItem is not PhalanxRemote) return true;
            Vector2 origin = new(Main.screenWidth / 2f - 238, Main.screenHeight - 176);
            Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)origin.X, (int)origin.Y, 476, 112), new Color(12, 23, 31, 235));
            int count = TileEntity.ByID.Values.Count(t => t is PhalanxEntity);
            Utils.DrawBorderString(Main.spriteBatch, Catalog.Text($"密集阵 · {(PhalanxWorld.Enabled ? "在线" : "待机")} · {count} 座 · {PhalanxWorld.AmmoName}", $"CIWS · {(PhalanxWorld.Enabled ? "ONLINE" : "STANDBY")} · {count} guns · {PhalanxWorld.AmmoName}"), origin + new Vector2(12, 9), Color.LightCyan, 0.72f);
            Utils.DrawBorderString(Main.spriteBatch, Catalog.Text("4500 发/分 · 无限弹药 · 每刻索敌", "4500 RPM · Infinite ammo · Scan every tick"), origin + new Vector2(12, 35), Color.LightGoldenrodYellow, 0.68f);
            Utils.DrawBorderString(Main.spriteBatch, Catalog.Text("左键 开/关 · 右键 切弹 · Shift+右键 部署平台", "Left: power · Right: ammo · Shift+right: deploy"), origin + new Vector2(12, 60), Color.White, 0.64f);
            Utils.DrawBorderString(Main.spriteBatch, Catalog.Text("四柱：星旋→星尘→日耀→星云，先破盾再打柱体", "Pillars: Vortex > Stardust > Solar > Nebula; shields first"), origin + new Vector2(12, 84), new Color(154, 191, 174), 0.59f);
            return true;
        }, InterfaceScaleType.UI));
    }
}
