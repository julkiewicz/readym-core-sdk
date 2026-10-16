using System.Runtime.InteropServices;
using Friflo.Engine.ECS;

namespace ReadyM.SDK.Tests.Ecs;

/// <summary>
/// The entity store's access guard. The default is a reader-writer lock: query loops that throw on structural
/// changes hold a read, the guarded structural changes hold a write. The owner-thread guard takes no lock and rejects
/// a guarded operation from a thread other than its owner; the checked guard takes no lock and runs a caller-supplied
/// check on every enter.
/// </summary>
public class StoreAccessGuardTests
{
    [StructLayout(LayoutKind.Auto)]
    private struct Probe : IComponent
    {
        public int Value;
    }

    [StructLayout(LayoutKind.Auto)]
    private struct Marker : IComponent
    {
        public int Value;
    }

    private static readonly TimeSpan Blocked = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan Generous = TimeSpan.FromSeconds(10);

    // --- the default: a reader-writer lock

    [Fact]
    public void A_store_built_without_a_guard_uses_the_reader_writer_lock()
    {
        Assert.IsType<ReaderWriterLockGuard>(new EntityStore().AccessGuard);
        Assert.IsType<ReaderWriterLockGuard>(new EntityStore(PidType.UsePidAsId).AccessGuard);
    }

    [Fact]
    public void A_query_loop_holds_the_read_lock_until_it_ends()
    {
        var store = new EntityStore();
        var guard = (ReaderWriterLockGuard)store.AccessGuard;
        store.CreateEntity().AddComponent(new Probe { Value = 1 });

        foreach (var _ in store.Query<Probe>().Entities)
        {
            Assert.True(guard.IsReadLockHeld);
        }
        Assert.False(guard.IsReadLockHeld);

        foreach (var _ in store.Query<Probe>().Chunks)
        {
            Assert.True(guard.IsReadLockHeld);
        }
        Assert.False(guard.IsReadLockHeld);
    }

    [Fact]
    public void A_structural_change_on_another_thread_waits_for_a_query_loop_to_end()
    {
        var store = new EntityStore();
        var entity = store.CreateEntity();
        entity.AddComponent(new Probe { Value = 1 });
        Task change;

        var loop = store.Query<Probe>().Entities.GetEnumerator();
        try
        {
            Assert.True(loop.MoveNext());
            change = Task.Factory.StartNew(() => entity.AddComponent(new Marker { Value = 2 }),
                TaskCreationOptions.LongRunning);

            Assert.False(change.Wait(Blocked));
            Assert.False(entity.HasComponent<Marker>());
        }
        finally
        {
            loop.Dispose();
        }

        Assert.True(change.Wait(Generous));
        Assert.True(entity.HasComponent<Marker>());
    }

    [Fact]
    public void A_structural_change_inside_a_query_loop_on_the_same_thread_cannot_take_the_write_lock()
    {
        var store = new EntityStore();
        var entity = store.CreateEntity();
        entity.AddComponent(new Probe { Value = 1 });

        foreach (var _ in store.Query<Probe>().Entities)
        {
            Assert.Throws<LockRecursionException>(() => entity.AddComponent(new Marker { Value = 2 }));
        }
    }

    // --- the owner-thread guard

    [Fact]
    public void A_store_keeps_the_guard_it_was_built_with()
    {
        var guard = StoreAccessGuard.OwnerThread();

        Assert.Same(guard, new EntityStore(guard).AccessGuard);
        Assert.Same(guard, new EntityStore(PidType.UsePidAsId, guard).AccessGuard);
        Assert.Throws<ArgumentNullException>(() => new EntityStore(null!));
    }

    [Fact]
    public void The_owner_thread_guard_rejects_another_thread_at_once_instead_of_making_it_wait()
    {
        var store = new EntityStore(StoreAccessGuard.OwnerThread());
        var entity = store.CreateEntity();
        entity.AddComponent(new Probe { Value = 1 });

        // A query loop is running here; under the lock the other thread would wait for it to end.
        var loop = store.Query<Probe>().Entities.GetEnumerator();
        try
        {
            Assert.True(loop.MoveNext());
            var error = RunOnAnotherThread(() => entity.AddComponent(new Marker { Value = 2 }));
            Assert.IsType<InvalidOperationException>(error);
        }
        finally
        {
            loop.Dispose();
        }
        Assert.False(entity.HasComponent<Marker>());
    }

    [Fact]
    public void A_structural_change_inside_a_query_loop_on_the_owner_thread_reaches_the_stores_own_check()
    {
        var store = new EntityStore(StoreAccessGuard.OwnerThread());
        var entity = store.CreateEntity();
        entity.AddComponent(new Probe { Value = 1 });

        foreach (var _ in store.Query<Probe>().Entities)
        {
            // No lock to recurse into: the store's own query-loop check is what refuses the change.
            Assert.Throws<StructuralChangeException>(() => entity.AddComponent(new Marker { Value = 2 }));
        }

        entity.AddComponent(new Marker { Value = 2 });
        Assert.True(entity.HasComponent<Marker>());
    }

    [Fact]
    public void The_owner_thread_guard_throws_on_a_structural_change_from_another_thread()
    {
        var store = new EntityStore(StoreAccessGuard.OwnerThread());
        var entity = store.CreateEntity();
        entity.AddComponent(new Probe { Value = 1 });

        var error = RunOnAnotherThread(() => entity.AddComponent(new Marker { Value = 2 }));

        Assert.IsType<InvalidOperationException>(error);
        Assert.False(entity.HasComponent<Marker>());
    }

    [Fact]
    public void The_owner_thread_guard_throws_on_a_query_loop_from_another_thread()
    {
        var store = new EntityStore(StoreAccessGuard.OwnerThread());
        store.CreateEntity().AddComponent(new Probe { Value = 1 });
        var query = store.Query<Probe>();

        Assert.IsType<InvalidOperationException>(RunOnAnotherThread(() =>
        {
            foreach (var _ in query.Entities) { }
        }));
        Assert.IsType<InvalidOperationException>(RunOnAnotherThread(() =>
        {
            foreach (var _ in query.Chunks) { }
        }));
    }

    [Fact]
    public void The_owner_thread_guard_can_name_its_owner()
    {
        var owner = new Thread(() => { });
        var guard = (OwnerThreadGuard)StoreAccessGuard.OwnerThread(owner);
        var store = new EntityStore(guard);
        var entity = store.CreateEntity();

        Assert.Same(owner, guard.Owner);
        Assert.Throws<InvalidOperationException>(() => entity.AddComponent(new Probe { Value = 1 }));
        Assert.Throws<InvalidOperationException>(() =>
        {
            foreach (var _ in store.Query<Probe>().Entities) { }
        });
        Assert.Throws<ArgumentNullException>(() => StoreAccessGuard.OwnerThread(null!));
    }

    [Fact]
    public void Creating_and_deleting_entities_are_not_guarded()
    {
        // The ECS never guarded entity creation and deletion (with the lock either): only the operations
        // StoreAccessGuard lists enter a read or a write. Pinned so a change to that is deliberate.
        var store = new EntityStore(StoreAccessGuard.OwnerThread());
        var kept = store.CreateEntity();
        Entity created = default;

        Assert.Null(RunOnAnotherThread(() => created = store.CreateEntity()));
        Assert.False(created.IsNull);
        Assert.Null(RunOnAnotherThread(() => kept.DeleteEntity()));
        Assert.True(kept.IsNull);
    }

    // --- the checked guard: a caller-supplied check on every enter

    [Fact]
    public void A_checked_guard_runs_its_check_on_every_read_and_write_enter()
    {
        var checks = 0;
        var store = new EntityStore(StoreAccessGuard.Checked(() => checks++));
        Assert.IsType<CheckedAccessGuard>(store.AccessGuard);
        var entity = store.CreateEntity();
        Assert.Equal(0, checks);

        entity.AddComponent(new Probe { Value = 1 });
        var afterWrite = checks;
        Assert.True(afterWrite >= 1);

        foreach (var _ in store.Query<Probe>().Entities) { }
        Assert.Equal(afterWrite + 1, checks);

        foreach (var _ in store.Query<Probe>().Chunks) { }
        Assert.Equal(afterWrite + 2, checks);

        entity.RemoveComponent<Probe>();
        Assert.True(checks > afterWrite + 2);
    }

    [Fact]
    public void A_checked_guard_takes_no_lock()
    {
        var store = new EntityStore(StoreAccessGuard.Checked(() => { }));
        var entity = store.CreateEntity();
        entity.AddComponent(new Probe { Value = 1 });

        foreach (var _ in store.Query<Probe>().Entities)
        {
            // No lock to recurse into: the store's own query-loop check is what refuses the change.
            Assert.Throws<StructuralChangeException>(() => entity.AddComponent(new Marker { Value = 2 }));
        }
    }

    [Fact]
    public void A_throwing_check_propagates_out_of_the_guarded_operation()
    {
        var refuse = false;
        var store = new EntityStore(StoreAccessGuard.Checked(() =>
        {
            if (refuse) throw new InvalidOperationException("not the owner");
        }));
        var entity = store.CreateEntity();
        entity.AddComponent(new Probe { Value = 1 });
        refuse = true;

        var write = Assert.Throws<InvalidOperationException>(() => entity.AddComponent(new Marker { Value = 2 }));
        Assert.Equal("not the owner", write.Message);
        Assert.False(entity.HasComponent<Marker>());

        var read = Assert.Throws<InvalidOperationException>(() =>
        {
            foreach (var _ in store.Query<Probe>().Entities) { }
        });
        Assert.Equal("not the owner", read.Message);

        // A refused read left nothing held: once the check passes again the store works as before.
        refuse = false;
        entity.AddComponent(new Marker { Value = 2 });
        Assert.True(entity.HasComponent<Marker>());
        Assert.Throws<ArgumentNullException>(() => StoreAccessGuard.Checked(null!));
    }

    private static Exception? RunOnAnotherThread(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                error = e;
            }
        });
        thread.Start();
        Assert.True(thread.Join(Generous));
        return error;
    }
}
