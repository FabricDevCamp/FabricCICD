using Microsoft.Fabric.Api.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


public class PlatformFileConfig {
  public string version { get; set; }
  public string logicalId { get; set; }
}

public class PlatformFileMetadata {
  public string type { get; set; }
  public string displayName { get; set; }
}

public class FabricPlatformFile {
  public PlatformFileMetadata metadata { get; set; }
  public PlatformFileConfig config { get; set; }
}

public class ReportDefinitionFile {
  public string version { get; set; }
  public DatasetReference datasetReference { get; set; }
}

public class DatasetReference {
  public ByPathReference byPath { get; set; }
  public ByConnectionReference byConnection { get; set; }
}

public class ByPathReference {
  public string path { get; set; }
}

public class ByConnectionReference {
  public string connectionString { get; set; }
  public object pbiServiceModelId { get; set; }
  public string pbiModelVirtualServerName { get; set; }
  public string pbiModelDatabaseName { get; set; }
  public string name { get; set; }
  public string connectionType { get; set; }
}

public class DeploymentItemSet {
  public List<DeploymentItem> DeploymentItems { get; set; }
  public List<string> ItemNames { 
    get {
    var itemNames = new List<string>();
      foreach (var item in DeploymentItems) {
        itemNames.Add(item.ItemName);
      }
      return itemNames;
    } 
  }
}

public class DeploymentItem {
  public string DisplayName { get; set; }
  public string Type { get; set; }
  public string ItemName { get { return $"{DisplayName}.{Type}"; } }
  public ItemDefinition Definition { get; set; }
}

public class DeploymentItemFile {
  public string Path { get; set; }
  public string Content { get; set; }
}

public class GitRepoFile {
  public string FullPath { get; set; }
  public string Content { get; set; }

  public string ItemName { 
    get {
      return FullPath.Contains("/") ? FullPath.Substring(0, FullPath.IndexOf("/")) : FullPath; 
    }
  }
  
  public string Path {
    get {
      int firstSlash = FullPath.IndexOf("/");
      if (firstSlash == -1) {
        return FullPath;
      }
      else {
        int start = firstSlash + 1;
        int length = FullPath.Length - start;
        return FullPath.Substring(start, length);

      }
    }
  }

  public string FileName {
    get {
      return FullPath.Substring(FullPath.LastIndexOf('/') + 1);
    }
  }

}