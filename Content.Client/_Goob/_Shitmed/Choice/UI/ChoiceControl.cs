// Minimal choice control used by Shitmed surgery UI.

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Analyzers;
using Robust.Shared.Input;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client._Goob._Shitmed.Choice.UI;

[Virtual]
public class ChoiceControl : ContainerButton
{
    private readonly PanelContainer _backer;
    private readonly TextureRect _icon;
    private readonly RichTextLabel _label;
    private bool _selected;
    private static readonly Color IconTint = Color.FromHex("#b8f0ff");

    /// <summary>
    /// Expose the underlying button to match upstream API.
    /// </summary>
    public BaseButton Button => this;

    public ChoiceControl()
    {
        HorizontalExpand = true;
        VerticalExpand = true;
        MouseFilter = MouseFilterMode.Stop;
        AddStyleClass("OpenBoth");

        _backer = new PanelContainer
        {
            PanelOverride = CreateStyle(Color.FromHex("#1e2330"), Color.FromHex("#3d526b")),
            HorizontalExpand = true,
            VerticalExpand = true,
            Margin = new Thickness(2f, 2f)
        };

        var box = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 4,
            Margin = new Thickness(6f, 4f)
        };

        _icon = new TextureRect
        {
            MinSize = new Vector2(24f, 24f),
            Stretch = TextureRect.StretchMode.KeepCentered,
            Visible = false
        };

        _label = new RichTextLabel
        {
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Center
        };

        box.AddChild(_icon);
        box.AddChild(_label);
        _backer.AddChild(box);
        AddChild(_backer);

        OnMouseEntered += _ => UpdateVisualState();
        OnMouseExited += _ => UpdateVisualState();
        OnButtonDown += _ => UpdateVisualState();
        OnButtonUp += _ => UpdateVisualState();
    }

    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();
        UpdateVisualState();
    }

    public void Set(string text, Texture? texture)
    {
        var msg = new FormattedMessage();
        msg.AddText(text);
        Set(msg, texture);
    }

    public void SetSelected(bool selected)
    {
        if (_selected == selected)
            return;

        _selected = selected;
        UpdateVisualState();
    }

    public void Set(FormattedMessage text, Texture? texture)
    {
        _label.SetMessage(text);
        _icon.Texture = texture;
        _icon.Visible = texture != null;
        _icon.Modulate = IconTint;
    }

    private void UpdateVisualState()
    {
        if (_backer == null)
            return;

        StyleBoxFlat style;
        if (Disabled)
            style = CreateStyle(Color.FromHex("#1a1c24"), Color.FromHex("#31343e"));
        else if (DrawMode == DrawModeEnum.Pressed)
            style = CreateStyle(Color.FromHex("#1b2636"), Color.FromHex("#3aa8d8"));
        else if (DrawMode == DrawModeEnum.Hover)
            style = _selected
                ? CreateStyle(Color.FromHex("#1f3042"), Color.FromHex("#7bd7ff"))
                : CreateStyle(Color.FromHex("#253043"), Color.FromHex("#5bc5ff"));
        else
            style = _selected
                ? CreateStyle(Color.FromHex("#1f3042"), Color.FromHex("#7bd7ff"))
                : CreateStyle(Color.FromHex("#1e2330"), Color.FromHex("#3d526b"));

        _backer.PanelOverride = style;
    }

    private static StyleBoxFlat CreateStyle(Color bg, Color border)
    {
        return new StyleBoxFlat
        {
            BackgroundColor = bg,
            BorderColor = border,
            BorderThickness = new Thickness(1)
        };
    }
}
