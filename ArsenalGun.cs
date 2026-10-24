using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace ModernArsenal;

[Autoload(false)]
internal sealed partial class ArsenalGun : ModItem
{
    internal readonly GunSpec Spec;
    internal int Rounds;
    internal int LoadedAmmo;
    internal ArsenalGun(GunSpec spec) => Spec = spec;
    public override string Name => Spec.Id;
    public override string Texture => "ModernArsenal/Assets/Guns/" + Spec.Id;
    protected override bool CloneNewInstances => true;

    public override void SetDefaults()
    {
        Item.width = Spec.Family == "Pistol" ? 36 : 66;
        Item.height = 28;
        Item.damage = Spec.Damage;
        Item.DamageType = DamageClass.Ranged;
        Item.useTime = Spec.Interval;
        Item.useAnimation = Spec.Interval * Spec.Burst;
        Item.reuseDelay = Spec.Burst > 1 ? 18 : 0;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.holdStyle = ItemHoldStyleID.HoldHeavy;
        Item.noMelee = true;
        Item.autoReuse = true;
        Item.knockBack = Spec.Knockback;
        Item.crit = Spec.Crit;
        Item.shoot = ModContent.ProjectileType<BallisticRound>();
        Item.shootSpeed = Spec.Speed;
        Item.rare = Spec.Tier + 1;
        Item.value = 0;
        Item.UseSound = Spec.Family is "Shotgun" or "Sniper" ? SoundID.Item38 : SoundID.Item11;
    }

    public override Vector2? HoldoutOffset() => new(-8, -3);

    public override bool CanUseItem(Player player)
    {
        ApplyAttachmentSound();
        if (player.whoAmI != Main.myPlayer) return true;
        ArsenalPlayer state = player.GetModPlayer<ArsenalPlayer>();
        if (state.ReloadTicks > 0 || ArmorySystem.IsOpen || GunsmithSystem.IsOpen) return false;
        if (Rounds > 0) return true;
        state.BeginReload();
        return false;
    }

    internal AmmoSpec Ammo => Catalog.Data.Ammo.FirstOrDefault(a => a.Type == LoadedAmmo && a.Caliber == Spec.Caliber);

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
    {
        AmmoSpec ammo = Ammo;
        if (Rounds <= 0 || ammo == null || player.GetModPlayer<ArsenalPlayer>().ReloadTicks > 0 || ArmorySystem.IsOpen || GunsmithSystem.IsOpen) return false;
        Rounds--;
        StartShotVisuals();
        int ammoIndex = Array.IndexOf(Catalog.Data.Ammo, ammo);
        bool slug = ammo.Kind == "Slug" && Spec.Family == "Shotgun";
        int pellets = slug ? 1 : Spec.Pellets;
        damage += (int)player.GetTotalDamage(DamageClass.Ranged).ApplyTo(ammo.Damage);
        if (slug) damage = (int)(damage * Spec.Pellets * 0.78f);
        Vector2 aim = velocity.SafeNormalize(Vector2.UnitX * player.direction);
        // The muzzle stays at the player when a wall blocks the barrel offset.
        // Match the native holdout origin used by the 46-pixel firearm atlas rows.
        Vector2 barrel = new((Spec.Animation.Muzzle[0] * 2 + 2) * player.direction,
            (Spec.Animation.Muzzle[1] * 2 - 23) * player.gravDir);
        Vector2 muzzle = position + new Vector2(-2 * player.direction, -3 * player.gravDir) +
            barrel.RotatedBy((aim * player.direction).ToRotation() * player.gravDir);
        if (Has(GunAttachments.Suppressor)) muzzle += aim * 24;
        bool targetInBarrel = Main.npc.Any(n => n.active && !n.friendly && !n.dontTakeDamage &&
            Collision.CheckAABBvLineCollision(n.position, n.Size, position, muzzle));
        if (!targetInBarrel && Collision.CanHitLine(position, 1, 1, muzzle, 1, 1)) position = muzzle;
        float spread = MathHelper.ToRadians(slug ? 1.1f : Spec.Spread);
        if (player.velocity.LengthSquared() > 9) spread *= 1.5f;
        if (player.scope && player.controlUseTile) spread *= 0.65f;
        for (int i = 0; i < pellets; i++)
        {
            Vector2 shot = velocity.RotatedByRandom(spread) / BallisticRound.Updates;
            // Keep the network damage payload in range; ai[2] restores the full source damage before defense.
            int payload = Math.Min(damage, 30000);
            Projectile.NewProjectile(source, position, shot, type, payload, knockback, player.whoAmI, ammoIndex, 0, damage / (float)Math.Max(1, payload));
        }
        if (!Main.dedServ)
        {
            bool suppressed = Has(GunAttachments.Suppressor);
            for (int i = 0; i < (suppressed ? 1 : 3); i++)
                Dust.NewDustPerfect(position, DustID.Torch, aim * (2 + i), 130, default, suppressed ? 0.35f : 0.8f).noGravity = true;
            Lighting.AddLight(position, new Vector3(0.8f, 0.55f, 0.2f) * (suppressed ? 0.15f : 1f));
        }
        Sync(player);
        return false;
    }

    internal void Sync(Player player)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            NetMessage.SendData(MessageID.SyncEquipment, -1, -1, null, player.whoAmI, player.selectedItem);
    }

    public override void HoldItem(Player player)
    {
        if (Spec.Family is "Sniper" or "Marksman") player.scope = true;
        ApplyAttachmentSound();
        UpdateAttachmentLight(player);
    }
    private void ApplyAttachmentSound() => Item.UseSound = (Spec.Family is "Shotgun" or "Sniper" ? SoundID.Item38 : SoundID.Item11) with
        { Volume = Has(GunAttachments.Suppressor) ? 0.2f : 1f, Pitch = Has(GunAttachments.Suppressor) ? -0.35f : 0f };

    public override void ModifyTooltips(List<TooltipLine> tooltips)
    {
        tooltips.Add(new TooltipLine(Mod, "Role", Catalog.Family(Spec.Family) + " · " + Spec.CaliberLabel()));
        tooltips.Add(new TooltipLine(Mod, "Magazine", Catalog.Text($"弹匣 {Rounds}/{Spec.Magazine} · 换弹 {Spec.Reload / 60f:0.00} 秒 · R 换弹", $"Magazine {Rounds}/{Spec.Magazine} · Reload {Spec.Reload / 60f:0.00}s · Press R")));
        tooltips.Add(new TooltipLine(Mod, "Ammo", Catalog.Text("当前弹种：", "Loaded: ") + (Ammo?.Name ?? Catalog.Text("空弹匣", "empty"))));
        if (Spec.Burst > 1) tooltips.Add(new TooltipLine(Mod, "Burst", Catalog.Text("三连发；持续按住可反复点射", "Three-round burst; hold to repeat")));
        tooltips.Add(new TooltipLine(Mod, "Unlock", Catalog.Stage(Spec.Tier)) { OverrideColor = new Color(148, 211, 181) });
        tooltips.Add(new TooltipLine(Mod, "Gunsmith", Catalog.Text("V 改枪：消音器、白光手电、可见激光 / 红外", "V: gunsmith — suppressor, white light, visible / IR aiming")));
    }

    public override void SaveData(TagCompound tag)
    {
        tag["rounds"] = Rounds;
        tag["ammo"] = Ammo?.Id ?? "";
        tag["attachments"] = (int)Attachments;
    }
    public override void LoadData(TagCompound tag)
    {
        AmmoSpec ammo = Catalog.Data.Ammo.FirstOrDefault(a => a.Id == tag.GetString("ammo") && a.Caliber == Spec.Caliber);
        LoadedAmmo = ammo?.Type ?? 0;
        Rounds = ammo == null ? 0 : Math.Clamp(tag.GetInt("rounds"), 0, Spec.Magazine);
        Attachments = CleanAttachments(tag.GetInt("attachments"));
    }
    public override void NetSend(BinaryWriter writer) { writer.Write((byte)Rounds); writer.Write(LoadedAmmo); writer.Write((byte)Attachments); }
    public override void NetReceive(BinaryReader reader)
    {
        int rounds = reader.ReadByte(); LoadedAmmo = reader.ReadInt32();
        Rounds = Ammo == null ? 0 : Math.Clamp(rounds, 0, Spec.Magazine);
        Attachments = CleanAttachments(reader.ReadByte());
    }
}

[Autoload(false)]
internal sealed class ArsenalAmmo : ModItem
{
    internal readonly AmmoSpec Spec;
    internal ArsenalAmmo(AmmoSpec spec) => Spec = spec;
    protected override bool CloneNewInstances => true;
    public override string Name => Spec.Id;
    public override string Texture => "ModernArsenal/Assets/Ammo/" + Spec.Id;
    public override void SetDefaults()
    {
        Item.width = 12; Item.height = 24;
        Item.maxStack = 9999;
        Item.damage = Spec.Damage;
        Item.DamageType = DamageClass.Ranged;
        Item.ammo = Type;
        Item.consumable = true;
        Item.shoot = ModContent.ProjectileType<BallisticRound>();
        Item.rare = Spec.Tier + 1;
        Item.value = 0;
    }
    public override void ModifyTooltips(List<TooltipLine> tooltips)
    {
        tooltips.Add(new TooltipLine(Mod, "Ballistics", Catalog.Text($"穿甲 {Spec.Armor} · 最多命中 {Spec.Penetrate} 个目标 · 射程 {Spec.Range / 16f:0} 格", $"Armor penetration {Spec.Armor} · {Spec.Penetrate} targets · Range {Spec.Range / 16f:0} tiles")));
        tooltips.Add(new TooltipLine(Mod, "Supply", Catalog.Text("在弹药补给箱免费领取；无需合成", "Claim free from a Supply Crate; no crafting")));
    }
}

internal static class CaliberNames
{
    internal static string CaliberLabel(this GunSpec spec) => Catalog.Data.Ammo.First(a => a.Caliber == spec.Caliber).Name.Split(new[] { " FMJ", " Match", " 00" }, StringSplitOptions.None)[0];
}
