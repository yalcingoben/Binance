using Binance;
using Binance.AutoTrader.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Binance.AutoTrader.Facades
{
    public static class fcdAnalyse
    {
        public static object lockedAnalyseObject = new object();
        public static Dictionary<string, PumpCoin> pumpDict = new Dictionary<string, PumpCoin>();

        public static async void Analyse(IEnumerable<SymbolStatistics> symbolStat, PumpSettings pumpSettings)
        {            
            try
            {
                // 1 dakika geçtiyse Cache fiyatlarını güncelle
                var task1 = Task.Run(() => fcdCache.PriceCache(symbolStat));
                var task2 = Task.Run(() => fcdAnalyse.FindPumpCoins(symbolStat, pumpSettings));
                await Task.WhenAll(task1, task2);
            }
            catch (Exception)
            {
             
            }            
        }

        public static void FindPumpCoins(IEnumerable<SymbolStatistics> symbolStat, PumpSettings pumpSettings)
        {
            lock (lockedAnalyseObject)
            {                
                try
                {
                    var statistics = new List<SymbolStatistics>();
                    var index = fcdCache.symbolPrices.Count > pumpSettings.basePriceSecond ? pumpSettings.basePriceSecond : fcdCache.symbolPrices.Count - 1;

                    var priceCache = fcdCache.SymbolPrices(index).Where(p => p.QuoteVolume > pumpSettings.minVolume && p.QuoteVolume < pumpSettings.maxVolume);
                    foreach (var priceC in priceCache)
                    {
                        var newPriceSymbol = symbolStat.FirstOrDefault(p => p.Symbol == priceC.Symbol);
                        decimal currentVolChange = newPriceSymbol.QuoteVolume - priceC.QuoteVolume;
                        decimal priceChange = ((newPriceSymbol.LastPrice - priceC.LastPrice) / priceC.LastPrice) * 100;
                        if (pumpSettings.volChangeCheck <= currentVolChange && priceChange >= pumpSettings.pumpPriceChange)
                        {
                            if (pumpDict.ContainsKey(priceC.Symbol))
                            {
                                pumpDict[priceC.Symbol].CurrentDate = DateTime.Now;
                                pumpDict[priceC.Symbol].PriceChange = priceChange;
                                pumpDict[priceC.Symbol].VolumeChange = currentVolChange;
                                pumpDict[priceC.Symbol].SymbolStat = newPriceSymbol;
                                pumpDict[priceC.Symbol].PumpOrDump = "PUMP";
                            }
                            else
                            {
                                var pumpCoin = new PumpCoin
                                {
                                    CurrentDate = DateTime.Now,
                                    PriceChange = priceChange,
                                    PumpOrDump = "PUMP",
                                    SymbolStat = newPriceSymbol,
                                    OldSymbolStat = priceC,
                                    VolumeChange = currentVolChange
                                };
                                if (pumpSettings.User != null)
                                {
                                    var result = Task.Run(() => fcdOrder.CreateOrder(pumpSettings, pumpCoin));
                                }
                                pumpDict.Add(priceC.Symbol, new PumpCoin
                                {
                                    CurrentDate = DateTime.Now,
                                    PriceChange = priceChange,
                                    PumpOrDump = "PUMP",
                                    SymbolStat = newPriceSymbol,
                                    VolumeChange = currentVolChange
                                });
                                Display(pumpCoin);
                            }
                            fcdCache.Write(newPriceSymbol, Math.Round(priceChange, 2), "PUMP");
                        }
                        if (priceChange < pumpSettings.dumpPriceChange)
                        {
                            if (pumpDict.ContainsKey(priceC.Symbol))
                            {
                                pumpDict[priceC.Symbol].CurrentDate = DateTime.Now;
                                pumpDict[priceC.Symbol].PriceChange = priceChange;
                                pumpDict[priceC.Symbol].VolumeChange = currentVolChange;
                                pumpDict[priceC.Symbol].SymbolStat = newPriceSymbol;
                                pumpDict[priceC.Symbol].PumpOrDump = "DUMP";
                            }
                            else
                            {
                                pumpDict.Add(priceC.Symbol, new PumpCoin
                                {
                                    CurrentDate = DateTime.Now,
                                    PriceChange = priceChange,
                                    PumpOrDump = "DUMP",
                                    SymbolStat = newPriceSymbol,
                                    VolumeChange = currentVolChange
                                });
                            }
                            fcdCache.Write(newPriceSymbol, Math.Round(priceChange, 2), "DUMP");
                        }
                    }
                    ClearPumpCoins();
                }
                catch (Exception)
                {
                    
                }
            }
        }
        private static void ClearPumpCoins()
        {
            if (pumpDict.Count > 0)
            {
                var dict = new Dictionary<string, PumpCoin>();
                foreach (var item in pumpDict)
                {
                    if (item.Value.CurrentDate > DateTime.Now.AddMinutes(-10))
                    {
                        dict.Add(item.Key, item.Value);
                    }
                }
                pumpDict = dict;
            }
        }
        private static void Display()
        {
            if (pumpDict.Count > 0)
                foreach (KeyValuePair<string, PumpCoin> item in pumpDict.OrderByDescending(p => p.Value.CurrentDate))
                {
                    string format = "{0, -12} {1, -8} | Chg: % {9:0.00} | Last: {7:0.00000000} % {2:00.00} | H: {3:0.00000000} | L: {4:0.00000000} | V: {5:0.} | B: {6:0.00000000} | A: {8:0.00000000} | {10}";
                    Console.WriteLine(string.Format(format, item.Value.CurrentDate, item.Key,
                        item.Value.SymbolStat.PriceChangePercent, item.Value.SymbolStat.HighPrice, item.Value.SymbolStat.LowPrice,
                        item.Value.SymbolStat.QuoteVolume, item.Value.SymbolStat.BidPrice, item.Value.SymbolStat.LastPrice,
                        item.Value.SymbolStat.AskPrice, item.Value.PriceChange, item.Value.PumpOrDump));
                }
        }
        private static void Display(PumpCoin item)
        {
            string format = " {0, -12} {1, -8} | Chg: % {2:0.00} | OldPrice: {3:0.00000000} | LastPrice: {4:0.00000000} | OldVol: {5} | LastVol: {6} | {7}";
            Console.WriteLine(string.Format(format, item.CurrentDate, item.SymbolStat.Symbol, item.PriceChange,
                item.OldSymbolStat.LastPrice, item.SymbolStat.LastPrice, item.OldSymbolStat.QuoteVolume, item.SymbolStat.QuoteVolume, item.PumpOrDump));
        }
    }
}
