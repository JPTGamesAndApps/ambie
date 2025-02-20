﻿using AmbientSounds.Services;
using AmbientSounds.Services.Uwp;
using AmbientSounds.ViewModels;
using AmbientSounds.Constants;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Toolkit.Diagnostics;
using System;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.System.Profile;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using System.Threading.Tasks;
using System.Net.Http;
using AmbientSounds.Factories;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.Foundation.Collections;
using Windows.Globalization;

#nullable enable

namespace AmbientSounds
{ 
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Application
    {
        private static readonly bool _isTenFootPc = false;
        private IServiceProvider? _serviceProvider;
        private AppServiceConnection _appServiceConnection;
        private BackgroundTaskDeferral _appServiceDeferral;

        /// <summary>
        /// Initializes the singleton application object.
        /// </summary>
        public App()
        {
            this.InitializeComponent();

            if (IsTenFoot)
            {
                // Ref: https://docs.microsoft.com/en-us/windows/uwp/xbox-apps/how-to-disable-mouse-mode
                this.RequiresPointerMode = ApplicationRequiresPointerMode.WhenRequested;

                // Ref: https://docs.microsoft.com/en-us/windows/uwp/design/input/gamepad-and-remote-interactions#reveal-focus
                this.FocusVisualKind = FocusVisualKind.Reveal;
            }

            SetAppRequestedTheme();
        }

        public static bool IsTenFoot => AnalyticsInfo.VersionInfo.DeviceFamily == "Windows.Xbox" || _isTenFootPc;

        public static Frame? AppFrame { get; private set; }

        /// <summary>
        /// Gets the <see cref="IServiceProvider"/> instance for the current application instance.
        /// </summary>
        public static IServiceProvider Services
        {
            get
            {
                IServiceProvider? serviceProvider = ((App)Current)._serviceProvider;

                if (serviceProvider is null)
                {
                    ThrowHelper.ThrowInvalidOperationException("The service provider is not initialized");
                }

                return serviceProvider;
            }
        }

        /// <inheritdoc/>
        protected override async void OnLaunched(LaunchActivatedEventArgs e)
        {
            await ActivateAsync(e.PrelaunchActivated);
        }

        /// <inheritdoc/>
        protected override async void OnActivated(IActivatedEventArgs args)
        {

            if (args is ToastNotificationActivatedEventArgs toastActivationArgs)
            {
                new PartnerCentreNotificationRegistrar().TrackLaunch(toastActivationArgs.Argument);
                await ActivateAsync(false);
            }
            else if (args.Kind == ActivationKind.Protocol && args is ProtocolActivatedEventArgs e)
            {
                // Ensure that the app does not try to load
                // previous state of active sounds. This prevents
                // conflicts with processing the url and loading
                // sounds from the url.
                await ActivateAsync(false, new AppSettings { LoadPreviousState = false });
                var processor = App.Services.GetRequiredService<ILinkProcessor>();
                processor.Process(e.Uri);
            }
        }

        protected override void OnBackgroundActivated(BackgroundActivatedEventArgs args)
        {
            base.OnBackgroundActivated(args);
            if (args.TaskInstance.TriggerDetails is AppServiceTriggerDetails appService)
            {
                _appServiceDeferral = args.TaskInstance.GetDeferral();
                args.TaskInstance.Canceled += OnAppServicesCanceled;
                _appServiceConnection = appService.AppServiceConnection;
                _appServiceConnection.RequestReceived += OnAppServiceRequestReceived;
                _appServiceConnection.ServiceClosed += AppServiceConnection_ServiceClosed;
            }
        }

        private async void OnAppServiceRequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            AppServiceDeferral messageDeferral = args.GetDeferral();
            var controller = App.Services.GetService<AppServiceController>();
            if (controller != null)
            {
                await controller.ProcessRequest(args.Request);
            }
            else
            {
                var message = new ValueSet();
                message.Add("result", "Fail. Launch Ambie in the foreground to use its app services.");
                await args.Request.SendResponseAsync(message);
            }

            messageDeferral.Complete();
        }

        private void OnAppServicesCanceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            _appServiceDeferral.Complete();
        }

        private void AppServiceConnection_ServiceClosed(AppServiceConnection sender, AppServiceClosedEventArgs args)
        {
            _appServiceDeferral.Complete();
        }

        private async Task ActivateAsync(bool prelaunched, IAppSettings? appsettings = null)
        {

            // Do not repeat app initialization when the Window already has content
            if (Window.Current.Content is not Frame rootFrame)
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;

                // Configure the services for later use
                _serviceProvider = ConfigureServices(appsettings);
                var navigator = App.Services.GetRequiredService<INavigator>();
                navigator.Frame = rootFrame;
            }

            SetPreferredLanguage();

            if (prelaunched == false)
            {
                CoreApplication.EnablePrelaunch(true);

                // Navigate to the root page if one isn't loaded already
                if (rootFrame.Content is null)
                {
                    rootFrame.Navigate(typeof(Views.MainPage));
                }

                // Ensure the current window is active
                Window.Current.Activate();
            }

            AppFrame = rootFrame;
            CustomizeTitleBar(rootFrame.ActualTheme == ElementTheme.Dark);
            await TryRegisterNotifications();
        }

        private Task TryRegisterNotifications()
        {
            var settingsService = App.Services.GetRequiredService<IUserSettings>();

            if (settingsService.Get<bool>(UserSettingsConstants.Notifications))
            {
                return new PartnerCentreNotificationRegistrar().Register();
            }
            else
            {
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// Invoked when navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception($"Failed to load Page {e.SourcePageType.FullName}.");
        }

        /// <summary>
        /// Removes title bar and sets title bar button backgrounds to transparent.
        /// </summary>
        private void CustomizeTitleBar(bool darkTheme)
        {
            CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = true;

            var viewTitleBar = ApplicationView.GetForCurrentView().TitleBar;
            viewTitleBar.ButtonBackgroundColor = Colors.Transparent;
            viewTitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            viewTitleBar.ButtonForegroundColor = darkTheme ? Colors.LightGray : Colors.Black;
        }

        /// <summary>
        /// Set preferred language to override default one - only if override is turned on.
        /// </summary>
        private void SetPreferredLanguage()
        {
            var settingsService = App.Services.GetRequiredService<IUserSettings>();

            //It appears that the primarylanguageoverride is persistent over sessions. Doc says not to set it in every session. https://docs.microsoft.com/en-us/uwp/api/windows.globalization.applicationlanguages.primarylanguageoverride?view=winrt-19041
            //reoverride only if it's not already set to the correct value. Not sure why doc doesn't want us to override every time - probably it keeps on adding that language as the most preferred language even if its already there (e.g. 1st and 2nd primary language override will become en-US) or some other performance issue.
            if (settingsService.Get<bool>(UserSettingsConstants.OverrideLanguage) && !ApplicationLanguages.PrimaryLanguageOverride.Equals(settingsService.Get<string>(UserSettingsConstants.PreferredLanguage)))
            {
                ApplicationLanguages.PrimaryLanguageOverride = settingsService.Get<string>(UserSettingsConstants.PreferredLanguage);
            }
            else //lets reset it back as previous set value will be persistent.
            {
                ApplicationLanguages.PrimaryLanguageOverride = string.Empty;//not sure if there is a better way. something like ApplicationLanguages.Languages[0] which probably returns the language we set as primary language override.
            }
        }

        /// <summary>
        /// Method for setting requested app theme based on user's local settings.
        /// </summary>
        private void SetAppRequestedTheme()
        {
            object themeObject = ApplicationData.Current.LocalSettings.Values[UserSettingsConstants.Theme];
            if (themeObject != null)
            {
                string theme = themeObject.ToString();
                switch (theme)
                {
                    case "light":
                        App.Current.RequestedTheme = ApplicationTheme.Light;
                        break;
                    case "dark":
                        App.Current.RequestedTheme = ApplicationTheme.Dark;
                        break;
                    default:
                        break;
                }
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values[UserSettingsConstants.Theme] = "default";
            }
        }

        /// <summary>
        /// Configures a new <see cref="IServiceProvider"/> instance with the required services.
        /// </summary>
        private static IServiceProvider ConfigureServices(IAppSettings? appsettings = null)
        {
            var client = new HttpClient();

            var provider = new ServiceCollection()
                .AddSingleton(client)
                .AddSingleton<SoundListViewModel>()
                .AddSingleton<CatalogueListViewModel>()
                .AddTransient<SoundSuggestionViewModel>()
                .AddTransient<ScreensaverViewModel>()
                .AddTransient<SettingsViewModel>()
                .AddSingleton<UploadFormViewModel>()
                .AddTransient<MainPageViewModel>()
                .AddTransient<ShareResultsViewModel>()
                .AddSingleton<AppServiceController>()
                .AddTransient<IStoreNotificationRegistrar, PartnerCentreNotificationRegistrar>()
                .AddTransient<ISystemInfoProvider, SystemInfoProvider>()
                .AddTransient<IDialogService, DialogService>()
                .AddTransient<IFileDownloader, FileDownloader>()
                .AddTransient<ISoundVmFactory, SoundVmFactory>()
                .AddTransient<IFileWriter, FileWriter>()
                .AddTransient<IUserSettings, LocalSettings>()
                .AddTransient<IShareLinkBuilder, ShareLinkBuilder>()
                .AddTransient<ITimerService, TimerService>()
                .AddTransient<ISoundMixService, SoundMixService>()
                .AddTransient<IRenamer, Renamer>()
                .AddTransient<ILinkProcessor, LinkProcessor>()
                .AddSingleton<IUploadService, UploadService>()
                .AddTransient<IFilePicker, FilePicker>()
                .AddTransient<IMsaAuthClient, MsalClient>()
                .AddSingleton<INavigator, Navigator>()
                .AddSingleton<ICloudFileWriter, CloudFileWriter>()
                .AddSingleton<PlayerViewModel>()
                .AddSingleton<SleepTimerViewModel>()
                .AddSingleton<ActiveTrackListViewModel>()
                .AddSingleton<AccountControlViewModel>()
                .AddSingleton<UploadedSoundsListViewModel>()
                .AddSingleton<ISyncEngine, SyncEngine>()
                .AddSingleton<IAccountManager, AccountManager>()
                .AddSingleton<IPreviewService, PreviewService>()
                .AddSingleton<IIapService, StoreService>()
                .AddSingleton<IDownloadManager, DownloadManager>()
                .AddSingleton<IScreensaverService, ScreensaverService>()
                .AddSingleton<ITelemetry, AppCentreTelemetry>()
                .AddSingleton<IOnlineSoundDataProvider, OnlineSoundDataProvider>()
                .AddSingleton<IMixMediaPlayerService, MixMediaPlayerService>()
                .AddSingleton<ISoundDataProvider, SoundDataProvider>()
                .AddSingleton<IAppSettings>(appsettings ?? new AppSettings())
                .BuildServiceProvider();

            // preload appservice controller to ensure its
            // dispatcher queue loads properly on the ui thread.
            provider.GetService<AppServiceController>();
            return provider;
        }
    }
}
