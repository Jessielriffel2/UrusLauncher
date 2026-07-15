using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using LegendLauncher.Core.Models;
using LegendLauncher.GameHost.Legacy;

namespace LegendLauncher.Tests.GameHost;

public sealed class GameHostWindowTests
{
    [Fact]
    public void SessionForm_IsBorderlessAndExcludedFromTaskbar()
    {
        var result = RunInSta(() =>
        {
            LegacyRuntimeAssets assets = LegacyRuntimeAssets.Discover(Path.GetTempPath());
            using var form = new LegacyGameHostForm(
                assets,
                new LaunchSession(new Uri(
                    "https://lobr.creaction-network.com/client/Loading.swf")),
                new GameRuntimeOptions(Path.GetTempPath()),
                static (_, _) => { });
            return (form.FormBorderStyle, form.ShowInTaskbar, form.ControlBox);
        });

        Assert.Equal(FormBorderStyle.None, result.FormBorderStyle);
        Assert.False(result.ShowInTaskbar);
        Assert.False(result.ControlBox);
    }

    [Fact]
    public void DiagnosticForm_PreservesStandaloneWindowChrome()
    {
        var result = RunInSta(() =>
        {
            using var form = new LegacyGameHostForm(
                LegacyRuntimeAssets.Discover(Path.GetTempPath()));
            return (form.FormBorderStyle, form.ShowInTaskbar);
        });

        Assert.Equal(FormBorderStyle.Sizable, result.FormBorderStyle);
        Assert.True(result.ShowInTaskbar);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ParentExitCloseRequest_IsIdempotentBeforeOrAfterHandleCreation(
        bool createHandleBeforeRequest)
    {
        RunInSta(() =>
        {
            using var form = new LegacyGameHostForm(
                LegacyRuntimeAssets.Discover(Path.GetTempPath()));
            int uiThreadId = Environment.CurrentManagedThreadId;
            int? closingThreadId = null;
            int closingCount = 0;
            form.FormClosing += (_, _) =>
            {
                closingThreadId = Environment.CurrentManagedThreadId;
                closingCount++;
            };

            if (createHandleBeforeRequest)
            {
                _ = form.Handle;
            }

            var requestThread = new Thread(() =>
            {
                form.RequestCloseBecauseParentExited();
                form.RequestCloseBecauseParentExited();
            });
            requestThread.Start();
            requestThread.Join();

            if (!createHandleBeforeRequest)
            {
                _ = form.Handle;
            }

            PumpMessagesUntil(() => form.IsDisposed, TimeSpan.FromSeconds(2));

            Assert.True(form.IsDisposed);
            Assert.Equal(1, closingCount);
            Assert.Equal(uiThreadId, closingThreadId);
            return true;
        });
    }

    [Fact]
    public void WindowIdentity_AcceptsOnlyWindowOwnedByExpectedProcess()
    {
        RunInSta(() =>
        {
            using var window = new TestNativeWindow();
            GameHostWindowIdentity.EnsureOwnedByProcess(
                window.Handle,
                Environment.ProcessId);

            int differentProcessId = Environment.ProcessId == int.MaxValue
                ? Environment.ProcessId - 1
                : Environment.ProcessId + 1;
            Assert.Throws<InvalidOperationException>(() =>
                GameHostWindowIdentity.EnsureOwnedByProcess(
                    window.Handle,
                    differentProcessId));
            return true;
        });

        Assert.Throws<InvalidOperationException>(() =>
            GameHostWindowIdentity.EnsureOwnedByProcess(nint.Zero, Environment.ProcessId));
    }

    private static T RunInSta<T>(Func<T> action)
    {
        T? result = default;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }

        return result!;
    }

    private static void PumpMessagesUntil(Func<bool> condition, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (!condition() && DateTime.UtcNow < deadline)
        {
            Application.DoEvents();
            Thread.Sleep(5);
        }
    }

    private sealed class TestNativeWindow : NativeWindow, IDisposable
    {
        public TestNativeWindow()
        {
            CreateHandle(new CreateParams
            {
                Caption = "LegendLauncher.Tests.GameHostWindow",
                X = 0,
                Y = 0,
                Width = 8,
                Height = 8,
            });
        }

        public void Dispose()
        {
            DestroyHandle();
        }
    }
}
