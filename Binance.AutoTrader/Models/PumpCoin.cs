using Binance;
using System;
using System.Collections.Generic;
using System.Text;

namespace Binance.AutoTrader.Models
{
    public class PumpCoin
    {
        public DateTime CurrentDate { get; set; }
        public SymbolStatistics SymbolStat { get; set; }
        public SymbolStatistics OldSymbolStat { get; set; }
        public decimal PriceChange { get; set; }
        public decimal VolumeChange { get; set; }
        public string PumpOrDump { get; set; }
    }
}
