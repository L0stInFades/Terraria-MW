using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace ModernArsenal;

internal sealed class GunAnimation
{
    public string Id { get; set; }
    public string Action { get; set; }
    public int[] Part { get; set; }
    public int Travel { get; set; }
    public float KickPixels { get; set; }
    public float KickDegrees { get; set; }
    public int Cycle { get; set; }
    public int Peak { get; set; }
    public int ActionDelay { get; set; }
    public int ActionTicks { get; set; }
    public int EjectDelay { get; set; }
    public int[] Muzzle { get; set; }
    public int[] Eject { get; set; }
    public float CasingX { get; set; }
    public float CasingY { get; set; }
    public int Casing { get; set; }
    public int Flash { get; set; }
    public int FlashTicks { get; set; }
    public string Notes { get; set; }
    internal float KickAt(int age)
    {
        if (age < 0 || age >= Cycle) return 0;
        if (age < Peak) return (age + 1f) / Peak;
        float remaining = 1f - (age - Peak) / (float)(Cycle - Peak);
        return remaining * remaining;
    }
    internal int FrameAt(int age) => age < ActionDelay || age >= ActionDelay + ActionTicks ? 0 :
        1 + Math.Min(4, (age - ActionDelay) * 5 / ActionTicks);
}

public sealed class SpentCasing : ModDust
{
    public override string Texture => "ModernArsenal/Assets/Casings";
    public override void OnSpawn(Dust dust)
    {
        dust.noGravity = true;
        dust.noLight = true;
        dust.frame = new Rectangle(0, 0, 10, 6);
        dust.fadeIn = 0;
    }
    public override bool Update(Dust dust)
    {
        Vector2 previous = dust.position;
        dust.position += dust.velocity;
        dust.velocity.Y += 0.14f;
        dust.velocity.X *= 0.985f;
        if (Collision.SolidCollision(dust.position, 4, 3))
        {
            dust.position = previous;
            dust.velocity = new Vector2(dust.velocity.X * 0.6f, -Math.Abs(dust.velocity.Y) * 0.35f);
        }
        dust.rotation += dust.velocity.X * 0.12f;
        dust.alpha += 5;
        if (dust.alpha >= 255) dust.active = false;
        return false;
    }
    public override Color? GetAlpha(Dust dust, Color lightColor) => new Color(230, 207, 151, 255) * (1f - dust.alpha / 255f);
}
