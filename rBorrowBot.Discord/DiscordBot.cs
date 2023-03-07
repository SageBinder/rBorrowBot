using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using Reddit.Controllers;
using static rBorrowBot.RedditApiEstablisher.RedditApiEstablisher;

namespace rBorrowBot.Discord {
public class DiscordBot {
    private const string HistoriesChannelResourceName = "histories_channel.txt";
    private const string NoHistoriesChannelResourceName = "no_histories_channel.txt";
    private const string LastPostResourceName = "last_post.txt";
    private const string SetHistoriesKeyword = "!histories";
    private const string SetNoHistoriesKeyword = "!nohistories";

    private readonly DiscordClient _botClient;
    private DiscordChannel _historiesChannel;
    private DiscordChannel _noHistoriesChannel;

    public DiscordBot() {
        _botClient = new DiscordClient(new DiscordConfiguration {
            Token = Resources.Resources.ReadAllText("discord_token.txt"),
            TokenType = TokenType.Bot,
            Intents = DiscordIntents.AllUnprivileged | DiscordIntents.MessageContents,
            AutoReconnect = true
        });
        _botClient.ConnectAsync().Wait();

        SetOnMessageCreated();
        LoadChannels();
        LoadMissedPosts();
    }

    private void SetOnMessageCreated() {
        _botClient.MessageCreated += (client, e) => {
            var text = e.Message.Content;
            Console.WriteLine($"Message Created: {text}");
            if(text.Equals(SetHistoriesKeyword)) {
                Console.WriteLine("histories");
                _historiesChannel = e.Channel;
                Resources.Resources.WriteTo(HistoriesChannelResourceName, _historiesChannel.Id.ToString());
                _botClient.SendMessageAsync(_historiesChannel, $"Setting #{_historiesChannel.Name} to active channel for users with histories");
            } else if(text.Equals(SetNoHistoriesKeyword)) {
                _noHistoriesChannel = e.Channel;
                Resources.Resources.WriteTo(NoHistoriesChannelResourceName, _noHistoriesChannel.Id.ToString());
                _botClient.SendMessageAsync(_noHistoriesChannel, $"Setting #{_noHistoriesChannel.Name} to active channel for users with no histories");
            }

            return Task.CompletedTask;
        };
    }
    
    private void LoadChannels() {
        if(Resources.Resources.ResourceExists(HistoriesChannelResourceName)) {
            var channelId = Resources.Resources.ReadAllText(HistoriesChannelResourceName);
            try {
                _historiesChannel = _botClient.GetChannelAsync(ulong.Parse(channelId)).Result;
            } catch(Exception) {
                Console.WriteLine($"Error: could not access histories channel with id {channelId}");
            }
        }
        
        if(Resources.Resources.ResourceExists(NoHistoriesChannelResourceName)) {
            var channelId = Resources.Resources.ReadAllText(NoHistoriesChannelResourceName);
            try {
                _noHistoriesChannel = _botClient.GetChannelAsync(ulong.Parse(channelId)).Result;
            } catch(Exception) {
                Console.WriteLine($"Error: could not access nohistories channel with id {channelId}");
            }
        }
    }

    private void LoadMissedPosts() {
        if(!Resources.Resources.ResourceExists(LastPostResourceName) || (_historiesChannel == null && _noHistoriesChannel == null)) {
            return;
        }

        var api = EstablishApiIfNecessaryAndGet().Result;
        var lastPostFullname = Resources.Resources.ReadAllText(LastPostResourceName);
        var postsFromSearch = api.GetPosts(new List<string> { lastPostFullname });
        if(!postsFromSearch.Any()) {
            return;
        }
        var lastPost = postsFromSearch[0];
        var subredditName = lastPost.Subreddit;
        var subreddit = api.Subreddit(subredditName);
        Handle(subreddit.Posts.GetNew(before: lastPostFullname));
    }
    
    public void Handle(List<Post> newPosts) {
        Console.WriteLine("Handling...");
        if(_historiesChannel == null && _noHistoriesChannel == null) {
            return;
        }
        
        newPosts.Sort((post1, post2) => DateTime.Compare(post1.Created, post2.Created));
        var filteredPosts = newPosts.Where(post => {
            var cleanedText = Regex.Replace(post.Title.ToUpper(), @"\s+", "");
            return cleanedText.Contains("[REQ]");
        }).ToList();

        if(!filteredPosts.Any()) {
            Console.WriteLine("No posts to handle.");
            return;
        }
        Console.WriteLine("Posts to handle.");
        
        var users = new HashSet<string>(from post in filteredPosts select post.Author);
        
        var userHistories = users.ToDictionary(user => user, GetUserHistory);

        var postsFromUsersWithHistories =
            from post in filteredPosts where userHistories[post.Author]["paid"].Any() select post;
        var postsFromUsersWithoutHistories =
            from post in filteredPosts where !userHistories[post.Author]["paid"].Any() select post;

        var withHistories =
            from post in postsFromUsersWithHistories
            select $"Date/time: {post.Created.ToString("s", CultureInfo.InvariantCulture)}\n" +
                   $"User: u/{post.Author}\n" +
                   $"Num requests: {userHistories[post.Author]["req"].Count()}\n" +
                   $"Num paid: {userHistories[post.Author]["paid"].Count()}\n" +
                   $"Num unpaid: {userHistories[post.Author]["unpaid"].Count()}\n" +
                   $"https://old.reddit.com{post.Permalink}\n" +
                   $"{post.Comments.GetComments().Find(comment => comment.Author.Equals("LoansBot") && comment.Depth == 1)?.Body}";
        var withoutHistories =
            from post in postsFromUsersWithoutHistories
            select $"Date/time: {post.Created.ToString("s", CultureInfo.InvariantCulture)}\n" +
                   $"User: u/{post.Author}\n" +
                   $"Num requests: {userHistories[post.Author]["req"].Count()}\n" +
                   $"Num paid: {userHistories[post.Author]["paid"].Count()}\n" +
                   $"Num unpaid: {userHistories[post.Author]["unpaid"].Count()}\n" +
                   $"https://old.reddit.com{post.Permalink}" +
                   $"{post.Comments.GetComments().Find(comment => comment.Author.Equals("LoansBot") && comment.Depth == 1)?.Body}";

        if(_historiesChannel != null) {
            foreach(var s in withHistories) {
                Console.WriteLine($"Sending:\n{s}");
                _botClient.SendMessageAsync(_historiesChannel, s).Wait();
                
            }
        }

        if(_noHistoriesChannel != null) {
            foreach(var s in withoutHistories) {
                Console.WriteLine($"Sending:\n{s}");
                _botClient.SendMessageAsync(_noHistoriesChannel, s).Wait();
            }
        }
        
        Resources.Resources.WriteTo(LastPostResourceName, newPosts.Last().Fullname);
    }

    private static Dictionary<string, IEnumerable<Post>> GetUserHistory(string user) {
        var api = EstablishApiIfNecessaryAndGet().Result;
        var borrow = api.Subreddit("borrow");
        var borrowPosts = borrow.Search($"title:{user} OR author:{user}", null);

        var borrowRequests = from post in borrowPosts where post.Title.ToUpper().Contains("[REQ]") select post;
        var paidBorrowPosts = from post in borrowPosts where post.Title.ToUpper().Contains("[PAID]") select post;
        var unpaidBorrowPosts = from post in borrowPosts where post.Title.ToUpper().Contains("[UNPAID]") select post;
        
        var postDict = new Dictionary<string, IEnumerable<Post>> {
            { "req", borrowRequests },
            { "paid", paidBorrowPosts },
            { "unpaid", unpaidBorrowPosts }
        };

        return postDict;
    }
}
}