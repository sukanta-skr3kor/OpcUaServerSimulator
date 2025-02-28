//*********************************************************************************************
//* File             :   OpcUaNodeManager.cs
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
/// OpcUa NodeManager 
/// Class to construct the opcua nodes
/// </summary>
public class OpcUaNodeManager : CustomNodeManager2
{
    /// <summary>
    /// Defines the _namespaceIndex
    /// </summary>
    private ushort _namespaceIndex;

    /// <summary>
    /// Defines the _rootFolder
    /// </summary>
    private BaseObjectState _rootFolder;

    /// <summary>
    /// Defines the _variableNodes
    /// </summary>
    private Dictionary<Tuple<string, bool, int>, BaseDataVariableState> _variableNodes = new();

    /// <summary>
    /// Defines the _random
    /// </summary>
    private readonly Random _random = new Random();

    /// <summary>
    /// Integer counter
    /// </summary>
    private int _counter = 0;

    /// <summary>
    /// Initializes a new instance of OpcUaNodeManager
    /// </summary>
    /// <param name="server"></param>
    /// <param name="config"></param>
    public OpcUaNodeManager(IServerInternal server, ApplicationConfiguration config)
        : base(server, config, "http://xyz.com/SimulationOpcUaServer")
    {
        SystemContext.NodeIdFactory = this;
    }

    /// <summary>
    /// Create OPCUA AddressSpace
    /// </summary>
    /// <param name="externalReferences">The externalReferences<see cref="IDictionary{NodeId, IList{IReference}}"/></param>
    public override void CreateAddressSpace(IDictionary<NodeId, IList<IReference>> externalReferences)
    {
        _namespaceIndex = Server.NamespaceUris.GetIndexOrAppend("http://xyz.com/SimulationOpcUaServer");

        // Create root folder
        _rootFolder = new BaseObjectState(null)
        {
            SymbolicName = "Demo",
            NodeId = new NodeId("Demo", _namespaceIndex),
            BrowseName = new QualifiedName("Demo", _namespaceIndex),
            DisplayName = "Demo",
            TypeDefinitionId = ObjectTypeIds.FolderType
        };

        AddPredefinedNode(SystemContext, _rootFolder);

        // Link root to Objects folder
        if (!externalReferences.TryGetValue(ObjectIds.ObjectsFolder, out IList<IReference> references))
        {
            externalReferences[ObjectIds.ObjectsFolder] = references = new List<IReference>();
        }

        references.Add(new NodeStateReference(ReferenceTypeIds.Organizes, false, _rootFolder.NodeId));
        _rootFolder.AddReference(ReferenceTypeIds.Organizes, true, ObjectIds.ObjectsFolder);
    }

    /// <summary>
    /// Load Hierarchy From opcnodes.xml file
    /// </summary>
    /// <param name="xmlFilePath">The xmlFilePath<see cref="string"/></param>
    public void LoadHierarchyFromXml(string xmlFilePath)
    {
        XmlDocument doc = new XmlDocument();
        doc.Load(xmlFilePath);

        XmlNode rootNode = doc.SelectSingleNode("/OpcUaHierarchy");

        if (null != rootNode)
        {
            foreach (XmlNode folderNode in rootNode.SelectNodes("Folder"))
            {
                AddFolderRecursively(folderNode, _rootFolder);
            }
        }
    }

    // Recursive method to add folders and variables
    /// <summary>
    /// Add Folder Recursively
    /// </summary>
    /// <param name="folderNode">The folderNode<see cref="XmlNode"/></param>
    /// <param name="parentFolder">The parentFolder<see cref="BaseObjectState"/></param>
    private void AddFolderRecursively(XmlNode folderNode, BaseObjectState parentFolder)
    {
        string folderName = folderNode.Attributes["Name"]?.Value ?? "UnnamedFolder";

        var folder = new BaseObjectState(parentFolder)
        {
            SymbolicName = folderName,
            NodeId = new NodeId(folderName, _namespaceIndex),
            BrowseName = new QualifiedName(folderName, _namespaceIndex),
            DisplayName = folderName,
            TypeDefinitionId = ObjectTypeIds.FolderType
        };

        parentFolder.AddChild(folder);
        AddPredefinedNode(SystemContext, folder);

        // Add variables
        foreach (XmlNode variableNode in folderNode.SelectNodes("Variable"))
        {
            AddVariable(variableNode, folder);
        }

        // Recursively process subfolders
        foreach (XmlNode subFolderNode in folderNode.SelectNodes("Folder"))
        {
            AddFolderRecursively(subFolderNode, folder);
        }
    }

    /// <summary>
    /// AddVariable
    /// </summary>
    /// <param name="variableNode">The variableNode<see cref="XmlNode"/></param>
    /// <param name="parentFolder">The parentFolder<see cref="BaseObjectState"/></param>
    private void AddVariable(XmlNode variableNode, BaseObjectState parentFolder)
    {
        string varName = variableNode.Attributes["Name"]?.Value ?? "UnnamedVariable";
        string dataTypeStr = variableNode.Attributes["DataType"]?.Value ?? "String";
        string initialValueStr = variableNode.Attributes["InitialValue"]?.Value ?? "";
        string displayName = variableNode.Attributes["DisplayName"]?.Value ?? varName;
        bool isStatic = variableNode.Attributes["IsStatic"]?.Value.ToLowerInvariant() == "true";
        string maxValueStr = variableNode.Attributes["MaxValue"]?.Value ?? "100";
        int.TryParse(maxValueStr, out int maxValue);

        // Parse OPC UA BuiltInType
        if (!Enum.TryParse(dataTypeStr, true, out BuiltInType dataType))
        {
            Console.WriteLine($"Unsupported data type: {dataTypeStr}. Defaulting to String.");
            dataType = BuiltInType.String;
        }

        Type systemType = TypeInfo.GetSystemType((uint)dataType, null);

        if (null == systemType)
        {
            Console.WriteLine($"Failed to get system type for: {dataTypeStr}, defaulting to string.");
            systemType = typeof(string);
        }

        // Safely convert initial value
        object initialValue = GetConvertedValue(initialValueStr, systemType);

        Tuple<string, bool, int> nodeData = new(varName, isStatic, maxValue);

        if (Guid.TryParse(varName, out Guid nodeGuid))//check if GUID nodes are there
        {
            var variable = new BaseDataVariableState(parentFolder)
            {
                NodeId = new NodeId(nodeGuid, _namespaceIndex),
                BrowseName = new QualifiedName(displayName, _namespaceIndex),
                DisplayName = displayName,
                TypeDefinitionId = VariableTypeIds.BaseDataVariableType,
                DataType = (uint)dataType,
                ValueRank = ValueRanks.Scalar,
                Value = initialValue,
                StatusCode = StatusCodes.Good,
                Timestamp = DateTime.UtcNow,
                AccessLevel = AccessLevels.CurrentReadOrWrite,
                UserAccessLevel = AccessLevels.CurrentReadOrWrite
            };

            parentFolder.AddChild(variable);
            AddPredefinedNode(SystemContext, variable);

            _variableNodes.Add(nodeData, variable);
        }
        else
        {
            var variable = new BaseDataVariableState(parentFolder)
            {
                NodeId = new NodeId(varName, _namespaceIndex),
                BrowseName = new QualifiedName(varName, _namespaceIndex),
                DisplayName = displayName,
                TypeDefinitionId = VariableTypeIds.BaseDataVariableType,
                DataType = (uint)dataType,
                ValueRank = ValueRanks.Scalar,
                Value = initialValue,
                StatusCode = StatusCodes.Good,
                Timestamp = DateTime.UtcNow,
                AccessLevel = AccessLevels.CurrentReadOrWrite,
                UserAccessLevel = AccessLevels.CurrentReadOrWrite
            };
            parentFolder.AddChild(variable);
            AddPredefinedNode(SystemContext, variable);

            _variableNodes.Add(nodeData, variable);
        }
    }

    /// <summary>
    /// Get Converted Value
    /// </summary>
    /// <param name="valueStr">The valueStr<see cref="string"/></param>
    /// <param name="targetType">The targetType<see cref="Type"/></param>
    /// <returns>The <see cref="object"/></returns>
    private static object GetConvertedValue(string valueStr, Type targetType)
    {
        try
        {
            if (targetType == typeof(bool))
                return bool.Parse(valueStr);

            if (targetType == typeof(int))
                return int.Parse(valueStr);

            if (targetType == typeof(double))
                return double.Parse(valueStr);

            if (targetType == typeof(float))
                return float.Parse(valueStr);

            if (targetType == typeof(DateTime))
                return DateTime.Parse(valueStr);

            if (targetType == typeof(string))
                return valueStr;

            // Handle unsupported or custom types
            return valueStr;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to convert '{valueStr}' to {targetType?.Name ?? "Unknown"}. Defaulting to null. Error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Update variables dynamically based on their type
    /// Default update inetval is 1 second if not set
    /// </summary>
    /// <param name="updateIntervalMs">The updateIntervalMs<see cref="int"/></param>
    /// <returns>The <see cref="Task"/></returns>
    public async Task StartDynamicUpdatesAsync(int updateIntervalMs = 1000)
    {
        BaseDataVariableState? baseDataVariable = null;

        while (true)
        {
            try
            {
                foreach (var kvp in _variableNodes)
                {
                    var variable = kvp.Value;

                    if (!kvp.Key.Item2)
                    {
                        int maxValue = kvp.Key.Item3;
                        object newValue = GenerateRandomValue(variable.DataType, maxValue);
                        variable.Value = newValue;
                    }
                    variable.Timestamp = DateTime.UtcNow;
                    variable.ClearChangeMasks(SystemContext, false);

                    baseDataVariable = variable;
                }
            }
            catch (Exception exp)
            {
                if (null != baseDataVariable)
                {
                    baseDataVariable.Value = null;
                    baseDataVariable.StatusCode = StatusCodes.Bad;
                    baseDataVariable.Timestamp = DateTime.UtcNow;
                }

                Console.WriteLine(exp.Message);
            }

            await Task.Delay(updateIntervalMs);
        }
    }
    /// <summary>
    ///  Generate Random Value for the nodes
    /// </summary>
    /// <param name="dataType"></param>
    /// <param name="maxValue"></param>
    /// <returns></returns>
    private object GenerateRandomValue(NodeId dataType, int maxValue = 100)
    {
        if (dataType == DataTypeIds.Integer)
        {
            var val = ++_counter;

            if (val > maxValue)
            {
                _counter = 1;
                return _counter;
            }

            return val;
        }

        if (dataType == DataTypeIds.Double)
        {
            var val = Math.Round(_random.NextDouble() * 100, 2);

            if (val > maxValue)
            {
                val = maxValue;
                return val;
            }

            return val;
        }

        if (dataType == DataTypeIds.Boolean)
            return _random.Next(0, 2) == 1;

        if (dataType == DataTypeIds.Int32)
            return _random.Next(0, maxValue);

        if (dataType == DataTypeIds.String)
            return "Invalid_" + _random.Next(0, maxValue);

        return null;
    }
}
