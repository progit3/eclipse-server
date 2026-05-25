using Content.Client.Stylesheets.SheetletConfigs;
using Content.Client.Stylesheets.Stylesheets;
using Content.Client.UserInterface.Systems.Chat.Controls;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client.Stylesheets.Sheetlets.Hud;

[CommonSheetlet]
public sealed class ChatSheetlet<T> : Sheetlet<T> where T: PalettedStylesheet, IButtonConfig
{
    public override StyleRule[] GetRules(T sheet, object config)
    {
        IButtonConfig btnCfg = sheet;

        var chatBg = EclipsePanel("#070300F8", "#A85E1290", 0, 0);
        var chatChannelButton = EclipsePanel("#070300F4", "#A85E1240", 8, 3);
        var chatFilterButton = EclipsePanel("#070300F4", "#A85E1266", 4, 3);

        return
        [
            E<PanelContainer>()
                .Class(ChatInputBox.StyleClassChatPanel)
                .Panel(chatBg),
            E<LineEdit>()
                .Class(ChatInputBox.StyleClassChatLineEdit)
                .Prop(LineEdit.StylePropertyStyleBox, new StyleBoxEmpty()),
            E<Button>().Class(ChatInputBox.StyleClassChatFilterOptionButton).Box(chatChannelButton),
            E<ContainerButton>().Class(ChatInputBox.StyleClassChatFilterOptionButton).Box(chatFilterButton),
        ];
    }

    private static StyleBoxFlat EclipsePanel(string background, string border, float horizontalPadding, float verticalPadding)
    {
        var style = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex(background),
            BorderColor = Color.FromHex(border),
            BorderThickness = Color.FromHex(border).A > 0 ? new Thickness(1) : new Thickness(0),
        };

        style.SetContentMarginOverride(StyleBox.Margin.Horizontal, horizontalPadding);
        style.SetContentMarginOverride(StyleBox.Margin.Vertical, verticalPadding);
        return style;
    }
}
