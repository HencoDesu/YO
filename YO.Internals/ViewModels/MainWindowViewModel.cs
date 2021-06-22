﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using YO.Internals.Configuration;
using YO.Internals.Extensions;
using YO.Internals.Shikimori;
using YO.Internals.Shikimori.Data;
using YO.Internals.Shikimori.Parameters;

namespace YO.Internals.ViewModels
{
	public class MainWindowViewModel : ViewModelBase
	{
		private readonly IConfiguration _configuration;
		private readonly IShikimoriApi _shikimoriApi;
		private readonly HttpClient _httpClient;

		public Interaction<LoginViewModel, string> ShowLoginDialog { get; }
		public ReactiveCommand<Unit, Unit> OpenLoginDialog { get; }
		
		[Reactive]
		public bool IsAuthorized { get; set; }
		
		[Reactive]
		public bool IsLoading { get; set; }
		
		[Reactive]
		public IEnumerable<AnimeViewModel> Animes { get; set; }

		public MainWindowViewModel(IConfiguration configuration, 
								   IShikimoriApi shikimoriApi,
								   HttpClient httpClient)
		{
			_configuration = configuration;
			_shikimoriApi = shikimoriApi;
			_httpClient = httpClient;

			_configuration.WhenAnyValue(c => c.ShikimoriUsername)
						  .Subscribe(async newName => await OnUserNameChanged(newName));
			ShowLoginDialog = new Interaction<LoginViewModel, string>();
			OpenLoginDialog = ReactiveCommand.CreateFromTask(OpenLoginDialogImpl);
		}

		private async Task OpenLoginDialogImpl()
		{
			var loginViewModel = new LoginViewModel(_configuration.ShikimoriUsername);
			_configuration.ShikimoriUsername = await ShowLoginDialog.Handle(loginViewModel);
		}

		private async Task OnUserNameChanged(string? newName)
		{
			IsAuthorized = !string.IsNullOrEmpty(newName);
			
			if (IsAuthorized)
			{
				IsLoading = true;
				var user = await _shikimoriApi.Users.GetByNickname(newName);
				var rateRequestParams = new GetUserRatesParameters()
				{
					UserId = user.Id,
					TargetType = DataType.Anime,
					Status = RateStatus.Watching
				};
				var animeRates = await _shikimoriApi.UserRates.GetUserRates(rateRequestParams);
				var animeIds = animeRates.Select(ur => ur.TargetId);
				var animeRequestParams = new GetAnimesParameters()
				{
					Ids = animeIds,
					Limit = 50
				};
				var animes = await _shikimoriApi.Animes.GetAnimes(animeRequestParams);
				using (var webClient = new WebClient())
				{
					var list = new List<AnimeViewModel>();
					foreach (var animeInfo in animes)
					{
						var anime = new AnimeViewModel(animeInfo)
						{
							Poster = await animeInfo.GetAnimePoster(webClient)
						};
						list.Add(anime);
					}
					Animes = list;
				}
				
				IsLoading = false;
			}
		}
	}
}