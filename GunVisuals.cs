using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace ModernArsenal;

[Flags]
internal enum GunAttachments : byte { None = 0, Suppressor = 1, Flashlight = 2, Laser = 4, Infrared = 8 }

internal sealed partial class ArsenalGun
{
    internal GunAttachments Attachments;
    private bool shotStarted, caseEjected;
    private ulong shotTick;
    private Asset<Texture2D> actionTexture, flashTexture;
    internal bool Has(GunAttachments attachment) => (Attachments & attachment) != 0;
    internal static GunAttachments CleanAttachments(int value)
    {
        GunAttachments result = (GunAttachments)(value & 15);
        if ((result & GunAttachments.Infrared) != 0) result &= ~GunAttachments.Laser;
        return result;
    }
    internal void ClearVisuals() { shotStarted = false; caseEjected = true; }
    private void StartShotVisuals() { shotStarted = true; shotTick = Main.GameUpdateCount; caseEjected = false; }
    internal int ShotAge(Player player) => shotStarted ? (int)Math.Min(int.MaxValue, Main.GameUpdateCount - shotTick) :
        player.whoAmI != Main.myPlayer && player.itemAnimation > 0 ? (player.itemAnimationMax - player.itemAnimation) % Math.Max(1, Item.useTime) : int.MaxValue;
    internal static Vector2 AimFor(Player player)
    {
        if (player.whoAmI == Main.myPlayer && !Main.playerInventory && !player.mouseInterface && !Main.drawingPlayerChat && !GunsmithSystem.IsOpen && !ArmorySystem.IsOpen)
            return (Main.MouseWorld - player.MountedCenter).SafeNormalize(Vector2.UnitX * player.direction);
        return (Vector2.UnitX * player.direction).RotatedBy(player.itemRotation * player.gravDir);
    }
    public override void HoldStyle(Player player, Rectangle heldItemFrame)
    {
        if (player.dead || player.frozen || player.pulley || player.sleeping.isSleeping) return;
        Vector2 aim = AimFor(player);
        if (player.whoAmI == Main.myPlayer && !Main.playerInventory && !player.mouseInterface && !GunsmithSystem.IsOpen && !ArmorySystem.IsOpen)
            player.ChangeDir(aim.X < 0 ? -1 : 1);
        player.itemRotation = (aim * player.direction).ToRotation() * player.gravDir;
        player.itemLocation = new Vector2(player.Center.X - player.direction * 2, player.MountedCenter.Y - heldItemFrame.Height / 2f);
        PoseArms(player);
    }
    public override void UseStyle(Player player, Rectangle heldItemFrame) => PoseArms(player);
    private void PoseArms(Player player)
    {
        float kick = Spec.Animation.KickAt(ShotAge(player));
        float recoil = -MathHelper.ToRadians(Spec.Animation.KickDegrees) * kick * player.direction;
        float angle = (Vector2.UnitX * player.direction).RotatedBy(player.itemRotation * player.gravDir + recoil).ToRotation() - MathHelper.PiOver2;
        player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, angle);
        if (Spec.Family != "Pistol")
        {
            bool pumping = Spec.Animation.Action == "pump" && Spec.Animation.FrameAt(ShotAge(player)) is 2 or 3;
            player.SetCompositeArmBack(true, pumping ? Player.CompositeArmStretchAmount.Quarter : Player.CompositeArmStretchAmount.ThreeQuarters, angle + 0.15f * player.direction);
        }
    }
    private static Vector2 TexturePoint(DrawData data, Vector2 point, int width, int height)
    {
        if ((data.effect & SpriteEffects.FlipHorizontally) != 0) point.X = width - point.X;
        if ((data.effect & SpriteEffects.FlipVertically) != 0) point.Y = height - point.Y;
        return data.position + ((point - data.origin) * data.scale).RotatedBy(data.rotation);
    }
    public override bool ModifyItemDraw(ref PlayerDrawSet info, ref DrawData data, ref DrawData? coloredData, ref DrawData? glowData)
    {
        Player player = info.drawPlayer;
        GunAnimation motion = Spec.Animation;
        int age = ShotAge(player);
        int frame = motion.FrameAt(age);
        Rectangle source = data.sourceRect ?? new Rectangle(0, 0, TextureAssets.Item[Type].Width(), TextureAssets.Item[Type].Height());
        float kick = motion.KickAt(age);
        Vector2 aim = (Vector2.UnitX * player.direction).RotatedBy(player.itemRotation * player.gravDir);
        data.position -= aim * motion.KickPixels * kick;
        data.rotation -= MathHelper.ToRadians(motion.KickDegrees) * kick * player.direction * player.gravDir;
        actionTexture ??= ModContent.Request<Texture2D>("ModernArsenal/Assets/Animations/" + Spec.Id, AssetRequestMode.ImmediateLoad);
        data.texture = actionTexture.Value;
        data.sourceRect = new Rectangle(0, frame * source.Height, source.Width, source.Height);
        Vector2 muzzlePixel = new(motion.Muzzle[0] * 2, motion.Muzzle[1] * 2);
        if (motion.Action == "barrett") muzzlePixel.X -= motion.Travel * new[] { 0f, 0.6f, 1f, 0.7f, 0.25f, 0f }[frame] * 2;
        Vector2 muzzle = TexturePoint(data, muzzlePixel, source.Width, source.Height);
        bool suppressed = Has(GunAttachments.Suppressor);
        if (Has(GunAttachments.Laser) || Has(GunAttachments.Infrared) && Main.LocalPlayer.nightVision)
            DrawLaser(ref info, muzzle, aim, Has(GunAttachments.Infrared));
        info.DrawDataCache.Add(data);
        if (suppressed)
        {
            Texture2D suppressor = ModContent.Request<Texture2D>("ModernArsenal/Assets/Suppressor").Value;
            info.DrawDataCache.Add(new DrawData(suppressor, muzzle, null, data.color, data.rotation,
                new Vector2(player.direction > 0 ? 0 : suppressor.Width, suppressor.Height / 2f), data.scale, data.effect));
            muzzle += aim * suppressor.Width * data.scale.X;
        }
        if (Has(GunAttachments.Flashlight)) DrawDevice(ref info, data, source, muzzlePixel + new Vector2(-14, 8), "Flashlight");
        if (Has(GunAttachments.Laser) || Has(GunAttachments.Infrared)) DrawDevice(ref info, data, source, muzzlePixel + new Vector2(-24, 4), "Infrared");
        if (age < motion.FlashTicks)
        {
            flashTexture ??= ModContent.Request<Texture2D>("ModernArsenal/Assets/Flashes/" + Spec.Id, AssetRequestMode.ImmediateLoad);
            Texture2D flash = flashTexture.Value;
            float size = suppressed ? 0.3f : 1f;
            info.DrawDataCache.Add(new DrawData(flash, muzzle, null, Color.White * (suppressed ? 0.25f : 1f), data.rotation,
                new Vector2(player.direction > 0 ? 0 : flash.Width, flash.Height / 2f), data.scale * size, data.effect));
        }
        if (!Main.gameMenu && info.shadow == 0 && player.whoAmI == Main.myPlayer && !caseEjected && age >= motion.EjectDelay && age < motion.Cycle)
        {
            caseEjected = true;
            Vector2 port = TexturePoint(data, new Vector2(motion.Eject[0], motion.Eject[1]) * 2, source.Width, source.Height) + Main.screenPosition;
            Dust casing = Dust.NewDustPerfect(port, ModContent.DustType<SpentCasing>(), new Vector2(motion.CasingX * player.direction, motion.CasingY * player.gravDir), 0, default, 0.7f);
            casing.frame.Y = motion.Casing == 1 ? 6 : 0;
            if (motion.Casing == 2)
            {
                Dust link = Dust.NewDustPerfect(port, ModContent.DustType<SpentCasing>(), new Vector2(-1.1f * player.direction, 1.3f), 0, default, 0.65f);
                link.frame.Y = 12;
            }
        }
        return false;
    }
    private static void DrawDevice(ref PlayerDrawSet info, DrawData gun, Rectangle source, Vector2 point, string asset)
    {
        Texture2D texture = ModContent.Request<Texture2D>("ModernArsenal/Assets/" + asset).Value;
        info.DrawDataCache.Add(new DrawData(texture, TexturePoint(gun, point, source.Width, source.Height), null, gun.color, gun.rotation,
            texture.Size() / 2, gun.scale, gun.effect));
    }
    private static void DrawLaser(ref PlayerDrawSet info, Vector2 muzzle, Vector2 direction, bool infrared)
    {
        Vector2 start = muzzle + Main.screenPosition;
        if (!Collision.CanHitLine(info.drawPlayer.MountedCenter, 1, 1, start, 1, 1)) start = info.drawPlayer.MountedCenter;
        float distance = 0;
        while (distance < 1600 && !Collision.SolidCollision(start + direction * distance, 2, 2)) distance += 8;
        Color color = infrared ? new Color(135, 245, 164, 90) : new Color(255, 50, 50, 90);
        info.DrawDataCache.Add(new DrawData(TextureAssets.MagicPixel.Value, start - Main.screenPosition, new Rectangle(0, 0, 1, 1), color,
            direction.ToRotation(), new Vector2(0, 0.5f), new Vector2(distance, 1), SpriteEffects.None));
        info.DrawDataCache.Add(new DrawData(TextureAssets.MagicPixel.Value, start + direction * distance - Main.screenPosition, new Rectangle(0, 0, 1, 1),
            color * 2f, 0, new Vector2(0.5f), new Vector2(3), SpriteEffects.None));
    }
    private void UpdateAttachmentLight(Player player)
    {
        if (Main.dedServ || !Has(GunAttachments.Flashlight)) return;
        Vector2 aim = AimFor(player);
        for (int ray = -2; ray <= 2; ray++)
        {
            Vector2 direction = aim.RotatedBy(ray * 0.095f);
            for (int step = 1; step <= 28; step++)
            {
                Vector2 point = player.MountedCenter + direction * (step * 16);
                if (Collision.SolidCollision(point, 4, 4)) break;
                float brightness = 1.1f - step / 32f;
                Lighting.AddLight(point, brightness, brightness * 0.95f, brightness * 0.8f);
            }
        }
    }
}
