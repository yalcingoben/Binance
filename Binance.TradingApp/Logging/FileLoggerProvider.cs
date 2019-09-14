using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Binance.TradingApp.Logging
{
    public sealed class FileLoggerProvider : ILoggerProvider
    {
        private readonly ILogger _fileLogger;

        public FileLoggerProvider(string filePath, LogLevel level)
        {
            _fileLogger = new FileLogger(filePath, level);
        }

        public ILogger CreateLogger(string categoryName)
        {
            return _fileLogger;
        }

        public void Dispose()
        { }
    }
}
