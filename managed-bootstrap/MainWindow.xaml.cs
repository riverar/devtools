//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011 Garrett Serack . All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace CoApp.Bootstrapper {
    using System;
    using System.ComponentModel;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Documents;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Windows.Threading;
    using Microsoft.Win32;

    internal enum LocalizedMessage {
        IDS_ERROR_CANT_OPEN_PACKAGE = 500,
        IDS_MISSING_MSI_FILE_ON_COMMANDLINE,
        IDS_REQUIRES_ADMIN_RIGHTS,
        IDS_SOMETHING_ODD,
        IDS_FRAMEWORK_INSTALL_CANCELLED,
        IDS_UNABLE_TO_DOWNLOAD_FRAMEWORK,
        IDS_UNABLE_TO_FIND_SECOND_STAGE,
        IDS_MAIN_MESSAGE,
        IDS_CANT_CONTINUE,
        IDS_FOR_ASSISTANCE,
        IDS_OK_TO_CANCEL,
        IDS_CANCEL,
        IDS_UNABLE_TO_ACQUIRE_COAPP_INSTALLER,
        IDS_UNABLE_TO_LOCATE_INSTALLER_UI,
        IDS_UNABLE_TO_ACQUIRE_RESOURCES,
        IDS_CANCELLING,
        IDS_MSI_FILE_NOT_FOUND,
        IDS_MSI_FILE_NOT_VALID,
    }

    /// <summary>
    ///   Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow {
        private static MainWindow _mainwindow ;
        internal static event Action _mainWindowReady;

        // this allows us to have an event that we can add 
        // actions to that will always call as soon as the UI is ready
        // or instantly if the UI is ready now. 
        
        public static event Action WhenReady {
            add {
                lock (typeof(MainWindow)) {
                    if (_mainwindow == null) {
                        _mainWindowReady += value;
                    } else {
                        if( _mainwindow.Dispatcher.CheckAccess()) {
                            value();    
                        } else {
                            _mainwindow.Dispatcher.BeginInvoke(value);
                        }
                    }
                }
            }
            remove {
                lock (typeof(MainWindow)) {
                    if (_mainwindow == null) {
                        _mainWindowReady -= value;
                    }
                }
            }
        }

        internal static MainWindow MainWin {
            get { return _mainwindow; }
            
            set {
                lock (typeof(MainWindow)) {
                    if (_mainwindow == null) {
                        _mainwindow = value;
                        if (_mainWindowReady != null) {
                            _mainWindowReady();
                        }
                    }
                }
            }
        }

        private const string HelpUrl = "http://coapp.org/help/";
        public int CurrentProgress { get { return SingleStep.ActualPercent; } set { SingleStep.ActualPercent = value; } }
        
        internal static readonly Lazy<NativeResourceModule> NativeResources = new Lazy<NativeResourceModule>(() => {
            try {
                return new NativeResourceModule(SingleStep.AcquireFile("coapp.resources.dll"));
            }
            catch {
                return null;
            }
        }, LazyThreadSafetyMode.PublicationOnly);

        public MainWindow() {
            Logger.Warning("In Window Constructor.");
            InitializeComponent();
            Logger.Warning("Component Initialized.");
            Opacity = 0;

            Task.Factory.StartNew(() => {
                if (NativeResources.Value != null) {
                    Dispatcher.Invoke((Action)delegate {
                        containerPanel.Background.SetValue(ImageBrush.ImageSourceProperty, NativeResources.Value.GetBitmapImage(1201));
                        logoImage.SetValue(Image.SourceProperty, NativeResources.Value.GetBitmapImage(1202));
                    });
                    Logger.Warning("Loaded Resources.");
                } else {
                    Logger.Warning("Unable to load resources Continuing anyway.");
                }
            });
            
            // try to short circuit early
            if( CurrentProgress >= 98 && !SingleStep.Cancelling ) {
                MainWin = this;
                return;
            }

            // after the window is shown...
            Loaded += (o, e) => {
                Topmost = false;
                // if we're really close to the end, let's not even bother with the progress window.
                if (CurrentProgress < 95) {
                    Opacity = 1;
                }
                MainWin = this;
            };
        }

        internal static void Fail(LocalizedMessage message, string messageText) {
            if (!SingleStep.Cancelling) {

                WhenReady+= () => {
                    SingleStep.Cancelling = true;
                    messageText = GetString(message, messageText);
                    
                    MainWin.containerPanel.Background = new SolidColorBrush(
                        new Color {
                            A = 255,
                            R = 18,
                            G = 112,
                            B = 170
                        });
                                
                    MainWin.progressPanel.Visibility = Visibility.Collapsed;
                    MainWin.failPanel.Visibility = Visibility.Visible;
                    MainWin.messageText.Text = messageText;
                    MainWin.helpLink.NavigateUri = new Uri(HelpUrl + (message + 100));
                    MainWin.helpLink.Inlines.Clear();
                    MainWin.helpLink.Inlines.Add(new Run(HelpUrl + (message + 100)));
                    MainWin.Visibility = Visibility.Visible;
                    MainWin.Opacity = 1;
                };
            }
        }

        internal static string GetString(LocalizedMessage resourceId, string defaultString) {
            return NativeResources.Value != null ? (NativeResources.Value.GetString((uint)resourceId) ?? defaultString) : defaultString;
        }

        private void HeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            DragMove();
        }

        private void CloseBtnClick(object sender, RoutedEventArgs e) {
            // stop the download/install...
            if( !SingleStep.Cancelling ) {
                // check first.
                if( new AreYouSure().ShowDialog() != true ) {
                    SingleStep.Cancelling = true; // prevents any other errors/messages.
                    // wait for MSI to clean up ?
                    while ( SingleStep.InstallTask != null ) {
                        SingleStep.InstallTask.Wait(100);
                    }
                    Application.Current.Shutdown();        
                }
            } else {
                 Application.Current.Shutdown();    
            }
        }

        internal void Updated() {
            Dispatcher.BeginInvoke((Action)delegate { installationProgress.Value = CurrentProgress; });
        }
    }
}