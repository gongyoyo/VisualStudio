﻿using System;
using System.Collections.ObjectModel;
using GitHub.Models;
using ReactiveUI;

namespace GitHub.InlineReviews.ViewModels
{
    /// <summary>
    /// A comment thread.
    /// </summary>
    public interface ICommentThreadViewModel
    {
        /// <summary>
        /// Gets the comments in the thread.
        /// </summary>
        ObservableCollection<ICommentViewModel> Comments { get; }

        /// <summary>
        /// Gets the current user under whos account new comments will be created.
        /// </summary>
        IAccount CurrentUser { get; }

        /// <summary>
        /// Called by a comment in the thread to post itself as a new comment to the API.
        /// </summary>
        ReactiveCommand<ICommentModel> PostComment { get; }
    }
}
