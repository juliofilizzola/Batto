using Tmds.DBus.Protocol;

namespace Batto.Linux.Tray;

/// <summary>
/// Context menu of the tray icon, exported as com.canonical.dbusmenu at <see cref="StatusNotifierItem.MenuPath"/>.
/// Two entries: open the window and quit.
/// </summary>
public sealed class DBusMenu : IPathMethodHandler
{
    public const string Interface = "com.canonical.dbusmenu";

    private const string PropertiesInterface = "org.freedesktop.DBus.Properties";
    private const string ErrorFailed = "org.freedesktop.DBus.Error.Failed";
    private const string ErrorUnknownMethod = "org.freedesktop.DBus.Error.UnknownMethod";
    private const string ErrorUnknownProperty = "org.freedesktop.DBus.Error.UnknownProperty";
    private const string ErrorAccessDenied = "org.freedesktop.DBus.Error.AccessDenied";
    private const string EventClicked = "clicked";
    private const string LabelKey = "label";
    private const string OpenLabel = "Abrir Batto";
    private const string QuitLabel = "Sair";
    private const uint Revision = 1;
    private const uint DbusMenuVersion = 3;
    private const int RootId = 0;
    private const int OpenId = 1;
    private const int QuitId = 2;

    public Action? Open { get; init; }
    public Action? Quit { get; init; }

    public string Path => StatusNotifierItem.MenuPath;
    public bool HandlesChildPaths => false;

    public ValueTask HandleMethodAsync(MethodContext context)
    {
        try
        {
            return Dispatch(context);
        }
        catch (Exception ex)
        {
            CrashLogger.Log($"{GetType().Name} {context.Request.InterfaceAsString}.{context.Request.MemberAsString}", ex);
            context.ReplyError(ErrorFailed, "Erro interno ao atender o método.");
            return default;
        }
    }

    private ValueTask Dispatch(MethodContext context)
    {
        var request = context.Request;
        string @interface = request.InterfaceAsString ?? string.Empty;
        string member = request.MemberAsString ?? string.Empty;

        switch (@interface, member)
        {
            case (Interface, "GetLayout"):
                ReplyLayout(context);
                return default;

            case (Interface, "Event"):
                HandleEvent(context);
                return default;

            case (Interface, "AboutToShow"):
                ReplyBool(context, false);
                return default;

            case (Interface, "GetGroupProperties"):
                ReplyEmptyGroups(context);
                return default;

            case (PropertiesInterface, "Get"):
                HandleGet(context);
                return default;

            case (PropertiesInterface, "GetAll"):
                ReplyAllProperties(context);
                return default;

            case (PropertiesInterface, "Set"):
                context.ReplyError(ErrorAccessDenied, "The menu properties are read-only.");
                return default;

            default:
                context.ReplyError(ErrorUnknownMethod, $"Unknown method {@interface}.{member}.");
                return default;
        }
    }

    private static void ReplyLayout(MethodContext context)
    {
        using var writer = context.CreateReplyWriter("u(ia{sv}av)");
        writer.WriteUInt32(Revision);
        writer.WriteStructureStart();
        writer.WriteInt32(RootId);

        ArrayStart properties = writer.WriteDictionaryStart();
        writer.WriteDictionaryEnd(properties);

        ArrayStart children = writer.WriteArrayStart(DBusType.Variant);
        writer.WriteVariant(MenuItem(OpenId, OpenLabel));
        writer.WriteVariant(MenuItem(QuitId, QuitLabel));
        writer.WriteArrayEnd(children);

        context.Reply(writer.CreateMessage());
    }

    /// <summary>A leaf menu item: (id, properties, no children).</summary>
    private static VariantValue MenuItem(int id, string label)
    {
        var properties = new Dict<string, VariantValue>();
        properties.Add(LabelKey, VariantValue.String(label));

        var noChildren = new Array<VariantValue>();
        return VariantValue.Struct(VariantValue.Int32(id), properties, noChildren);
    }

    private void HandleEvent(MethodContext context)
    {
        var reader = context.Request.GetBodyReader();
        int id = reader.ReadInt32();
        string eventId = reader.ReadString();

        if (eventId == EventClicked)
        {
            switch (id)
            {
                case OpenId:
                    Open?.Invoke();
                    break;
                case QuitId:
                    Quit?.Invoke();
                    break;
            }
        }

        ReplyEmpty(context, string.Empty);
    }

    private void HandleGet(MethodContext context)
    {
        var reader = context.Request.GetBodyReader();
        string @interface = reader.ReadString();
        string property = reader.ReadString();

        var value = @interface == Interface ? ReadProperty(property) : null;
        if (value is null)
        {
            context.ReplyError(ErrorUnknownProperty, $"Unknown property {property}.");
            return;
        }

        using var writer = context.CreateReplyWriter("v");
        writer.WriteVariant(value.Value);
        context.Reply(writer.CreateMessage());
    }

    private static void ReplyAllProperties(MethodContext context)
    {
        using var writer = context.CreateReplyWriter("a{sv}");
        ArrayStart dictionary = writer.WriteDictionaryStart();
        foreach (var name in new[] { "Version", "TextDirection", "Status" })
        {
            writer.WriteDictionaryEntryStart();
            writer.WriteString(name);
            writer.WriteVariant(ReadProperty(name)!.Value);
        }
        writer.WriteDictionaryEnd(dictionary);
        context.Reply(writer.CreateMessage());
    }

    private static VariantValue? ReadProperty(string property) => property switch
    {
        "Version" => VariantValue.UInt32(DbusMenuVersion),
        "TextDirection" => VariantValue.String("ltr"),
        "Status" => VariantValue.String("normal"),
        _ => (VariantValue?)null
    };

    private static void ReplyBool(MethodContext context, bool value)
    {
        using var writer = context.CreateReplyWriter("b");
        writer.WriteBool(value);
        context.Reply(writer.CreateMessage());
    }

    private static void ReplyEmptyGroups(MethodContext context)
    {
        using var writer = context.CreateReplyWriter("a(ia{sv})");
        ArrayStart groups = writer.WriteArrayStart(DBusType.Struct);
        writer.WriteArrayEnd(groups);
        context.Reply(writer.CreateMessage());
    }

    private static void ReplyEmpty(MethodContext context, string signature)
    {
        using var writer = context.CreateReplyWriter(signature);
        context.Reply(writer.CreateMessage());
    }
}
