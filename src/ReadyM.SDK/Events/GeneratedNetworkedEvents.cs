using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace ReadyM.SDK.Events;

/// <summary>
/// Runs the network event registrations an assembly generated: the contracts its Common project declared, and
/// the handler sets a mod declared. Called for every assembly a host loads, before the registry is read.
/// </summary>
/// <remarks>
/// A target that has <c>[ModuleInitializer]</c> runs these itself as it loads and this finds nothing left to
/// do, which is why both are safe to say twice. A netstandard2.0 target has no such attribute, so there it is
/// this call that runs them.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class GeneratedNetworkEvents
{
    /// <summary>The types a compilation gets when it declares events, or handlers for one.</summary>
    private static readonly (string Type, string Method)[] EntryPoints =
    [
        ("ReadyM.SDK.Generated.NetworkEventDeclarations", "DeclareAll"),
        ("ReadyM.SDK.Generated.NetworkEventHandlerRegistrations", "RegisterAll"),
    ];

    public static int ApplyAll(
        IEnumerable<Assembly> assemblies, Action<Assembly>? onApplied = null, Action<Assembly, Exception>? onFailed = null)
    {
        var applied = 0;

        foreach (var assembly in assemblies.Distinct())
        {
            try
            {
                if (!Apply(assembly))
                    continue;

                applied++;
                onApplied?.Invoke(assembly);
            }
            catch (Exception ex)
            {
                onFailed?.Invoke(assembly, ex);
            }
        }

        return applied;
    }

    public static bool Apply(Assembly assembly)
    {
        var ran = false;

        foreach (var (type, method) in EntryPoints)
        {
            var entry = assembly.GetType(type, throwOnError: false);

            if (entry?.GetMethod(method, BindingFlags.Public | BindingFlags.Static) is not { } run)
                continue;

            run.Invoke(null, null);
            ran = true;
        }

        return ran;
    }
}
