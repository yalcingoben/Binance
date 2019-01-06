using Binance.Application;
using Binance.WebSocket;
using Binance.Cache;
using Binance;
using Binance.AutoTrader.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Binance.Utility;
using System.Linq;
using System.Diagnostics;
using Binance.AutoTrader.Services;
using System.Text;

namespace Binance.AutoTrader
{
    class Program
    {
        public static bool sendTelegramMessages = false;
        public static object lockedObject = new object();
        public static object lockedAnalyseObject = new object();
        public static ArrayList symbolPrices;
        public static int maxCacheSize = 60;
        public static int basePriceSecond = 10;
        public static decimal pumpPriceChange = 3;
        public static decimal dumpPriceChange = -3;
        public static decimal minVolume = 80;
        public static decimal maxVolume = 1000;
        public static decimal volChangeCheck = 2;
        public static decimal userBtcBalance = 10;
        public static decimal addPercentToCurrentPrice = 5;
        public static decimal addPercentForProfit = 10;
        public static string strategy = "QuickPump";
        public static DateTime pumpStartDate = DateTime.Now;
        public static DateTime pumpEndDate = DateTime.Now.AddHours(5);
        public static List<string> discardList = new List<string>();
        public static Dictionary<string, PumpCoin> pumpDict = new Dictionary<string, PumpCoin>();
        public static Dictionary<string, PumpCoin> dumpDict = new Dictionary<string, PumpCoin>();
        public static Dictionary<string, PumpCoin> atlDict = new Dictionary<string, PumpCoin>();

        public static IConfigurationRoot Configuration;

        public static IServiceProvider ServiceProvider;

        public static IBinanceApi Api;
        public static IBinanceApiUser User;
        public static readonly object ConsoleSync = new object();

        public static bool IsOrdersTestOnly = true;
        public static IEnumerable<Symbol> SymbolRestriction;

        public static List<SymbolStatistics> SymbolPrices(int index)
        {
            lock (lockedObject)
            {
                return symbolPrices[index] as List<SymbolStatistics>;
            }
        }
        public static async Task Main(string[] args)
        {
            var cts = new CancellationTokenSource();

            try
            {
                Configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", true, false)
                    .Build();

                // Configure services.
                ServiceProvider = new ServiceCollection()
                    // ReSharper disable once ArgumentsStyleLiteral
                    .AddBinance(useSingleCombinedStream: true) // add default Binance services.

                    // Use alternative, low-level, web socket client implementation.
                    //.AddTransient<IWebSocketClient, WebSocket4NetClient>()
                    //.AddTransient<IWebSocketClient, WebSocketSharpClient>()

                    .AddOptions()
                    .Configure<BinanceApiOptions>(Configuration.GetSection("ApiOptions"))

                    .AddLogging(builder => builder // configure logging.
                        .SetMinimumLevel(LogLevel.Trace)
                        .AddFile(Configuration.GetSection("Logging:File")))

                    // Use alternative, low-level, web socket client implementation.
                    //.AddTransient<IWebSocketClient, WebSocket4NetClient>()
                    //.AddTransient<IWebSocketClient, WebSocketSharpClient>()

                    .BuildServiceProvider();
                            

                var apiKey = Configuration.GetSection("User:ApiKey").Get<string>();
                var apiSecret = Configuration.GetSection("User:ApiSecret").Get<string>();

                if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiSecret))
                {
                    Console.WriteLine("Api Key Not Found.");
                }

                if (!string.IsNullOrEmpty(apiKey))
                {
                    User = ServiceProvider
                        .GetService<IBinanceApiUserProvider>()
                        .CreateUser(apiKey, apiSecret);
                }

                Api = ServiceProvider.GetService<IBinanceApi>();

                pumpPriceChange = Configuration.GetSection("Settings:PumpPercentCheck").Get<decimal>();
                dumpPriceChange = Configuration.GetSection("Settings:DumpPercentCheck").Get<decimal>();
                minVolume = Configuration.GetSection("Settings:MinVolume").Get<decimal>();
                maxVolume = Configuration.GetSection("Settings:MaxVolume").Get<decimal>();
                maxCacheSize = Configuration.GetSection("Settings:MaxCacheSize").Get<int>();
                basePriceSecond = Configuration.GetSection("Settings:BasePriceSecond").Get<int>();
                volChangeCheck = Configuration.GetSection("Settings:VolChangeCheck").Get<decimal>();
                IsOrdersTestOnly = Configuration.GetSection("Settings:IsTest").Get<bool>();
                addPercentToCurrentPrice = Configuration.GetSection("Settings:AddPercentToCurrentPrice").Get<decimal>();
                addPercentForProfit = Configuration.GetSection("Settings:AddPercentForProfit").Get<decimal>();
                var useBalancePercent = Configuration.GetSection("Settings:UseBalancePercent").Get<decimal>();
                strategy = Configuration.GetSection("Settings:Strategy").Get<string>();
                var discards = Configuration.GetSection("Settings:Discards").Get<string>();
                if (!string.IsNullOrEmpty(discards))
                    discardList = discards.Split(",").ToList<string>();
                pumpStartDate = Configuration.GetSection("Settings:PumpStartDate").Get<DateTime>();
                pumpEndDate = Configuration.GetSection("Settings:PumpEndDate").Get<DateTime>();
                sendTelegramMessages = Configuration.GetSection("Settings:SendTelegramMessages").Get<bool>();

                if (!string.IsNullOrEmpty(apiKey))
                {
                    SymbolRestriction = await Api.GetSymbolsAsync(cts.Token);

                    var account = await Api.GetAccountInfoAsync(User, token: cts.Token);
                    var btcVal = account.Balances.FirstOrDefault(p => p.Asset == "BTC").Free;
                    userBtcBalance = Math.Round((btcVal * useBalancePercent / 100), 8);

                    Console.WriteLine($"Available BTC Balance: {btcVal}");
                    Console.WriteLine($"Use For Pump(%{useBalancePercent}): {userBtcBalance}");
                }
                symbolPrices = new ArrayList(maxCacheSize);

                var cache = ServiceProvider.GetService<ISymbolStatisticsWebSocketCache>();
                // Add error event handler.
                cache.Error += (s, e) => Console.WriteLine(e.Exception.Message);

                try
                {
                    //lock (Program.ConsoleSync)
                    //    Console.WriteLine("  Canceling all open orders...");

                    //await Program.Api.CancelAllOrdersAsync(Program.User, null, token: cts.Token);

                    //lock (Program.ConsoleSync)
                    //{
                    //    Console.WriteLine("  Done (all open orders canceled).");
                    //    Console.WriteLine();
                    //}

                    var api = ServiceProvider.GetService<IBinanceApi>();
                    Console.WriteLine("Tarama başlatılıyor...");
                    // Query and display the 24-hour statistics.
                    PriceCache(await api.Get24HourStatisticsAsync());

                    //var order = await CreateBuyOrder(buyOrder);

                    // Subscribe cache to symbols (automatically begin streaming).
                    cache.Subscribe(Display);
                    Console.WriteLine("Tarama başlatıldı...");

                    Console.ReadKey(true); // wait for user input.
                }
                finally
                {
                    // Unsubscribe cache (automatically end streaming).
                    cache.Unsubscribe();
                }

                //using (var controller = new RetryTaskController())
                //{
                //    var api = ServiceProvider.GetService<IBinanceApi>();
                //    Console.WriteLine("Tarama başlatılıyor...");
                //    // Query and display the 24-hour statistics.
                //    PriceCache(await api.Get24HourStatisticsAsync());
                //    // Monitor 24-hour statistics and display updates in real-time.
                //    controller.Begin(
                //        tkn => cache.SubscribeAsync(evt => Analyse(evt.Statistics, cts), tkn),
                //        err => Console.WriteLine(err.Message));
                //    Console.WriteLine("Tarama başlatıldı...");
                //    Console.ReadKey(true);
                //}

            }
            catch (Exception e)
            {
                lock (ConsoleSync)
                {
                    Console.WriteLine($"! FAIL: \"{e.Message}\"");
                    if (e.InnerException != null)
                    {
                        Console.WriteLine($"  -> Exception: \"{e.InnerException.Message}\"");
                    }
                }
            }
            finally
            {
                cts.Cancel();
                cts.Dispose();

                User?.Dispose();

                lock (ConsoleSync)
                {
                    Console.WriteLine();
                    Console.WriteLine("  ...press any key to close window.");
                    Console.ReadKey(true);
                }
            }
        }

        // ReSharper disable once InconsistentNaming
        private static readonly object _sync = new object();

        private static Task _displayTask = Task.CompletedTask;
        private static void Display(SymbolStatisticsCacheEventArgs args)
        {
            lock (_sync)
            {
                if (_displayTask.IsCompleted)
                {
                    // Delay to allow multiple data updates between display updates.
                    _displayTask = Task.Delay(250)
                        .ContinueWith(_ =>
                        {
                            var latestStatistics = args.Statistics;

                            Analyse(latestStatistics);
                            //Console.SetCursorPosition(0, 0);

                            // Display top 5 symbols with highest % price change.
                            //foreach (var stats in latestStatistics.OrderBy(s => s.PriceChangePercent).Reverse().Take(5))
                            //{
                            //    Console.WriteLine($"  24-hour statistics for {stats.Symbol}:".PadRight(119));
                            //    Console.WriteLine($"    %: {stats.PriceChangePercent:0.00} | O: {stats.OpenPrice:0.00000000} | H: {stats.HighPrice:0.00000000} | L: {stats.LowPrice:0.00000000} | V: {stats.Volume:0.}".PadRight(119));
                            //    Console.WriteLine($"    Bid: {stats.BidPrice:0.00000000} | Last: {stats.LastPrice:0.00000000} | Ask: {stats.AskPrice:0.00000000} | Avg: {stats.WeightedAveragePrice:0.00000000}".PadRight(119));
                            //    Console.WriteLine();
                            //}

                            //Console.WriteLine(_message.PadRight(119));
                        });
                }
            }
        }
        private static void Display()
        {
            Console.SetCursorPosition(0, 10);
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
        private static void ClearPumpCoins()
        {
            int addMinute = -10;
            if (pumpDict.Count > 0)
            {
                if (strategy == "BigPump")
                    addMinute = -20;

                var dict = new Dictionary<string, PumpCoin>();
                foreach (var item in pumpDict)
                {
                    if (item.Value.CurrentDate > DateTime.Now.AddMinutes(addMinute))
                    {
                        dict.Add(item.Key, item.Value);
                    }
                }
                pumpDict = dict;
            }
            if (atlDict.Count > 0)
            {
                if (strategy == "BigPump")
                    addMinute = -20;

                var dict = new Dictionary<string, PumpCoin>();
                foreach (var item in atlDict)
                {
                    if (item.Value.CurrentDate > DateTime.Now.AddMinutes(addMinute))
                    {
                        dict.Add(item.Key, item.Value);
                    }
                }
                atlDict = dict;
            }
        }
        private static void Display(PumpCoin item)
        {
            try
            {
                string format = " {0, -12} {1, -8} | Chg: % {2:0.00} | OldPrice: {3:0.00000000} | LastPrice: {4:0.00000000} | OldVol: {5} | LastVol: {6} | {7} | Chg24h: % {8:0.00}";
                Console.WriteLine(string.Format(format, item.CurrentDate, item.SymbolStat.Symbol, item.PriceChange,
                    item.OldSymbolStat.LastPrice, item.SymbolStat.LastPrice, item.OldSymbolStat.QuoteVolume, item.SymbolStat.QuoteVolume, item.PumpOrDump, item.SymbolStat.PriceChangePercent));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        private static async Task Write(SymbolStatistics stats, decimal artis, string pumpOrDump)
        {
            using (StreamWriter f = new StreamWriter(AppContext.BaseDirectory + "\\Pump.txt", true))
            {
                await f.WriteLineAsync($"{DateTime.Now} {stats.Symbol} %: {stats.PriceChangePercent:0.00} | O: {stats.OpenPrice:0.00000000} | H: {stats.HighPrice:0.00000000} | L: {stats.LowPrice:0.00000000} | V: {stats.QuoteVolume:0.}"
                    + $"    Bid: {stats.BidPrice:0.00000000} | Last: {stats.LastPrice:0.00000000} | Ask: {stats.AskPrice:0.00000000} | Avg: {stats.WeightedAveragePrice:0.00000000} | Change : {artis} | {pumpOrDump}");
            }
        }

        public static async void Analyse(IEnumerable<SymbolStatistics> symbolStat)
        {
            // 1 dakika geçtiyse Cache fiyatlarını güncelle
            var task1 = Task.Run(() => PriceCache(symbolStat));
            Task task2 = null;
            if (strategy == "QuickPump")
                task2 = Task.Run(() => PumpStrategy(symbolStat));
            else if (strategy == "BigPump")
                task2 = Task.Run(() => BigPumpStrategy(symbolStat));
            await Task.WhenAll(task1, task2);
        }

        public static void PumpStrategy(IEnumerable<SymbolStatistics> symbolStat)
        {
            lock (lockedAnalyseObject)
            {
                var statistics = new List<SymbolStatistics>();
                var index = symbolPrices.Count > basePriceSecond ? basePriceSecond : symbolPrices.Count - 1;

                var priceCache = SymbolPrices(index).Where(p => p.QuoteVolume > minVolume && p.QuoteVolume < maxVolume && !discardList.Contains(p.Symbol));
                foreach (var priceC in priceCache)
                {
                    var newPriceSymbol = symbolStat.FirstOrDefault(p => p.Symbol == priceC.Symbol);
                    decimal currentVolChange = newPriceSymbol.QuoteVolume - priceC.QuoteVolume;
                    decimal priceChange = ((newPriceSymbol.LastPrice - priceC.LastPrice) / priceC.LastPrice) * 100;
                    if (volChangeCheck <= currentVolChange && priceChange >= pumpPriceChange)
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
                            if (User != null && userBtcBalance >= 0.001m)
                            {
                                var result = Task.Run(() => CreateOrderAsync(pumpCoin));
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
                            PlaySound(AppContext.BaseDirectory + "\\Alert\\DoorBell.wav");
                            if (sendTelegramMessages)
                                TelegramMessageService.SendMessage(string.Format("PUMP - {0} - FPrice: {1} - LPrice: {2} - Ch: {3}", newPriceSymbol.Symbol, priceC.LastPrice, newPriceSymbol.LastPrice, CalculateChange(priceC.LastPrice, newPriceSymbol.LastPrice)));
                        }
                        var write = Task.Run(() => Write(newPriceSymbol, Math.Round(priceChange, 2), "PUMP"));
                    }
                    if (priceChange < dumpPriceChange)
                    {
                        if (pumpDict.ContainsKey(priceC.Symbol))
                        {
                            pumpDict[priceC.Symbol].CurrentDate = DateTime.Now;
                            pumpDict[priceC.Symbol].PriceChange = pumpPriceChange;
                            pumpDict[priceC.Symbol].VolumeChange = currentVolChange;
                            pumpDict[priceC.Symbol].SymbolStat = newPriceSymbol;
                            pumpDict[priceC.Symbol].PumpOrDump = "DUMP";
                        }
                        else
                        {
                            var pumpCoin = new PumpCoin
                            {
                                CurrentDate = DateTime.Now,
                                PriceChange = pumpPriceChange,
                                PumpOrDump = "DUMP",
                                SymbolStat = newPriceSymbol,
                                VolumeChange = currentVolChange
                            };
                            pumpDict.Add(priceC.Symbol, pumpCoin);
                            Display(pumpCoin);
                            PlaySound(AppContext.BaseDirectory + "\\Alert\\DoorBell.wav");
                            if (sendTelegramMessages)
                                TelegramMessageService.SendMessage(string.Format("DUMP - {0} - FPrice: {1} - LPrice: {2} - Ch: {3}", newPriceSymbol.Symbol, priceC.LastPrice, newPriceSymbol.LastPrice, CalculateChange(priceC.LastPrice, newPriceSymbol.LastPrice)));
                        }
                        var writeDump = Task.Run(() => Write(newPriceSymbol, Math.Round(priceChange, 2), "DUMP"));
                    }
                    if (priceC.LowPrice > newPriceSymbol.LowPrice && CalculateChange(priceC.LowPrice, newPriceSymbol.LowPrice) > 2)
                    {
                        if (atlDict.ContainsKey(priceC.Symbol))
                        {
                            atlDict[priceC.Symbol].CurrentDate = DateTime.Now;
                            atlDict[priceC.Symbol].PriceChange = pumpPriceChange;
                            atlDict[priceC.Symbol].VolumeChange = currentVolChange;
                            atlDict[priceC.Symbol].SymbolStat = newPriceSymbol;
                            atlDict[priceC.Symbol].PumpOrDump = "ATL";
                        }
                        else
                        {
                            var pumpCoin = new PumpCoin
                            {
                                CurrentDate = DateTime.Now,
                                PriceChange = pumpPriceChange,
                                PumpOrDump = "ATL",
                                SymbolStat = newPriceSymbol,
                                VolumeChange = currentVolChange
                            };
                            atlDict.Add(priceC.Symbol, pumpCoin);
                            Display(pumpCoin);
                            PlaySound(AppContext.BaseDirectory + "\\Alert\\DoorBell.wav");
                            if (sendTelegramMessages)
                                TelegramMessageService.SendMessage(string.Format("ATL - {0} - FPrice: {1} - LPrice: {2} - Ch: {3}", newPriceSymbol.Symbol, priceC.LowPrice, newPriceSymbol.LowPrice, CalculateChange(priceC.LowPrice, newPriceSymbol.LowPrice)));
                        }
                        var writeDump = Task.Run(() => Write(newPriceSymbol, Math.Round(priceChange, 2), "ATL"));
                    }
                }
                ClearPumpCoins();
            }
        }

        public static void BigPumpStrategy(IEnumerable<SymbolStatistics> symbolStat)
        {
            lock (lockedAnalyseObject)
            {
                var statistics = new List<SymbolStatistics>();
                var index = symbolPrices.Count > basePriceSecond ? basePriceSecond : symbolPrices.Count - 1;

                var priceCache = SymbolPrices(0).Where(p => p.QuoteVolume > minVolume && p.QuoteVolume < maxVolume && !discardList.Contains(p.Symbol));
                foreach (var priceC in priceCache)
                {
                    var newPriceSymbol = symbolStat.FirstOrDefault(p => p.Symbol == priceC.Symbol);
                    decimal currentVolChange = newPriceSymbol.QuoteVolume - priceC.QuoteVolume;
                    decimal priceChange = ((newPriceSymbol.LastPrice - priceC.LastPrice) / priceC.LastPrice) * 100;
                    if (volChangeCheck <= currentVolChange && priceC.PriceChangePercent < 10 && newPriceSymbol.PriceChangePercent > 10 && newPriceSymbol.PriceChangePercent < 30)
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
                            if (User != null && userBtcBalance >= 0.001m)
                            {
                                if (DateTime.Now >= pumpStartDate && DateTime.Now <= pumpEndDate)
                                {
                                    var result = Task.Run(() => CreateOrderAsync(pumpCoin));
                                    Display(pumpCoin);
                                }
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
                            PlaySound(AppContext.BaseDirectory + "\\Alert\\DoorBell.wav");
                        }
                        var write = Task.Run(() => Write(newPriceSymbol, Math.Round(priceChange, 2), "PUMP"));
                    }
                }
            }
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

        public static async Task CreateOrderAsync(PumpCoin coin)
        {
            Symbol smb = SymbolRestriction.FirstOrDefault(p => (p.BaseAsset.Symbol + p.QuoteAsset.Symbol) == coin.SymbolStat.Symbol);
            var buyPrice = Math.Round((coin.SymbolStat.AskPrice * (1 + addPercentToCurrentPrice / 100)), smb.BaseAsset.Precision);
            var kalan = (buyPrice - smb.Price.Minimum) % smb.Price.Increment;
            buyPrice = buyPrice - kalan;
            var quantity = Math.Round((userBtcBalance / buyPrice), smb.BaseAsset.Precision);
            var adet = (quantity - smb.Quantity.Minimum) % smb.Quantity.Increment;
            quantity = quantity - adet;
            quantity = smb.Quantity.GetLowerValidValue(quantity);

            var buyOrder = new LimitOrder(Program.User)
            {
                Symbol = coin.SymbolStat.Symbol,
                Side = OrderSide.Buy,
                Quantity = quantity,
                Price = buyPrice
            };
            var order = await CreateBuyOrder(buyOrder);
            if (order != null)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine(string.Format("BUY -  {0} - MaxPrice: {1} - Quantity: {2}", coin.SymbolStat.Symbol, order.Fills.Max(p => p.Price), order.Fills.Sum(p => p.Quantity)));
                var sellPrice = Math.Round((order.Fills.Max(p => p.Price) * (1 + addPercentForProfit / 100)), smb.BaseAsset.Precision);
                kalan = (sellPrice - smb.Price.Minimum) % smb.Price.Increment;
                sellPrice = sellPrice - kalan;
                var sellOrder = new LimitOrder(Program.User)
                {
                    Symbol = coin.SymbolStat.Symbol,
                    Side = OrderSide.Sell,
                    Quantity = quantity,
                    Price = sellPrice
                };
                var sellResponse = await CreateSellOrder(sellOrder);
                if (sellResponse != null)
                {
                    sb.AppendLine(string.Format("SELL - {0} - MinPrice: {1} - Quantity: {2}", coin.SymbolStat.Symbol, sellResponse.Fills.Min(p => p.Price), sellResponse.Fills.Sum(p => p.Quantity)));
                }
                TelegramMessageService.SendMessage(sb.ToString());
            }
        }

        public static async Task<Order> CreateBuyOrder(ClientOrder clientOrder, CancellationToken token = default)
        {
            Order order = null;
            if (Program.IsOrdersTestOnly)
            {
                await Program.Api.TestPlaceAsync(clientOrder, token: token);
                var limitOrder = clientOrder as LimitOrder;
                lock (Program.ConsoleSync)
                {
                    Console.WriteLine($"\t ~ TEST ~ >> LIMIT {limitOrder.Side} order (ID: {limitOrder.Id}) placed for {limitOrder.Quantity:0.00000000} {limitOrder.Symbol} @ {limitOrder.Price:0.00000000}");
                    PlaySound(AppContext.BaseDirectory + "\\Alert\\DoorBell.wav");
                }
            }
            else
            {
                order = await Program.Api.PlaceAsync(clientOrder, token: token);

                // ReSharper disable once InvertIf
                if (order != null)
                {
                    lock (Program.ConsoleSync)
                    {
                        Console.WriteLine($">> LIMIT {order.Side} order (ID: {order.Id}) placed for {order.OriginalQuantity:0.00000000} {order.Symbol} @ {order.Price:0.00000000}");

                        foreach (var fill in order.Fills)
                        {
                            Console.WriteLine($"   {fill.Quantity:0.00000000} @ {fill.Price:0.00000000}  fee: {fill.Commission:0.00000000} {fill.CommissionAsset}  [Trade ID: {fill.TradeId}]");
                        }
                        PlaySound(AppContext.BaseDirectory + "\\Alert\\DoorBell.wav");
                    }
                }
            }
            return order;
        }
        public static async Task<Order> CreateSellOrder(LimitOrder clientOrder, CancellationToken token = default)
        {
            Order order = null;
            if (Program.IsOrdersTestOnly)
            {
                await Program.Api.TestPlaceAsync(clientOrder, token: token);

                lock (Program.ConsoleSync)
                {
                    Console.WriteLine($"\t ~ TEST ~ >> LIMIT {clientOrder.Side} order (ID: {clientOrder.Id}) placed for {clientOrder.Quantity:0.00000000} {clientOrder.Symbol} @ {clientOrder.Price:0.00000000}");
                }
            }
            else
            {
                order = await Program.Api.PlaceAsync(clientOrder, token: token);

                // ReSharper disable once InvertIf
                if (order != null)
                {
                    lock (Program.ConsoleSync)
                    {
                        Console.WriteLine($"\t >> LIMIT {order.Side} order (ID: {order.Id}) placed for {order.OriginalQuantity:0.00000000} {order.Symbol} @ {order.Price:0.00000000}");
                        foreach (var fill in order.Fills)
                        {
                            Console.WriteLine($"   {fill.Quantity:0.00000000} @ {fill.Price:0.00000000}  fee: {fill.Commission:0.00000000} {fill.CommissionAsset}  [Trade ID: {fill.TradeId}]");
                        }
                    }
                }
            }
            return order;
        }
        public static void PlaySound(string file)
        {
            Process.Start(@"powershell", $@"-c (New-Object Media.SoundPlayer '{file}').PlaySync();");
        }

        public static decimal CalculateChange(decimal firstPrice, decimal lastPrice)
        {
            return Math.Round(((lastPrice - firstPrice) / firstPrice) * 100, 2);
        }

    }
}
