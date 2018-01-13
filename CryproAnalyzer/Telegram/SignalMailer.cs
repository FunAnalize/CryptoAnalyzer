﻿using System;
using System.Linq;
using System.Threading;
using Bittrex.Net;
using CryproAnalyzer.Analyzers;
using CryproAnalyzer.Models;
using Telegram.Bot;

namespace CryproAnalyzer.Telegram
{
    internal class SignalMailer
    {
        private readonly Thread _thread;

        public SignalMailer()
        {
            _thread = new Thread(Act);
        }

        public void Start()
        {
            _thread.Start();
        }

        public void Stop()
        {
            _thread.Interrupt();
        }

        private static void Act()
        {
            var botClient = new TelegramBotClient(Tokens.TelegramToken);
            var bittrexClient = new BittrexClient(Tokens.BittrexKey, Tokens.BittrexSecret);

            var glassAnalyzer = new GlassAnalyzer(bittrexClient);
            var lowerAvergeAnalyzer = new LowerAvergeAnalyzer(bittrexClient);

            while (true)
            {
                var markets = bittrexClient.GetMarketsAsync().Result;

                foreach (var bittrexMarket in markets.Result)
                {
                    if (!bittrexMarket.MarketName.StartsWith("BTC"))
                    {
                        continue;
                    }

                    var lowerAvergeAnalyzerResult = lowerAvergeAnalyzer.Analyze(bittrexMarket.MarketName, 15);

                    var glassAnalyzerResult = glassAnalyzer.Analyze(bittrexMarket.MarketName);

                    if (lowerAvergeAnalyzerResult is null || glassAnalyzerResult is null)
                    {
                        continue;
                    }

                    Console.WriteLine("Market: {0} Avarge: {1}, Current {2}, Percent: {3}, GoodBuy: {4}, Ratio: {5}",
                        lowerAvergeAnalyzerResult.MarketName,
                        lowerAvergeAnalyzerResult.Average,
                        lowerAvergeAnalyzerResult.Current,
                        lowerAvergeAnalyzerResult.Percent,
                        lowerAvergeAnalyzerResult.GoodBuy,
                        glassAnalyzerResult.Ratio);

                    if (lowerAvergeAnalyzerResult.Percent <= 10 || glassAnalyzerResult.Ratio < 0.65m) continue;

                    using (var db = new AnalyzerContext())
                    {
                        var users = db.Users.Where(user => user.IsSubscribed);
                        foreach (var user in users)
                        {
                            botClient.SendTextMessageAsync(user.ChatId,
                                "Маркет: " + bittrexMarket.MarketName + "\n" +
                                "Текущая цена: " + lowerAvergeAnalyzerResult.Current + "\n" +
                                "Отклонение  от среднего: " + lowerAvergeAnalyzerResult.Percent + "\n" +
                                "Коэффициент оредеров: " + glassAnalyzerResult.Ratio);
                        }
                    }
                }

                Thread.Sleep(600000);
            }
        }
    }
}