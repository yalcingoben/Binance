using Binance.AutoTrader.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Binance.AutoTrader.Facades
{
    public class fcdAccount
    {
        public static async void LoadAcountBalance(PumpSettings pumpSettings)
        {
            var cts = new CancellationTokenSource();
            try
            {
                if (!string.IsNullOrEmpty(pumpSettings.User.ApiKey))
                {
                    pumpSettings.SymbolRestriction = await pumpSettings.Api.GetSymbolsAsync(cts.Token);

                    var account = await pumpSettings.Api.GetAccountInfoAsync(pumpSettings.User, token: cts.Token);
                    var btcVal = account.Balances.FirstOrDefault(p => p.Asset == "BTC").Free;
                    pumpSettings.userBtcBalance = Math.Round((btcVal * pumpSettings.useBalancePercent / 100), 8);

                    Console.WriteLine($"Available BTC Balance: {btcVal}");
                    Console.WriteLine($"Use For Pump(%{pumpSettings.useBalancePercent}): {pumpSettings.userBtcBalance}");
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                cts.Cancel();
                cts.Dispose();
            }
        }

    }
}
