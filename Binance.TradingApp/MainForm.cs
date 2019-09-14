using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Binance.Client;
using Binance.TradingApp.Controllers;
using Binance.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Binance.TradingApp
{
    public partial class MainForm : Form
    {
        public static IBinanceApi Api;
        public static IBinanceApiUser User;
        public static IEnumerable<Symbol> SymbolRestriction;
        public static IConfigurationRoot Configuration;

        public static IServiceProvider ServiceProvider;
        private static readonly string _asset = Asset.BTC;

        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            var account = new AccountController();
            var task1 = Task.Run(() => account.LoadAccountBalances());
        }

        public async Task LoadMainForm()
        {
            
        }

        

        //private static void Display(AccountUpdateEventArgs args)
        //{
        //    lock (_sync)
        //    {
        //        foreach (var balance in args.AccountInfo.Balances)
        //        {
        //            Console.WriteLine();
        //            Console.WriteLine(balance == null
        //                ? "  [None]"
        //                : $"  {balance.Asset}:  {balance.Free} (free)   {balance.Locked} (locked)");
        //            Console.WriteLine();
        //        }                
        //    }
        //}


    }
}
