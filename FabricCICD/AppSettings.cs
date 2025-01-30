

public class AppSettings {

  public const string FabricRestApiBaseUrl = "https://api.fabric.microsoft.com/v1";
  public const string PowerBiRestApiBaseUrl = "https://api.powerbi.com";
  public const string OneLakeBaseUrl = "https://onelake.dfs.fabric.microsoft.com";

  public static AppAuthenticationMode AuthenticationMode = AppAuthenticationMode.UserAuthWithAzurePowershell;

  // TODO: add Capacity Id for Fabric-enabled Premium capacity
  public const string FabricCapacityId = "11111111-1111-1111-1111-111111111111";


  // Public client application created in Entra Id Service for user auth
  public const string UserAuthClientId = "22222222-2222-2222-2222-222222222222";
  public const string UserAuthRedirectUri = "http://localhost";

  // Condifential client application created in Entra Id Service for service principal auth
  public const string ServicePrincipalAuthTenantId = "33333333-3333-3333-3333-333333333333";
  public const string ServicePrincipalAuthClientId = "44444444-4444-4444-4444-444444444444";
  public const string ServicePrincipalAuthClientSecret = "YOUR_CLIENT_SECRET";
  public const string ServicePrincipalObjectId = "55555555-5555-5555-5555-555555555555";

  // Add Entra object Id for user account of user running demo
  public const string AdminUser1Id = "66666666-6666-6666-6666-666666666666";

  // update this URL with the URL to your Azure Dev Ops location
  public const string AzureDevOpsOrganizationName = "{YOUR_AZURE_DEVOPS_ORGANIZATION_NAME}";
  public const string AzureDevOpsApiBaseUrl = $"https://dev.azure.com/{AzureDevOpsOrganizationName}";

  // paths to folders inside this project to read and write files
  public const string LocalExportFolder = @"..\..\..\ItemDefinitionExports\";
  public const string LocalTemplateFilesRoot = @"..\..\..\ItemDefinitions\TemplateFiles\";
  public const string LocalItemTemplatesFolder = @"..\..\..\ItemDefinitions\ItemTemplateFolders\";
  public const string LocalSolutionTemplatesFolder = @"..\..\..\ItemDefinitions\SolutionTemplateFolders\";
  public const string LocalGitRepoExports = @"..\..\..\ItemDefinitionExports\FromGit\";
  public const string LocalWorkspaceExports = @"..\..\..\ItemDefinitionExports\FromWorkspace\";

}
