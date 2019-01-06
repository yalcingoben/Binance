using Binance;
using System;
using System.Collections.Generic;
using System.Text;

namespace Binance.AutoTrader.Models
{
    public class PumpSettings
    {
        public int maxCacheSize { get; set; } =  60;
        public int basePriceSecond  { get; set; } =  10;
        public decimal pumpPriceChange  { get; set; } =  3;
        public decimal dumpPriceChange  { get; set; } =  -3;
        public decimal minVolume  { get; set; } =  80;
        public decimal maxVolume  { get; set; } =  1000;
        public decimal volChangeCheck  { get; set; } =  2;
        public decimal userBtcBalance  { get; set; } =  10;
        public decimal useBalancePercent { get; set; } = 0;
        public decimal addPercentToCurrentPrice  { get; set; } =  5;
        public decimal addPercentForProfit  { get; set; } =  10;
        public IBinanceApi Api { get; set; }
        public IBinanceApiUser User { get; set; }
        public bool IsOrdersTestOnly { get; set; } = true;
        public IEnumerable<Symbol> SymbolRestriction { get; set; }
    }
}
