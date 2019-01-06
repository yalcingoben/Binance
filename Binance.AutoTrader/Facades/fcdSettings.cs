using Binance.AutoTrader.Models;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Binance.AutoTraderFacades
{
    public static class fcdSettings
    {
        public static void LoadSettings(IConfigurationRoot Configuration, ref PumpSettings pumpSettings)
        {
            pumpSettings.pumpPriceChange = Convert.ToDecimal(Configuration.GetSection("Settings").GetSection("PumpPercentCheck").Value);
            pumpSettings.dumpPriceChange = Convert.ToDecimal(Configuration.GetSection("Settings").GetSection("DumpPercentCheck").Value);
            pumpSettings.minVolume = Convert.ToDecimal(Configuration.GetSection("Settings").GetSection("MinVolume").Value);
            pumpSettings.maxVolume = Convert.ToDecimal(Configuration.GetSection("Settings").GetSection("MaxVolume").Value);
            pumpSettings.maxCacheSize = Convert.ToInt32(Configuration.GetSection("Settings").GetSection("MaxCacheSize").Value);
            pumpSettings.basePriceSecond = Convert.ToInt32(Configuration.GetSection("Settings").GetSection("BasePriceSecond").Value);
            pumpSettings.volChangeCheck = Convert.ToDecimal(Configuration.GetSection("Settings").GetSection("VolChangeCheck").Value);
            pumpSettings.IsOrdersTestOnly = Convert.ToBoolean(Configuration.GetSection("Settings").GetSection("IsTest").Value);
            pumpSettings.addPercentToCurrentPrice = Convert.ToDecimal(Configuration.GetSection("Settings").GetSection("AddPercentToCurrentPrice").Value);
            pumpSettings.addPercentForProfit = Convert.ToDecimal(Configuration.GetSection("Settings").GetSection("AddPercentForProfit").Value);
            pumpSettings.useBalancePercent = Convert.ToDecimal(Configuration.GetSection("Settings").GetSection("UseBalancePercent").Value);            
        }

    }
}
