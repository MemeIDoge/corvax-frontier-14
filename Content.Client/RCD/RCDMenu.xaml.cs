using Content.Client.UserInterface.Controls;
using Content.Shared.Popups;
using Content.Shared.RCD;
using Content.Shared.RCD.Components;
using Robust.Client.AutoGenerated;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Prototypes;
using System.Numerics;

namespace Content.Client.RCD;

[GenerateTypedNameReferences]
public sealed partial class RCDMenu : RadialMenu
{
    [Dependency] private readonly EntityManager _entManager = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    private readonly SpriteSystem _spriteSystem;
    private readonly SharedPopupSystem _popup;

    public event Action<ProtoId<RCDPrototype>>? SendRCDSystemMessageAction;

    private EntityUid _owner;

    public RCDMenu(EntityUid owner, RCDMenuBoundUserInterface bui)
    {
        IoCManager.InjectDependencies(this);
        RobustXamlLoader.Load(this);

        _spriteSystem = _entManager.System<SpriteSystem>();
        _popup = _entManager.System<SharedPopupSystem>();

        _owner = owner;

        // Find the main radial container
        var main = FindControl<RadialContainer>("Main");

        if (main == null)
            return;

        // Populate secondary radial containers
        if (!_entManager.TryGetComponent<RCDComponent>(owner, out var rcd))
            return;

        foreach (var protoId in rcd.AvailablePrototypes)
        {
            if (!_protoManager.TryIndex(protoId, out var proto))
                continue;

            if (proto.Mode == RcdMode.Invalid)
                continue;

            var parent = FindControl<RadialContainer>(proto.Category);

            if (parent == null)
                continue;

            var tooltip = Loc.GetString(proto.SetName);

            if ((proto.Mode == RcdMode.ConstructTile || proto.Mode == RcdMode.ConstructObject) &&
                proto.Prototype != null && _protoManager.TryIndex(proto.Prototype, out var entProto))
            {
                tooltip = Loc.GetString(entProto.Name);
            }

            tooltip = OopsConcat(char.ToUpper(tooltip[0]).ToString(), tooltip.Remove(0, 1));

            var button = new RCDMenuButton()
            {
                StyleClasses = { "RadialMenuButton" },
                SetSize = new Vector2(64f, 64f),
                ToolTip = tooltip,
                ProtoId = protoId,
            };

            if (proto.Sprite != null)
            {
                var tex = new TextureRect()
                {
                    VerticalAlignment = VAlignment.Center,
                    HorizontalAlignment = HAlignment.Center,
                    Texture = _spriteSystem.Frame0(proto.Sprite),
                    TextureScale = new Vector2(2f, 2f),
                };

                button.AddChild(tex);
            }

            parent.AddChild(button);

            // Ensure that the button that transitions the menu to the associated category layer
            // is visible in the main radial container (as these all start with Visible = false)
            foreach (var child in main.Children)
            {
                var castChild = child as RadialMenuTextureButton;

                if (castChild is not RadialMenuTextureButton)
                    continue;

                if (castChild.TargetLayer == proto.Category)
                {
                    castChild.Visible = true;
                    break;
                }
            }
        }

        // Set up menu actions
        foreach (var child in Children)
            AddRCDMenuButtonOnClickActions(child);

        OnChildAdded += AddRCDMenuButtonOnClickActions;

        SendRCDSystemMessageAction += bui.SendRCDSystemMessage;
    }


    private static string OopsConcat(string a, string b)
    {
        // This exists to prevent Roslyn being clever and compiling something that fails sandbox checks.
        return a + b;
    }

    private void AddRCDMenuButtonOnClickActions(Control control)
    {
        var radialContainer = control as RadialContainer;

        if (radialContainer == null)
            return;

        foreach (var child in radialContainer.Children)
        {
            var castChild = child as RCDMenuButton;

            if (castChild == null)
                continue;

            castChild.OnButtonUp += _ =>
            {
                SendRCDSystemMessageAction?.Invoke(castChild.ProtoId);

                if (_playerManager.LocalSession?.AttachedEntity != null &&
                    _protoManager.TryIndex(castChild.ProtoId, out var proto))
                {
                    var msg = Loc.GetString("rcd-component-change-mode", ("mode", Loc.GetString(proto.SetName)));

                    if (proto.Mode == RcdMode.ConstructTile || proto.Mode == RcdMode.ConstructObject)
                    {
                        var name = Loc.GetString(proto.SetName);

                        if (proto.Prototype != null &&
                            _protoManager.TryIndex(proto.Prototype, out var entProto))
                            name = entProto.Name;

                        msg = Loc.GetString("rcd-component-change-build-mode", ("name", name));
                    }

                    // Popup message
                    _popup.PopupClient(msg, _owner, _playerManager.LocalSession.AttachedEntity);
                }

                Close();
            };
        }
    }
}

public sealed class RCDMenuButton : RadialMenuTextureButton
{
    public ProtoId<RCDPrototype> ProtoId { get; set; }

    public RCDMenuButton()
    {

    }
}
