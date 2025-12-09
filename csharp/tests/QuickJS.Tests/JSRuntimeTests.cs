// Licensed under the MIT License.

using Xunit;

namespace QuickJS.Tests;

/// <summary>
/// Tests for JSRuntime class.
/// </summary>
public class JSRuntimeTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_CreatesRuntimeWithDefaults()
    {
        using var runtime = new JSRuntime();

        Assert.False(runtime.IsDisposed);
        Assert.Equal(0, runtime.ContextCount);
        Assert.True(runtime.AtomCount > 0); // Standard atoms are pre-created
        Assert.True(runtime.ClassCount > 0); // Standard classes are registered
        Assert.Equal(JSRuntime.NoMemoryLimit, runtime.MemoryLimit);
        Assert.Equal(JSRuntime.DefaultStackSize, runtime.MaxStackSize);
        Assert.Equal(JSRuntime.DefaultGCThreshold, runtime.GCThreshold);
    }

    [Fact]
    public void Constructor_RegistersStandardClasses()
    {
        using var runtime = new JSRuntime();

        // Check some standard class registrations
        Assert.NotNull(runtime.GetClass(JSClassId.Object));
        Assert.NotNull(runtime.GetClass(JSClassId.Array));
        Assert.NotNull(runtime.GetClass(JSClassId.Error));
        Assert.NotNull(runtime.GetClass(JSClassId.CFunction));
    }

    #endregion

    #region Context Management Tests

    [Fact]
    public void CreateContext_ReturnsNewContext()
    {
        using var runtime = new JSRuntime();

        using var context = runtime.CreateContext();

        Assert.NotNull(context);
        Assert.Same(runtime, context.Runtime);
        Assert.Equal(1, runtime.ContextCount);
    }

    [Fact]
    public void CreateContext_MultipleContexts()
    {
        using var runtime = new JSRuntime();

        using var ctx1 = runtime.CreateContext();
        using var ctx2 = runtime.CreateContext();
        using var ctx3 = runtime.CreateContext();

        Assert.Equal(3, runtime.ContextCount);
    }

    [Fact]
    public void CreateContext_ThrowsWhenDisposed()
    {
        var runtime = new JSRuntime();
        runtime.Dispose();

        Assert.Throws<ObjectDisposedException>(() => runtime.CreateContext());
    }

    [Fact]
    public void GetContexts_ReturnsAllContexts()
    {
        using var runtime = new JSRuntime();
        using var ctx1 = runtime.CreateContext();
        using var ctx2 = runtime.CreateContext();

        var contexts = runtime.GetContexts().ToList();

        Assert.Equal(2, contexts.Count);
        Assert.Contains(ctx1, contexts);
        Assert.Contains(ctx2, contexts);
    }

    #endregion

    #region Memory Management Tests

    [Fact]
    public void MemoryLimit_DefaultIsNoLimit()
    {
        using var runtime = new JSRuntime();
        Assert.Equal(JSRuntime.NoMemoryLimit, runtime.MemoryLimit);
    }

    [Fact]
    public void MemoryLimit_CanBeSet()
    {
        using var runtime = new JSRuntime();
        runtime.MemoryLimit = 1024 * 1024; // 1 MB

        Assert.Equal(1024 * 1024, runtime.MemoryLimit);
    }

    [Fact]
    public void MemoryLimit_RejectsNegativeValues()
    {
        using var runtime = new JSRuntime();

        Assert.Throws<ArgumentOutOfRangeException>(() => runtime.MemoryLimit = -2);
    }

    [Fact]
    public void RecordAllocation_TracksMemory()
    {
        using var runtime = new JSRuntime();
        runtime.MemoryLimit = 1000;

        Assert.True(runtime.RecordAllocation(100));
        Assert.Equal(100, runtime.MemoryUsed);
        Assert.Equal(1, runtime.AllocationCount);

        Assert.True(runtime.RecordAllocation(200));
        Assert.Equal(300, runtime.MemoryUsed);
        Assert.Equal(2, runtime.AllocationCount);
    }

    [Fact]
    public void RecordAllocation_RejectsWhenOverLimit()
    {
        using var runtime = new JSRuntime();
        runtime.MemoryLimit = 100;

        Assert.True(runtime.RecordAllocation(50));
        Assert.False(runtime.RecordAllocation(100)); // Would exceed limit
        Assert.Equal(50, runtime.MemoryUsed); // Unchanged
    }

    [Fact]
    public void RecordDeallocation_ReducesMemory()
    {
        using var runtime = new JSRuntime();
        runtime.RecordAllocation(100);

        runtime.RecordDeallocation(30);

        Assert.Equal(70, runtime.MemoryUsed);
    }

    [Fact]
    public void RecordDeallocation_DoesNotGoBelowZero()
    {
        using var runtime = new JSRuntime();
        runtime.RecordAllocation(50);

        runtime.RecordDeallocation(100);

        Assert.Equal(0, runtime.MemoryUsed);
    }

    [Fact]
    public void GCThreshold_CanBeSet()
    {
        using var runtime = new JSRuntime();
        runtime.GCThreshold = 512 * 1024;

        Assert.Equal(512 * 1024, runtime.GCThreshold);
    }

    [Fact]
    public void ShouldTriggerGC_ReturnsTrueWhenOverThreshold()
    {
        using var runtime = new JSRuntime();
        runtime.GCThreshold = 100;

        runtime.RecordAllocation(50);
        Assert.False(runtime.ShouldTriggerGC());

        runtime.RecordAllocation(60);
        Assert.True(runtime.ShouldTriggerGC());
    }

    [Fact]
    public void ShouldTriggerGC_ReturnsFalseWhenDisabled()
    {
        using var runtime = new JSRuntime();
        runtime.GCThreshold = -1; // Disable

        runtime.RecordAllocation(1000000);

        Assert.False(runtime.ShouldTriggerGC());
    }

    [Fact]
    public void GetMemoryStats_ReturnsCorrectValues()
    {
        using var runtime = new JSRuntime();
        runtime.MemoryLimit = 1000;
        runtime.RecordAllocation(100);
        using var ctx = runtime.CreateContext();

        var stats = runtime.GetMemoryStats();

        Assert.Equal(100, stats.MemoryUsed);
        Assert.Equal(1000, stats.MemoryLimit);
        Assert.Equal(1, stats.AllocationCount);
        Assert.Equal(1, stats.ContextCount);
        Assert.True(stats.ClassCount > 0);
        Assert.True(stats.AtomCount > 0);
    }

    #endregion

    #region Stack Size Tests

    [Fact]
    public void MaxStackSize_DefaultIsCorrect()
    {
        using var runtime = new JSRuntime();
        Assert.Equal(JSRuntime.DefaultStackSize, runtime.MaxStackSize);
    }

    [Fact]
    public void MaxStackSize_CanBeSet()
    {
        using var runtime = new JSRuntime();
        runtime.MaxStackSize = 512 * 1024;

        Assert.Equal(512 * 1024, runtime.MaxStackSize);
    }

    [Fact]
    public void MaxStackSize_RejectsNegativeValues()
    {
        using var runtime = new JSRuntime();

        Assert.Throws<ArgumentOutOfRangeException>(() => runtime.MaxStackSize = -1);
    }

    [Fact]
    public void MaxStackSize_ZeroDisablesCheck()
    {
        using var runtime = new JSRuntime();
        runtime.MaxStackSize = 0;

        Assert.Equal(0, runtime.MaxStackSize);
    }

    #endregion

    #region Class Registration Tests

    [Fact]
    public void RegisterClass_ReturnsNewClassId()
    {
        using var runtime = new JSRuntime();
        int initialCount = runtime.ClassCount;

        var classId = runtime.RegisterClass("MyClass");

        Assert.Equal(initialCount, (int)classId);
        Assert.Equal(initialCount + 1, runtime.ClassCount);
    }

    [Fact]
    public void RegisterClass_ThrowsOnDuplicateName()
    {
        using var runtime = new JSRuntime();

        runtime.RegisterClass("UniqueClass");

        Assert.Throws<InvalidOperationException>(() => runtime.RegisterClass("UniqueClass"));
    }

    [Fact]
    public void GetClass_ReturnsRegisteredClass()
    {
        using var runtime = new JSRuntime();
        var classId = runtime.RegisterClass("TestClass");

        var classDef = runtime.GetClass(classId);

        Assert.NotNull(classDef);
        Assert.Equal("TestClass", classDef.ClassName);
        Assert.Equal(classId, classDef.ClassId);
    }

    [Fact]
    public void GetClassId_ReturnsCorrectId()
    {
        using var runtime = new JSRuntime();
        var classId = runtime.RegisterClass("LookupClass");

        var foundId = runtime.GetClassId("LookupClass");

        Assert.Equal(classId, foundId);
    }

    [Fact]
    public void GetClassId_ReturnsNoneForUnknown()
    {
        using var runtime = new JSRuntime();

        var classId = runtime.GetClassId("NonExistentClass");

        Assert.Equal(JSClassId.None, classId);
    }

    #endregion

    #region Atom Management Tests

    [Fact]
    public void InternAtom_ReturnsAtom()
    {
        using var runtime = new JSRuntime();

        var atom = runtime.InternAtom("test");

        Assert.False(atom.IsEmpty);
    }

    [Fact]
    public void InternAtom_ReturnsSameAtomForSameString()
    {
        using var runtime = new JSRuntime();

        var atom1 = runtime.InternAtom("duplicate");
        var atom2 = runtime.InternAtom("duplicate");

        Assert.Equal(atom1, atom2);
    }

    [Fact]
    public void GetAtomString_ReturnsOriginalString()
    {
        using var runtime = new JSRuntime();
        var atom = runtime.InternAtom("hello");

        var str = runtime.GetAtomString(atom);

        Assert.Equal("hello", str);
    }

    #endregion

    #region Exception Handling Tests

    [Fact]
    public void HasException_InitiallyFalse()
    {
        using var runtime = new JSRuntime();
        Assert.False(runtime.HasException);
    }

    [Fact]
    public void SetException_SetsHasException()
    {
        using var runtime = new JSRuntime();

        runtime.SetException(JSValue.FromString("error"));

        Assert.True(runtime.HasException);
    }

    [Fact]
    public void GetAndClearException_ReturnsAndClears()
    {
        using var runtime = new JSRuntime();
        runtime.SetException(JSValue.FromString("test error"));

        var exception = runtime.GetAndClearException();

        Assert.Equal("test error", exception.ToString());
        Assert.False(runtime.HasException);
    }

    [Fact]
    public void GetAndClearException_ReturnsUndefinedWhenNoException()
    {
        using var runtime = new JSRuntime();

        var exception = runtime.GetAndClearException();

        Assert.True(exception.IsUndefined);
    }

    [Fact]
    public void ClearException_ClearsException()
    {
        using var runtime = new JSRuntime();
        runtime.SetException(JSValue.FromString("error"));

        runtime.ClearException();

        Assert.False(runtime.HasException);
    }

    #endregion

    #region Interrupt Handling Tests

    [Fact]
    public void IsInterruptRequested_InitiallyFalse()
    {
        using var runtime = new JSRuntime();
        Assert.False(runtime.IsInterruptRequested);
    }

    [Fact]
    public void RequestInterrupt_SetsFlag()
    {
        using var runtime = new JSRuntime();

        runtime.RequestInterrupt();

        Assert.True(runtime.IsInterruptRequested);
    }

    [Fact]
    public void ClearInterrupt_ClearsFlag()
    {
        using var runtime = new JSRuntime();
        runtime.RequestInterrupt();

        runtime.ClearInterrupt();

        Assert.False(runtime.IsInterruptRequested);
    }

    [Fact]
    public void CheckInterrupt_ReturnsCorrectly()
    {
        using var runtime = new JSRuntime();
        runtime.InterruptInterval = 5;

        // First 4 checks should return false
        for (int i = 0; i < 4; i++)
        {
            Assert.False(runtime.CheckInterrupt());
        }

        // Fifth check should check the flag (which is false)
        Assert.False(runtime.CheckInterrupt());

        // Now request interrupt
        runtime.RequestInterrupt();

        // Next 4 checks return false (counter not reached)
        for (int i = 0; i < 4; i++)
        {
            Assert.False(runtime.CheckInterrupt());
        }

        // This check should return true (interrupt requested)
        Assert.True(runtime.CheckInterrupt());
    }

    [Fact]
    public void InterruptInterval_CanBeSet()
    {
        using var runtime = new JSRuntime();
        runtime.InterruptInterval = 1000;

        Assert.Equal(1000, runtime.InterruptInterval);
    }

    [Fact]
    public void InterruptInterval_RejectsZeroOrNegative()
    {
        using var runtime = new JSRuntime();

        Assert.Throws<ArgumentOutOfRangeException>(() => runtime.InterruptInterval = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => runtime.InterruptInterval = -1);
    }

    #endregion

    #region Job Queue Tests

    [Fact]
    public void PendingJobCount_InitiallyZero()
    {
        using var runtime = new JSRuntime();
        Assert.Equal(0, runtime.PendingJobCount);
    }

    [Fact]
    public void HasPendingJobs_InitiallyFalse()
    {
        using var runtime = new JSRuntime();
        Assert.False(runtime.HasPendingJobs());
    }

    [Fact]
    public void EnqueueJob_IncrementsPendingCount()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();
        var func = new JSFunction((_, _) => JSValue.Undefined, "test");

        runtime.EnqueueJob(func, Array.Empty<JSValue>(), context);

        Assert.Equal(1, runtime.PendingJobCount);
        Assert.True(runtime.HasPendingJobs());
    }

    [Fact]
    public void ExecutePendingJobs_ExecutesAllJobs()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();
        int callCount = 0;
        var func = new JSFunction((_, _) => { callCount++; return JSValue.Undefined; }, "test");

        runtime.EnqueueJob(func, Array.Empty<JSValue>(), context);
        runtime.EnqueueJob(func, Array.Empty<JSValue>(), context);

        var executed = runtime.ExecutePendingJobs();

        Assert.Equal(2, executed);
        Assert.Equal(2, callCount);
        Assert.Equal(0, runtime.PendingJobCount);
    }

    [Fact]
    public void ExecutePendingJobs_StopsOnInterrupt()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();
        int callCount = 0;
        var func = new JSFunction((_, _) =>
        {
            callCount++;
            if (callCount == 1) runtime.RequestInterrupt();
            return JSValue.Undefined;
        }, "test");

        runtime.EnqueueJob(func, Array.Empty<JSValue>(), context);
        runtime.EnqueueJob(func, Array.Empty<JSValue>(), context);
        runtime.EnqueueJob(func, Array.Empty<JSValue>(), context);

        var executed = runtime.ExecutePendingJobs();

        Assert.Equal(1, executed); // Only first job executed before interrupt
        Assert.Equal(1, callCount);
        Assert.True(runtime.HasPendingJobs()); // Jobs remain
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_SetsIsDisposed()
    {
        var runtime = new JSRuntime();

        runtime.Dispose();

        Assert.True(runtime.IsDisposed);
    }

    [Fact]
    public void Dispose_DisposesContexts()
    {
        var runtime = new JSRuntime();
        var ctx1 = runtime.CreateContext();
        var ctx2 = runtime.CreateContext();

        runtime.Dispose();

        Assert.True(ctx1.IsDisposed);
        Assert.True(ctx2.IsDisposed);
    }

    [Fact]
    public void Dispose_ClearsJobQueue()
    {
        var runtime = new JSRuntime();
        var context = runtime.CreateContext();
        var func = new JSFunction((_, _) => JSValue.Undefined, "test");
        runtime.EnqueueJob(func, Array.Empty<JSValue>(), context);

        runtime.Dispose();

        Assert.Equal(0, runtime.PendingJobCount);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var runtime = new JSRuntime();

        runtime.Dispose();
        runtime.Dispose(); // Should not throw

        Assert.True(runtime.IsDisposed);
    }

    #endregion

    #region UserOpaque Tests

    [Fact]
    public void UserOpaque_InitiallyNull()
    {
        using var runtime = new JSRuntime();
        Assert.Null(runtime.UserOpaque);
    }

    [Fact]
    public void UserOpaque_CanBeSetAndRetrieved()
    {
        using var runtime = new JSRuntime();
        var data = new object();

        runtime.UserOpaque = data;

        Assert.Same(data, runtime.UserOpaque);
    }

    #endregion

    #region RuntimeInfo Tests

    [Fact]
    public void RuntimeInfo_InitiallyNull()
    {
        using var runtime = new JSRuntime();
        Assert.Null(runtime.RuntimeInfo);
    }

    [Fact]
    public void RuntimeInfo_CanBeSetAndRetrieved()
    {
        using var runtime = new JSRuntime();

        runtime.RuntimeInfo = "QuickJS.NET v0.1";

        Assert.Equal("QuickJS.NET v0.1", runtime.RuntimeInfo);
    }

    #endregion
}
