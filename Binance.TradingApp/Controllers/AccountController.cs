using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Binance.Client;
using Binance.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Binance.TradingApp.Controllers
{
    public class AccountController
    {
        string listenKey = string.Empty;
        ServiceProvider services;
        IConfigurationRoot configuration;
        IBinanceApi api;
        IUserDataWebSocketClient client;
        IUserDataWebSocketStreamControl streamControl;


        public AccountController()
        {
            // Load configuration.
            configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", true, false)
                .Build();

            // Configure services.
            services = new ServiceCollection()
                .AddBinance() // add default Binance services.
                .AddLogging(builder => builder // configure logging.
                    .SetMinimumLevel(LogLevel.Trace)
                    .AddFile(configuration.GetSection("Logging:File")))
                .BuildServiceProvider();

            api = services.GetService<IBinanceApi>();
            client = services.GetService<IUserDataWebSocketClient>();
            streamControl = services.GetService<IUserDataWebSocketStreamControl>();
        }

        public async Task LoadAccountBalances()
        {
            try
            {   
                // Get API key.
                var key = configuration["BinanceApiKey"] // user secrets configuration.
                    ?? configuration.GetSection("User")["ApiKey"]; // appsettings.json configuration.

                // Get API secret.
                var secret = configuration["BinanceApiSecret"] // user secrets configuration.
                    ?? configuration.GetSection("User")["ApiSecret"]; // appsettings.json configuration.
                
                var userProvider = services.GetService<IBinanceApiUserProvider>();

                client.Error += (s, e) => HandleError(e.Exception);

                using (var user = userProvider.CreateUser(key, secret))
                {
                    // Query and display current account balance.
                    var account = await api.GetAccountInfoAsync(user);
                    Display(account);

                    listenKey = await streamControl.OpenStreamAsync(user); // add user and start timer.

                    streamControl.ListenKeyUpdate += (s, a) =>
                    {
                        try
                        {
                            // Unsubscribe old listen key.
                            Console.WriteLine($"Unsubscribe old listen key... {a.OldListenKey}");
                            client.Unsubscribe(a.OldListenKey);

                            if (a.NewListenKey == null)
                            {
                                Console.WriteLine("! Failed to get new listen key...");
                                return;
                            }

                            Console.WriteLine($"Subscribe to new listen key... {a.NewListenKey}");
                            client.Subscribe<AccountUpdateEventArgs>(a.NewListenKey, user, Display);

                            listenKey = a.NewListenKey;
                        }
                        catch (Exception e) { Console.WriteLine(e.Message); }
                    };

                    Console.WriteLine($"Subscribe to listen key... {listenKey}");
                    // Subscribe listen key and user (automatically begin streaming).
                    client.Subscribe<AccountUpdateEventArgs>(listenKey, user, Display);

                    // Optionally wait for web socket open event.
                    await client.WaitUntilWebSocketOpenAsync();                    
                    
                }
            }
            catch (Exception e) { Console.WriteLine(e.Message); }
        }

        public async Task DisconnectAsync()
        {
            // Unsubscribe listen key (automatically end streaming).
            client.Unsubscribe(listenKey);

            //await streamControl.CloseStreamAsync(user);
            streamControl.Dispose();
        }

        private static readonly object _sync = new object();

        private static void Display(AccountUpdateEventArgs args)
            => Display(args.AccountInfo);

        private static void Display(AccountInfo accountInfo)
        {
            lock (_sync)
            {
                foreach (var balance in accountInfo.Balances.Where(p => p.Free > 0))
                {
                    Console.WriteLine();
                    Console.WriteLine(balance == null
                        ? "  [None]"
                        : $"  {balance.Asset}:  {balance.Free} (free)   {balance.Locked} (locked)");
                    Console.WriteLine();
                }
            }
        }

        private static void HandleError(Exception e)
        {
            lock (_sync)
            {
                Console.WriteLine(e.Message);
            }
        }

    }
}
