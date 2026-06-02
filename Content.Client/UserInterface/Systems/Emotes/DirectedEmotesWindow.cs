using System.Numerics;
using Content.Client.Stylesheets;
using Content.Shared._EinsteinEngines.DirectedEmotes;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Systems.Emotes;

public sealed class DirectedEmotesWindow : DefaultWindow
{
    private readonly BoxContainer _participants;
    private readonly BoxContainer _messages;
    private readonly LineEdit _input;
    private readonly Button _sendButton;
    private readonly Button _addParticipantButton;

    public event Action<string>? OnSend;
    public event Action? OnAddParticipant;

    public DirectedEmotesWindow()
    {
        Title = Loc.GetString("directed-emotes-dialog-title");
        MinSize = SetSize = new Vector2(560, 360);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            VerticalExpand = true
        };

        var side = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            MinWidth = 160,
            Margin = new Thickness(0, 0, 8, 0)
        };

        side.AddChild(new Label
        {
            Text = Loc.GetString("directed-emotes-dialog-participants"),
            StyleClasses = { StyleClass.LabelHeading }
        });

        _participants = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            VerticalExpand = true
        };
        side.AddChild(_participants);

        _addParticipantButton = new Button
        {
            Text = Loc.GetString("directed-emotes-dialog-add-participant")
        };
        _addParticipantButton.OnPressed += _ => OnAddParticipant?.Invoke();
        side.AddChild(_addParticipantButton);

        var main = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true
        };

        var scroll = new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            HScrollEnabled = false
        };

        _messages = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true
        };
        scroll.AddChild(_messages);
        main.AddChild(scroll);

        var inputRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            Margin = new Thickness(0, 8, 0, 0)
        };

        _input = new LineEdit
        {
            HorizontalExpand = true,
            PlaceHolder = Loc.GetString("directed-emotes-dialog-input-placeholder")
        };
        _input.OnTextEntered += args => Send(args.Text);
        inputRow.AddChild(_input);

        _sendButton = new Button
        {
            Text = Loc.GetString("directed-emotes-dialog-send")
        };
        _sendButton.OnPressed += _ => Send(_input.Text);
        inputRow.AddChild(_sendButton);

        main.AddChild(inputRow);

        root.AddChild(side);
        root.AddChild(main);
        ContentsContainer.AddChild(root);
    }

    public void SetParticipants(DirectedEmotesParticipantState[] participants, bool canAddParticipant)
    {
        _participants.RemoveAllChildren();
        foreach (var participant in participants)
        {
            _participants.AddChild(new Label
            {
                Text = participant.InRange
                    ? participant.Name
                    : Loc.GetString("directed-emotes-dialog-participant-out-of-range", ("player", participant.Name)),
                StyleClasses = { participant.InRange ? StyleClass.StatusGood : StyleClass.StatusOkay }
            });
        }

        _addParticipantButton.Disabled = !canAddParticipant;
    }

    public void AddLine(string sender, string message, bool systemMessage)
    {
        var text = systemMessage
            ? FormattedMessage.EscapeText(message)
            : Loc.GetString(
                "directed-emotes-dialog-message-wrap",
                ("player", FormattedMessage.EscapeText(sender)),
                ("message", FormattedMessage.EscapeText(message)));

        var label = new RichTextLabel
        {
            HorizontalExpand = true,
            Margin = new Thickness(0, 0, 0, 4)
        };
        label.SetMessage(FormattedMessage.FromMarkupPermissive(text));
        if (systemMessage)
            label.StyleClasses.Add(StyleClass.Italic);

        _messages.AddChild(label);
    }

    private void Send(string text)
    {
        text = text.Trim();
        if (text.Length == 0)
            return;

        OnSend?.Invoke(text);
        _input.Clear();
    }
}
