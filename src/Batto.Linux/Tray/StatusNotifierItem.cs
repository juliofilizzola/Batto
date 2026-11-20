using System.Text;
using Tmds.DBus.Protocol;

namespace Batto.Linux.Tray;

/// <summary>
/// The StatusNotifierItem exported to the desktop tray (KDE Plasma, Waybar and any SNI host).
/// Exposes the ring icon, the tooltip and the menu, and emits the NewIcon and NewToolTip signals when they change.
/// Serves the org.freedesktop.DBus.Properties calls itself, since the library does not generate them.
/// </summary>
public sealed class StatusNotifierItem : IPathMethodHandler
{
    public const string ObjectPathValue = "/StatusNotifierItem";
    public const string Interface = "org.kde.StatusNotifierItem";
    public const string MenuPath = "/MenuBar";

    private const string PropertiesInterface = "org.freedesktop.DBus.Properties";
    private const string IntrospectableInterface = "org.freedesktop.DBus.Introspectable";
    private const string ItemId = "batto";
    private const string ItemTitle = "Batto";
    private const string StatusActive = "Active";
    private const string CategoryApplicationStatus = "ApplicationStatus";
    private const string ErrorUnknownProperty = "org.freedesktop.DBus.Error.UnknownProperty";
    private const string ErrorFailed = "org.freedesktop.DBus.Error.Failed";
    private const string ErrorUnknownMethod = "org.freedesktop.DBus.Error.UnknownMethod";
    private const string ErrorAccessDenied = "org.freedesktop.DBus.Error.AccessDenied";

    private static readonly string IntrospectXml = $"""
        <node>
          <interface name="{Interface}">
            <method name="Activate"><arg type="i" direction="in"/><arg type="i" direction="in"/></method>
            <method name="SecondaryActivate"><arg type="i" direction="in"/><arg type="i" direction="in"/></method>
            <method name="ContextMenu"><arg type="i" direction="in"/><arg type="i" direction="in"/></method>
            <method name="Scroll"><arg type="i" direction="in"/><arg type="s" direction="in"/></method>
            <signal name="NewIcon"/>
            <signal name="NewToolTip"/>
            <signal name="NewStatus"><arg type="s"/></signal>
            <property name="Category" type="s" access="read"/>
            <property name="Id" type="s" access="read"/>
            <property name="Title" type="s" access="read"/>
            <property name="Status" type="s" access="read"/>
            <property name="WindowId" type="i" access="read"/>
            <property name="IconName" type="s" access="read"/>
            <property name="IconPixmap" type="a(iiay)" access="read"/>
            <property name="ToolTip" type="(sa(iiay)ss)" access="read"/>
            <property name="ItemIsMenu" type="b" access="read"/>
            <property name="Menu" type="o" access="read"/>
          </interface>
        </node>
        """;

    private readonly DBusConnection _connection;
    private readonly object _gate = new();
    private int _pixmapSize;
    private byte[] _pixmap = [];
    private string _tooltipText = string.Empty;

    /// <summary>Left click on the tray icon: show the main window.</summary>
    public Action? Activated { get; init; }

    public string Path => ObjectPathValue;
    public bool HandlesChildPaths => false;

    public StatusNotifierItem(DBusConnection connection)
    {
        _connection = connection;
    }

    /// <summary>Replaces the icon (a square ARGB pixmap of the given side) and tooltip, and tells the host to read them again.</summary>
    public void Update(int pixmapSize, byte[] pixmap, string tooltipText)
    {
        bool iconChanged;
        bool tooltipChanged;
        lock (_gate)
        {
            iconChanged = _pixmapSize != pixmapSize || !_pixmap.AsSpan().SequenceEqual(pixmap);
            tooltipChanged = _tooltipText != tooltipText;
            _pixmapSize = pixmapSize;
            _pixmap = pixmap;
            _tooltipText = tooltipText;
        }

        if (iconChanged) EmitSignal("NewIcon", null);
        if (tooltipChanged) EmitSignal("NewToolTip", null);
    }

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
        if (context.IsDBusIntrospectRequest)
        {
            context.ReplyIntrospectXml([Encoding.UTF8.GetBytes(IntrospectXml)]);
            return default;
        }

        var request = context.Request;
        string @interface = request.InterfaceAsString ?? string.Empty;
        string member = request.MemberAsString ?? string.Empty;

        switch (@interface, member)
        {
            case (Interface, "Activate"):
                Activated?.Invoke();
                ReplyEmpty(context);
                return default;

            case (Interface, "SecondaryActivate" or "ContextMenu" or "Scroll"):
                ReplyEmpty(context);
                return default;

            case (PropertiesInterface, "Get"):
                HandleGet(context);
                return default;

            case (PropertiesInterface, "GetAll"):
                HandleGetAll(context);
                return default;

            case (PropertiesInterface, "Set"):
                context.ReplyError(ErrorAccessDenied, "The StatusNotifierItem properties are read-only.");
                return default;

            case (IntrospectableInterface, "Introspect"):
                context.ReplyIntrospectXml([Encoding.UTF8.GetBytes(IntrospectXml)]);
                return default;

            default:
                context.ReplyError(ErrorUnknownMethod, $"Unknown method {@interface}.{member}.");
                return default;
        }
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

    private void HandleGetAll(MethodContext context)
    {
        var reader = context.Request.GetBodyReader();
        string @interface = reader.ReadString();
        if (@interface != Interface)
        {
            context.ReplyError(ErrorUnknownProperty, $"Unknown interface {@interface}.");
            return;
        }

        using var writer = context.CreateReplyWriter("a{sv}");
        ArrayStart dictionary = writer.WriteDictionaryStart();
        foreach (var name in AllPropertyNames)
        {
            var value = ReadProperty(name);
            if (value is null) continue;

            writer.WriteDictionaryEntryStart();
            writer.WriteString(name);
            writer.WriteVariant(value.Value);
        }
        writer.WriteDictionaryEnd(dictionary);
        context.Reply(writer.CreateMessage());
    }

    private static readonly string[] AllPropertyNames =
    [
        "Category", "Id", "Title", "Status", "WindowId", "IconName", "IconPixmap", "ToolTip", "ItemIsMenu", "Menu"
    ];

    private VariantValue? ReadProperty(string property)
    {
        int size;
        byte[] pixmap;
        string tooltipText;
        lock (_gate)
        {
            size = _pixmapSize;
            pixmap = _pixmap;
            tooltipText = _tooltipText;
        }

        return property switch
        {
            "Category" => VariantValue.String(CategoryApplicationStatus),
            "Id" => VariantValue.String(ItemId),
            "Title" => VariantValue.String(ItemTitle),
            "Status" => VariantValue.String(StatusActive),
            "WindowId" => VariantValue.Int32(0),
            "IconName" => VariantValue.String(string.Empty),
            "IconPixmap" => PixmapList(size, pixmap),
            "ToolTip" => VariantValue.Struct(
                VariantValue.String(string.Empty),
                PixmapList(size, pixmap),
                VariantValue.String(ItemTitle),
                VariantValue.String(tooltipText)),
            "ItemIsMenu" => VariantValue.Bool(false),
            "Menu" => VariantValue.ObjectPath(new ObjectPath(MenuPath)),
            _ => (VariantValue?)null
        };
    }

    /// <summary>The a(iiay) list: one (width, height, ARGB bytes) entry.</summary>
    private static VariantValue PixmapList(int size, byte[] pixmap)
    {
        var list = new Array<Struct<int, int, Array<byte>>>();
        list.Add(Struct.Create(size, size, new Array<byte>(pixmap)));
        return list;
    }

    private static void ReplyEmpty(MethodContext context)
    {
        using var writer = context.CreateReplyWriter(string.Empty);
        context.Reply(writer.CreateMessage());
    }

    private void EmitSignal(string member, string? signature)
    {
        using var writer = _connection.GetMessageWriter();
        writer.WriteSignalHeader(null, ObjectPathValue, Interface, member, signature);
        _connection.TrySendMessage(writer.CreateMessage());
    }
}
