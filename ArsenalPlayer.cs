using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace ModernArsenal;

public sealed class ArsenalPlayer : ModPlayer
{
    internal int ReloadTicks;
    internal int ReloadTotal;
    internal ulong LastClaim;
    private ArsenalGun reloading;
    private ArsenalGun lastGun;
    private bool starterGranted;
    internal static IEnumerable<int> InventorySlots => Enumerable.Range(54, 4).Concat(Enumerable.Range(0, 50));

    internal static IEnumerable<Item> Starter()
    {
        Item gun = new(Catalog.Data.Guns[0].Type);
        ArsenalGun usp = (ArsenalGun)gun.ModItem;
        usp.LoadedAmmo = Catalog.Data.Ammo.First(a => a.Id == "FortyFiveFMJ").Type;
        usp.Rounds = usp.Spec.Magazine;
        yield return gun;
        yield return new Item(usp.LoadedAmmo, 600);
    }
    public override IEnumerable<Item> AddStartingItems(bool mediumCoreDeath)
    {
        if (mediumCoreDeath) return Array.Empty<Item>();
        starterGranted = true;
        return Starter();
    }
    public override void OnEnterWorld()
    {
        if (!starterGranted)
        {
            foreach (Item item in Starter()) Player.QuickSpawnItem(Player.GetSource_GiftOrReward(), item, item.stack);
            starterGranted = true;
        }
        ReloadTicks = 0; reloading = null;
        var keys = PlayerInput.CurrentProfile.InputModes[InputMode.Keyboard].KeyStatus;
        keys.TryAdd(Mod.Name + "/Reload", new());
        if (ModernArsenal.ReloadKey.GetAssignedKeys().Count == 0) ModernArsenal.ReloadKey.GetAssignedKeys().Add("R");
        keys.TryAdd(Mod.Name + "/ModifyGun", new());
        if (ModernArsenal.ModifyKey.GetAssignedKeys().Count == 0) ModernArsenal.ModifyKey.GetAssignedKeys().Add("V");
        Main.NewText(Catalog.Text("现代军械库：右键补给箱领弹；R 换弹，V 改枪。", "Modern Arsenal: right-click a Supply Crate for ammo; R reloads, V opens the gunsmith."), 166, 220, 194);
    }
    public override void SaveData(TagCompound tag) => tag["starterGranted"] = starterGranted;
    public override void LoadData(TagCompound tag) => starterGranted = tag.GetBool("starterGranted");
    public override void ProcessTriggers(TriggersSet triggersSet)
    {
        if (Main.drawingPlayerChat || Main.editSign || Main.editChest || PlayerInput.WritingText) return;
        if (ModernArsenal.ModifyKey.JustPressed) GunsmithSystem.Toggle();
        if (ModernArsenal.ReloadKey.JustPressed && !ArmorySystem.IsOpen && !GunsmithSystem.IsOpen) BeginReload();
    }
    internal Item Reserve(ArsenalGun gun)
    {
        foreach (int slot in InventorySlots)
        {
            Item item = Player.inventory[slot];
            if (item.stack > 0 && item.ModItem is ArsenalAmmo ammo && ammo.Spec.Caliber == gun.Spec.Caliber &&
                (gun.Rounds == 0 || item.type == gun.LoadedAmmo)) return item;
        }
        return null;
    }
    internal bool BeginReload()
    {
        if (Player.dead || ArmorySystem.IsOpen || GunsmithSystem.IsOpen || ReloadTicks > 0 ||
            Player.HeldItem.ModItem is not ArsenalGun gun || gun.Rounds >= gun.Spec.Magazine || Reserve(gun) == null)
            return false;
        reloading = gun;
        gun.ClearVisuals();
        Player.itemAnimation = Player.itemTime = Player.reuseDelay = 0;
        Player.channel = false;
        ReloadTicks = ReloadTotal = gun.Spec.Reload;
        if (!Main.dedServ) SoundEngine.PlaySound(SoundID.Item149 with { Volume = 0.4f }, Player.Center);
        return true;
    }
    public override void PostUpdate()
    {
        if (Player.whoAmI != Main.myPlayer) return;
        ArsenalGun current = Player.HeldItem.ModItem as ArsenalGun;
        if (!ReferenceEquals(lastGun, current)) { lastGun?.ClearVisuals(); lastGun = current; }
        if (Player.dead) current?.ClearVisuals();
        if (ReloadTicks <= 0) return;
        if (Player.dead || !ReferenceEquals(Player.HeldItem.ModItem, reloading))
        { ReloadTicks = 0; reloading = null; return; }
        if (--ReloadTicks != 0) return;
        FinishReload();
    }
    internal void FinishReload()
    {
        ArsenalGun gun = reloading;
        ReloadTicks = 0; reloading = null;
        if (gun == null || !ReferenceEquals(Player.HeldItem.ModItem, gun) || Player.dead) return;
        Item reserve = Reserve(gun);
        if (reserve == null) return;
        int type = reserve.type;
        int needed = gun.Spec.Magazine - gun.Rounds;
        foreach (int slot in InventorySlots)
        {
            Item item = Player.inventory[slot];
            if (item.type != type || item.stack <= 0) continue;
            int take = Math.Min(needed, item.stack);
            item.stack -= take;
            gun.Rounds += take;
            needed -= take;
            if (item.stack == 0) item.TurnToAir();
            if (Main.netMode == NetmodeID.MultiplayerClient)
                NetMessage.SendData(MessageID.SyncEquipment, -1, -1, null, Player.whoAmI, slot);
            if (needed == 0) break;
        }
        gun.LoadedAmmo = type;
        gun.Sync(Player);
        if (!Main.dedServ) SoundEngine.PlaySound(SoundID.Item149 with { Volume = 0.6f, Pitch = 0.25f }, Player.Center);
    }
}
