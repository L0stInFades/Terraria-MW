using System;
using System.Linq;
using System.Text.Json;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ModernArsenal;

internal sealed class GunSpec
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Family { get; set; }
    public string Caliber { get; set; }
    public int Damage { get; set; }
    public int Interval { get; set; }
    public int Magazine { get; set; }
    public int Reload { get; set; }
    public float Speed { get; set; }
    public float Spread { get; set; }
    public int Pellets { get; set; }
    public int Burst { get; set; }
    public int Tier { get; set; }
    public float Knockback { get; set; }
    public int Crit { get; set; }
    internal GunAnimation Animation;
    public int Type => ModContent.Find<ModItem>("ModernArsenal/" + Id).Type;
}

internal sealed class AmmoSpec
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Caliber { get; set; }
    public int Damage { get; set; }
    public int Tier { get; set; }
    public int Armor { get; set; }
    public int Penetrate { get; set; }
    public float Gravity { get; set; }
    public float Drag { get; set; }
    public int Range { get; set; }
    public string Color { get; set; }
    public string Kind { get; set; }
    public int Type => ModContent.Find<ModItem>("ModernArsenal/" + Id).Type;
}

internal sealed class Catalog
{
    public GunSpec[] Guns { get; set; }
    public AmmoSpec[] Ammo { get; set; }
    internal static Catalog Data;
    internal static bool Chinese => Language.ActiveCulture.Name == "zh-Hans";
    internal static string Text(string zh, string en) => Chinese ? zh : en;
    internal static int Progress => NPC.downedPlantBoss ? 5 : NPC.downedMechBossAny ? 4 : Main.hardMode ? 3 :
        NPC.downedBoss2 || NPC.downedBoss3 ? 2 : NPC.downedBoss1 ? 1 : 0;
    internal static string Stage(int tier) => Text(
        new[] { "新兵 · 开局", "战术 · 克眼后", "精锐 · 邪恶首领或骷髅王后", "特战 · 困难模式", "重装 · 任一机械首领后", "王牌 · 世纪之花后" }[tier],
        new[] { "Recruit / Start", "Tactical / Eye defeated", "Veteran / Evil boss or Skeletron", "Specialist / Hardmode", "Heavy / Any mechanical boss", "Ace / Plantera defeated" }[tier]);
    internal static string Family(string family) => Text(family switch
    { "Pistol" => "手枪", "SMG" => "冲锋枪", "Rifle" => "突击步枪", "Shotgun" => "霰弹枪", "LMG" => "轻机枪", "Marksman" => "精确射手", _ => "狙击步枪" }, family);

    internal static void Load(Mod mod)
    {
        Data = JsonSerializer.Deserialize<Catalog>(mod.GetFileBytes("Data/arsenal.json"));
        if (Data?.Guns?.Length != 24 || Data.Ammo?.Length != 19 ||
            Data.Guns.Select(g => g.Id).Concat(Data.Ammo.Select(a => a.Id)).Distinct().Count() != 43 ||
            Data.Guns.Any(g => g.Magazine <= 0 || g.Reload <= 0 || g.Interval <= 0 || g.Burst <= 0 || g.Tier is < 0 or > 5 || !Data.Ammo.Any(a => a.Caliber == g.Caliber && a.Tier <= g.Tier)) ||
            Data.Ammo.Any(a => a.Tier is < 0 or > 5 || a.Penetrate <= 0 || a.Range <= 0 || a.Drag <= 0 || a.Drag > 1))
            throw new InvalidOperationException("Invalid Modern Arsenal content catalog");
        GunAnimation[] animations = JsonSerializer.Deserialize<GunAnimation[]>(mod.GetFileBytes("Data/animations.json"));
        if (animations.Length != Data.Guns.Length || animations.Select(a => a.Id).Distinct().Count() != Data.Guns.Length ||
            animations.Any(a => !Data.Guns.Any(g => g.Id == a.Id) || a.Cycle <= a.Peak || a.Peak < 1 || a.ActionTicks < 1 ||
                a.ActionDelay < 0 || a.ActionDelay + a.ActionTicks > a.Cycle || a.EjectDelay < 0 || a.EjectDelay >= a.Cycle ||
                a.Muzzle?.Length != 2 || a.Eject?.Length != 2 || a.Part?.Length != 4 || a.KickPixels < 0 || a.KickDegrees < 0))
            throw new InvalidOperationException("Invalid per-weapon animation catalog");
        foreach (GunSpec gun in Data.Guns) gun.Animation = animations.Single(a => a.Id == gun.Id);
        foreach (AmmoSpec ammo in Data.Ammo) mod.AddContent(new ArsenalAmmo(ammo));
        foreach (GunSpec gun in Data.Guns) mod.AddContent(new ArsenalGun(gun));
    }
}
