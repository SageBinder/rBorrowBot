using System;
using Reddit;
using Reddit.Controllers;
using Reddit.Controllers.EventArgs;
using static rBorrowBot.RedditApiEstablisher.RedditApiEstablisher;

namespace rBorrowBot.Reddit {
public class SubredditMonitor {
    private readonly EventHandler<PostsUpdateEventArgs> _onNewPost;
    private Subreddit _subreddit;
    private readonly string _subredditName;
    private bool _isMonitoring;

    public SubredditMonitor(EventHandler<PostsUpdateEventArgs> onNewPost, string subredditName) {
        _onNewPost = onNewPost;
        _subredditName = subredditName;
        var api = EstablishApiIfNecessaryAndGet().Result;
        RefreshApi(api);
    }

    public void RefreshApi(RedditClient api) {
        var wasMonitoring = _isMonitoring;
        
        StopMonitoring();

        var subreddit = api.Subreddit(_subredditName);
        subreddit.Posts.GetNew();
        subreddit.Posts.MonitorNew();
        _subreddit = subreddit;

        if(wasMonitoring) {
            StartMonitoring();
        }
    }

    public void StartMonitoring() {
        _subreddit.Posts.NewUpdated += _onNewPost;
        _isMonitoring = true;
    }

    public void StopMonitoring() {
        if(_subreddit != null) {
            _subreddit.Posts.NewUpdated -= _onNewPost;
        }

        _isMonitoring = false;
    }
}
}