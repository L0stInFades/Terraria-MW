using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ModernArsenal;

public sealed class ModernArsenal : Mod
{
    internal static ModKeybind ReloadKey;
    internal static ModKeybind ModifyKey;
    public override void Load()
    {
        Catalog.Load(this);
        ReloadKey = KeybindLoader.RegisterKeybind(this, "Reload", "R");
        ModifyKey = KeybindLoader.RegisterKeybind(this, "ModifyGun", "V");
    }

    public override void Unload() { ReloadKey = ModifyKey = null; Catalog.Data = null; }

    public override void HandlePacket(BinaryReader reader, int whoAmI)
    {
        byte kind = reader.ReadByte();
        if (kind == 0 && Main.netMode == NetmodeID.Server)
        {
            int x = reader.ReadInt16(), y = reader.ReadInt16();
            bool gun = reader.ReadBoolean();
            int index = reader.ReadByte();
            SupplyService.Claim(Main.player[whoAmI], x, y, gun, index);
        }
        else if (kind == 1 && Main.netMode == NetmodeID.MultiplayerClient)
            ArmorySystem.Status = SupplyService.Result(reader.ReadByte());
        else if (kind == 2 && Main.netMode == NetmodeID.Server && whoAmI >= 0 && whoAmI < Main.maxPlayers)
            PhalanxWorld.Command(Main.player[whoAmI], reader.ReadByte());
    }
}
