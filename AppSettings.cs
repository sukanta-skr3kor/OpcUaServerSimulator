//*********************************************************************************************
//* File             :   AppSettings.cs
//* Author           :   Sukanta Rout
//* Date             :   25/2/2025
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
public class AppSettings
{
    public int UpdateIntervalMs { get; set; }
    public required string OpcUaServerBaseAddress { get; set; }
}
