using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Binance.AutoTrader.Facades
{
    public static class fcdCache
    {
        public static int maxCacheSize { get; set; } = 10; 
        public static object lockedObject = new object();
        public static ArrayList symbolPrices;
        public static List<SymbolStatistics> SymbolPrices(int index)
        {
            lock (lockedObject)
            {
                return symbolPrices[index] as List<SymbolStatistics>;
            }
        }
        public static void LoadCacheSettings(int _maxCacheSize)
        {
            maxCacheSize = _maxCacheSize;
            symbolPrices = new ArrayList(_maxCacheSize);
        }

        public static void PriceCache(IEnumerable<SymbolStatistics> symbolStat)
        {
            lock (lockedObject)
            {
                if (symbolPrices.Count == maxCacheSize)
                    symbolPrices.RemoveAt(0);
                symbolPrices.Add(symbolStat.Where(p => p.Symbol.EndsWith("BTC")).ToList());
            }
        }

        public static void Write(SymbolStatistics stats, decimal artis, string pumpOrDump)
        {
            using (StreamWriter f = new StreamWriter(AppContext.BaseDirectory + "\\Pump.txt", true))
            {
                f.WriteLineAsync($"{DateTime.Now} {stats.Symbol} %: {stats.PriceChangePercent:0.00} | O: {stats.OpenPrice:0.00000000} | H: {stats.HighPrice:0.00000000} | L: {stats.LowPrice:0.00000000} | V: {stats.QuoteVolume:0.}"
                    + $"    Bid: {stats.BidPrice:0.00000000} | Last: {stats.LastPrice:0.00000000} | Ask: {stats.AskPrice:0.00000000} | Avg: {stats.WeightedAveragePrice:0.00000000} | Change : {artis} | {pumpOrDump}");
            }
        }
    }
}
