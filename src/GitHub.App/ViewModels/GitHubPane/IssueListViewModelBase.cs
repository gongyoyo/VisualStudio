﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using GitHub.Collections;
using GitHub.Extensions;
using GitHub.Extensions.Reactive;
using GitHub.Models;
using GitHub.Primitives;
using GitHub.Services;
using ReactiveUI;

namespace GitHub.ViewModels.GitHubPane
{
    public abstract class IssueListViewModelBase : PanePageViewModelBase, IIssueListViewModelBase
    {
        readonly IRepositoryService repositoryService;
        IReadOnlyList<IIssueListItemViewModelBase> items;
        ICollectionView itemsView;
        IDisposable subscription;
        IssueListMessage message;
        IRepositoryModel remoteRepository;
        IReadOnlyList<IRepositoryModel> forks;
        string searchQuery;
        string selectedState;
        string stringFilter;
        int numberFilter;
        IUserFilterViewModel authorFilter;

        public IssueListViewModelBase(IRepositoryService repositoryService)
        {
            this.repositoryService = repositoryService;
            OpenItem = ReactiveCommand.CreateAsyncTask(OpenItemImpl);
        }

        public IUserFilterViewModel AuthorFilter
        {
            get { return authorFilter; }
            private set { this.RaiseAndSetIfChanged(ref authorFilter, value); }
        }

        public IReadOnlyList<IRepositoryModel> Forks
        {
            get { return forks; }
            set { this.RaiseAndSetIfChanged(ref forks, value); }
        }

        public IReadOnlyList<IIssueListItemViewModelBase> Items
        {
            get { return items; }
            private set { this.RaiseAndSetIfChanged(ref items, value); }
        }

        public ICollectionView ItemsView
        {
            get { return itemsView; }
            private set { this.RaiseAndSetIfChanged(ref itemsView, value); }
        }

        public ILocalRepositoryModel LocalRepository { get; private set; }

        public IssueListMessage Message
        {
            get { return message; }
            private set { this.RaiseAndSetIfChanged(ref message, value); }
        }

        public IRepositoryModel RemoteRepository
        {
            get { return remoteRepository; }
            set { this.RaiseAndSetIfChanged(ref remoteRepository, value); }
        }

        public string SearchQuery
        {
            get { return searchQuery; }
            set { this.RaiseAndSetIfChanged(ref searchQuery, value); }
        }

        public string SelectedState
        {
            get { return selectedState; }
            set { this.RaiseAndSetIfChanged(ref selectedState, value); }
        }

        public abstract IReadOnlyList<string> States { get; }

        public ReactiveCommand<Unit> OpenItem { get; }

        public async Task InitializeAsync(ILocalRepositoryModel repository, IConnection connection)
        {
            LocalRepository = repository;
            SelectedState = States.FirstOrDefault();
            AuthorFilter = new UserFilterViewModel(LoadAuthors);

            var parentOwner = await repositoryService.ReadParentOwnerLogin(
                HostAddress.Create(repository.CloneUrl),
                repository.Owner,
                repository.Name);

            if (parentOwner == null)
            {
                RemoteRepository = repository;
            }
            else
            {
                RemoteRepository = new RepositoryModel(
                    repository.Name,
                    UriString.ToUriString(repository.CloneUrl.ToRepositoryUrl(parentOwner)));

                Forks = new IRepositoryModel[]
                {
                    RemoteRepository,
                    repository,
                };
            }

            this.WhenAnyValue(x => x.SelectedState, x => x.RemoteRepository)
                .Skip(1)
                .Subscribe(_ => Refresh().Forget());

            Observable.Merge(
                this.WhenAnyValue(x => x.SearchQuery).Skip(1).SelectUnit(),
                AuthorFilter.WhenAnyValue(x => x.Selected).Skip(1).SelectUnit())
                .Subscribe(_ => FilterChanged());

            IsLoading = true;
            await Refresh();
        }

        public override Task Refresh()
        {
            subscription?.Dispose();

            var dispose = new CompositeDisposable();
            var itemSource = CreateItemSource();
            var items = new VirtualizingList<IIssueListItemViewModelBase>(itemSource, null);
            var view = new VirtualizingListCollectionView<IIssueListItemViewModelBase>(items);

            view.Filter = FilterItem;
            Items = items;
            ItemsView = view;

            dispose.Add(itemSource);
            dispose.Add(
                Observable.CombineLatest(
                    itemSource.WhenAnyValue(x => x.IsLoading),
                    view.WhenAnyValue(x => x.Count),
                    (loading, count) => Tuple.Create(loading, count))
                .Subscribe(x => UpdateState(x.Item1, x.Item2)));
            subscription = dispose;

            return Task.CompletedTask;
        }

        protected abstract IVirtualizingListSource<IIssueListItemViewModelBase> CreateItemSource();
        protected abstract Task DoOpenItem(IIssueListItemViewModelBase item);
        protected abstract Task<Page<ActorModel>> LoadAuthors(string after);

        void FilterChanged()
        {
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                numberFilter = 0;

                if (SearchQuery.StartsWith('#'))
                {
                    int.TryParse(SearchQuery.Substring(1), out numberFilter);
                }

                if (numberFilter == 0)
                {
                    stringFilter = SearchQuery.ToUpper();
                }
            }
            else
            {
                stringFilter = null;
                numberFilter = 0;
            }

            ItemsView?.Refresh();
        }

        bool FilterItem(object o)
        {
            var item = o as IIssueListItemViewModelBase;
            var result = true;

            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {

                if (item != null)
                {
                    if (numberFilter != 0)
                    {
                        result = item.Number == numberFilter;
                    }
                    else
                    {
                        result = item.Title.ToUpper().Contains(stringFilter);
                    }
                }
            }

            if (result && AuthorFilter.Selected != null)
            {
                result = item.Author.Login.Equals(
                    AuthorFilter.Selected.Login,
                    StringComparison.CurrentCultureIgnoreCase);
            }

            return result;
        }

        async Task OpenItemImpl(object i)
        {
            var item = i as IIssueListItemViewModelBase;
            if (item != null) await DoOpenItem(item);
        }

        void UpdateState(bool loading, int count)
        {
            var message = IssueListMessage.None;

            if (!loading)
            {
                if (count == 0)
                {
                    if (SelectedState == States[0] &&
                        string.IsNullOrWhiteSpace(SearchQuery) &&
                        AuthorFilter.Selected == null)
                    {
                        message = IssueListMessage.NoOpenItems;
                    }
                    else
                    {
                        message = IssueListMessage.NoItemsMatchCriteria;
                    }
                }

                IsLoading = false;
            }

            IsBusy = loading;
            Message = message;
        }
    }
}
