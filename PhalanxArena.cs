using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace ModernArsenal;

public sealed class PhalanxWorld : ModSystem
{
    internal static bool Enabled;
    internal static byte Ammo;
    internal static readonly List<Point16> Arenas = new();
    private static readonly long[] lastRequest = new long[256];
    internal static string AmmoName => Ammo == 0 ? Catalog.Text("20mm 穿甲弹", "20mm APDS") : Catalog.Text("20mm 高爆曳光", "20mm HEI-T");
    public override void ClearWorld()
    { Enabled = false; Ammo = 0; Arenas.Clear(); Array.Fill(lastRequest, -1000); }
    public override void SaveWorldData(TagCompound tag)
    {
        tag["power"] = Enabled; tag["ammo"] = (int)Ammo;
        tag["arenas"] = Arenas.Select(p => new TagCompound { ["x"] = (int)p.X, ["y"] = (int)p.Y }).ToList();
    }
    public override void LoadWorldData(TagCompound tag)
    {
        Enabled = tag.GetBool("power"); Ammo = (byte)Math.Clamp(tag.GetInt("ammo"), 0, 1);
        Arenas.Clear();
        foreach (TagCompound entry in tag.GetList<TagCompound>("arenas"))
        {
            int x = entry.GetInt("x"), y = entry.GetInt("y");
            if (WorldGen.InWorld(x, y, 10) && x + PhalanxArena.Width < Main.maxTilesX - 10 && y > 80) Arenas.Add(new Point16(x, y));
        }
    }
    public override void NetSend(BinaryWriter writer)
    {
        writer.Write(Enabled); writer.Write(Ammo); writer.Write((ushort)Arenas.Count);
        foreach (Point16 point in Arenas) { writer.Write(point.X); writer.Write(point.Y); }
    }
    public override void NetReceive(BinaryReader reader)
    {
        Enabled = reader.ReadBoolean(); Ammo = (byte)Math.Clamp((int)reader.ReadByte(), 0, 1);
        Arenas.Clear(); int count = reader.ReadUInt16();
        if (count > 1024) throw new InvalidDataException("Invalid CIWS arena count");
        for (int i = 0; i < count; i++)
        {
            short x = reader.ReadInt16(), y = reader.ReadInt16();
            if (WorldGen.InWorld(x, y, 10)) Arenas.Add(new Point16(x, y));
        }
    }
    internal static void Request(Player player, byte operation)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            ModPacket packet = ModContent.GetInstance<ModernArsenal>().GetPacket();
            packet.Write((byte)2); packet.Write(operation); packet.Send();
        }
        else Command(player, operation);
    }
    internal static bool Command(Player player, byte operation)
    {
        if (operation > 2 || !player.active || player.dead || player.whoAmI < 0 || player.whoAmI >= Main.maxPlayers ||
            !player.HasItem(ModContent.ItemType<PhalanxRemote>())) return false;
        long tick = (long)Main.GameUpdateCount;
        if (tick - lastRequest[player.whoAmI] < 10) return false;
        lastRequest[player.whoAmI] = tick;
        if (operation == 2)
        {
            if (!PhalanxArena.Equip(player)) return false;
        }
        else if (operation == 0) Enabled = !Enabled;
        else Ammo = (byte)(1 - Ammo);
        if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.WorldData);
        if (!Main.dedServ && operation != 2)
            Main.NewText(Catalog.Text($"密集阵：{(Enabled ? "已开机" : "已关闭")} · {AmmoName} · 无限弹药", $"CIWS: {(Enabled ? "ON" : "OFF")} · {AmmoName} · Infinite ammunition"), Color.LightCyan);
        return true;
    }
}

internal static class PhalanxArena
{
    internal const int Width = 240, Rows = 6, Spacing = 12;
    internal static Vector2 Arrival(Point16 arena) => new((arena.X + Width / 2 + 6) * 16, arena.Y * 16);
    internal static bool Exists(Point16 arena) => WorldGen.InWorld(arena.X, arena.Y, 10) &&
        Main.tile[arena.X, arena.Y].HasTile && Main.tile[arena.X, arena.Y].TileType == TileID.Platforms;
    internal static bool ClearSite(int left, int floor)
    {
        if (!WorldGen.InWorld(left, floor, 20) || !WorldGen.InWorld(left + Width, floor - Spacing * (Rows - 1) - 8, 20)) return false;
        for (int x = left; x < left + Width; x++)
            for (int y = floor - Spacing * (Rows - 1) - 8; y <= floor; y++)
                if (Main.tile[x, y].HasTile || Main.tile[x, y].LiquidAmount > 0) return false;
        return true;
    }
    internal static Point16 Build(Vector2 near)
    {
        foreach (Point16 old in PhalanxWorld.Arenas)
            if (Exists(old) && Vector2.DistanceSquared(Arrival(old), near) < 3000 * 3000) return old;
        int center = Math.Clamp((int)(near.X / 16), Width / 2 + 24, Main.maxTilesX - Width / 2 - 24);
        int preferred = Math.Clamp((int)(near.Y / 16) - 4, 100, Main.maxTilesY - 24);
        Point16 selected = Point16.NegativeOne;
        foreach (int offset in new[] { 0, Width, -Width })
        {
            int left = Math.Clamp(center - Width / 2 + offset, 24, Main.maxTilesX - Width - 24);
            for (int floor = preferred; floor >= Math.Max(100, preferred - 160); floor -= 2)
                if (ClearSite(left, floor)) { selected = new Point16(left, floor); break; }
            if (selected != Point16.NegativeOne) break;
        }
        if (selected == Point16.NegativeOne) return selected;
        int x0 = selected.X, y0 = selected.Y;
        List<Point16> created = new();
        List<Point16> entities = new();
        int chest = -1;
        void Place(int x, int y, ushort type)
        {
            Tile tile = Main.tile[x, y];
            tile.HasTile = true; tile.TileType = type; tile.Slope = SlopeType.Solid; tile.IsHalfBlock = false;
            tile.TileFrameX = tile.TileFrameY = 0;
            created.Add(new Point16(x, y));
        }
        try
        {
            for (int row = 0; row < Rows; row++)
                for (int dx = 0; dx < Width; dx++)
                {
                    if (dx == Width / 2 - 8 || dx == Width / 2 + 8) continue;
                    Place(x0 + dx, y0 - row * Spacing, TileID.Platforms);
                }
            for (int row = 0; row < Rows; row += 2)
                foreach (int dx in new[] { 54, Width - 58 })
                {
                    int x = x0 + dx, floor = y0 - row * Spacing;
                    WorldGen.PlaceObject(x + 2, floor - 1, ModContent.TileType<PhalanxTile>());
                    int top = floor - PhalanxTile.Height;
                    if (!Main.tile[x, top].HasTile || Main.tile[x, top].TileType != ModContent.TileType<PhalanxTile>())
                        throw new InvalidOperationException("CIWS platform placement failed");
                    for (int xx = 0; xx < PhalanxTile.Width; xx++) for (int yy = 0; yy < PhalanxTile.Height; yy++) created.Add(new Point16(x + xx, top + yy));
                    Point16 position = new(x, top);
                    if (!TileEntity.ByPosition.ContainsKey(position)) ModContent.GetInstance<PhalanxEntity>().Place(x, top);
                    entities.Add(position);
                }
            for (int dx = Width / 2 - 2; dx <= Width / 2 + 1; dx++)
                Main.tile[x0 + dx, y0].TileType = TileID.GrayBrick;
            chest = WorldGen.PlaceChest(x0 + Width / 2 - 1, y0 - 1);
            if (chest < 0) throw new InvalidOperationException("CIWS control chest placement failed");
            Main.chest[chest].name = Catalog.Text("密集阵控制箱", "CIWS control locker");
            for (int xx = 0; xx < 2; xx++) for (int yy = 1; yy <= 2; yy++) created.Add(new Point16(Main.chest[chest].x + xx, y0 - yy));
            Main.chest[chest].item[0] = new Item(ModContent.ItemType<PhalanxTurret>(), 8);
            Main.chest[chest].item[1] = new Item(ModContent.ItemType<PhalanxRemote>());
            foreach (int row in new[] { 0, 3 })
                foreach (int dx in new[] { 24, 88, 152, 216 })
                {
                    int x = x0 + dx, floor = y0 - row * Spacing;
                    WorldGen.PlaceObject(x, floor - 1, TileID.Campfire);
                    if (!Main.tile[x, floor - 1].HasTile || Main.tile[x, floor - 1].TileType != TileID.Campfire)
                        throw new InvalidOperationException("Arena campfire placement failed");
                    for (int xx = -1; xx <= 1; xx++) for (int yy = 1; yy <= 2; yy++) created.Add(new Point16(x + xx, floor - yy));
                }
            Item heartLantern = new(ItemID.HeartLantern);
            foreach (int row in new[] { 2, 5 })
                foreach (int dx in new[] { 24, 88, 152, 216 })
                {
                    int x = x0 + dx, roof = y0 - row * Spacing;
                    Main.tile[x, roof].TileType = TileID.GrayBrick;
                    WorldGen.PlaceObject(x, roof + 1, heartLantern.createTile, style: heartLantern.placeStyle);
                    if (!Main.tile[x, roof + 1].HasTile || Main.tile[x, roof + 1].TileType != heartLantern.createTile)
                        throw new InvalidOperationException("Arena heart lantern placement failed");
                    for (int yy = 1; yy <= 2; yy++) created.Add(new Point16(x, roof + yy));
                }
            foreach (int dx in new[] { Width / 2 - 8, Width / 2 + 8 })
                for (int y = y0 - Spacing * (Rows - 1) - 5; y < Math.Min(y0 + 150, Main.maxTilesY - 20); y++)
                {
                    if (Main.tile[x0 + dx, y].HasTile) break;
                    Place(x0 + dx, y, TileID.Rope);
                }
            foreach (Point16 point in created) WorldGen.SquareTileFrame(point.X, point.Y, true);
            PhalanxWorld.Arenas.Add(selected);
            if (Main.netMode == NetmodeID.Server)
            {
                // Tile-square packets have a fixed buffer; synchronize a large arena in small sections.
                for (int x = x0; x < x0 + Width; x += 24)
                    for (int y = y0 - Spacing * (Rows - 1) - 8; y < Math.Min(y0 + 150, Main.maxTilesY - 20); y += 24)
                        NetMessage.SendTileSquare(-1, Math.Clamp(x + 12, 12, Main.maxTilesX - 13), Math.Clamp(y + 12, 12, Main.maxTilesY - 13), 24);
                foreach (Point16 position in entities)
                {
                    TileEntity entity = TileEntity.ByPosition[position];
                    NetMessage.SendData(MessageID.TileEntitySharing, -1, -1, null, entity.ID, position.X, position.Y);
                }
                NetMessage.SendData(MessageID.WorldData);
            }
            return selected;
        }
        catch
        {
            PhalanxWorld.Arenas.Remove(selected);
            foreach (Point16 point in entities) ModContent.GetInstance<PhalanxEntity>().Kill(point.X, point.Y);
            if (chest >= 0) { Main.chest[chest].item = Enumerable.Range(0, 40).Select(_ => new Item()).ToArray(); Chest.DestroyChest(Main.chest[chest].x, Main.chest[chest].y); }
            foreach (Point16 point in created) Main.tile[point.X, point.Y].ClearTile();
            throw;
        }
    }
    internal static int ControlChest(Point16 arena) => Chest.FindChest(arena.X + Width / 2 - 1, arena.Y - 2);
    internal static bool Equip(Player player)
    {
        Point16 arena = Build(player.Center);
        if (arena == Point16.NegativeOne)
        {
            if (!Main.dedServ) Main.NewText(Catalog.Text("附近没有足够净空，请移到开阔处部署平台。", "Move to an open area with enough clear space for the platform position."), Color.Gold);
            return false;
        }
        int chestId = ControlChest(arena);
        if (chestId < 0) throw new InvalidOperationException("The CIWS control locker is missing");
        Item[] stock = Main.chest[chestId].item;
        int spare = Array.FindIndex(stock, i => i.ModItem is PhalanxTurret);
        if (spare >= 0) stock[spare].stack = Math.Max(8, stock[spare].stack);
        else
        {
            int free = Array.FindIndex(stock, i => i.IsAir);
            if (free >= 0) stock[free] = new Item(ModContent.ItemType<PhalanxTurret>(), 8);
        }
        Item[] inventory = player.inventory;
        int remoteType = ModContent.ItemType<PhalanxRemote>();
        int current = Array.FindIndex(inventory, 0, 50, i => i.type == remoteType);
        if (current >= 0) (inventory[9], inventory[current]) = (inventory[current], inventory[9]);
        else
        {
            int empty = Array.FindIndex(inventory, 0, 50, i => i.IsAir);
            if (empty >= 0) inventory[empty] = inventory[9];
            else if (!inventory[9].IsAir)
            {
                Item[] storage = Main.chest[chestId].item;
                int slot = Array.FindIndex(storage, i => i.IsAir);
                if (slot < 0)
                { if (!Main.dedServ) Main.NewText(Catalog.Text("背包与控制箱都满了，请先腾出空间。", "Free an inventory or control-locker slot first."), Color.Gold); return false; }
                storage[slot] = inventory[9];
            }
            inventory[9] = new Item(remoteType);
        }
        player.selectedItem = 9;
        player.Teleport(Arrival(arena) - new Vector2(player.width / 2f, player.height));
        if (Main.netMode == NetmodeID.Server)
        {
            for (int i = 0; i < 50; i++) NetMessage.SendData(MessageID.SyncEquipment, -1, -1, null, player.whoAmI, i);
            NetMessage.SendData(MessageID.TeleportEntity, -1, -1, null, 0, player.whoAmI, player.position.X, player.position.Y, 1);
        }
        if (!Main.dedServ) Main.NewText(Catalog.Text("10 号快捷栏：密集阵遥控器；控制箱有备用炮塔。原物品保留在背包或控制箱。", "Hotbar 10: CIWS remote. Spare turrets are in the control locker; displaced equipment is preserved."), Color.LightCyan);
        return true;
    }
}
