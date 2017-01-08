using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

using IPLocator_GUI;

namespace AddressLocator
{
    class DatabaseInfoObject
    {
        public String City;
        public String Country;
        public String ZipCode;
        public String Province;

        public String Latitude;
        public String Longitude;

        public int AddressFloor;
        public int AddressCeiling;

        /*
         * DB is a CSV with the following line structure:
         *  (Address Floor, Address Ceiling, Country Code, Country Full Name, Province, City, Latitude, Longitude, Zip Code)
         */
        public void LoadDatabaseLine(String DatabaseLine)
        {
            String[] DatabaseInfo = DatabaseLine.Split(new String[] { "\",\"" }, StringSplitOptions.None);
            AddressFloor = Int32.Parse(DatabaseInfo[0].Replace("\"", String.Empty));
            AddressCeiling = Int32.Parse(DatabaseInfo[1]);
            Country = DatabaseInfo[3];
            Province = DatabaseInfo[4];
            City = DatabaseInfo[5];
            Latitude = DatabaseInfo[6];
            Longitude = DatabaseInfo[7];
            ZipCode = DatabaseInfo[8].Replace("\"", String.Empty);
        }

        public DatabaseInfoObject(String DatabaseLine = null)
        {
            if (String.IsNullOrEmpty(DatabaseLine))
            {
                AddressFloor = 0;
                AddressCeiling = 0;
                City = String.Empty;
                Country = String.Empty;
                ZipCode = String.Empty;
                Province = String.Empty;
                Latitude = String.Empty;
                Longitude = String.Empty;
            }
            else
            {
                LoadDatabaseLine(DatabaseLine);
            }
        }
    }

    class ClientInfoObject
    {
        public String City;
        public String Country;
        public String ZipCode;
        public String Province;

        public String Latitude;
        public String Longitude;

        public String PortNo;
        public String address;
        public String UrlString;

        public void SetAddressInfo(DatabaseInfoObject AddressInfo)
        {
            City = AddressInfo.City;
            Country = AddressInfo.Country;
            ZipCode = AddressInfo.ZipCode;
            Province = AddressInfo.Province;
            Latitude = AddressInfo.Latitude;
            Longitude = AddressInfo.Longitude;
        }

        public ClientInfoObject(String Address, String UrlString, String PortNo = "80")
        {
            this.City = String.Empty;
            this.Country = String.Empty;
            this.ZipCode = String.Empty;
            this.Latitude = String.Empty;
            this.Longitude = String.Empty;

            this.PortNo = PortNo;
            this.address = Address;
            this.UrlString = UrlString;
        }
    }

    /*
     * Designed to help keep things moving quickly while minimizing memory usage.
     * The threading ensures we can read without blocking, and the event handler
     * structure keeps things simple when passing to multiple threaded interfaces.
     */
    class ThreadedLineReader
    {
        private String path;
        private Thread ProcessingThread;

        public delegate void LineReadEventHandler(String line, bool IsException = false);
        public LineReadEventHandler LineReadEvent;

        private void ProcessRequestedFile()
        {
            if (LineReadEvent != null)
            {
                try
                {
                    foreach (String line in File.ReadAllLines(path))
                    {
                        LineReadEvent(line);
                    }
                }
                catch (Exception ex)
                {
                    LineReadEvent(ex.ToString(), true);
                }
            }
        }

        public void StartReading()
        {
            ProcessingThread = new Thread(new ThreadStart(ProcessRequestedFile));
            ProcessingThread.Start();
        }

        public ThreadedLineReader(String FilePath)
        {
            if (File.Exists(FilePath))
            {
                path = FilePath;
            }
            else
            {
                throw new FileNotFoundException(String.Format("[!] No file in location: {0}", FilePath));
            }
        }
    }

    /*
     * Helps give us something pretty to look at during processing
     * without slowing down the operation of the rest of the program.
     */
    class ThreadedConsoleInterface
    {
        private bool paused;
        private bool active;
        private Thread WriteThread;
        private ConcurrentQueue<String> WriteQueue;

        public delegate void StatusUpdateEventHandler(String message);
        public StatusUpdateEventHandler StatusUpdateEventSubscribers;

        private void WriteThreadFunction()
        {
            while (active)
            {
                if (!(paused))
                {
                    if (WriteQueue.Count > 0)
                    {
                        String message = String.Empty;
                        bool DequeueSuccessful = false;
                        while (!(DequeueSuccessful))
                        {
                            DequeueSuccessful = WriteQueue.TryDequeue(out message);
                        }
                        if(StatusUpdateEventSubscribers != null)
                        {
                            StatusUpdateEventSubscribers(message);
                        }
                        Thread.Sleep(50);
                    }
                    else
                    {
                        Thread.Sleep(250);
                    }
                }
                else
                {
                    Thread.Sleep(100);
                }
            }
        }

        public void Pause()
        {
            paused = true;
        }

        public void Resume()
        {
            paused = false;
        }

        public void ClearQueue()
        {
            WriteQueue = new ConcurrentQueue<string>();
        }

        public void PauseClear()
        {
            Pause();
            ClearQueue();
        }

        public void PauseClearResume()
        {
            PauseClear();
            Resume();
        }

        public void WriteLine(String line)
        {
            WriteQueue.Enqueue(line);
        }

        public void StopWriting()
        {
            active = false;
            PauseClear();
            WriteThread.Join();
        }

        public void StartWriting()
        {
            if (WriteThread == null)
            {
                WriteThread = new Thread(new ThreadStart(WriteThreadFunction));
            }
            active = true;
            WriteThread.Start();
        }

        public void WriteImmediately(String line, bool terminate = false)
        {
            Pause();
            Console.WriteLine(line);
            if (terminate)
            {
                StopWriting();
            }
            else
            {
                Resume();
            }
        }

        public ThreadedConsoleInterface()
        {
            ClearQueue();
            active = false;
            paused = false;
            WriteThread = new Thread(new ThreadStart(WriteThreadFunction));
        }
    }

    class AddressResolver
    {
        private String DatabasePath;
        private int DatabaseLineCount;
        private List<long> LineIndices;

        private static int IPStringToDecimal(String IpString)
        {
            int index = 0;
            byte[] AddressValueArray = new Byte[4];
            foreach (String octet in IpString.Split('.'))
            {
                AddressValueArray[index++] = byte.Parse(octet);
            }
            return BitConverter.ToInt32(AddressValueArray, 0);
        }

        // Cache the end location of each line to speed up enumeration
        private void CacheLineIndices()
        {
            LineIndices = new List<long>();
            using (var FileHandle = File.OpenRead(this.DatabasePath))
            {
                LineIndices.Add(FileHandle.Position);
                int chr;
                while ((chr = FileHandle.ReadByte()) != -1)
                {
                    if (chr == '\n')
                    {
                        LineIndices.Add(FileHandle.Position);
                    }
                }
            }
            DatabaseLineCount = LineIndices.Count;
        }

        // Binary search because I just don't have 1.5GB of free RAM
        public ClientInfoObject ResolveLocation(ClientInfoObject client)
        {
            int floor = 0;
            int ceiling = this.DatabaseLineCount - 1;
            if (ceiling > floor)
            {
                int DecimalAddress = IPStringToDecimal(client.address);
                while (true)
                {
                    int middle = (int)Math.Floor((float)(floor + ceiling) / 2);
                    String DatabaseLine = String.Empty;
                    using (var FileHandle = File.OpenRead(DatabasePath))
                    {
                        // Use the index cache to avoid repeatedly iterating the entire database
                        FileHandle.Position = LineIndices[middle];
                        using (StreamReader LineReader = new StreamReader(FileHandle))
                        {
                            DatabaseLine = LineReader.ReadLine();
                        }
                    }
                    DatabaseInfoObject AddressInfo = new DatabaseInfoObject(DatabaseLine);
                    if (DecimalAddress > AddressInfo.AddressFloor && DecimalAddress < AddressInfo.AddressCeiling)
                    {
                        // Search succeeded
                        client.SetAddressInfo(AddressInfo);
                        break;
                    }
                    // Continue searching
                    else if (DecimalAddress > AddressInfo.AddressCeiling)
                    {
                        floor = middle + 1;
                    }
                    else if (DecimalAddress < AddressInfo.AddressFloor)
                    {
                        ceiling = middle - 1;
                    }
                    else
                    {
                        // Search failed
                        break;
                    }
                }
            }
            return client;
        }

        public AddressResolver(String DatabasePath)
        {
            if (File.Exists(DatabasePath))
            {
                this.DatabasePath = DatabasePath;
                this.CacheLineIndices();
            }
            else
            {
                String error = String.Format("[!] Database not found: {0}", DatabasePath);
                Console.WriteLine(error);
                throw new FileNotFoundException(error);
            }
        }
    }

    class BulkAddressLocator
    {
        private bool active;

        private int ParserTimeout;
        private int ResolverTimeout;
        private int DefaultTimeoutValue;

        private Thread ParsingThread;
        private Thread ResolverThread;

        private ThreadedLineReader FileProcessor;
        private AddressResolver GeolocationDatabase;
        private ConcurrentQueue<String> ParserQueue;
        private ConcurrentQueue<ClientInfoObject> ResolverQueue;

        public ThreadedConsoleInterface IConsole;

        public delegate void AddressResolvedEventHandler(ClientInfoObject client);
        public AddressResolvedEventHandler AddressResolvedEventSubscription;

        private void LineReadEventHandler(String line, bool IsException = false)
        {
            if (IsException)
            {
                HandleException(line);
            }
            ParserQueue.Enqueue(line);
        }

        private void HandleException(String ErrorMessage)
        {
            IConsole.StopWriting();
            Console.WriteLine("[!] Sorry, this application has encountered an exception and needs to close.\nDetails: {0}", ErrorMessage);
            Environment.Exit(1);
        }

        private void AddressParsingThread()
        {
            int TimeoutValue = DefaultTimeoutValue * 4; // Allow for quarter-second sleep time
            ParserTimeout = TimeoutValue;
            while (active && ParserTimeout > 0)
            {
                if (ParserQueue.Count > 0)
                {
                    bool DequeueSuccessful = false;
                    String message = String.Empty;
                    while (!(DequeueSuccessful))
                    {
                        DequeueSuccessful = ParserQueue.TryDequeue(out message);
                    }
                    // Regular expression matches an IPv4 address and optional port number, separated with a :
                    Match addrmatch = Regex.Match(message, @"(([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\.){3}([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])([:]\d{0,6}){0,1}");
                    if (addrmatch.Success)
                    {
                        String portnumber = "80";
                        String address = addrmatch.Groups[0].Value;
                        IConsole.WriteLine(String.Format("[+] Identified IP Address: {0}", address));
                        if (address.Contains(":")) // Check if we need a non-standard port number
                        {
                            portnumber = address.Split(':')[1];
                            address = address.Split(':')[0];
                        }
                        else if (message.Contains("https"))
                        {
                            portnumber = "443"; // Just in case we have any HTTPS URLs
                        }
                        // Queue for location resolution
                        ResolverQueue.Enqueue(new ClientInfoObject(address, message, portnumber));
                    }
                    ParserTimeout = TimeoutValue; // Reset the timeout counter every time we process a message.
                }
                else
                {
                    ParserTimeout--;
                    Thread.Sleep(250);
                }
            }
            IConsole.PauseClearResume();
        }

        private void LocationResolvingThread()
        {
            int TimeoutValue = DefaultTimeoutValue * 4; // Allow for quarter-second sleep time
            ResolverTimeout = TimeoutValue;
            while (active && ResolverTimeout > 0)
            {
                if (ResolverQueue.Count > 0)
                {
                    ClientInfoObject client = null;
                    bool DequeueSuccessful = false;
                    while (!(DequeueSuccessful))
                    {
                        DequeueSuccessful = ResolverQueue.TryDequeue(out client);
                    }
                    client = GeolocationDatabase.ResolveLocation(client);
                    IConsole.WriteLine(String.Format("[+] Address resolved: {0} -> {1}, {2}", client.address, client.City, client.Country));
                    ResolverTimeout = DefaultTimeoutValue; // Reset the timeout counter
                    if(this.AddressResolvedEventSubscription != null)
                    {
                        this.AddressResolvedEventSubscription(client);
                    }
                }
                else
                {
                    ResolverTimeout--;
                    Thread.Sleep(250);
                }
            }
            IConsole.PauseClearResume();
            IConsole.WriteLine("[+] Initialized successfully.");
        }

        public void ProcessUntilCompleted()
        {
            active = true;
            FileProcessor.LineReadEvent += LineReadEventHandler;
            IConsole.StartWriting();
            IConsole.WriteLine("[+] Loading geolocation database... ");
            GeolocationDatabase = new AddressResolver(String.Join(@"\", new String[] { AppDomain.CurrentDomain.BaseDirectory + "data", "IP_DB6" }));
            FileProcessor.StartReading();
            ParsingThread.Start();
            ResolverThread.Start();
        }

        public BulkAddressLocator(String FilePath)
        {
            active = false;
            ParserTimeout = 0;
            ResolverTimeout = 0;
            DefaultTimeoutValue = 3;

            ParserQueue = new ConcurrentQueue<string>();
            ResolverQueue = new ConcurrentQueue<ClientInfoObject>();

            IConsole = new ThreadedConsoleInterface();
            FileProcessor = new ThreadedLineReader(FilePath);
            ParsingThread = new Thread(new ThreadStart(AddressParsingThread));
            ResolverThread = new Thread(new ThreadStart(LocationResolvingThread));
        }
    }
}