using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.ObjectData;

namespace ModernArsenal;

public sealed class SupplyCrate : ModItem
{
    public override string Texture => "ModernArsenal/Assets/SupplyCrate";
    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(ModContent.TileType<SupplyTile>());
        Item.width = 36; Item.height = 26; Item.maxStack = 99;
        Item.value = 0; Item.rare = ItemRarityID.Green;
    }
}

public sealed class SupplyTile : ModTile
{
    public override string Texture => "ModernArsenal/Assets/SupplyTile";
    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true;
        Main.tileLavaDeath[Type] = false;
        TileID.Sets.DisableSmartCursor[Type] = true;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style3x2);
        TileObjectData.newTile.Origin = new Point16(1, 1);
        TileObjectData.addTile(Type);
        RegisterItemDrop(ModContent.ItemType<SupplyCrate>());
        AddMapEntry(new Color(119, 154, 108), CreateMapEntryName());
        DustType = DustID.Iron;
        HitSound = SoundID.Tink;
    }
    public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;
    public override bool RightClick(int i, int j)
    {
        ArmorySystem.Open(i, j);
        return true;
    }
    public override void MouseOver(int i, int j)
    {
        Main.LocalPlayer.noThrow = 2;
        Main.LocalPlayer.cursorItemIconEnabled = true;
        Main.LocalPlayer.cursorItemIconID = ModContent.ItemType<SupplyCrate>();
    }
}

public sealed class SupplyWorld : ModSystem
{
    internal static Point Home;
    private int ticks;
    private static readonly List<Point> beds = new();
    private static bool scannedBeds;
    public override void ClearWorld() { Home = Point.Zero; ticks = 0; beds.Clear(); scannedBeds = false; }
    public override void PostWorldLoad() => EnsureCrate();
    public override void PostUpdateWorld()
    {
        if (++ticks % 600 == 0) EnsureCrate();
    }
    internal static bool Exists(int x, int y) => WorldGen.InWorld(x, y, 5) && Main.tile[x, y].HasTile && Main.tile[x, y].TileType == ModContent.TileType<SupplyTile>();
    internal static bool FreeSite(int x, int floor, bool indoor)
    {
        if (!WorldGen.InWorld(x, floor, 10) || !WorldGen.InWorld(x + 2, floor - 2, 10)) return false;
        for (int dx = 0; dx < 3; dx++)
        {
            Tile ground = Main.tile[x + dx, floor];
            if (!ground.HasTile || !Main.tileSolid[ground.TileType] || ground.IsActuated || ground.Slope != SlopeType.Solid || ground.IsHalfBlock) return false;
            for (int dy = 1; dy <= 2; dy++)
            {
                Tile tile = Main.tile[x + dx, floor - dy];
                if (tile.HasTile || tile.LiquidAmount > 0 || (indoor && (tile.WallType == 0 || !Main.wallHouse[tile.WallType]))) return false;
            }
        }
        return true;
    }
    internal static void EnsureCrate()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient) return;
        bool exists = Exists(Home.X, Home.Y);
        if (exists && Main.tile[Home.X, Home.Y].WallType != 0 && Main.wallHouse[Main.tile[Home.X, Home.Y].WallType]) return;
        Point FindSite(Point center, int radiusX, int radiusY, bool indoor)
        {
            Point found = Point.Zero;
            int best = int.MaxValue;
            for (int x = Math.Max(20, center.X - radiusX); x < Math.Min(Main.maxTilesX - 20, center.X + radiusX); x++)
                for (int y = Math.Max(20, center.Y - radiusY); y < Math.Min(Main.maxTilesY - 20, center.Y + radiusY); y++)
                {
                    int distance = Math.Abs(x - center.X) + Math.Abs(y - center.Y) * 2;
                    if (distance >= best || !FreeSite(x, y, indoor)) continue;
                    // Leave the central spawn column and doors clear.
                    if (Math.Abs(x - Main.spawnTileX) < 5) continue;
                    bool door = false;
                    for (int dx = -2; dx <= 4; dx++)
                        for (int dy = 1; dy <= 3; dy++)
                            if (Main.tile[x + dx, y - dy].HasTile && Main.tile[x + dx, y - dy].TileType is TileID.ClosedDoor or TileID.OpenDoor) door = true;
                    if (door) continue;
                    found = new Point(x, y - 2); best = distance;
                }
            return found;
        }
        Point spawn = new(Main.spawnTileX, Main.spawnTileY);
        Point target = FindSite(spawn, 170, 100, true);
        IEnumerable<Point> occupiedHomes = Main.player.Where(p => p.active && p.SpawnX > 0).Select(p => new Point(p.SpawnX, p.SpawnY))
            .Concat(Main.npc.Where(n => n.active && n.townNPC && !n.homeless).Select(n => new Point(n.homeTileX, n.homeTileY)));
        foreach (Point home in occupiedHomes)
        {
            if (target != Point.Zero) break;
            target = FindSite(home, 40, 28, true);
        }
        if (target == Point.Zero && !scannedBeds)
        {
            // A migrated world may have its actual house far from its original spawn.
            for (int x = 20; x < Main.maxTilesX - 20; x++)
                for (int y = 20; y < Main.maxTilesY - 20; y++)
                {
                    Tile tile = Main.tile[x, y];
                    if (tile.HasTile && tile.TileType == TileID.Beds && tile.TileFrameX % 72 == 0 && tile.TileFrameY % 36 == 0)
                        beds.Add(new Point(x, y));
                }
            scannedBeds = true;
        }
        foreach (Point bed in beds.OrderBy(p => Math.Abs(p.X - spawn.X) + Math.Abs(p.Y - spawn.Y)))
        {
            if (target != Point.Zero) break;
            target = FindSite(bed, 40, 28, true);
        }
        if (target == Point.Zero && !exists) target = FindSite(spawn, 170, 100, false);
        if (target == Point.Zero) return;
        WorldGen.PlaceObject(target.X + 1, target.Y + 1, ModContent.TileType<SupplyTile>());
        if (!Exists(target.X, target.Y)) return;
        if (exists)
        {
            for (int dx = 0; dx < 3; dx++) for (int dy = 0; dy < 2; dy++)
                if (Exists(Home.X + dx, Home.Y + dy)) Main.tile[Home.X + dx, Home.Y + dy].ClearTile();
            if (Main.netMode == NetmodeID.Server) NetMessage.SendTileSquare(-1, Home.X + 1, Home.Y + 1, 5);
        }
        Home = target;
        if (Main.netMode == NetmodeID.Server)
        {
            NetMessage.SendTileSquare(-1, Home.X + 1, Home.Y + 1, 5);
            NetMessage.SendData(MessageID.WorldData);
        }
    }
    public override void SaveWorldData(TagCompound tag) { tag["x"] = Home.X; tag["y"] = Home.Y; }
    public override void LoadWorldData(TagCompound tag) => Home = new Point(tag.GetInt("x"), tag.GetInt("y"));
    public override void NetSend(BinaryWriter writer) { writer.Write(Home.X); writer.Write(Home.Y); }
    public override void NetReceive(BinaryReader reader) => Home = new Point(reader.ReadInt32(), reader.ReadInt32());
}

internal static class SupplyService
{
    internal static string Result(int code) => code switch
    {
        1 => Catalog.Text("补给已领取。弹药放入弹药栏或背包后，持枪按 R。", "Supply received. Hold a gun and press R to reload."),
        2 => Catalog.Text("尚未解锁：请推进世界进度。", "Locked: advance world progression."),
        3 => Catalog.Text("背包已满，请先腾出空间。", "Inventory full. Free some space first."),
        4 => Catalog.Text("请靠近弹药补给箱。", "Move closer to the Supply Crate."),
        5 => Catalog.Text("背包中已经有这把枪。", "You already carry this weapon."),
        _ => Catalog.Text("补给请求过快或无效。", "Supply request is too fast or invalid.")
    };
    internal static bool InReach(Player player, int x, int y) => player.active && !player.dead && SupplyWorld.Exists(x, y) &&
        Vector2.DistanceSquared(player.Center, new Vector2(x * 16 + 8, y * 16 + 8)) <= 160 * 160;
    internal static void Request(int x, int y, bool gun, int index)
    {
        if (Main.netMode != NetmodeID.MultiplayerClient) { Claim(Main.LocalPlayer, x, y, gun, index); return; }
        ModPacket packet = ModContent.GetInstance<ModernArsenal>().GetPacket();
        packet.Write((byte)0); packet.Write((short)x); packet.Write((short)y); packet.Write(gun); packet.Write((byte)index); packet.Send();
    }
    internal static int Claim(Player player, int x, int y, bool gun, int index)
    {
        int code;
        ArsenalPlayer state = player.GetModPlayer<ArsenalPlayer>();
        if (!InReach(player, x, y)) code = 4;
        else if (index < 0 || index >= (gun ? Catalog.Data.Guns.Length : Catalog.Data.Ammo.Length) ||
            (state.LastClaim != 0 && Main.GameUpdateCount - state.LastClaim < 8)) code = 0;
        else
        {
            state.LastClaim = Main.GameUpdateCount;
            int tier = gun ? Catalog.Data.Guns[index].Tier : Catalog.Data.Ammo[index].Tier;
            int type = gun ? Catalog.Data.Guns[index].Type : Catalog.Data.Ammo[index].Type;
            if (tier > Catalog.Progress) code = 2;
            else if (gun && player.HasItem(type)) code = 5;
            else code = Grant(player, type, gun ? 1 : 600) > 0 ? 1 : 3;
        }
        if (Main.netMode == NetmodeID.Server)
        {
            ModPacket packet = ModContent.GetInstance<ModernArsenal>().GetPacket();
            packet.Write((byte)1); packet.Write((byte)code); packet.Send(player.whoAmI);
        }
        else ArmorySystem.Status = Result(code);
        return code;
    }
    internal static int Grant(Player player, int type, int count)
    {
        int given = 0;
        Item prototype = new(type);
        var slots = prototype.ModItem is ArsenalAmmo ? ArsenalPlayer.InventorySlots.ToArray() : Enumerable.Range(0, 50).ToArray();
        foreach (bool empty in new[] { false, true })
            foreach (int slot in slots)
            {
                Item item = player.inventory[slot];
                if (empty ? !item.IsAir : item.type != type || item.IsAir) continue;
                if (item.IsAir) { player.inventory[slot] = item = prototype.Clone(); item.stack = 0; }
                int take = Math.Min(count - given, item.maxStack - item.stack);
                if (take <= 0) continue;
                item.stack += take; given += take;
                if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncEquipment, -1, -1, null, player.whoAmI, slot);
                if (given == count) return given;
            }
        return given;
    }
}
