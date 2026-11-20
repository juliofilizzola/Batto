using Batto.Notifications;
using Tmds.DBus.Protocol;

namespace Batto.Linux.Notifications;

/// <summary>Desktop notifications through org.freedesktop.Notifications (KDE, GNOME, and every freedesktop notification daemon).</summary>
public sealed class FreedesktopNotifier
{
    private const string Service = "org.freedesktop.Notifications";
    private const string ObjectPathValue = "/org/freedesktop/Notifications";
    private const string Interface = "org.freedesktop.Notifications";
    private const string NotifyMember = "Notify";
    private const string NotifySignature = "susssasa{sv}i";
    private const string AppName = "Batto";
    private const string WarningIcon = "battery-caution";
    private const string InfoIcon = "bluetooth";
    private const int DefaultExpireTimeout = -1;
    private const uint NoReplacement = 0;

    private readonly DBusConnection _session;

    public FreedesktopNotifier(DBusConnection session)
    {
        _session = session;
    }

    /// <summary>Sends the notification without blocking the caller. Failures go to the crash log, never to the user.</summary>
    public void Show(NotificationPayload payload) => _ = ShowAsync(payload);

    /// <summary>Sends the notification and returns the id the notification server assigned, or 0 when it could not be sent.</summary>
    public async Task<uint> ShowAsync(NotificationPayload payload)
    {
        try
        {
            var request = BuildNotifyMessage(payload);
            return await _session.CallMethodAsync(request, (Message message, object? state) => message.GetBodyReader().ReadUInt32(), null);
        }
        catch (Exception ex)
        {
            CrashLogger.Log("freedesktop notification", ex);
            return 0;
        }
    }

    private MessageBuffer BuildNotifyMessage(NotificationPayload payload)
    {
        using var writer = _session.GetMessageWriter();
        writer.WriteMethodCallHeader(
            destination: Service,
            path: ObjectPathValue,
            @interface: Interface,
            member: NotifyMember,
            signature: NotifySignature);

        writer.WriteString(AppName);
        writer.WriteUInt32(NoReplacement);
        writer.WriteString(payload.IsWarning ? WarningIcon : InfoIcon);
        writer.WriteString(payload.Title);
        writer.WriteString(payload.Message);

        ArrayStart actions = writer.WriteArrayStart(DBusType.String);
        writer.WriteArrayEnd(actions);

        ArrayStart hints = writer.WriteDictionaryStart();
        writer.WriteDictionaryEnd(hints);

        writer.WriteInt32(DefaultExpireTimeout);
        return writer.CreateMessage();
    }
}
