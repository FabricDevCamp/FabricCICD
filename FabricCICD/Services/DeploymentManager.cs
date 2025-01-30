using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Fabric.Api.Core.Models;

public class DeploymentManager {

  public static void DeployWorkspaceWithPowerBiSolution(string WorkspaceName) {

    string semanticModelName = "Product Sales Imported";

    AppLogger.LogSolution("Deploy Solution with Imported Sales Model and Report");

    AppLogger.LogStep($"Creating new workspace named [{WorkspaceName}]");
    var workspace = FabricRestApi.CreateWorkspace(WorkspaceName);
    AppLogger.LogSubstep($"New workspace created with Id of [{workspace.Id}]");

    AppLogger.LogStep($"Creating new import-mode semantic model named [{semanticModelName}]");
    var modelCreateRequest =
      ItemDefinitionFactory.GetImportedSalesModelCreateRequest(semanticModelName);
    var model = FabricRestApi.CreateItem(workspace.Id, modelCreateRequest);
    AppLogger.LogSubstep($"New semantic model created with Id of [{model.Id.Value.ToString()}]");

    AppLogger.LogSubstep($"Creating new connection for semantic model");
    var url = PowerBiRestApi.GetWebDatasourceUrl(workspace.Id, model.Id.Value);
    var connection = FabricRestApi.CreateAnonymousWebConnection(url);

    AppLogger.LogSubstep($"Binding connection to semantic model");
    PowerBiRestApi.BindSemanticModelToConnection(workspace.Id, model.Id.Value, connection.Id);

    AppLogger.LogSubstep($"Refreshing semantic model");
    PowerBiRestApi.RefreshDataset(workspace.Id, model.Id.Value);

    AppLogger.LogStep($"Creating new report named [{semanticModelName}]");

    var createRequestReport =
      ItemDefinitionFactory.GetSalesReportCreateRequest(model.Id.Value, semanticModelName);

    var report = FabricRestApi.CreateItem(workspace.Id, createRequestReport);

    AppLogger.LogSubstep($"New report created with Id of [{report.Id.Value.ToString()}]");

    AppLogger.LogStep("Solution deployment complete");

    AppLogger.LogOperationStart("Press ENTER to open workspace in the browser");
    Console.ReadLine();
    AppLogger.LogOperationComplete();

    OpenWorkspaceInBrowser(workspace.Id);
  }

  public static void DeployWorkspaceWithLakehouseSolution(string WorkspaceName) {

    string lakehouseName = "sales";
    string notebookName = "Create Lakehouse Tables";
    string semanticModelName = "Product Sales DirectLake";

    AppLogger.LogSolution("Deploy Solution with Lakehouse, Notebook, DirectLake Semantic Model and Report");

    AppLogger.LogStep($"Creating new workspace named [{WorkspaceName}]");
    var workspace = FabricRestApi.CreateWorkspace(WorkspaceName, AppSettings.FabricCapacityId);
    AppLogger.LogSubstep($"Workspace created with Id of [{workspace.Id.ToString()}]");

    AppLogger.LogStep($"Creating new lakehouse named [{lakehouseName}]");
    var lakehouse = FabricRestApi.CreateLakehouse(workspace.Id, lakehouseName);
    AppLogger.LogSubstep($"Lakehouse created with Id of [{lakehouse.Id.Value.ToString()}]");

    AppLogger.LogStep($"Creating new notebook named [{notebookName}]");

    string notebookContent = ItemDefinitionFactory.GetTemplateFile(@"Notebooks\CreateLakehouseTables.py");
    var notebookCreateRequest = ItemDefinitionFactory.GetCreateNotebookRequest(workspace.Id, lakehouse, notebookName, notebookContent);
    var notebook = FabricRestApi.CreateItem(workspace.Id, notebookCreateRequest);

    AppLogger.LogSubstep($"Notebook created with Id of [{notebook.Id.Value.ToString()}]");

    AppLogger.LogSubOperationStart($"Running notebook");
    FabricRestApi.RunNotebook(workspace.Id, notebook);
    AppLogger.LogOperationComplete();

    AppLogger.LogStep("Querying lakehouse properties to get SQL endpoint connection info");
    var sqlEndpoint = FabricRestApi.GetSqlEndpointForLakehouse(workspace.Id, lakehouse.Id.Value);
    AppLogger.LogSubstep($"Server: {sqlEndpoint.ConnectionString}");
    AppLogger.LogSubstep($"Database: " + sqlEndpoint.Id);

    AppLogger.LogStep($"Creating new semantic model named [{semanticModelName}]");
    var modelCreateRequest =
      ItemDefinitionFactory.GetDirectLakeSalesModelCreateRequest(semanticModelName, sqlEndpoint.ConnectionString, sqlEndpoint.Id);

    var model = FabricRestApi.CreateItem(workspace.Id, modelCreateRequest);

    AppLogger.LogSubstep($"Semantic model created with Id of [{model.Id.Value.ToString()}]");

    AppLogger.LogSubstep($"Creating SQL connection for semantic model");
    var sqlConnection = FabricRestApi.CreateSqlConnectionWithServicePrincipal(sqlEndpoint.ConnectionString, sqlEndpoint.Id);

    AppLogger.LogSubstep($"Binding SQL connection to semantic model");
    PowerBiRestApi.BindSemanticModelToConnection(workspace.Id, model.Id.Value, sqlConnection.Id);

    AppLogger.LogStep($"Creating new report named [{semanticModelName}]");

    var createRequestReport =
      ItemDefinitionFactory.GetSalesReportCreateRequest(model.Id.Value, semanticModelName);

    var report = FabricRestApi.CreateItem(workspace.Id, createRequestReport);
    AppLogger.LogSubstep($"Report created with Id of [{report.Id.Value.ToString()}]");
  
    AppLogger.LogStep("Customer tenant provisioning complete");

    AppLogger.LogOperationStart("Press ENTER to open workspace in the browser");
    AppLogger.LogOperationComplete();

    OpenWorkspaceInBrowser(workspace.Id);

  }

  public static void DeploySolutionFromProjectTemplate(string ProjectName, string TargetWorkspaceName) {

    AppLogger.LogStep($"Deploying Project[{ProjectName}] project template to new workspace named [{TargetWorkspaceName}]");

    var deploymentItemSet = GetDeploymentItemSetFromGitRepo(ProjectName);

    var sourceWorkspace = FabricRestApi.GetWorkspaceByName(ProjectName);
    var sourceWorkspaceItems = FabricRestApi.GetWorkspaceItems(sourceWorkspace.Id);

    AppLogger.LogStep($"Creating new workspace named [{TargetWorkspaceName}]");

    var targetWorkspace = FabricRestApi.CreateWorkspace(TargetWorkspaceName);

    AppLogger.LogSubstep($"Workspace created with id of {targetWorkspace.Id.ToString()}");


    AppLogger.LogStep($"Deploying Workspace Items");

    var lakehouseNames = new List<string>();
    var lakehouseIdRedirects = new Dictionary<string, string>();
    var lakehouseSqlEndpointRedirects = new Dictionary<string, string>();

    string sqlEndPointServer = null;
    string sqlEndPointDatabase = null;

    lakehouseIdRedirects.Add(sourceWorkspace.Id.ToString(), targetWorkspace.Id.ToString());

    var lakehouses = deploymentItemSet.DeploymentItems.Where(item => item.Type == "Lakehouse");

    foreach (var lakehouse in lakehouses) {
      var sourceLakehouse = sourceWorkspaceItems.FirstOrDefault(item => (item.Type == "Lakehouse" &&
                                                                         item.DisplayName.Equals(lakehouse.DisplayName)));

      Guid sourceLakehouseId = sourceWorkspaceItems.FirstOrDefault(item => item.Type == "Lakehouse").Id.Value;
      var sourceLakehouseSqlEndpoint = FabricRestApi.GetSqlEndpointForLakehouse(sourceWorkspace.Id, sourceLakehouse.Id.Value);

      AppLogger.LogSubstep($"Creating [{lakehouse.ItemName}]");
      var targetLakehouse = FabricRestApi.CreateLakehouse(targetWorkspace.Id, lakehouse.DisplayName);

      var targetLakehouseSqlEndpoint = FabricRestApi.GetSqlEndpointForLakehouse(targetWorkspace.Id, targetLakehouse.Id.Value);
      
      lakehouseNames.Add(targetLakehouse.DisplayName);
      lakehouseIdRedirects.Add(sourceLakehouse.Id.Value.ToString(), targetLakehouse.Id.Value.ToString());

      if (!lakehouseSqlEndpointRedirects.Keys.Contains(sourceLakehouseSqlEndpoint.ConnectionString)) {
        lakehouseSqlEndpointRedirects.Add(sourceLakehouseSqlEndpoint.ConnectionString, targetLakehouseSqlEndpoint.ConnectionString);
      }

      lakehouseSqlEndpointRedirects.Add(sourceLakehouseSqlEndpoint.Id, targetLakehouseSqlEndpoint.Id);

      // temp
      sqlEndPointServer = targetLakehouseSqlEndpoint.ConnectionString;
      sqlEndPointDatabase = targetLakehouseSqlEndpoint.Id;
    }

    var notebooks = deploymentItemSet.DeploymentItems.Where(item => item.Type == "Notebook");

    foreach (var notebook in notebooks) {
      AppLogger.LogSubstep($"Creating [{notebook.ItemName}]");
      var createRequest = new CreateItemRequest(notebook.DisplayName, notebook.Type);
      createRequest.Definition = FabricRestApi.UpdateItemDefinitionPart(notebook.Definition, "notebook-content.py", lakehouseIdRedirects);
      var targetNotebook = FabricRestApi.CreateItem(targetWorkspace.Id, createRequest);

      AppLogger.LogSubOperationStart($"Running  [{notebook.ItemName}]");
      FabricRestApi.RunNotebook(targetWorkspace.Id, targetNotebook);
      AppLogger.LogOperationComplete();

    }

    var models = deploymentItemSet.DeploymentItems.Where(item => item.Type == "SemanticModel");

    // create dictionary to track Semantic Model Id mapping to rebind repots to correct cloned
    var semanticModelRedirects = new Dictionary<string, string>();

    foreach (var model in models) {

      // ignore default semantic model for lakehouse
      if (!lakehouseNames.Contains(model.DisplayName)) {

        var sourceModel = sourceWorkspaceItems.FirstOrDefault(item => (item.Type == "SemanticModel" &&
                                                                       item.DisplayName.Equals(model.DisplayName)));

        // update expressions.tmdl with SQL endpoint info for lakehouse in feature workspace
        var modelDefinition = FabricRestApi.UpdateItemDefinitionPart(model.Definition, "definition/expressions.tmdl", lakehouseSqlEndpointRedirects);

        // use item definition to create clone in target workspace
        AppLogger.LogSubstep($"Creating [{model.ItemName}]");
        var createRequest = new CreateItemRequest(model.DisplayName, model.Type);
        createRequest.Definition = modelDefinition;        
        var targetModel = FabricRestApi.CreateItem(targetWorkspace.Id, createRequest);

        // track mapping between source semantic model and target semantic model
        semanticModelRedirects.Add(sourceModel.Id.Value.ToString(), targetModel.Id.Value.ToString());

        CreateAndBindSemanticModelConnectons(targetWorkspace.Id, targetModel.Id.Value);

      }

    }

    var reports = deploymentItemSet.DeploymentItems.Where(item => item.Type == "Report");
    foreach (var report in reports) {

      var sourceReport = sourceWorkspaceItems.FirstOrDefault(item => (item.Type == "Report" &&
                                                                      item.DisplayName.Equals(report.DisplayName)));

      // update expressions.tmdl with SQL endpoint info for lakehouse in feature workspace
      var reportDefinition = UpdateReportDefinitionWithRedirection(report.Definition, targetWorkspace.Id);

      // use item definition to create clone in target workspace
      AppLogger.LogSubstep($"Creating [{report.ItemName}]");
      var createRequest = new CreateItemRequest(report.DisplayName, report.Type);
      createRequest.Definition = reportDefinition;
      var targetReport = FabricRestApi.CreateItem(targetWorkspace.Id, createRequest);

    }

    Console.WriteLine();
    Console.WriteLine("Solution deployment complete");
    Console.WriteLine();

    Console.Write("Press ENTER to open workspace in the browser");
    Console.ReadLine();


    OpenWorkspaceInBrowser(targetWorkspace.Id);

  }

  public static void UpdateSolutionFromProjectTemplate(string ProjectName, string TargetWorkspaceName) {

    AppLogger.LogStep($"Updating tenant workspace [{TargetWorkspaceName}] using [{ProjectName}] project template");

    var deploymentItemSet = GetDeploymentItemSetFromGitRepo(ProjectName);

    AppLogger.LogStep($"Processing deployment Updates");

    var sourceWorkspace = FabricRestApi.GetWorkspaceByName(ProjectName);
    var sourceWorkspaceItems = FabricRestApi.GetWorkspaceItems(sourceWorkspace.Id);

    var targetWorkspace = FabricRestApi.GetWorkspaceByName(TargetWorkspaceName);
    var targetWorkspaceItems = FabricRestApi.GetWorkspaceItems(targetWorkspace.Id);

    var lakehouseNames = new List<string>();
    var lakehouseIdRedirects = new Dictionary<string, string>();
    var lakehouseSqlEndpointRedirects = new Dictionary<string, string>();

    string sqlEndPointServer = null;
    string sqlEndPointDatabase = null;

    lakehouseIdRedirects.Add(sourceWorkspace.Id.ToString(), targetWorkspace.Id.ToString());

    // lakehouse processing
    var lakehouses = deploymentItemSet.DeploymentItems.Where(item => item.Type == "Lakehouse");
    foreach (var lakehouse in lakehouses) {

      var sourceLakehouse = sourceWorkspaceItems.FirstOrDefault(item => (item.Type == "Lakehouse" &&
                                                                         item.DisplayName.Equals(lakehouse.DisplayName)));

      Guid sourceLakehouseId = sourceWorkspaceItems.FirstOrDefault(item => item.Type == "Lakehouse").Id.Value;
      var sourceLakehouseSqlEndpoint = FabricRestApi.GetSqlEndpointForLakehouse(sourceWorkspace.Id, sourceLakehouse.Id.Value);

      var targetLakehouse = targetWorkspaceItems.Where(item => (item.DisplayName.Equals(lakehouse.DisplayName) &&
                                                                   (item.Type == lakehouse.Type))).FirstOrDefault();

      if (targetLakehouse != null) {
        // update item - nothing to do for lakehouse        
      }
      else {
        // create item
        AppLogger.LogSubstep($"Creating [{lakehouse.ItemName}]");
        targetLakehouse = FabricRestApi.CreateLakehouse(targetWorkspace.Id, lakehouse.DisplayName);
      }

      var targetLakehouseSqlEndpoint = FabricRestApi.GetSqlEndpointForLakehouse(targetWorkspace.Id, targetLakehouse.Id.Value);

      lakehouseNames.Add(targetLakehouse.DisplayName);
      lakehouseIdRedirects.Add(sourceLakehouse.Id.Value.ToString(), targetLakehouse.Id.Value.ToString());

      if (!lakehouseSqlEndpointRedirects.Keys.Contains(sourceLakehouseSqlEndpoint.ConnectionString)) {
        lakehouseSqlEndpointRedirects.Add(sourceLakehouseSqlEndpoint.ConnectionString, targetLakehouseSqlEndpoint.ConnectionString);
      }
      lakehouseSqlEndpointRedirects.Add(sourceLakehouseSqlEndpoint.Id, targetLakehouseSqlEndpoint.Id);

      // temp
      sqlEndPointServer = targetLakehouseSqlEndpoint.ConnectionString;
      sqlEndPointDatabase = targetLakehouseSqlEndpoint.Id;
    }

    // notebook 
    var notebooks = deploymentItemSet.DeploymentItems.Where(item => item.Type == "Notebook");
    foreach (var notebook in notebooks) {
      var sourceNoteboook = sourceWorkspaceItems.FirstOrDefault(item => (item.Type == "Notebook" &&
                                                                         item.DisplayName.Equals(notebook.DisplayName)));

      var targetNotebook = targetWorkspaceItems.Where(item => (item.DisplayName.Equals(notebook.DisplayName) &&
                                                              (item.Type == notebook.Type))).FirstOrDefault();

      ItemDefinition notebookDefiniton = FabricRestApi.UpdateItemDefinitionPart(notebook.Definition, "notebook-content.py", lakehouseIdRedirects);

      if (targetNotebook != null) {
        // update existing notebook
        AppLogger.LogSubstep($"Updating [{notebook.ItemName}]");
        var updateRequest = new UpdateItemDefinitionRequest(notebookDefiniton);
        FabricRestApi.UpdateItemDefinition(targetWorkspace.Id, targetNotebook.Id.Value, updateRequest);
      }
      else {
        // create item
        AppLogger.LogSubstep($"Creating [{notebook.ItemName}]");
        var createRequest = new CreateItemRequest(notebook.DisplayName, notebook.Type);
        createRequest.Definition = FabricRestApi.UpdateItemDefinitionPart(notebook.Definition, "notebook-content.py", lakehouseIdRedirects);
        targetNotebook = FabricRestApi.CreateItem(targetWorkspace.Id, createRequest);
      }

      // do not run notebooks by default when updating - uncomment to change
      // AppLogger.LogSubOperationStart($"Running notebook");
      // FabricRestApi.RunNotebook(targetWorkspace.Id, targetNotebook);
      // AppLogger.LogOperationComplete();

    }

    var models = deploymentItemSet.DeploymentItems.Where(item => item.Type == "SemanticModel");
    var semanticModelRedirects = new Dictionary<string, string>();
    foreach (var model in models) {

      // ignore default semantic model for lakehouse
      if (!lakehouseNames.Contains(model.DisplayName)) {

        var sourceModel = sourceWorkspaceItems.FirstOrDefault(item => (item.Type == "SemanticModel" &&
                                                                       item.DisplayName == model.DisplayName));

        var targetModel = targetWorkspaceItems.Where(item => (item.Type == model.Type) &&
                                                             (item.DisplayName == model.DisplayName)).FirstOrDefault();

        // update expressions.tmdl with SQL endpoint info for lakehouse in feature workspace
        var modelDefinition = FabricRestApi.UpdateItemDefinitionPart(model.Definition, "definition/expressions.tmdl", lakehouseSqlEndpointRedirects);

        if (targetModel != null) {
          AppLogger.LogSubstep($"Updating [{model.ItemName}]");
          // update existing model
          var updateRequest = new UpdateItemDefinitionRequest(modelDefinition);
          FabricRestApi.UpdateItemDefinition(targetWorkspace.Id, targetModel.Id.Value, updateRequest);
        }
        else {
          AppLogger.LogSubstep($"Creating [{model.ItemName}]");
          var createRequest = new CreateItemRequest(model.DisplayName, model.Type);
          createRequest.Definition = modelDefinition;
          targetModel = FabricRestApi.CreateItem(targetWorkspace.Id, createRequest);
          AppLogger.LogSubstep($"Creating connection for [{model.ItemName}]");

          var sqlConnection = FabricRestApi.CreateSqlConnectionWithServicePrincipal(sqlEndPointServer, sqlEndPointDatabase);
          AppLogger.LogSubstep($"Binding connection to [{model.ItemName}]");
          PowerBiRestApi.BindSemanticModelToConnection(targetWorkspace.Id, targetModel.Id.Value, sqlConnection.Id);
          Thread.Sleep(15000);

          AppLogger.LogSubstep($"Refreshing [{model.ItemName}]");
          PowerBiRestApi.RefreshDataset(targetWorkspace.Id, targetModel.Id.Value);
        }

        // track mapping between source semantic model and target semantic model
        semanticModelRedirects.Add(sourceModel.Id.Value.ToString(), targetModel.Id.Value.ToString());

      }

    }

    // reports
    var reports = deploymentItemSet.DeploymentItems.Where(item => item.Type == "Report");
    foreach (var report in reports) {

      var sourceReport = sourceWorkspaceItems.FirstOrDefault(item => (item.Type == "Report" &&
                                                                      item.DisplayName.Equals(report.DisplayName)));

      var targetReport = targetWorkspaceItems.FirstOrDefault(item => (item.Type == "Report" &&
                                                                   item.DisplayName.Equals(report.DisplayName)));


      // update expressions.tmdl with SQL endpoint info for lakehouse in feature workspace
      var reportDefinition = UpdateReportDefinitionWithRedirection(report.Definition, targetWorkspace.Id);


      if (targetReport != null) {
        // update existing report
        AppLogger.LogSubstep($"Updating [{report.ItemName}]");
        var updateRequest = new UpdateItemDefinitionRequest(reportDefinition);
        FabricRestApi.UpdateItemDefinition(targetWorkspace.Id, targetReport.Id.Value, updateRequest);
      }
      else {
        // use item definition to create clone in target workspace
        AppLogger.LogSubstep($"Creating [{report.ItemName}]");
        var createRequest = new CreateItemRequest(report.DisplayName, report.Type);
        createRequest.Definition = reportDefinition;
        targetReport = FabricRestApi.CreateItem(targetWorkspace.Id, createRequest);
      }
    }

    // delete orphaned items
    List<string> sourceWorkspaceItemNames = new List<string>();
    sourceWorkspaceItemNames.AddRange(
      sourceWorkspaceItems.Select(item => $"{item.DisplayName}.{item.Type}")
    );

    var lakehouseNamesInTarget = targetWorkspaceItems.Where(item => item.Type == "Lakehouse").Select(item => item.DisplayName).ToList();

    foreach (var item in targetWorkspaceItems) {
      string itemName = $"{item.DisplayName}.{item.Type}";
      if (!sourceWorkspaceItemNames.Contains(itemName) && 
         (item.Type != "SQLEndpoint") &&
         !(item.Type == "SemanticModel" && lakehouseNamesInTarget.Contains(item.DisplayName))) {
        try {
          AppLogger.LogSubstep($"Deleting [{itemName}]");
          FabricRestApi.DeleteItem(targetWorkspace.Id, item);
        }
        catch {
          AppLogger.LogSubstep($"Could not delete [{itemName}]");

        }
      }
    }

    Console.WriteLine();
    Console.WriteLine("Solution deployment update complete");
    Console.WriteLine();

    Console.Write("Press ENTER to open workspace in the browser");
    Console.ReadLine();


    OpenWorkspaceInBrowser(targetWorkspace.Id);

  }

  public static void CreateAndBindSemanticModelConnectons(Guid WorkspaceId, Guid SemanticModelId) {

    var datasources = PowerBiRestApi.GetDatasourcesForSemanricModels(WorkspaceId, SemanticModelId);

    foreach (var datasource in datasources) {

      if (datasource.DatasourceType == "SQL") {

        string sqlEndPointServer = datasource.ConnectionDetails.Server;
        string sqlEndPointDatabase = datasource.ConnectionDetails.Database;

        // you cannot create the connection until you configure a service principal in AppSettings.cs
        if (AppSettings.ServicePrincipalObjectId != "00000000-0000-0000-0000-000000000000") {
          AppLogger.LogSubstep($"Creating SQL connection for semantic model");
          var sqlConnection = FabricRestApi.CreateSqlConnectionWithServicePrincipal(sqlEndPointServer, sqlEndPointDatabase);
          AppLogger.LogSubstep($"Binding connection to semantic model");
          PowerBiRestApi.BindSemanticModelToConnection(WorkspaceId, SemanticModelId, sqlConnection.Id);
        }

      }

      if (datasource.DatasourceType == "Web") {
        string url = datasource.ConnectionDetails.Url;

        AppLogger.LogSubstep($"Creating Web connection for semantic model");
        var webConnection = FabricRestApi.CreateAnonymousWebConnection(url);

        AppLogger.LogSubstep($"Binding connection to semantic model");
        PowerBiRestApi.BindSemanticModelToConnection(WorkspaceId, SemanticModelId, webConnection.Id);

        AppLogger.LogSubstep($"Refreshing semantic model");
        PowerBiRestApi.RefreshDataset(WorkspaceId, SemanticModelId);

      }

    }
  }

  public static void ConnectWorkspaceToGit(string WorkspaceName, string BranchName = "main") {

    var workspace = FabricRestApi.GetWorkspaceByName(WorkspaceName);

    // create new project in Azure Dev Ops
    AdoProjectManager.CreateProject(WorkspaceName, workspace);

    var gitConnectRequest = new GitConnectRequest(
      new AzureDevOpsDetails(WorkspaceName, BranchName,
                                            "/",
                                            AppSettings.AzureDevOpsOrganizationName,
                                            WorkspaceName));

    FabricRestApi.ConnectWorkspaceToGitRepository(workspace.Id, gitConnectRequest);

    AdoProjectManager.CreateBranch(WorkspaceName, "dev");

    AppLogger.LogOperationStart("Workspace connection to GIT has been created and synchronized");
    Console.ReadLine();
    AppLogger.LogOperationComplete();

  }

  public static void BranchOutToFeatureWorkspace(string WorkspaceName, string FeatureName) {

    string featureWorkspaceName = WorkspaceName + " - " + FeatureName;

    AppLogger.LogSolution($"Branch out [{WorkspaceName}] workspace to [{featureWorkspaceName}] workspace");

    var sourceWorkspace = FabricRestApi.GetWorkspaceByName(WorkspaceName);

    AppLogger.LogStep($"Creating new workspace named [{featureWorkspaceName}]");
    var featureWorkspace = FabricRestApi.CreateWorkspace(featureWorkspaceName);
    AppLogger.LogSubstep($"Workspace created with Id of [{featureWorkspace.Id.ToString()}]");

    var lakehouseNames = new List<string>();
    var lakehouseIdRedirects = new Dictionary<string, string>();
    var lakehouseSqlEndpointRedirects = new Dictionary<string, string>();

    lakehouseIdRedirects.Add(sourceWorkspace.Id.ToString(), featureWorkspace.Id.ToString());

    // Enumerate through semantic models in source workspace to create semantic models in target workspace
    var lakehouses = FabricRestApi.GetItems(sourceWorkspace.Id, "Lakehouse");

    foreach (var sourceLakehouse in lakehouses) {

      var sourceLakehouseSqlEndpoint = FabricRestApi.GetSqlEndpointForLakehouse(sourceWorkspace.Id, sourceLakehouse.Id.Value);

      AppLogger.LogStep($"Creating new lakehouse named [{sourceLakehouse.DisplayName}]");
      var newLakehouse = FabricRestApi.CreateLakehouse(featureWorkspace.Id, sourceLakehouse.DisplayName);
      AppLogger.LogSubstep($"Lakehouse created with Id of [{newLakehouse.Id.Value.ToString()}]");

      var newLakehouseSqlEndpoint = FabricRestApi.GetSqlEndpointForLakehouse(featureWorkspace.Id, newLakehouse.Id.Value);

      lakehouseNames.Add(sourceLakehouse.DisplayName);
      lakehouseIdRedirects.Add(sourceLakehouse.Id.Value.ToString(), newLakehouse.Id.Value.ToString());
      lakehouseSqlEndpointRedirects.Add(sourceLakehouseSqlEndpoint.ConnectionString, newLakehouseSqlEndpoint.ConnectionString);
      lakehouseSqlEndpointRedirects.Add(sourceLakehouseSqlEndpoint.Id, newLakehouseSqlEndpoint.Id);
    }

    var notebooks = FabricRestApi.GetItems(sourceWorkspace.Id, "Notebook");
    foreach (var sourceNotebook in notebooks) {

      // get item definition from source Guid
      var notebookDefinition = FabricRestApi.GetItemDefinition(sourceWorkspace.Id, sourceNotebook.Id.Value);
      notebookDefinition = FabricRestApi.UpdateItemDefinitionPart(notebookDefinition, "notebook-content.py", lakehouseIdRedirects);

      // use item definition to create clone in target workspace
      AppLogger.LogStep($"Creating new notebook named [{sourceNotebook.DisplayName}]");
      var createRequest = new CreateItemRequest(sourceNotebook.DisplayName, sourceNotebook.Type);
      createRequest.Definition = notebookDefinition;
      var clonedNotebook = FabricRestApi.CreateItem(featureWorkspace.Id, createRequest);
      AppLogger.LogSubstep($"Notebook created with Id of [{clonedNotebook.Id.Value.ToString()}]");

      AppLogger.LogSubOperationStart($"Running notebook");
      FabricRestApi.RunNotebook(featureWorkspace.Id, clonedNotebook);
      AppLogger.LogOperationComplete();

    }

    // create dictionary to track Semantic Model Id mapping to rebind repots to correct cloned
    var semanticModelRedirects = new Dictionary<string, string>();

    // Enumerate through semantic models in source workspace to create semantic models in target workspace
    var sementicModels = FabricRestApi.GetItems(sourceWorkspace.Id, "SemanticModel");
    foreach (var sourceModel in sementicModels) {

      // ignore default semantic model for lakehouse
      if (!lakehouseNames.Contains(sourceModel.DisplayName)) {

        AppLogger.LogStep($"Creating new semantic model named [{sourceModel.DisplayName}]");

        // get item definition from source item
        var sementicModelDefinition = FabricRestApi.GetItemDefinition(sourceWorkspace.Id, sourceModel.Id.Value, "TMDL");

        // update expressions.tmdl with SQL endpoint info for lakehouse in feature workspace
        sementicModelDefinition = FabricRestApi.UpdateItemDefinitionPart(sementicModelDefinition, "definition/expressions.tmdl", lakehouseSqlEndpointRedirects);

        // use item definition to create clone in target workspace
        var createRequest = new CreateItemRequest(sourceModel.DisplayName, sourceModel.Type);
        createRequest.Definition = sementicModelDefinition;
        var clonedModel = FabricRestApi.CreateItem(featureWorkspace.Id, createRequest);

        AppLogger.LogSubstep($"Semantic model created with Id of [{clonedModel.Id.Value.ToString()}]");

        // track mapping between source semantic model and target semantic model
        semanticModelRedirects.Add(sourceModel.Id.Value.ToString(), clonedModel.Id.Value.ToString());

        AppLogger.LogSubstep($"Binding connection to semantic model");
        CreateAndBindSemanticModelConnectons(featureWorkspace.Id, clonedModel.Id.Value);
      }
    }

    var reports = FabricRestApi.GetItems(sourceWorkspace.Id, "Report");
    foreach (var sourceReport in reports) {

      AppLogger.LogStep($"Creating new report named [{sourceReport.DisplayName}]");

      // get item definition from source workspace
      var itemDef = FabricRestApi.GetItemDefinition(sourceWorkspace.Id, sourceReport.Id.Value);

      var reportDefinition = FabricRestApi.GetItemDefinition(sourceWorkspace.Id, sourceReport.Id.Value);
      reportDefinition = FabricRestApi.UpdateItemDefinitionPart(reportDefinition, "definition.pbir", semanticModelRedirects);

      // use item definition to create clone in target workspace
      var createRequest = new CreateItemRequest(sourceReport.DisplayName, sourceReport.Type);
      createRequest.Definition = reportDefinition;
      var clonedReport = FabricRestApi.CreateItem(featureWorkspace.Id, createRequest);

      AppLogger.LogSubstep($"Report created with Id of [{clonedReport.Id.Value.ToString()}]");

    }

    Console.WriteLine();
    Console.WriteLine("Branch out feature workspace provisioning complete");
    Console.WriteLine();

    Console.Write("Press ENTER to open workspace in the browser");
    Console.ReadLine();

    OpenWorkspaceInBrowser(featureWorkspace.Id);


  }

  public static void BranchOutToFeatureWorkspaceShallowCopy(string WorkspaceName, string FeatureName) {

    var sourceWorkspace = FabricRestApi.GetWorkspaceByName(WorkspaceName);

    var featureWorkspace = FabricRestApi.CreateWorkspace(WorkspaceName + " - " + FeatureName);

    // Enumerate through semantic models in source workspace to create semantic models in target workspace
    var lakehouses = FabricRestApi.GetItems(sourceWorkspace.Id, "Lakehouse");

    foreach (var sourceLakehouse in lakehouses) {

      var sourceLakehouseSqlEndpoint = FabricRestApi.GetSqlEndpointForLakehouse(sourceWorkspace.Id, sourceLakehouse.Id.Value);

      var newLakehouse = FabricRestApi.CreateLakehouse(featureWorkspace.Id, sourceLakehouse.DisplayName);
      var newLakehouseSqlEndpoint = FabricRestApi.GetSqlEndpointForLakehouse(featureWorkspace.Id, newLakehouse.Id.Value);

    }

    var notebooks = FabricRestApi.GetItems(sourceWorkspace.Id, "Notebook");
    foreach (var sourceNotebook in notebooks) {

      // get item definition from source Guid
      var notebookDefinition = FabricRestApi.GetItemDefinition(sourceWorkspace.Id, sourceNotebook.Id.Value);

      // use item definition to create clone in target workspace
      var createRequest = new CreateItemRequest(sourceNotebook.DisplayName, sourceNotebook.Type);
      createRequest.Definition = notebookDefinition;
      var clonedNotebook = FabricRestApi.CreateItem(featureWorkspace.Id, createRequest);

      FabricRestApi.RunNotebook(featureWorkspace.Id, clonedNotebook);

    }

    // create dictionary to track Semantic Model Id mapping to rebind repots to correct cloned
    var semanticModelRedirects = new Dictionary<string, string>();

    // Enumerate through semantic models in source workspace to create semantic models in target workspace
    var sementicModels = FabricRestApi.GetItems(sourceWorkspace.Id, "SemanticModel");
    foreach (var sourceModel in sementicModels) {


      // get item definition from source Guid
      var sementicModelDefinition = FabricRestApi.GetItemDefinition(sourceWorkspace.Id, sourceModel.Id.Value, "TMSL");

      // use item definition to create clone in target workspace
      var createRequest = new CreateItemRequest(sourceModel.DisplayName, sourceModel.Type);
      createRequest.Definition = sementicModelDefinition;
      var clonedModel = FabricRestApi.CreateItem(featureWorkspace.Id, createRequest);

      // track mapping between source semantic model and target semantic model
      semanticModelRedirects.Add(sourceModel.Id.Value.ToString(), clonedModel.Id.Value.ToString());


    }

    var reports = FabricRestApi.GetItems(sourceWorkspace.Id, "Report");
    foreach (var sourceReport in reports) {

      // get item definition from source workspace
      var itemDef = FabricRestApi.GetItemDefinition(sourceWorkspace.Id, sourceReport.Id.Value);

      var reportDefinition = FabricRestApi.GetItemDefinition(sourceWorkspace.Id, sourceReport.Id.Value);

      // use item definition to create clone in target workspace
      var createRequest = new CreateItemRequest(sourceReport.DisplayName, sourceReport.Type);
      createRequest.Definition = reportDefinition;
      var clonedNotebook = FabricRestApi.CreateItem(featureWorkspace.Id, createRequest);

    }

    Console.WriteLine();
    Console.WriteLine("Customer tenant provisioning complete");
    Console.WriteLine();

    Console.Write("Press ENTER to open workspace in the browser");
    Console.ReadLine();

    OpenWorkspaceInBrowser(featureWorkspace.Id);
  }

  public static void DeploySolutionFromGitIntegrationSource(string WorkspaceName) {

    AppLogger.LogSolution("Provision a new Fabric customer tenant and initialize from GIT");
    Workspace workspace = FabricRestApi.CreateWorkspace(WorkspaceName);

    string lastObjectId = AdoProjectManager.CreateProject(WorkspaceName, workspace);


    var gitConnectRequest = new GitConnectRequest(
      new AzureDevOpsDetails(WorkspaceName, "main", "/", "DevCampDevOps", WorkspaceName));

    FabricRestApi.ConnectWorkspaceToGitRepository(workspace.Id, gitConnectRequest);

    AppLogger.LogStep("Preparing semantic model by importing data");

    var model = FabricRestApi.GetSemanticModelByName(workspace.Id, "Product Sales");

    AppLogger.LogSubstep("Patching datasource credentials for semantic model");
    PowerBiRestApi.PatchAnonymousAccessWebCredentials(workspace.Id, model.Id.Value);

    AppLogger.LogSubstep("Refreshing semantic model");
    PowerBiRestApi.RefreshDataset(workspace.Id, model.Id.Value);

    AppLogger.LogSubstep("Semantic model ready for use");

    Console.WriteLine();
    Console.WriteLine("Customer tenant provisioning complete");
    Console.WriteLine();

    Console.Write("Press ENTER to open workspace in the browser");
    Console.ReadLine();

    OpenWorkspaceInBrowser(workspace.Id);

  }

  public static void BranchOutToFeatureWorkspaceUsingGitIntegration(string WorkspaceName, string FeatureName) {

    var sourceWorkspace = FabricRestApi.GetWorkspaceByName(WorkspaceName);

    var adoProject = AdoProjectManager.EnsureProjectExists(WorkspaceName);

    var gitConnection = FabricRestApi.GetWorkspaceGitConnection(sourceWorkspace.Id);
    if (gitConnection.GitConnectionState == GitConnectionState.NotConnected) {

      var gitConnectRequestSource = new GitConnectRequest(
        new AzureDevOpsDetails(WorkspaceName, "main", "/", "DevCampDevOps", WorkspaceName));

      FabricRestApi.ConnectWorkspaceToGitRepository(sourceWorkspace.Id, gitConnectRequestSource);

    }

    AdoProjectManager.CreateBranch(WorkspaceName, FeatureName);

    string featureWorkspaceName = WorkspaceName + " - " + FeatureName;

    var featureWorkspace = FabricRestApi.CreateWorkspace(featureWorkspaceName);


    var gitConnectRequest = new GitConnectRequest(
      new AzureDevOpsDetails(WorkspaceName, FeatureName, "/", "DevCampDevOps", WorkspaceName));

    FabricRestApi.ConnectWorkspaceToGitRepository(featureWorkspace.Id, gitConnectRequest);


    // enumerate through semantic models to set credentials and refresh
    var pbiDatasets = PowerBiRestApi.GetDatasetsInWorkspace(featureWorkspace.Id);
    foreach (var pbiDataset in pbiDatasets) {
      if (pbiDataset.IsRefreshable.Value) {
        var datasources = PowerBiRestApi.GetDatasourcesForDataset(featureWorkspace.Id.ToString(), pbiDataset.Id);
        foreach (var datasource in datasources) {
          if (datasource.DatasourceType == "Web") {
            AppLogger.LogStep("Patch credetials for web datasource");
            PowerBiRestApi.PatchAnonymousAccessWebCredentials(featureWorkspace.Id, new Guid(pbiDataset.Id));
          }
        }
        PowerBiRestApi.RefreshDataset(featureWorkspace.Id, new Guid(pbiDataset.Id));
      }
    }


    //// remap reports to cloned semantic models
    //var pbiReports = PowerBiRestApi.GetReportsInWorkspace(targetWorkspace.Id);
    //foreach (var pbiReport in pbiReports) {

    //  var reportId = pbiReport.Id;
    //  var modelId = new Guid(pbiReport.DatasetId);

    //  if (semanticModelMapping.ContainsKey(modelId)) {
    //    Guid redirectedModelId = semanticModelMapping[modelId];
    //    AppLogger.LogStep($"Rebing report named {pbiReport.Name} to semantic model with Id {redirectedModelId.ToString()}");
    //    PowerBiRestApi.BindReportToSemanticModel(targetWorkspace.Id, redirectedModelId, reportId);
    //  }

    //}


    Console.WriteLine();
    Console.WriteLine("Customer tenant provisioning complete");
    Console.WriteLine();

    Console.Write("Press ENTER to open workspace in the browser");
    Console.ReadLine();

    OpenWorkspaceInBrowser(featureWorkspace.Id);


  }

  public static DeploymentItemSet GetDeploymentItemSetFromGitRepo(string ProjectName) {

    AppLogger.LogStep($"Loading item definition files from GIT repository");

    var items = AdoProjectManager.GetItemsFromGitRepo(ProjectName);

    DeploymentItemSet deploymentItemSet = new DeploymentItemSet {
      DeploymentItems = new List<DeploymentItem>()
    };

    DeploymentItem currentItem = null;

    foreach (var item in items) {
      if (item.FileName == ".platform") {
        AppLogger.LogSubstep($"Loading [{item.ItemName}]");
        PlatformFileMetadata itemMetadata = JsonSerializer.Deserialize<FabricPlatformFile>(item.Content).metadata;

        currentItem = new DeploymentItem {
          DisplayName = itemMetadata.displayName,
          Type = itemMetadata.type,
          Definition = new ItemDefinition(new List<ItemDefinitionPart>())
        };

        deploymentItemSet.DeploymentItems.Add(currentItem);
      }
      else {
        string encodedContent = Convert.ToBase64String(Encoding.UTF8.GetBytes(item.Content));
        currentItem.Definition.Parts.Add(
          new ItemDefinitionPart(item.Path, encodedContent, PayloadType.InlineBase64)
        );
      }
    }

    return deploymentItemSet;

  }

  public static void CreateItemFromTemplateFolders(string WorkspaceName) {

    var workspace = FabricRestApi.CreateWorkspace(WorkspaceName, AppSettings.FabricCapacityId);

    var createItemRequest = ItemDefinitionFactory.GetCreateItemRequestFromFolder("Product Sales Imported.SemanticModel");

    var item = FabricRestApi.CreateItem(workspace.Id, createItemRequest);

    AppLogger.LogStep("Solution deployment complete");

    AppLogger.LogOperationStart("Press ENTER to open workspace in the browser");
    Console.ReadLine();
    AppLogger.LogOperationComplete();

    OpenWorkspaceInBrowser(workspace.Id);

  }

  private static ItemDefinitionPart CreateInlineBase64Part(string Path, string Payload) {
    string base64Payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(Payload));
    return new ItemDefinitionPart(Path, base64Payload, PayloadType.InlineBase64);
  }

  public static ItemDefinition UpdateReportDefinitionWithRedirection(ItemDefinition ItemDefinition, Guid WorkspaceId) {
    var partDefinition = ItemDefinition.Parts.Where(part => part.Path == "definition.pbir").First();
    ItemDefinition.Parts.Remove(partDefinition);
    byte[] PayloadBytes = Convert.FromBase64String(partDefinition.Payload);
    string PayloadContent = Encoding.UTF8.GetString(PayloadBytes, 0, PayloadBytes.Length);
    var reportDefinition = JsonSerializer.Deserialize<ReportDefinitionFile>(PayloadContent);

    if ((reportDefinition.datasetReference.byPath != null) &&
       (reportDefinition.datasetReference.byPath.path != null) &&
       (reportDefinition.datasetReference.byPath.path.Length > 0)) {

      string targetModelName = reportDefinition.datasetReference.byPath.path.Replace("../", "").Replace(".SemanticModel", "");

      var targetModel = FabricRestApi.GetSemanticModelByName(WorkspaceId, targetModelName);

      string reportDefinitionPartTemplate = ItemDefinitionFactory.GetTemplateFile(@"Reports\definition.pbir");
      string reportDefinitionPartContent = reportDefinitionPartTemplate.Replace("{SEMANTIC_MODEL_ID}", targetModel.Id.ToString());
      var reportDefinitionPart = CreateInlineBase64Part("definition.pbir", reportDefinitionPartContent);
      ItemDefinition.Parts.Add(reportDefinitionPart);
      return ItemDefinition;
    }
    else {
      throw new ApplicationException("EROR: definition.pbir did not have byPath reference");
    }

  }

  public static ItemDefinition UpdateReportDefinitionWithSemanticModelId(ItemDefinition ItemDefinition, Guid WorkspaceId, string TargetModelName) {
    var targetModel = FabricRestApi.GetSemanticModelByName(WorkspaceId, TargetModelName);
    Guid targetModelId = targetModel.Id.Value;

    var partDefinition = ItemDefinition.Parts.Where(part => part.Path == "definition.pbir").First();
    ItemDefinition.Parts.Remove(partDefinition);
    string reportDefinitionPartTemplate = ItemDefinitionFactory.GetTemplateFile(@"Reports\definition.pbir");
    string reportDefinitionPartContent = reportDefinitionPartTemplate.Replace("{SEMANTIC_MODEL_ID}", targetModel.Id.ToString());
    var reportDefinitionPart = CreateInlineBase64Part("definition.pbir", reportDefinitionPartContent);
    ItemDefinition.Parts.Add(reportDefinitionPart);
    return ItemDefinition;


  }

  public static void ExportItemDefinitionsFromWorkspace(string WorkspaceName) {
    FabricRestApi.ExportItemDefinitionsFromWorkspace(WorkspaceName);
  }

  public static void ViewWorkspaces() {

    var workspaces = FabricRestApi.GetWorkspaces();

    AppLogger.LogStep("Workspaces List");
    foreach (var workspace in workspaces) {
      AppLogger.LogSubstep($"{workspace.DisplayName} ({workspace.Id})");
    }

    Console.WriteLine();

  }

  public static void ViewCapacities() {

    var capacities = FabricRestApi.GetCapacities();

    AppLogger.LogStep("Capacities List");
    foreach (var capacity in capacities) {
      AppLogger.LogSubstep($"[{capacity.Sku}] {capacity.DisplayName} (ID={capacity.Id})");
    }

  }

  private static void OpenWorkspaceInBrowser(Guid WorkspaceId) {
    OpenWorkspaceInBrowser(WorkspaceId.ToString());
  }

  private static void OpenWorkspaceInBrowser(string WorkspaceId) {

    string url = "https://app.powerbi.com/groups/" + WorkspaceId;

    string chromeBrowserProfileName = "Profile 7";

    var process = new Process();
    process.StartInfo = new ProcessStartInfo(@"C:\Program Files\Google\Chrome\Application\chrome.exe");
    process.StartInfo.Arguments = url + $" --profile-directory=\"{chromeBrowserProfileName}\" ";
    process.Start();

  }

}
