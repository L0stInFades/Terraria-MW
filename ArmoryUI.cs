using System;
using System.Collections.Generic;
using System.Linq;
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

public sealed class ArmorySystem : ModSystem
{
    private static UserInterface window;
    internal static ArmoryUI Current => window?.CurrentState as ArmoryUI;
    internal static bool IsOpen => Current != null;
    internal static string Status = "";
    public override void Load() { if (!Main.dedServ) window = new UserInterface(); }
    public override void Unload() { window = null; Status = ""; }
    internal static void Open(int x, int y)
    {
        if (Main.dedServ || !SupplyService.InReach(Main.LocalPlayer, x, y)) return;
        GunsmithSystem.Close();
        Main.playerInventory = false;
        Main.drawingPlayerChat = false;
        Main.LocalPlayer.releaseUseItem = false;
        Status = Catalog.Text("弹药免费 · 每次领取 600 发 · 武器随进度开放", "Free ammo: 600 per claim. Weapons unlock with world progression.");
        window.SetState(new ArmoryUI(x, y));
        SoundEngine.PlaySound(SoundID.MenuOpen);
    }
    internal static void Close() => window?.SetState(null);
    public override void UpdateUI(GameTime gameTime)
    {
        if (Main.gameMenu || Main.LocalPlayer.dead) { Close(); return; }
        if (!IsOpen) return;
        if (!SupplyService.InReach(Main.LocalPlayer, Current.X, Current.Y) ||
            Main.keyState.IsKeyDown(Keys.Escape) || PlayerInput.Triggers.JustPressed.Inventory) { Close(); return; }
        window.Update(gameTime);
    }
    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        if (IsOpen)
        {
            GameInterfaceLayer chat = layers.Find(layer => layer.Name == "Vanilla: Player Chat");
            if (chat != null) chat.Active = false;
        }
        int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
        layers.Insert(index < 0 ? layers.Count : index, new LegacyGameInterfaceLayer("ModernArsenal: Armory", () =>
        {
            if (!Main.gameMenu)
            {
                if (IsOpen) window.Draw(Main.spriteBatch, new GameTime());
                DrawMagazine();
            }
            return true;
        }, InterfaceScaleType.UI));
    }
    private static void DrawMagazine()
    {
        if (Main.LocalPlayer.HeldItem.ModItem is not ArsenalGun gun || IsOpen) return;
        ArsenalPlayer state = Main.LocalPlayer.GetModPlayer<ArsenalPlayer>();
        // The UI layer already supplies screen dimensions in UI coordinates.
        Vector2 origin = new(Main.screenWidth / 2f - 170, Main.screenHeight - 146);
        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)origin.X, (int)origin.Y, 340, 72), new Color(13, 23, 30, 210));
        int reserve = ArsenalPlayer.InventorySlots.Sum(i =>
            Main.LocalPlayer.inventory[i].ModItem is ArsenalAmmo a && a.Spec.Caliber == gun.Spec.Caliber ? Main.LocalPlayer.inventory[i].stack : 0);
        string god = Main.LocalPlayer.GetModPlayer<ArsenalSandboxPlayer>().GodEnabled ? Catalog.Text(" · 无敌", " · GOD") : "";
        Utils.DrawBorderString(Main.spriteBatch, gun.Spec.Name + " · " + (gun.Ammo?.Kind ?? "EMPTY") + god, origin + new Vector2(12, 7), new Color(173, 222, 192), 0.78f);
        string text = state.ReloadTicks > 0 ? Catalog.Text($"换弹中  {state.ReloadTicks / 60f:0.0}s", $"RELOADING  {state.ReloadTicks / 60f:0.0}s") :
            $"{gun.Rounds:00} / {gun.Spec.Magazine:00}   ·   {reserve}   [R]";
        Utils.DrawBorderString(Main.spriteBatch, text, origin + new Vector2(12, 30), gun.Rounds > 0 ? Color.White : new Color(255, 170, 108), 0.8f);
        float fill = state.ReloadTicks > 0 ? 1 - state.ReloadTicks / (float)state.ReloadTotal : gun.Rounds / (float)gun.Spec.Magazine;
        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)origin.X + 12, (int)origin.Y + 61, 316, 3), new Color(49, 66, 72));
        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)origin.X + 12, (int)origin.Y + 61, (int)(316 * fill), 3), new Color(141, 210, 167));
    }
}

internal sealed class ArmoryUI : UIState
{
    internal readonly int X, Y;
    internal readonly Dictionary<string, UIElement> Controls = new();
    internal int Page;
    internal int Tab;
    internal int PageSize;
    private bool matching;
    private UIPanel panel;
    private UIElement cards;
    private UIText pages;
    private UIText status;
    private float width, height;
    internal ArmoryUI(int x, int y) { X = x; Y = y; }
    public override void OnInitialize()
    {
        width = Math.Min(940, PlayerInput.RealScreenWidth / Main.UIScale - 36);
        height = Math.Min(660, PlayerInput.RealScreenHeight / Main.UIScale - 36);
        panel = new UIPanel { HAlign = 0.5f, VAlign = 0.5f, BackgroundColor = new Color(17, 27, 36, 248), BorderColor = new Color(114, 164, 140) };
        panel.Width.Set(width, 0); panel.Height.Set(height, 0); panel.SetPadding(18); Append(panel);
        width -= 36; height -= 36;
        Label(Catalog.Text("现代军械库 / 经典战场", "MODERN ARSENAL / CLASSIC BATTLEFIELDS"), 0, 0, 1.08f, new Color(169, 225, 194));
        Label(Catalog.Stage(Catalog.Progress), 0, 35, 0.8f, new Color(177, 192, 206));
        Button("close", () => "×", ArmorySystem.Close, width - 32, -3, 32);
        float tabWidth = (width - 24) / 4;
        Button("guns", () => Catalog.Text("枪械 · 24", "WEAPONS · 24"), () => { Tab = 0; Page = 0; Refresh(); }, 0, 69, tabWidth);
        Button("ammo", () => Catalog.Text("弹药 · 19", "AMMO · 19"), () => { Tab = 1; Page = 0; Refresh(); }, tabWidth + 8, 69, tabWidth);
        Button("guide", () => Catalog.Text("使用说明", "FIELD GUIDE"), () => { Tab = 2; Page = 0; Refresh(); }, (tabWidth + 8) * 2, 69, tabWidth);
        Button("filter", () => Catalog.Text(matching ? "当前口径 ✓" : "全部口径", matching ? "HELD CALIBER ✓" : "ALL CALIBERS"),
            () => { matching = !matching; Page = 0; Refresh(); }, (tabWidth + 8) * 3, 69, tabWidth);
        int rows = Math.Clamp((int)((height - 230) / 96), 1, 4);
        PageSize = rows * 2;
        cards = new UIElement(); cards.Left.Set(0, 0); cards.Top.Set(116, 0); cards.Width.Set(width, 0); cards.Height.Set(rows * 96, 0); panel.Append(cards);
        cards.OnScrollWheel += (evt, _) => { Page += evt.ScrollWheelValue < 0 ? 1 : -1; Refresh(); };
        Button("previous", () => "‹", () => { Page--; Refresh(); }, 0, height - 100, 42);
        Button("next", () => "›", () => { Page++; Refresh(); }, width - 42, height - 100, 42);
        pages = Label("", width / 2 - 68, height - 92, 0.85f, Color.White);
        status = Label("", 0, height - 54, 0.73f, new Color(180, 220, 189));
        Label(Catalog.Text("R 换弹 · 弹药栏优先 · 弹匣打空后可切换弹种 · 离开箱子自动关闭", "R: reload · Ammo slots first · Empty magazine to change ammo · Walk away to close"), 0, height - 23, 0.64f, new Color(150, 170, 187));
        Refresh();
    }
    private UIText Label(string text, float x, float y, float scale, Color color)
    {
        UIText label = new(text, scale) { TextColor = color };
        label.Left.Set(x, 0); label.Top.Set(y, 0); panel.Append(label); return label;
    }
    private void Button(string id, Func<string> text, Action click, float x, float y, float w)
    {
        ArmoryButton button = new(text, click);
        button.Left.Set(x, 0); button.Top.Set(y, 0); button.Width.Set(w, 0); button.Height.Set(32, 0);
        panel.Append(button); Controls[id] = button;
    }
    internal void Refresh()
    {
        cards.RemoveAllChildren();
        foreach (string key in Controls.Keys.Where(k => k.StartsWith("claim")).ToArray()) Controls.Remove(key);
        if (Tab == 2)
        {
            Page = 0; pages.SetText("FIELD GUIDE");
            string[] help = Catalog.Chinese ? new[] {
                "01  开局赠送：USP .45（满弹匣）与 600 发 .45 ACP。",
                "02  家中自动生成补给箱。未建房时先放在出生点附近。",
                "03  点击枪械或弹药卡片领取，全部免费；灰色卡片需解锁。",
                "04  持枪按 R 换弹；打空后开火也会开始换弹。切枪会取消。",
                "05  优先读取弹药栏，再读取背包；补弹保留当前弹种。",
                "06  切换弹种：打空弹匣，把新弹种放在更靠前的弹药栏。",
                "07  AP 穿甲与穿透；JHP 克制低防；曳光弹用于观察落点。",
                "08  独头弹将霰弹转换为单发重弹；高速弹丸仍会被墙阻挡。",
                "09  狙击与精确步枪支持原生瞄准缩放；移动会增大散布。"
            } : new[] {
                "01  Starter kit: loaded USP .45 + 600 rounds of .45 ACP.",
                "02  A crate appears at home, or near spawn until a home is built.",
                "03  Click a card to claim free gear. Grey cards require progression.",
                "04  R reloads. Firing empty starts a reload. Switching cancels it.",
                "05  Ammo slots take priority over inventory. Top-ups keep the ammo type.",
                "06  Empty the magazine, then prioritize the new ammo in your ammo slots.",
                "07  AP defeats armor; JHP favors soft targets; tracers show the flight path.",
                "08  Slugs replace the pellet cloud. Walls stop all rounds.",
                "09  Sniper / marksman guns enable scope zoom. Movement widens spread."
            };
            for (int i = 0; i < help.Length; i++)
            {
                UIText line = new(help[i], 0.75f) { TextColor = new Color(210, 221, 226) };
                line.Top.Set(i * Math.Min(38, cards.Height.Pixels / 9), 0); cards.Append(line);
            }
            return;
        }
        string caliber = matching && Main.LocalPlayer.HeldItem.ModItem is ArsenalGun held ? held.Spec.Caliber : null;
        int[] ids = Tab == 0 ? Enumerable.Range(0, Catalog.Data.Guns.Length).Where(i => caliber == null || Catalog.Data.Guns[i].Caliber == caliber).ToArray() :
            Enumerable.Range(0, Catalog.Data.Ammo.Length).Where(i => caliber == null || Catalog.Data.Ammo[i].Caliber == caliber).ToArray();
        int pageCount = Math.Max(1, (ids.Length + PageSize - 1) / PageSize);
        Page = Math.Clamp(Page, 0, pageCount - 1);
        pages.SetText($"{Page + 1} / {pageCount}    ·    {ids.Length}");
        for (int n = 0; n < PageSize && Page * PageSize + n < ids.Length; n++)
        {
            int index = ids[Page * PageSize + n];
            bool gun = Tab == 0;
            ArmoryCard card = new(gun, index, () => SupplyService.Request(X, Y, gun, index));
            card.Left.Set(n % 2 * (width + 10) / 2, 0); card.Top.Set(n / 2 * 96, 0);
            card.Width.Set((width - 10) / 2, 0); card.Height.Set(87, 0);
            cards.Append(card); Controls["claim" + index] = card;
        }
    }
    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        if (panel.IsMouseHovering) Main.LocalPlayer.mouseInterface = true;
        string message = ArmorySystem.Status;
        Vector2 size = FontAssets.MouseText.Value.MeasureString(message) * 0.73f;
        status.SetText(message, size.X > width ? 0.73f * width / size.X : 0.73f, false);
    }
}

internal sealed class ArmoryButton : UIElement
{
    private readonly Func<string> text;
    private readonly Action click;
    internal ArmoryButton(Func<string> text, Action click) { this.text = text; this.click = click; }
    public override void LeftClick(UIMouseEvent evt) { base.LeftClick(evt); click(); SoundEngine.PlaySound(SoundID.MenuTick); }
    protected override void DrawSelf(SpriteBatch batch)
    {
        Rectangle rect = GetDimensions().ToRectangle();
        batch.Draw(TextureAssets.MagicPixel.Value, rect, IsMouseHovering ? new Color(57, 91, 82) : new Color(36, 53, 62));
        Utils.DrawBorderString(batch, text(), new Vector2(rect.Center.X, rect.Center.Y), Color.White, 0.72f, 0.5f, 0.5f);
    }
}

internal sealed class ArmoryCard : UIElement
{
    private readonly bool gun;
    private readonly int index;
    private readonly Action click;
    internal ArmoryCard(bool gun, int index, Action click) { this.gun = gun; this.index = index; this.click = click; }
    public override void LeftClick(UIMouseEvent evt) { base.LeftClick(evt); click(); SoundEngine.PlaySound(SoundID.MenuTick); }
    protected override void DrawSelf(SpriteBatch batch)
    {
        GunSpec weapon = gun ? Catalog.Data.Guns[index] : null;
        AmmoSpec ammo = gun ? null : Catalog.Data.Ammo[index];
        int tier = gun ? weapon.Tier : ammo.Tier;
        int type = gun ? weapon.Type : ammo.Type;
        bool locked = tier > Catalog.Progress;
        Rectangle rect = GetDimensions().ToRectangle();
        batch.Draw(TextureAssets.MagicPixel.Value, rect, locked ? new Color(33, 39, 48) : IsMouseHovering ? new Color(44, 73, 72) : new Color(28, 45, 54));
        batch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(rect.X, rect.Y, 3, rect.Height), locked ? new Color(85, 87, 95) : new Color(141, 197, 163));
        Texture2D texture = TextureAssets.Item[type].Value;
        float scale = Math.Min(110f / texture.Width, 45f / texture.Height);
        batch.Draw(texture, new Vector2(rect.X + 65, rect.Y + 43), null, locked ? Color.Gray : Color.White, 0, texture.Size() / 2, scale, SpriteEffects.None, 0);
        float nameScale = Math.Min(0.83f, (rect.Width - 140) / FontAssets.MouseText.Value.MeasureString(gun ? weapon.Name : ammo.Name).X);
        Utils.DrawBorderString(batch, gun ? weapon.Name : ammo.Name, new Vector2(rect.X + 130, rect.Y + 9), locked ? Color.Gray : new Color(230, 237, 224), nameScale);
        string info = gun ? Catalog.Text($"{weapon.Damage} 伤害  |  {weapon.Magazine} 发  |  {Catalog.Family(weapon.Family)}", $"{weapon.Damage} DMG  |  {weapon.Magazine} RND  |  {weapon.Family}") :
            Catalog.Text($"+{ammo.Damage} 伤害  |  穿甲 {ammo.Armor}  |  {ammo.Penetrate} 目标", $"+{ammo.Damage} DMG  |  AP {ammo.Armor}  |  {ammo.Penetrate} targets");
        float infoScale = Math.Min(0.64f, (rect.Width - 142) / FontAssets.MouseText.Value.MeasureString(info).X);
        Utils.DrawBorderString(batch, info, new Vector2(rect.X + 130, rect.Y + 35), new Color(164, 182, 194), infoScale);
        string action = locked ? Catalog.Stage(tier) : gun ? Catalog.Text("点击免费领取", "CLAIM FREE") : Catalog.Text("点击领取 600 发", "CLAIM 600 FREE");
        float actionScale = Math.Min(0.66f, (rect.Width - 142) / FontAssets.MouseText.Value.MeasureString(action).X);
        Utils.DrawBorderString(batch, action, new Vector2(rect.X + 130, rect.Y + 61), locked ? new Color(192, 150, 117) : new Color(154, 218, 172), actionScale);
        if (IsMouseHovering)
        {
            Main.LocalPlayer.mouseInterface = true;
            Main.HoverItem = new Item(type);
            Main.hoverItemName = Main.HoverItem.Name;
        }
    }
}
