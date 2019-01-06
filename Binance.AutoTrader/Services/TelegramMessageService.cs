using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace Binance.AutoTrader.Services
{
    public static class TelegramMessageService
    {
        public static string urlString = "https://api.telegram.org/bot{0}/sendMessage?chat_id={1}&text={2}";
        public static string apiToken = "501038318:AAE_a1UsC0XLlhMSW7wTy-aBNIk4saLxeJg";
        public static string chatId = "@kriptobukuculer";

        public static void SendMessage(string text)
        {
            try
            {
                var messageUrl = String.Format(urlString, apiToken, chatId, text);
                WebRequest request = WebRequest.Create(messageUrl);
                System.IO.Stream rs = request.GetResponse().GetResponseStream();
                StreamReader reader = new StreamReader(rs);
                string line = "";
                StringBuilder sb = new StringBuilder();
                while (line != null)
                {
                    line = reader.ReadLine();
                    if (line != null)
                        sb.Append(line);
                }
                string response = sb.ToString();
            }
            catch (Exception)
            {
            }
        }
    }
}
