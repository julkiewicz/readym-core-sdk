using System.Runtime.InteropServices;
using Friflo.Engine.ECS;

namespace ReadyM.SDK.Tests.Ecs;

/// <summary>
/// The entity store's access guard. The default is a reader-writer lock: query loops that throw on structural
/// changes hold a read, structural changes hold a write. The owner-thread guard takes no lock and rejects any
/// access from a thread other than its owner.
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

    [Fact]
    public void The_reader_writer_lock_guard_excludes_a_writer_while_a_reader_holds_it()
    {
        var guard = (ReaderWriterLockGuard)StoreAccessGuard.ReaderWriterLock();
        guard.EnterRead();
        var writer = Task.Factory.StartNew(() =>
        {
            guard.EnterWrite();
            guard.ExitWrite();
        }, TaskCreationOptions.LongRunning);

        Assert.False(writer.Wait(Blocked));
        guard.ExitRead();
        Assert.True(writer.Wait(Generous));
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
    public void The_owner_thread_guard_takes_no_lock()
    {
        var guard = StoreAccessGuard.OwnerThread();

        // A reader-writer lock refuses a write to a thread holding a read; with no lock there is nothing to refuse.
        guard.EnterRead();
        guard.EnterWrite();
        guard.ExitWrite();
        guard.ExitRead();

        // Another thread is rejected at once rather than made to wait for the read held here.
        guard.EnterRead();
        var other = Task.Factory.StartNew(guard.EnterWrite, TaskCreationOptions.LongRunning);
        Assert.True(((IAsyncResult)other).AsyncWaitHandle.WaitOne(Generous));
        Assert.IsType<InvalidOperationException>(other.Exception?.InnerException);
        guard.ExitRead();
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

        Assert.Same(owner, guard.Owner);
        Assert.Throws<InvalidOperationException>(guard.EnterRead);
        Assert.Throws<InvalidOperationException>(guard.EnterWrite);
        Assert.Throws<ArgumentNullException>(() => StoreAccessGuard.OwnerThread(null!));
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
