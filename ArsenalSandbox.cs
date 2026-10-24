using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace ModernArsenal;

public sealed class ArsenalSandboxPlayer : ModPlayer
{
    internal bool GodEnabled;
    private bool Invincible => GodEnabled && Main.netMode == NetmodeID.SinglePlayer;
    public override void SaveData(TagCompound tag) => tag["god"] = GodEnabled;
    public override void LoadData(TagCompound tag) => GodEnabled = tag.GetBool("god");
    public override void PostUpdateEquips()
    {
        if (!Invincible) return;
        Player.creativeGodMode = true;
        Player.noFallDmg = true;
        Player.lavaImmune = true;
        Player.breath = Player.breathMax;
        Player.statLife = Player.statLifeMax2;
        for (int type = 0; type < BuffLoader.BuffCount; type++)
            if (Main.debuff[type]) Player.buffImmune[type] = true;
    }
    public override void PostUpdateMiscEffects()
    {
        if (!Invincible) return;
        Player.lifeRegen = Math.Max(0, Player.lifeRegen);
        Player.lifeRegenCount = Math.Max(0, Player.lifeRegenCount);
    }
    public override bool ImmuneTo(PlayerDeathReason damageSource, int cooldownCounter, bool dodgeable) => Invincible;
    public override bool PreKill(double damage, int hitDirection, bool pvp, ref bool playSound, ref bool genGore, ref PlayerDeathReason damageSource)
    {
        if (!Invincible) return true;
        Player.statLife = Player.statLifeMax2;
        return false;
    }
}

public sealed class ArsenalCommand : ModCommand
{
    public override CommandType Type => CommandType.Chat;
    public override string Command => "arsenal";
    public override string Usage => "/arsenal god on | god off | all | rush | ciws";
    public override string Description => Catalog.Text("单人：切换无敌，或配齐全部已装弹枪械与弹药", "Single-player: toggle god mode or equip the complete loaded arsenal");
    public override void Action(CommandCaller caller, string input, string[] args)
    {
        if (Main.netMode != NetmodeID.SinglePlayer || caller.Player.dead)
        { caller.Reply(Catalog.Text("请在单人世界中存活时使用。", "Use this while alive in a single-player world.")); return; }
        if (args.Length == 2 && args[0] == "god" && args[1] is "on" or "off")
        {
            caller.Player.GetModPlayer<ArsenalSandboxPlayer>().GodEnabled = args[1] == "on";
            caller.Reply(args[1] == "on" ? Catalog.Text("无敌已开启并保存。", "God mode enabled and saved.") : Catalog.Text("无敌已关闭并保存。", "God mode disabled and saved."), Color.LightGreen);
        }
        else if (args.Length == 1 && args[0] == "rush")
        {
            if (!ModLoader.TryGetMod("ExampleBossRush", out Mod rush) || !rush.TryFind<ModItem>("BossRushItem", out var starter))
            { caller.Reply(Catalog.Text("请先启用 Example Boss Rush 及 Boss Rush API。", "Enable Example Boss Rush and Boss Rush API first.")); return; }
            new Item(starter.Type).ModItem.UseItem(caller.Player);
            caller.Reply(Catalog.Text("已切换连战；再次输入 /arsenal rush 可停止。", "Boss rush toggled; enter /arsenal rush again to stop."), Color.LightGreen);
        }
        else if (args.Length == 1 && args[0] == "ciws")
        {
            if (!PhalanxArena.Equip(caller.Player)) return;
        }
        else if (args.Length == 1 && args[0] == "all")
        {
            if (!EquipAll(caller.Player))
            { caller.Reply(Catalog.Text("背包空位不足，请先腾出空间。", "Free inventory space before equipping the complete arsenal.")); return; }
            caller.Reply(Catalog.Text("24 把枪已装满弹匣；19 种弹药各至少 600 发。", "24 loaded guns equipped; at least 600 of each of the 19 ammo types supplied."), Color.LightGreen);
        }
        else { caller.Reply(Usage); return; }
        Player.SavePlayer(Main.ActivePlayerFileData);
    }
    internal static bool EquipAll(Player player)
    {
        if (Main.netMode != NetmodeID.SinglePlayer || player.dead) return false;
        int missingGuns = Catalog.Data.Guns.Count(g => !player.inventory.Take(50).Any(i => i.type == g.Type));
        int missingAmmo = Catalog.Data.Ammo.Count(a => !ArsenalPlayer.InventorySlots.Any(i => player.inventory[i].type == a.Type));
        int ammoSpace = Enumerable.Range(54, 4).Count(i => player.inventory[i].IsAir);
        if (player.inventory.Take(50).Count(i => i.IsAir) < missingGuns + Math.Max(0, missingAmmo - ammoSpace)) return false;
        foreach (GunSpec spec in Catalog.Data.Guns)
            if (!player.inventory.Take(50).Any(i => i.type == spec.Type)) SupplyService.Grant(player, spec.Type, 1);
        foreach (AmmoSpec spec in Catalog.Data.Ammo)
        {
            int count = ArsenalPlayer.InventorySlots.Sum(i => player.inventory[i].type == spec.Type ? player.inventory[i].stack : 0);
            if (count < 600) SupplyService.Grant(player, spec.Type, 600 - count);
        }
        Item[] guns = Catalog.Data.Guns.Select(g => player.inventory.Take(50).First(i => i.type == g.Type)).ToArray();
        Item[] otherItems = player.inventory.Take(50).Where(i => !i.IsAir && !guns.Any(g => ReferenceEquals(g, i))).ToArray();
        for (int i = 0; i < 50; i++) player.inventory[i] = new Item();
        for (int i = 0; i < guns.Length; i++)
        {
            player.inventory[i] = guns[i];
            ArsenalGun gun = (ArsenalGun)guns[i].ModItem;
            gun.LoadedAmmo = Catalog.Data.Ammo.First(a => a.Caliber == gun.Spec.Caliber).Type;
            gun.Rounds = gun.Spec.Magazine;
        }
        for (int i = 0; i < otherItems.Length; i++) player.inventory[guns.Length + i] = otherItems[i];
        player.selectedItem = 0;
        return true;
    }
}
