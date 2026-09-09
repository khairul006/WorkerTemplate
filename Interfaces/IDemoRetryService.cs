using System;
using System.Collections.Generic;
using System.Text;
using WorkerTemplate.Models;

namespace WorkerTemplate.Interfaces;

public interface IDemoRetryService
{
    Task<RabbitmqHandlerResult> ProcessMessageWithRetryAsync(string payload, int retryCount);
}
