using System;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using Service.Models;
using SharpScheduler.Common;

namespace Service.Core
{
    public partial class ServiceKernel
    {
        private class WrapperProcess
        {
            private static readonly ObjectIDGenerator IdGenerator = new();
            private readonly CancellationTokenSource _cts;
            private readonly Process _process;

            public WrapperProcess(HandlerOptions options)
            {
                Port = Helper.GetFreeTcpPort();
                ID = IdGenerator.GetId(this, out _);
                _cts = new CancellationTokenSource();

                _process = new Process
                {
                    StartInfo = new ProcessStartInfo(_serviceOptions.DotnetPath)
                    {
                        Arguments = $"{_serviceOptions.WrapperPath} " +
                                    $"{nameof(WrapperProcessArgs.ServicePID)}={ServiceOptions.ServicePID} " +
                                    $"{nameof(WrapperProcessArgs.HandlerPort)}={Port} " + options,

                        CreateNoWindow = true
                    }
                };
            }

            public Task ProcessDeathWaiting { get; private set; }
            public int Port { get; }
            public long ID { get; }
            public bool IsRunning => !ProcessDeathWaiting.IsCompleted;

            public void Start()
            {
                _process.Start();
                ProcessDeathWaiting = Task.Run(async () =>
                {
                    await _process.WaitForExitAsync(_cts.Token);

                    Kill();

                    Log.Information("Handler[{0}] was stopped", ID);
                });
            }
            private void Kill(bool cancelDeathWaiting = false)
            {
                if (cancelDeathWaiting)
                {
                    _cts.Cancel();
                }

                try
                {
                    _process.Kill();
                }
                catch (Exception ex)
                {
                    Log.Error("Exception while trying to kill handler[{0}] process: {1}", ID, ex);
                }
                finally
                {
                    _process.Close();
                }
            }
        }
    }
}