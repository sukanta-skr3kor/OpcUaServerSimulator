//*********************************************************************************************
//* File             :   SimulationOpcUaServer.cs
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
/// SimulationOpcUaServer 
/// The server
/// </summary>
public class SimulationOpcUaServer : StandardServer
{
    /// <summary>
    /// CreateMasterNodeManager
    /// </summary>
    /// <param name="server">The server<see cref="IServerInternal"/></param>
    /// <param name="config">The config<see cref="ApplicationConfiguration"/></param>
    /// <returns>The <see cref="MasterNodeManager"/></returns>
    protected override MasterNodeManager CreateMasterNodeManager(IServerInternal server, ApplicationConfiguration config)
    {
        var nodeManagers = new List<INodeManager>
        {
            new OpcUaNodeManager(server, config)
        };
        return new MasterNodeManager(server, config, null, nodeManagers.ToArray());
    }

    /// <summary>
    /// OnServerStarted
    /// </summary>
    /// <param name="server">The server<see cref="IServerInternal"/></param>
    protected override void OnServerStarted(IServerInternal server)
    {
        base.OnServerStarted(server);

        Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        Console.WriteLine($"Starting OpcUa Simulation Server v{version.Major}.{version.Minor}.{version.Build}, please wait...");
        Thread.Sleep(500);
    }
}
