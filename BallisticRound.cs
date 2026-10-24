using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace ModernArsenal;

public sealed class BallisticRound : ModProjectile
{
    internal const int Updates = 8;
    internal float DamageScale => float.IsFinite(Projectile.ai[2]) ? Math.Clamp(Projectile.ai[2], 1f, 1000f) : 1f;
    public override string Texture => "ModernArsenal/Assets/Round";
    private AmmoSpec Spec => Catalog.Data.Ammo[Math.Clamp((int)Projectile.ai[0], 0, Catalog.Data.Ammo.Length - 1)];
    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 4;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.extraUpdates = Updates - 1;
        Projectile.timeLeft = 240 * Updates;
        Projectile.penetrate = 1;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
        Projectile.ignoreWater = false;
    }
    public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
    {
        Projectile.penetrate = Spec.Penetrate;
        Projectile.ArmorPenetration = Spec.Armor;
    }
    public override void AI()
    {
        AmmoSpec ammo = Spec;
        Projectile.velocity.X *= MathF.Pow(ammo.Drag, 1f / Updates);
        Projectile.velocity.Y += ammo.Gravity / (Updates * Updates);
        Projectile.ai[1] += Projectile.velocity.Length();
        Projectile.rotation = Projectile.velocity.ToRotation();
        if (Projectile.ai[1] >= ammo.Range) Projectile.Kill();
        if (ammo.Kind == "Tracer" && Projectile.numUpdates == 0)
            Lighting.AddLight(Projectile.Center, 0.65f, 0.25f, 0.08f);
    }
    public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
    {
        modifiers.SourceDamage *= DamageScale;
        float range = Spec.Range;
        modifiers.FinalDamage *= MathHelper.Clamp(1f - Math.Max(0, Projectile.ai[1] / range - 0.45f) * 0.7f, 0.6f, 1f);
        if (Spec.Kind == "JHP" && target.defense <= 15) modifiers.FinalDamage *= 1.15f;
    }
    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        Projectile.damage = Math.Max(1, (int)(Projectile.damage * 0.85f));
        Projectile.netUpdate = true;
    }
    public override bool PreDraw(ref Color lightColor)
    {
        uint hex = Convert.ToUInt32(Spec.Color, 16);
        Color color = new((int)(hex >> 16), (int)((hex >> 8) & 255), (int)(hex & 255));
        Vector2 start = Projectile.Center - Main.screenPosition;
        float length = Spec.Kind == "Tracer" ? 34 : 15;
        Main.EntitySpriteDraw(TextureAssets.MagicPixel.Value, start, new Rectangle(0, 0, 1, 1), color * 0.7f, Projectile.rotation,
            new Vector2(1, 0.5f), new Vector2(length, 2), SpriteEffects.None);
        Main.EntitySpriteDraw(TextureAssets.MagicPixel.Value, start, new Rectangle(0, 0, 1, 1), Color.White, 0, Vector2.Zero, new Vector2(3, 2), SpriteEffects.None);
        return false;
    }
    public override void OnKill(int timeLeft)
    {
        if (Main.dedServ) return;
        for (int i = 0; i < 3; i++)
            Dust.NewDustPerfect(Projectile.Center, DustID.Torch, -Projectile.velocity * 0.2f + Main.rand.NextVector2Circular(1.5f, 1.5f), 140, default, 0.6f).noGravity = true;
    }
}
