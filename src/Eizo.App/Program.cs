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
        ComWrappersSupport.InitializeComWrappers();

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

        mainInstance.Activated += (_, activationArguments) =>
            App.OnRedirectedActivation(activationArguments);

        Application.Start(callbackParameters =>
        {
            var synchronizationContext =
                new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(synchronizationContext);
            _ = new App();
        });
    }
}
