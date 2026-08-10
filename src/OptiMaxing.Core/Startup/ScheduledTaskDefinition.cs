using System.Xml.Linq;

namespace OptiMaxing.Core.Startup;

/// <summary>The parts of a Task Scheduler XML definition the autostart tab needs.</summary>
/// <remarks>
/// Parsed from the XML rather than from <c>schtasks /query</c>: that command prints the state as a
/// localized word ("Готово", "Отключено"), and matching on it would break the moment the tool ran
/// on a differently localized Windows. The XML is the same on every locale.
/// </remarks>
public sealed record ScheduledTaskDefinition(
    bool IsEnabled,
    bool StartsAtLogonOrBoot,
    string Command,
    string Arguments)
{
    public static ScheduledTaskDefinition? Parse(string xml)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }

        var root = document.Root;
        if (root is null)
        {
            return null;
        }

        // Some definitions omit the namespace; look elements up by local name so both parse.
        XElement? Child(XElement? parent, string name) =>
            parent?.Elements().FirstOrDefault(e => e.Name.LocalName == name);

        var triggers = Child(root, "Triggers");
        var startsAtLogonOrBoot = triggers is not null && triggers.Elements().Any(t =>
            t.Name.LocalName is "LogonTrigger" or "BootTrigger"
            && !string.Equals(Child(t, "Enabled")?.Value, "false", StringComparison.OrdinalIgnoreCase));

        var exec = Child(root, "Actions")?.Elements().FirstOrDefault(e => e.Name.LocalName == "Exec");

        // A missing Settings/Enabled means enabled: the element is only written out once something
        // has switched the task off.
        var enabled = !string.Equals(
            Child(Child(root, "Settings"), "Enabled")?.Value, "false", StringComparison.OrdinalIgnoreCase);

        return new ScheduledTaskDefinition(
            enabled,
            startsAtLogonOrBoot,
            Child(exec, "Command")?.Value.Trim() ?? string.Empty,
            Child(exec, "Arguments")?.Value.Trim() ?? string.Empty);
    }
}
