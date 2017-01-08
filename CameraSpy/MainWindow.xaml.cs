using System;
using System.Windows;
using System.Threading;
using System.Windows.Controls;
using Microsoft.Maps.MapControl.WPF;

using System.Collections.Concurrent;
using System.Windows.Input;
using System.Diagnostics;

namespace IPLocator_GUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private AddressLocator.BulkAddressLocator resolver;
        private delegate void PushpinCreator(AddressLocator.ClientInfoObject client);

        private ConcurrentQueue<AddressLocator.ClientInfoObject> PendingPushpinQueue;

        private void UpdateStatus(String message)
        {
            CurrentStatus.Content = message;
        }

        private void StatusUpdateEventSubscriber(String message)
        {
            CurrentStatus.Dispatcher.Invoke(new AddressLocator.ThreadedConsoleInterface.StatusUpdateEventHandler(this.UpdateStatus),
                new object[] { message });
        }

        private void PushpinClickedEventHandler(Object sender, RoutedEventArgs e)
        {
            this.resolver.IConsole.WriteLine("[+] Handling double click event.");
            String UrlString = (String) ((Pushpin) sender).Content;
            this.resolver.IConsole.WriteLine(String.Format("[+] Opening url: {0}", UrlString));
            Process UrlOpener = new Process();
            UrlOpener.StartInfo.UseShellExecute = true;
            UrlOpener.StartInfo.FileName = UrlString;
            UrlOpener.Start();
        }

        private void CreatePushpin(AddressLocator.ClientInfoObject client)
        {
            Location PinLocation = new Location(float.Parse(client.Latitude), float.Parse(client.Longitude));
            Pushpin pin = new Pushpin();
            pin.FontSize = 1;
            String TooltipContent = String.Join("\n", new String[]
            {
                String.Format("IP Address: {0}", client.address),
                String.Format("URL: {0}", client.UrlString),
                String.Format("Location: {0},{1},{2}", client.City, client.Province, client.Country)
            });
            ToolTipService.SetToolTip(pin, TooltipContent);
            pin.MouseDoubleClick += PushpinClickedEventHandler;
            pin.Content = client.UrlString;
            pin.Location = PinLocation;
            MainMap.Children.Add(pin);
        }

        private void ClientResolvedEventSubscriber(AddressLocator.ClientInfoObject client)
        {
            MainMap.Dispatcher.BeginInvoke(new PushpinCreator(this.CreatePushpin), new object[] { client });
        }

        public MainWindow()
        {
            InitializeComponent();
            MainMap.Focus();
            PendingPushpinQueue = new ConcurrentQueue<AddressLocator.ClientInfoObject>();
            this.resolver = new AddressLocator.BulkAddressLocator(String.Join(@"\", new String[] { AppDomain.CurrentDomain.BaseDirectory + "data", "c4" }));
            resolver.IConsole.StatusUpdateEventSubscribers += this.StatusUpdateEventSubscriber;
            resolver.AddressResolvedEventSubscription += this.ClientResolvedEventSubscriber;
            this.resolver.ProcessUntilCompleted();            
        }
    }
}
