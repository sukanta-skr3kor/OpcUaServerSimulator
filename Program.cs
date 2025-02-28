//*********************************************************************************************
//* File             :   Program.cs
//* Author           :   Sukanta Kumar
//* Date             :   19/2/2025
//* Description      :   Initial version
//* Version          :   1.0
//*-------------------------------------------------------------------------------------------
//* dd-MMM-yyyy	: Version 1.x, Changed By : xxx
//*
//*                 - 1)
//*                 - 2)
//*                 - 3)
//*                 - 4)
//*
//*********************************************************************************************

namespace OPCUA.Server.Simulator;

/// <summary>
/// Start of server
/// </summary>
internal sealed class Program
{
    /// <summary>
    /// Server entry
    /// </summary>
    /// <param name="args">The args<see cref="string[]"/></param>
    /// <returns>The <see cref="Task"/></returns>
    internal static async Task Main(string[] args)
    {
        Console.WriteLine(@"   
        ____  ___  _____  __  _____     ____                       _____            __     __          
       / __ \/ _ \/ ___/ / / / / _ |   / __/__ _____  _____ ____  / __(_)_ _  __ __/ /__ _/ /____  ____
      / /_/ / ___/ /__  / /_/ / __ |  _\ \/ -_) __/ |/ / -_) __/ _\ \/ /  ' \/ // / / _ `/ __/ _ \/ __/
      \____/_/   \___/  \____/_/ |_| /___/\__/_/  |___/\__/_/   /___/_/_/_/_/\_,_/_/\_,_/\__/\___/_/   
");

        // Build configuration
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        // Bind AppSettings section
        var appSettings = config.GetSection("AppSettings").Get<AppSettings>();

        StringCollection opcUaServerBaseAddres = new StringCollection()
        {
            "opc.tcp://localhost:4840/SimulationOpcUaServer"//default base address,if not defined in appsettings file
        };

        //Opc ua base address
        if (!string.IsNullOrEmpty(appSettings?.OpcUaServerBaseAddress))
        {
            opcUaServerBaseAddres = new StringCollection() { appSettings?.OpcUaServerBaseAddress };
        }

        int updateIntervalMs = 1000;//default 1 sec, if not defined in appsettings file
        //Tag values update inteval
        if (appSettings?.UpdateIntervalMs != null && !(appSettings?.UpdateIntervalMs <= 0))
        {
            updateIntervalMs = appSettings.UpdateIntervalMs;
        }

        // Create ApplicationInstance
        var application = new ApplicationInstance
        {
            ApplicationName = "SimulationOpcUaServer",
            ApplicationType = ApplicationType.Server,//it's a server
            ConfigSectionName = "Opc.Ua.Server" // Optional if using App.config
        };

        // Load Application Configuration
        var appConfig = new ApplicationConfiguration()
        {
            ApplicationName = "Simulation.OpcUaServer",
            ApplicationUri = $"urn:{Utils.GetHostName()}:SimulationOpcUaServer",
            ApplicationType = ApplicationType.Server,
            SecurityConfiguration = new SecurityConfiguration
            {
                ApplicationCertificate = new CertificateIdentifier
                {
                    StoreType = "Directory",
                    StorePath = "pki/own",
                    SubjectName = "CN=SimulationOpcUaServer, O=xyz, C=US"
                },
                TrustedPeerCertificates = new CertificateTrustList
                {
                    StoreType = "Directory",
                    StorePath = "pki/trusted"
                },
                RejectedCertificateStore = new CertificateTrustList
                {
                    StoreType = "Directory",
                    StorePath = "pki/rejected"
                },
                AutoAcceptUntrustedCertificates = true,  // Set to false in production(In simulator case we can ignore)
                AddAppCertToTrustedStore = true
            },
            ServerConfiguration = new ServerConfiguration
            {
                BaseAddresses = opcUaServerBaseAddres, 

                SecurityPolicies = new ServerSecurityPolicyCollection
                {
                    new ServerSecurityPolicy
                    {
                        SecurityMode = MessageSecurityMode.None,
                        SecurityPolicyUri = SecurityPolicies.None
                    },
                    new ServerSecurityPolicy
                    {
                        SecurityMode = MessageSecurityMode.SignAndEncrypt,
                        SecurityPolicyUri = SecurityPolicies.Basic256Sha256
                    }
                },

                UserTokenPolicies = new UserTokenPolicyCollection
                {
                    new UserTokenPolicy(UserTokenType.Anonymous),
                    new UserTokenPolicy(UserTokenType.UserName)
                },

                MinRequestThreadCount = 5,
                MaxRequestThreadCount = 100,
                MaxSessionCount = 100,//Max 100 sessions are allowed
                DiagnosticsEnabled = true
            },
            TransportQuotas = new TransportQuotas
            {
                OperationTimeout = 15000,
                MaxStringLength = 1048576
            },
            CertificateValidator = new CertificateValidator()
        };

        // Assign configuration to the application instance
        application.ApplicationConfiguration = appConfig;

        // Validate Application Configuration
        await appConfig.Validate(ApplicationType.Server);

        // Check and create application certificate using ApplicationInstance
        bool haveAppCertificate = await application.CheckApplicationInstanceCertificates(false, 2048);

        if (!haveAppCertificate)
        {
            throw new Exception($"Application instance certificate is invalid or missing. Cannot start OPC UA server.");
        }

        // Assign certificate validator
        appConfig.CertificateValidator.CertificateValidation += CertificateValidationHandler;

        // Start OPC UA Server using the ApplicationInstance
        var server = new SimulationOpcUaServer();
        await application.Start(server);

        // Access NodeManager and Load XML Hierarchy
        var nodeManager = server.CurrentInstance.NodeManager
                              .NodeManagers
                              .OfType<OpcUaNodeManager>()
                              .FirstOrDefault();

        if (null == nodeManager)
        {
            Console.WriteLine("NodeManager not found.");
            return;
        }

        // Load hierarchy from XML
        string xmlFilePath = "data/opcnodes.xml"; // Path to opcnodes XML file, where all heirarchy and tags are to be defined

        if (File.Exists(xmlFilePath))
        {
            nodeManager.LoadHierarchyFromXml(xmlFilePath);
            Console.WriteLine("Node creation completed.");
        }
        else
        {
            Console.WriteLine($"opcnodes.xml file not found at path: {xmlFilePath}");
            return;
        }

        // Start a long-running background task for dynamic updates of tag values
        Task.Factory.StartNew(() => nodeManager.StartDynamicUpdatesAsync(updateIntervalMs), TaskCreationOptions.LongRunning);

        Console.WriteLine($"Server url '{appConfig.ServerConfiguration.BaseAddresses[0]}'");
        Console.WriteLine("Server is running... Press Ctrl+C to stop.");

        await Task.Delay(-1); // Keeps the app running indefinitely(Don't remove this line, else the app will close)
    }

    /// <summary>
    /// CertificateValidationHandler
    /// </summary>
    /// <param name="validator"></param>
    /// <param name="e"></param>
    private static void CertificateValidationHandler(CertificateValidator validator, CertificateValidationEventArgs e)
    {
        if (e.Error.StatusCode == StatusCodes.BadCertificateUntrusted)
        {
            Console.WriteLine($"Untrusted Certificate: {e.Certificate.Subject}");
            e.Accept = true; // Accept untrusted certificates for development/testing purpose only
        }
    }
}
