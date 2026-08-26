using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WorkerTemplate.Models;

namespace WorkerTemplate.Interfaces
{
    public interface ITxnService
    {
        Task<RabbitmqHandlerResult> ProcessLPPMessageAsync(TxnLPPMsg payload, int retryCount);
    }
}
