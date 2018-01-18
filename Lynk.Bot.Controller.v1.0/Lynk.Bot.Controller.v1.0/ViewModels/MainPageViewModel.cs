using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Windows.Input;
using Xamarin.Forms;

namespace Lynk.Bot.Controller.v1._0.ViewModels
{
    public class MainPageViewModel : ViewModelBase
    {
        public ICommand ClickCommand { get; private set; }

        private string _remoteHostname;
        public string RemoteHostname
        {
            get { return _remoteHostname; }
            set { SetProperty(ref _remoteHostname, value); }
        }
        private int _remotePort = 0;

        public int RemotePort
        {
            get { return _remotePort; }
            set { SetProperty(ref _remotePort, value); }
        }


        TcpClient _client;
        public MainPageViewModel(INavigationService navigationService) : base(navigationService)
        {
            Title = "Main Page";
            _client = new TcpClient();
            ClickCommand = new DelegateCommand(() =>
            {
                Title = DateTime.UtcNow.ToLongDateString();
            }, () =>
            {
                return (!string.IsNullOrWhiteSpace(RemoteHostname) && RemotePort > 0);
            }).ObservesProperty(() => RemoteHostname)
            .ObservesProperty(() => RemotePort);
        }


    }
}
