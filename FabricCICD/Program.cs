
class Program {

  public static void Main() {

    Setup_ViewWorkspacesAndCapacities();

    // Demo01_DeploySolutionToWorkspace();
    // Demo02_BranchOutToFeatureWorkspace();
    // Demo03_DeployWorkspacTemplates();
    // Demo04_DeployCustomerTenantWorkspace();
    // Demo05_UpdateCustomerTenantWorkspace();
  }

  public static void Setup_ViewWorkspacesAndCapacities() {
    DeploymentManager.ViewWorkspaces();
    DeploymentManager.ViewCapacities();
  }

  public static void Demo01_DeploySolutionToWorkspace() {
    string workspaceName = "Contoso";
    DeploymentManager.DeployWorkspaceWithLakehouseSolution(workspaceName);
    // DeploymentManager.ConnectWorkspaceToGit(workspaceName);
  }

  public static void Demo02_BranchOutToFeatureWorkspace() {
    string workspaceName = "Contoso";
    string featureName = "Feature1";
    DeploymentManager.BranchOutToFeatureWorkspace(workspaceName, featureName);
  }

  const string PowerBiSolutionTemplate = "ISV Power BI Solution";
  const string LakehouseSolutionTemplate = "ISV Lakehouse Solution";

  public static void Demo03_DeployWorkspacTemplates() {
    // DeploymentManager.DeployWorkspaceWithPowerBiSolution(PowerBiSolutionTemplate);
    // DeploymentManager.ConnectWorkspaceToGit(PowerBiSolutionTemplate);
    DeploymentManager.DeployWorkspaceWithLakehouseSolution(LakehouseSolutionTemplate);
    DeploymentManager.ConnectWorkspaceToGit(LakehouseSolutionTemplate);
  }

  public static void Demo04_DeployCustomerTenantWorkspace() {
    DeploymentManager.DeploySolutionFromProjectTemplate(LakehouseSolutionTemplate, "Customer01");
  }

  public static void Demo05_UpdateCustomerTenantWorkspace() {
    DeploymentManager.UpdateSolutionFromProjectTemplate(LakehouseSolutionTemplate, "Customer01");
  }

}