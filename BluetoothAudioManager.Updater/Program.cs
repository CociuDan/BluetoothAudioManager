using BluetoothAudioManager.Shared.Helpers;
using BluetoothAudioManager.Updater.Services;
using BluetoothAudioManager.Updater.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Started with this argument from a staged copy, to replace the installed updater with a newer one.
if (args is [ServiceReplacer.Argument, var installFolder])
{
    ServiceReplacer.Run(installFolder);
    return;
}

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options => options.ServiceName = InstallLayout.ServiceName);
builder.Services.AddHostedService<UpdateWorker>();
builder.Build().Run();
