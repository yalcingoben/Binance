using Binance.AutoTrader.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Binance.AutoTrader.Facades
{
    public class fcdOrder
    {
        public static void CreateOrder(PumpSettings pumpSettings, PumpCoin coin)
        {
            Symbol smb = pumpSettings.SymbolRestriction.FirstOrDefault(p => (p.BaseAsset.Symbol + p.QuoteAsset.Symbol) == coin.SymbolStat.Symbol);
            var buyPrice = Math.Round((coin.OldSymbolStat.AskPrice * (1 + pumpSettings.addPercentToCurrentPrice / 100)), smb.BaseAsset.Precision);
            var kalan = (buyPrice - smb.Price.Minimum) % smb.Price.Increment;
            buyPrice = buyPrice - kalan;
            var quantity = Math.Round((pumpSettings.userBtcBalance / buyPrice), smb.BaseAsset.Precision);
            var adet = (quantity - smb.Quantity.Minimum) % smb.Quantity.Increment;
            quantity = quantity - adet;

            var buyOrder = new LimitOrder(pumpSettings.User)
            {
                Symbol = coin.SymbolStat.Symbol,
                Side = OrderSide.Buy,
                Quantity = quantity,
                Price = buyPrice
            };
            var sellPrice = Math.Round((buyPrice * (1 + pumpSettings.addPercentForProfit / 100)), smb.BaseAsset.Precision);
            kalan = (sellPrice - smb.Price.Minimum) % smb.Price.Increment;
            sellPrice = sellPrice - kalan;
            var sellOrder = new LimitOrder(pumpSettings.User)
            {
                Symbol = coin.SymbolStat.Symbol,
                Side = OrderSide.Sell,
                Quantity = quantity,
                Price = sellPrice
            };

            Task.Run(() => fcdOrder.CreateOrder(pumpSettings, buyOrder))
                .ContinueWith((prevTask) => fcdOrder.CreateOrder(pumpSettings, sellOrder));
        }

        public static async Task<bool> CreateOrder(PumpSettings pumpSettings, LimitOrder clientOrder)
        {
            var cts = new CancellationTokenSource();
            try
            {
                if (pumpSettings.IsOrdersTestOnly)
                {
                    await pumpSettings.Api.TestPlaceAsync(clientOrder, token: cts.Token);

                    lock (Program.ConsoleSync)
                    {
                        Console.WriteLine($"\t ~ TEST ~ >> LIMIT {clientOrder.Side} order (ID: {clientOrder.Id}) placed for {clientOrder.Quantity:0.00000000} {clientOrder.Symbol} @ {clientOrder.Price:0.00000000}");
                        Console.Beep(80, 2000);
                    }
                }
                else
                {
                    var order = await pumpSettings.Api.PlaceAsync(clientOrder, token: cts.Token);

                    // ReSharper disable once InvertIf
                    if (order != null)
                    {
                        lock (Program.ConsoleSync)
                        {
                            Console.WriteLine($"\t >> LIMIT {order.Side} order (ID: {order.Id}) placed for {order.OriginalQuantity:0.00000000} {order.Symbol} @ {order.Price:0.00000000}");
                            Console.Beep(80, 2000);
                        }
                    }
                }
            }
            catch (Exception)
            {

            }
            finally
            {

            }
            
            return true;
        }
    }
}
