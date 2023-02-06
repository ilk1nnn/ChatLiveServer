using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ChatLiveServer.Commands;
using ChatLiveServer.Models;
using ChatLiveServer.Services;
using WPF_ServerTcp;

namespace ChatLiveServer.ViewModels
{
    public class MainViewModel : BaseViewModel
    {

        private ObservableCollection<ClientItem> tcpClients;

        public ObservableCollection<ClientItem> TcpClients
        {
            get { return tcpClients; }
            set { tcpClients = value; OnPropertyChanged(); }
        }


        private ObservableCollection<ClientItem> offlineTcpClients;

        public ObservableCollection<ClientItem> OfflineTcpClients
        {
            get { return offlineTcpClients; }
            set { offlineTcpClients = value; OnPropertyChanged(); }
        }


        public ClientItem SelectedClient { get; set; }

        public TcpListener TcpListener { get; set; }


        DispatcherTimer checkDispatcher;

        public BinaryReader BinaryReader { get; set; }

        public BinaryWriter BinaryWriter { get; set; }


        public StackPanel MessagePanel { get; set; }
        public bool IsFirstStream { get; set; }


        public async void CheckIfOnlineAndReceiveMessage(ClientItem clientItem)
        {
            await Task.Run(() =>
            {
                bool hasConnected = true;
                App.Current.Dispatcher.Invoke((Action)delegate
                {

                    var stream = clientItem.Client.GetStream();
                    //BinaryReader = new BinaryReader(stream);
                    var BR = new BinaryReader(stream);
                    App.Current.Dispatcher.Invoke((Action)async delegate
                    {
                        while (true)
                        {
                            //var view = new MessageUC();
                            //var viewModel = new MessageViewModel();
                            //view.DataContext = viewModel;

                            await Task.Run(() =>
                            {
                                try
                                {
                                    //viewModel.ClientMessage = BR.ReadString();
                                    clientItem.Name = BR.ReadString();
                                }
                                catch (Exception)
                                {
                                    MessageBox.Show($"{clientItem.Name} disconnected");
                                    hasConnected = false;
                                }
                            });
                            if (!hasConnected) break;
                        }
                    });


                });
            });
        }
        public async void GetClients()
        {
            await Task.Run(async () =>
            {
                var client = await TcpListener.AcceptTcpClientAsync();
                IsFirstStream = true;
                var clientItem = new ClientItem();
                while (true)
                {
                    try
                    {
                        var stream = client.GetStream();
                        string name = "";
                        BinaryReader = new BinaryReader(stream);

                        if (IsFirstStream)
                        {
                            name = BinaryReader.ReadString();
                            IsFirstStream = !IsFirstStream;
                            App.Current.Dispatcher.Invoke((Action)delegate // <--- HERE
                            {
                                clientItem.Name = name;
                                clientItem.Client = client;
                                if (OfflineTcpClients.Any(c => c.Name == clientItem.Name))
                                {
                                    var cI = OfflineTcpClients.First(c => c.Name == clientItem.Name);
                                    OfflineTcpClients.Remove(cI);

                                }
                                TcpClients.Add(clientItem);
                            });

                        }
                        break;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"{clientItem.Name} disconnected");
                        IsFirstStream = true;
                        break;
                    }

                }

                CheckIfOnlineAndReceiveMessage(clientItem);
            });
        }

        public DispatcherTimer timer { get; set; }
        public MainViewModel()
        {
            IsFirstStream = true;
            checkDispatcher = new DispatcherTimer();
            checkDispatcher.Interval = TimeSpan.FromSeconds(2);
            checkDispatcher.Tick += CheckDispatcher_Tick;
            checkDispatcher.Start();

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick; ;
            timer.Start();


            OfflineTcpClients = new ObservableCollection<ClientItem>();
            TcpClients = new ObservableCollection<ClientItem>();

            var port = 27001;
            var ip = IPAddress.Parse(IPService.GetLocalIPAddress());
            var endPoint = new IPEndPoint(ip, port);

            TcpListener = new TcpListener(endPoint);
            TcpListener.Start();
            MessageBox.Show($"Listening on {TcpListener.LocalEndpoint}");

        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            for (int i = 0; i < TcpClients.Count; i++)
            {
                var item = TcpClients[i];
                var hasExistOn = TcpClients.Any(c => c == item);
                var hasExistOf = OfflineTcpClients.Any(c => c == item);
                if (!item.Client.Connected)
                {
                    if (hasExistOn)
                        TcpClients.Remove(item);
                    if (!hasExistOf)
                        OfflineTcpClients.Add(item);
                }
                else
                {
                    if (hasExistOf)
                        OfflineTcpClients.Remove(item);
                }
            }
        }

        private void CheckDispatcher_Tick(object sender, EventArgs e)
        {
            GetClients();
        }
    }
}
