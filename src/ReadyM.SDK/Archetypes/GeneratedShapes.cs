using System.ComponentModel;
using System.Linq;
using System.Reflection;
using ReadyM.Api.DI;

namespace ReadyM.SDK.Archetypes;

/// <summary>
/// Calls the registrations generated for the shapes an assembly declares. Found by their well-known names, since an
/// assembly has a concern's helper only when it declares shapes of that concern.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class GeneratedShapes
{
    /// <summary>The type a compilation gets when it declares shapes that need registering.</summary>
    private const string EntryPoint = "ReadyM.SDK.Generated.ShapeRegistrations";

    private const string Method = "RegisterAll";

    private const string ComponentHelper = "ReadyM.SDK.Generated.GeneratedComponentRegistrationHelper";

    private const string ReplicationHelper = "ReadyM.SDK.Generated.GeneratedReplicationRegistrationHelper";

    /// <summary>
    /// Registers what the assembly's shapes bring, each concern's helper it has, into the container.
    /// </summary>
    /// <returns>False when it has none.</returns>
    public static bool Register(IDependencyContainer services, Assembly assembly)
    {
        var registered = false;

        foreach (var helper in new[] { ComponentHelper, ReplicationHelper })
        {
            if (assembly.GetType(helper, throwOnError: false)?.GetMethod("Register", BindingFlags.Public | BindingFlags.Static) is not { } register)
                continue;

            register.Invoke(null, [services]);
            registered = true;
        }

        return registered;
    }

    // FixMe(2026-10-19-twins-scan-sdk): ShapeRegistrations.RegisterAll fills the statics of the kinds not registered in
    // the container yet. It goes with the last of them.
    public static int ApplyAll(IEnumerable<Assembly> assemblies, Action<Assembly>? onApplied = null, Action<Assembly, Exception>? onFailed = null)
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

    // FixMe(2026-10-19-twins-scan-sdk): see ApplyAll; it goes with it.
    public static bool Apply(Assembly assembly)
    {
        var entry = assembly.GetType(EntryPoint, throwOnError: false);

        if (entry?.GetMethod(Method, BindingFlags.Public | BindingFlags.Static) is not { } register)
            return false;

        register.Invoke(null, null);
        return true;
    }
}
