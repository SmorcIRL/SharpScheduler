using System;
using System.Threading.Tasks;
using SharpScheduler.Common;
using WrapperApplication.Models;
using WrapperApplication.Scheduler;

namespace WrapperApplication.Core
{
    public partial class WrapperKernel : Kernel
    {
        private readonly Handler _handler;
        private readonly WrapperProcessArgs _processArgs;
        private readonly IScheduler _scheduler;
        private HandlerOptions _handlerOptions;

        private IDisposable _schedulerSub;
        private Task _waitForServiceDeath;

        public WrapperKernel(IScheduler scheduler, WrapperProcessArgs processArgs)
        {
            _handler = new Handler();

            _scheduler = scheduler;
            _processArgs = processArgs;
        }
    }
}