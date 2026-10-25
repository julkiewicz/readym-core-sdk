using ReadyM.Api.ECS.Registry;

namespace ReadyM.Api.Tests;

// Ids are handed out once every component is in, by a comparison the registry is given, never in the order Register
// ran in
public class IdComponentRegistryTests
{
    private struct Alpha
    {
        public static byte Id = byte.MaxValue;
    }

    private struct Beta
    {
        public static byte Id = byte.MaxValue;
    }

    private struct Gamma
    {
        public static byte Id = byte.MaxValue;
    }

    private sealed class Registration(Action<INativeComponentRegistry> register) : INativeComponentRegistration
    {
        public void Register(INativeComponentRegistry registry) => register(registry);
    }

    // The same base with an order of the test's own, to show the comparison is the one that decides
    private sealed class ReversedRegistry(IEnumerable<INativeComponentRegistration> registrations)
        : IdComponentRegistryBase<INativeComponentRegistry, ValueType>(registrations,
            (left, right) => string.CompareOrdinal(right.TypeFullName, left.TypeFullName)), INativeComponentRegistry
    {
        public INativeComponentRegistry RegisterComponent<T>(T defaultValue = default) where T : struct
            => RegisterComponentImpl(defaultValue);

        public byte IdOf<T>() => GetId(typeof(T).FullName!);

        public List<Type> GetComponentTypes() => throw new NotSupportedException();

        public Type? GetComponentType(int componentId) => throw new NotSupportedException();
    }

    [Fact]
    public void The_native_registry_hands_out_ids_in_full_name_order_whatever_the_registration_order()
    {
        var registry = new NativeComponentRegistry([new Registration(r => r.RegisterComponent<Gamma>().RegisterComponent<Alpha>().RegisterComponent<Beta>())]);

        Assert.Equal([typeof(Alpha), typeof(Beta), typeof(Gamma)], registry.GetComponentTypes());
        Assert.Equal(typeof(Beta), registry.GetComponentType(1));
    }

    [Fact]
    public void The_native_registry_sets_each_type_id_field_to_its_id()
    {
        _ = new NativeComponentRegistry([new Registration(r => r.RegisterComponent<Gamma>().RegisterComponent<Alpha>().RegisterComponent<Beta>())]);

        Assert.Equal(0, Alpha.Id);
        Assert.Equal(1, Beta.Id);
        Assert.Equal(2, Gamma.Id);
    }

    [Fact]
    public void A_registry_given_its_own_comparison_hands_out_ids_by_it()
    {
        var registry = new ReversedRegistry([new Registration(r => r.RegisterComponent<Alpha>().RegisterComponent<Gamma>().RegisterComponent<Beta>())]);

        Assert.Equal(0, registry.IdOf<Gamma>());
        Assert.Equal(1, registry.IdOf<Beta>());
        Assert.Equal(2, registry.IdOf<Alpha>());
    }
}
