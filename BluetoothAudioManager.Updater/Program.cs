using BluetoothAudioManager.Shared.Helpers;
using BluetoothAudioManager.Updater.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options => options.ServiceName = InstallLayout.ServiceName);
builder.Services.AddHostedService<UpdateWorker>();
builder.Build().Run();
