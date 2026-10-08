using ReadyM.SDK.Archetypes;
using ReadyM.SDK.Tests.Server.Fixtures;

namespace ReadyM.SDK.Tests.Server;

/// <summary>
/// Asking an entity which scope holds it, the read side of creating one in a scope.
/// </summary>
/// <remarks>
/// The answer is a shape rather than an id, so it composes with everything else: what comes back
/// reads and writes like any other entity. The walk continues upwards, because an entity created in
/// a room is in the area that room sits in just as much as it is in the room.
/// </remarks>
public class ServerScopeLookupTests : ServerSdkTest
{
    private TestArea Area(int id)
    {
        var area = Entities.Create<TestArea>();

        area.AreaId = id;

        return area;
    }

    private TestRoom Room(int id, Scope within)
    {
        var room = Entities.Create<TestRoom>(within);

        room.RoomId = id;

        return room;
    }

    [Fact]
    public void An_entity_reports_the_scope_it_was_created_in()
    {
        var north = Area(1);
        var guard = Entities.Create<Guard>(north);

        Assert.True(Entities.TryGetScope<Guard, TestArea>(guard, out var found));
        Assert.Equal(1, found.AreaId);
    }

    /// <summary>A scope further out answers too, which is how an entity in a cell reaches its area.</summary>
    [Fact]
    public void An_entity_reports_a_scope_further_up()
    {
        var north = Area(1);
        var hall = Room(7, north);
        var guard = Entities.Create<Guard>(hall);

        Assert.True(Entities.TryGetScope<Guard, TestRoom>(guard, out var room));
        Assert.Equal(7, room.RoomId);

        Assert.True(Entities.TryGetScope<Guard, TestArea>(guard, out var area));
        Assert.Equal(1, area.AreaId);
    }

    /// A scope is itself held, so it answers for what is above it.
    [Fact]
    public void A_scope_reports_the_scope_holding_it()
    {
        var north = Area(1);
        var hall = Room(7, north);

        Assert.True(Entities.TryGetScope<TestRoom, TestArea>(hall, out var area));
        Assert.Equal(1, area.AreaId);
    }

    [Fact]
    public void An_entity_in_no_scope_reports_none()
    {
        var guard = Entities.Create<Guard>();

        Assert.False(Entities.TryGetScope<Guard, TestArea>(guard, out _));
    }

    /// <summary>Nothing of that shape holds it, which is a no rather than the nearest scope of any shape.</summary>
    [Fact]
    public void A_scope_of_another_shape_is_not_reported()
    {
        var north = Area(1);
        var guard = Entities.Create<Guard>(north);

        Assert.False(Entities.TryGetScope<Guard, TestRoom>(guard, out _));
    }
}
