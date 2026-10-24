using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace ModernArsenal;

public sealed class GunsmithSystem : ModSystem
{
    private static UserInterface window;
    internal static GunsmithUI Current => window?.CurrentState as GunsmithUI;
    internal static bool IsOpen => Current != null;
    public override void Load() { if (!Main.dedServ) window = new UserInterface(); }
    public override void Unload() => window = null;
    internal static void Close() => window?.SetState(null);
    internal static void Toggle()
    {
        if (IsOpen) { Close(); return; }
        Player player = Main.LocalPlayer;
        if (Main.gameMenu || player.dead || player.HeldItem.ModItem is not ArsenalGun gun) return;
        if (player.itemAnimation > 0 || player.GetModPlayer<ArsenalPlayer>().ReloadTicks > 0)
        {
            Main.NewText(Catalog.Text("停止开火、完成换弹后再改装。", "Stop firing and finish reloading before modifying a gun."), Color.Gold);
            return;
        }
        ArmorySystem.Close();
        gun.ClearVisuals();
        Main.playerInventory = false;
        window.SetState(new GunsmithUI(gun));
        SoundEngine.PlaySound(SoundID.MenuOpen);
    }
    public override void UpdateUI(GameTime gameTime)
    {
        if (!IsOpen) return;
        if (Main.gameMenu || Main.LocalPlayer.dead || !ReferenceEquals(Main.LocalPlayer.HeldItem.ModItem, Current.Gun) ||
            Main.keyState.IsKeyDown(Keys.Escape) || PlayerInput.Triggers.JustPressed.Inventory)
        { Close(); return; }
        window.Update(gameTime);
    }
    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        if (!IsOpen) return;
        GameInterfaceLayer chat = layers.Find(l => l.Name == "Vanilla: Player Chat");
        if (chat != null) chat.Active = false;
        int index = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
        layers.Insert(index < 0 ? layers.Count : index, new LegacyGameInterfaceLayer("ModernArsenal: Gunsmith", () =>
        { window.Draw(Main.spriteBatch, new GameTime()); return true; }, InterfaceScaleType.UI));
    }
}

internal sealed class GunsmithUI : UIState
{
    internal readonly ArsenalGun Gun;
    internal readonly Dictionary<string, UIElement> Controls = new();
    private UIPanel panel;
    private UIText status;
    internal GunsmithUI(ArsenalGun gun) => Gun = gun;
    public override void OnInitialize()
    {
        float width = Math.Min(620, PlayerInput.RealScreenWidth / Main.UIScale - 32);
        float height = Math.Min(535, PlayerInput.RealScreenHeight / Main.UIScale - 32);
        panel = new UIPanel { HAlign = 0.5f, VAlign = 0.5f, BackgroundColor = new Color(17, 29, 38, 250), BorderColor = new Color(123, 179, 149) };
        panel.Width.Set(width, 0); panel.Height.Set(height, 0); panel.SetPadding(18); Append(panel);
        width -= 36; height -= 36;
        float row = Math.Min(80, (height - 190) / 3);
        float rowHeight = Math.Min(40, row - 17);
        Label(Catalog.Text("改枪 · ", "GUNSMITH · ") + Gun.Spec.Name, 0, 0, 1f, new Color(177, 225, 193));
        Button("close", () => "×", GunsmithSystem.Close, width - 30, -3, 30, 30);
        UIElement preview = new GunPreview(Gun); preview.Top.Set(38, 0); preview.Width.Set(width, 0); preview.Height.Set(64, 0); panel.Append(preview);
        Button("suppressor", () => Catalog.Text("消音器", "SUPPRESSOR") + State(Gun.Has(GunAttachments.Suppressor)),
            () => Change(GunAttachments.Suppressor), 0, 96, width, rowHeight);
        Label(Catalog.Text("降低枪声和枪口焰；外形参考 RC2 / PBS-1 等产品", "Reduced report and flash; RC2 / PBS-1 inspired silhouette"), 4, 99 + rowHeight, 0.65f, new Color(171, 189, 199));
        Button("flashlight", () => Catalog.Text("白光手电", "WHITE LIGHT") + State(Gun.Has(GunAttachments.Flashlight)),
            () => Change(GunAttachments.Flashlight), 0, 96 + row, width, rowHeight);
        Label(Catalog.Text("X300 / Scout 类枪灯，照亮前方并被墙体阻挡", "X300 / Scout style light; walls stop the beam"), 4, 99 + row + rowHeight, 0.65f, new Color(171, 189, 199));
        Button("laser", LaserLabel, CycleLaser, 0, 96 + row * 2, width, rowHeight);
        Label(Catalog.Text("可见激光可直接观察；红外通道需夜视装备", "Visible laser: naked-eye aiming. IR: night vision required"), 4, 99 + row * 2 + rowHeight, 0.65f, new Color(171, 189, 199));
        Button("nightvision", () => Catalog.Text("领取夜视头盔", "GET NIGHT VISION HELMET"), () =>
        {
            bool owned = Main.LocalPlayer.HasItem(ItemID.NightVisionHelmet) || Main.LocalPlayer.armor[0].type == ItemID.NightVisionHelmet;
            bool success = owned || SupplyService.Grant(Main.LocalPlayer, ItemID.NightVisionHelmet, 1) == 1;
            status.SetText(Catalog.Text(success ? "将夜视头盔穿戴到头部护甲栏，即可观察红外线。" : "背包已满，请先腾出空间。", success ? "Equip the helmet in the head armor slot to observe IR." : "Free inventory space first."));
        }, 0, height - 105, width, 32);
        status = Label(Catalog.Text("改件免费切换，设置随这把枪保存。", "Free attachment toggles; settings are saved with this gun."), 0, height - 60, 0.64f, new Color(183, 218, 187));
        Label(Catalog.Text("V / Esc 关闭 · 切枪自动关闭 · 开界面时不能开火", "V / Esc closes · Switching guns closes · Firing is blocked here"), 0, height - 27, 0.63f, new Color(151, 172, 186));
    }
    private static string State(bool on) => Catalog.Text(on ? "  [已装]" : "  [未装]", on ? "  [ON]" : "  [OFF]");
    private string LaserLabel() => Catalog.Text("瞄准模块  [", "AIM MODULE  [") +
        (Gun.Has(GunAttachments.Infrared) ? Catalog.Text("红外 / 需夜视", "IR / NIGHT VISION") : Gun.Has(GunAttachments.Laser) ? Catalog.Text("可见激光", "VISIBLE LASER") : Catalog.Text("关闭", "OFF")) + "]";
    private void Change(GunAttachments attachment) { Gun.Attachments ^= attachment; Changed(); }
    private void CycleLaser()
    {
        bool visible = Gun.Has(GunAttachments.Laser), infrared = Gun.Has(GunAttachments.Infrared);
        Gun.Attachments &= ~(GunAttachments.Laser | GunAttachments.Infrared);
        if (!visible && !infrared) Gun.Attachments |= GunAttachments.Laser;
        else if (visible) Gun.Attachments |= GunAttachments.Infrared;
        Changed();
    }
    private void Changed()
    {
        Gun.Sync(Main.LocalPlayer);
        status.SetText(Catalog.Text("已应用到当前枪械。改装不消耗弹药。", "Applied to this gun. Modifying attachments uses no ammunition."));
    }
    private UIText Label(string text, float x, float y, float scale, Color color)
    {
        UIText label = new(text, scale) { TextColor = color };
        label.Left.Set(x, 0); label.Top.Set(y, 0); panel.Append(label); return label;
    }
    private void Button(string id, Func<string> text, Action action, float x, float y, float width, float height)
    {
        ArmoryButton button = new(text, action); button.Left.Set(x, 0); button.Top.Set(y, 0); button.Width.Set(width, 0); button.Height.Set(height, 0);
        panel.Append(button); Controls[id] = button;
    }
    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        if (panel.IsMouseHovering) Main.LocalPlayer.mouseInterface = true;
    }
}

internal sealed class GunPreview : UIElement
{
    private readonly ArsenalGun gun;
    internal GunPreview(ArsenalGun gun) => this.gun = gun;
    protected override void DrawSelf(SpriteBatch batch)
    {
        Rectangle bounds = GetDimensions().ToRectangle();
        Texture2D texture = TextureAssets.Item[gun.Type].Value;
        float scale = Math.Min(2f, (bounds.Width - 60) / (float)texture.Width);
        Vector2 position = new(bounds.Center.X, bounds.Center.Y);
        batch.Draw(texture, position, null, Color.White, 0, texture.Size() / 2, scale, SpriteEffects.None, 0);
        Vector2 muzzle = new(gun.Spec.Animation.Muzzle[0] * 2, gun.Spec.Animation.Muzzle[1] * 2);
        if (gun.Has(GunAttachments.Suppressor))
        {
            Texture2D suppressor = ModContent.Request<Texture2D>("ModernArsenal/Assets/Suppressor").Value;
            batch.Draw(suppressor, position + (muzzle - texture.Size() / 2) * scale, null, Color.White, 0, new Vector2(0, suppressor.Height / 2f), scale, SpriteEffects.None, 0);
        }
        foreach (var (attachment, asset, offset) in new[] {
            (GunAttachments.Flashlight, "Flashlight", new Vector2(-14, 8)), (GunAttachments.Laser | GunAttachments.Infrared, "Infrared", new Vector2(-24, 4)) })
            if ((gun.Attachments & attachment) != 0)
            {
                Texture2D device = ModContent.Request<Texture2D>("ModernArsenal/Assets/" + asset).Value;
                batch.Draw(device, position + (muzzle + offset - texture.Size() / 2) * scale, null, Color.White, 0, device.Size() / 2, scale, SpriteEffects.None, 0);
            }
    }
}
