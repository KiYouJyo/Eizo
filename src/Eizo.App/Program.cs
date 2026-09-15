using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using WinRT;

namespace Eizo;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        StartupTrace.Mark("Program.Main:begin");
        ComWrappersSupport.InitializeComWrappers();
        StartupTrace.Mark("ComWrappersSupport.InitializeComWrappers:end");

        var currentInstance = AppInstance.GetCurrent();
        var mainInstance = AppInstance.FindOrRegisterForKey(SingleInstanceActivation.InstanceKey);

        if (!mainInstance.IsCurrent)
        {
            var activationArguments = currentInstance.GetActivatedEventArgs();

            // Keep redirect synchronous before Application.Start, exactly as the
            // UrbanPlanToolbox implementation does. This avoids disturbing the
            // WinUI dispatcher/IME initialization of a secondary process.
            mainInstance
                .RedirectActivationToAsync(activationArguments)
                .AsTask()
                .GetAwaiter()
                .GetResult();
            return;
        }

        App.OnInitialActivation(currentInstance.GetActivatedEventArgs());

        mainInstance.Activated += (_, activationArguments) =>
            App.OnRedirectedActivation(activationArguments);

        StartupTrace.Mark("Application.Start:begin");
        Application.Start(callbackParameters =>
        {
            StartupTrace.Mark("Application.Start callback:begin");
            var synchronizationContext =
                new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(synchronizationContext);
            StartupTrace.Mark("App ctor:begin");
            _ = new App();
            StartupTrace.Mark("App ctor:end");
        });
    }
}
