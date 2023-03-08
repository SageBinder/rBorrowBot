using System;
using rBorrowBot.Discord;
using rBorrowBot.Reddit;
using static rBorrowBot.RedditApiEstablisher.RedditApiEstablisher;

namespace rBorrowBot.Main {
public static class RBorrowBot {
    public const string Subreddit = "borrow";
    
    public static void Main(string[] args) {
        Console.WriteLine("Hi");
    EstablishApi();
    
    var bot = new DiscordBot();
    
    var subredditMonitor = new SubredditMonitor((sender, newPosts) => {
        bot.Handle(newPosts.Added);
    }, Subreddit);
    subredditMonitor.StartMonitoring();
    
    var redditApiTimer = new System.Timers.Timer(1000 * 60 * 50); // 50 minutes
    redditApiTimer.Elapsed += (sender, eventArgs) => {
        subredditMonitor.RefreshApi(EstablishApiAndGet().Result);
    };
    redditApiTimer.AutoReset = true;
    redditApiTimer.Enabled = true;
    }
}
}