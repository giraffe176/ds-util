using DatasiteUploader.Models;
using DatasiteUploader.Models.Permissions;
using DatasiteUploader.Models.CsvTemplates;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DatasiteUploader.Services;

/// <summary>
/// Enterprise console interface for dataroom management
/// Provides rich, interactive commands for setting up and managing datarooms
/// </summary>
public sealed class DataroomManagementConsoleInterface
{
    private readonly IDataroomManagementService _dataroomService;
    private readonly ICsvPermissionService _csvService;
    private readonly IPermissionService _permissionService;
    private readonly ILogger<DataroomManagementConsoleInterface> _logger;
    private readonly UserContext _userContext;

    public DataroomManagementConsoleInterface(
        IDataroomManagementService dataroomService,
        ICsvPermissionService csvService,
        IPermissionService permissionService,
        ILogger<DataroomManagementConsoleInterface> logger,
        UserContext userContext)
    {
        _dataroomService = dataroomService ?? throw new ArgumentNullException(nameof(dataroomService));
        _csvService = csvService ?? throw new ArgumentNullException(nameof(csvService));
        _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
    }

    #region Main Menu & Navigation

    /// <summary>
    /// Display main dataroom management menu
    /// </summary>
    public async Task ShowMainMenuAsync(string projectId)
    {
        while (true)
        {
            Console.Clear();
            DisplayHeader("DATAROOM MANAGEMENT CONSOLE");
            
            Console.WriteLine($"Project: {projectId}");
            Console.WriteLine($"User: {_userContext.DisplayName} ({_userContext.Email})");
            Console.WriteLine();
            
            Console.WriteLine("📋 MAIN MENU");
            Console.WriteLine("1.  🏗️  Setup New Dataroom");
            Console.WriteLine("2.  📁  Manage Content Structure");
            Console.WriteLine("3.  👥  Manage Users & Roles");
            Console.WriteLine("4.  🔐  Manage Permissions");
            Console.WriteLine("5.  📊  CSV Import/Export Tools");
            Console.WriteLine("6.  📈  Analytics & Reports");
            Console.WriteLine("7.  🔧  Templates & Configuration");
            Console.WriteLine("8.  ⚙️   Administration");
            Console.WriteLine("9.  ❓  Help & Documentation");
            Console.WriteLine("0.  🚪  Exit");
            Console.WriteLine();

            var choice = PromptForInput("Select option: ");

            try
            {
                switch (choice)
                {
                    case "1":
                        await ShowDataroomSetupMenuAsync(projectId);
                        break;
                    case "2":
                        await ShowContentManagementMenuAsync(projectId);
                        break;
                    case "3":
                        await ShowUserManagementMenuAsync(projectId);
                        break;
                    case "4":
                        await ShowPermissionManagementMenuAsync(projectId);
                        break;
                    case "5":
                        await ShowCsvToolsMenuAsync(projectId);
                        break;
                    case "6":
                        await ShowAnalyticsMenuAsync(projectId);
                        break;
                    case "7":
                        await ShowTemplatesMenuAsync(projectId);
                        break;
                    case "8":
                        await ShowAdministrationMenuAsync(projectId);
                        break;
                    case "9":
                        ShowHelpMenu();
                        break;
                    case "0":
                        return;
                    default:
                        ShowError("Invalid selection. Please try again.");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in main menu operation");
                ShowError($"An error occurred: {ex.Message}");
                PromptToContinue();
            }
        }
    }

    #endregion

    #region Dataroom Setup Menu

    private async Task ShowDataroomSetupMenuAsync(string projectId)
    {
        while (true)
        {
            Console.Clear();
            DisplayHeader("DATAROOM SETUP");
            
            Console.WriteLine("🏗️  SETUP OPTIONS");
            Console.WriteLine("1.  🚀  Quick Setup (Standard Template)");
            Console.WriteLine("2.  🎯  Custom Setup (Manual Configuration)");
            Console.WriteLine("3.  📋  Setup from CSV Templates");
            Console.WriteLine("4.  📄  Generate Setup Templates");
            Console.WriteLine("5.  📋  Clone from Existing Dataroom");
            Console.WriteLine("6.  🔄  Setup Wizard (Step-by-Step)");
            Console.WriteLine("0.  ⬅️   Back to Main Menu");
            Console.WriteLine();

            var choice = PromptForInput("Select setup method: ");

            switch (choice)
            {
                case "1":
                    await QuickDataroomSetupAsync(projectId);
                    break;
                case "2":
                    await CustomDataroomSetupAsync(projectId);
                    break;
                case "3":
                    await CsvDataroomSetupAsync(projectId);
                    break;
                case "4":
                    await GenerateSetupTemplatesAsync(projectId);
                    break;
                case "5":
                    await CloneDataroomAsync(projectId);
                    break;
                case "6":
                    await DataroomSetupWizardAsync(projectId);
                    break;
                case "0":
                    return;
                default:
                    ShowError("Invalid selection. Please try again.");
                    break;
            }
        }
    }

    private async Task QuickDataroomSetupAsync(string projectId)
    {
        Console.Clear();
        DisplayHeader("QUICK DATAROOM SETUP");
        
        Console.WriteLine("🚀 Quick setup creates a standard dataroom with predefined structure and roles.");
        Console.WriteLine();
        
        // Display available templates
        Console.WriteLine("📋 Available Templates:");
        var templates = new[]
        {
            ("1", "Due Diligence", "Standard M&A due diligence dataroom"),
            ("2", "Board Materials", "Board meeting materials and governance"),
            ("3", "Audit Review", "External audit document review"),
            ("4", "Legal Discovery", "Legal case discovery and review"),
            ("5", "IPO Process", "Initial public offering documentation"),
            ("6", "Private Equity", "PE investment review process")
        };

        foreach (var (id, name, description) in templates)
        {
            Console.WriteLine($"  {id}. {name} - {description}");
        }
        Console.WriteLine();

        var templateChoice = PromptForInput("Select template (1-6): ");
        var dataroomName = PromptForInput("Dataroom name: ");
        
        if (string.IsNullOrWhiteSpace(dataroomName))
        {
            ShowError("Dataroom name is required.");
            PromptToContinue();
            return;
        }

        var dataroomType = templateChoice switch
        {
            "1" => DataroomType.DueDiligence,
            "2" => DataroomType.BoardMaterials,
            "3" => DataroomType.AuditReview,
            "4" => DataroomType.LegalDiscovery,
            "5" => DataroomType.IPO,
            "6" => DataroomType.PrivateEquity,
            _ => DataroomType.DueDiligence
        };

        Console.WriteLine();
        Console.WriteLine("🔄 Setting up dataroom...");
        
        try
        {
            // Generate template and setup dataroom
            var template = await _dataroomService.GenerateDataroomTemplateAsync(dataroomType);
            
            var setupRequest = new DataroomSetupRequest
            {
                ProjectId = projectId,
                DataroomName = dataroomName,
                Type = dataroomType,
                ContentStructure = template.ContentStructure,
                Roles = template.DefaultRoles,
                Settings = template.DefaultSettings,
                CreatedByUserId = _userContext.UserId
            };

            var (success, result, errors) = await _dataroomService.CreateDataroomAsync(setupRequest);

            if (success)
            {
                Console.WriteLine();
                ShowSuccess("✅ Dataroom setup completed successfully!");
                Console.WriteLine($"   📁 Content items created: {result.ContentItemsCreated}");
                Console.WriteLine($"   👥 Roles created: {result.RolesCreated}");
                Console.WriteLine($"   🔐 Permissions set: {result.PermissionsSet}");
                Console.WriteLine($"   ⏱️  Setup time: {result.SetupDuration:mm\\:ss}");
                Console.WriteLine();
                Console.WriteLine("Next steps:");
                Console.WriteLine("• Upload content to the created folders");
                Console.WriteLine("• Invite users and assign roles");
                Console.WriteLine("• Review and adjust permissions as needed");
                Console.WriteLine("• Publish content when ready");
            }
            else
            {
                ShowError("❌ Dataroom setup failed:");
                foreach (var error in errors)
                {
                    Console.WriteLine($"   • {error}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during quick dataroom setup");
            ShowError($"Setup failed: {ex.Message}");
        }

        PromptToContinue();
    }

    #endregion

    #region CSV Tools Menu

    private async Task ShowCsvToolsMenuAsync(string projectId)
    {
        while (true)
        {
            Console.Clear();
            DisplayHeader("CSV IMPORT/EXPORT TOOLS");
            
            Console.WriteLine("📊 CSV OPERATIONS");
            Console.WriteLine("1.  📥  Import Roles from CSV");
            Console.WriteLine("2.  📥  Import Permissions from CSV");
            Console.WriteLine("3.  📥  Import User Assignments from CSV");
            Console.WriteLine("4.  📥  Import Complete Setup from CSV");
            Console.WriteLine("5.  📤  Export Current Configuration to CSV");
            Console.WriteLine("6.  📋  Generate CSV Templates");
            Console.WriteLine("7.  🔍  Validate CSV Before Import");
            Console.WriteLine("8.  👁️   Preview CSV Import Changes");
            Console.WriteLine("9.  📚  CSV Format Documentation");
            Console.WriteLine("0.  ⬅️   Back to Main Menu");
            Console.WriteLine();

            var choice = PromptForInput("Select CSV operation: ");

            switch (choice)
            {
                case "1":
                    await ImportRolesFromCsvAsync(projectId);
                    break;
                case "2":
                    await ImportPermissionsFromCsvAsync(projectId);
                    break;
                case "3":
                    await ImportUserAssignmentsFromCsvAsync(projectId);
                    break;
                case "4":
                    await ImportCompleteSetupFromCsvAsync(projectId);
                    break;
                case "5":
                    await ExportConfigurationToCsvAsync(projectId);
                    break;
                case "6":
                    await GenerateCsvTemplatesAsync(projectId);
                    break;
                case "7":
                    await ValidateCsvFileAsync(projectId);
                    break;
                case "8":
                    await PreviewCsvImportAsync(projectId);
                    break;
                case "9":
                    ShowCsvDocumentation();
                    break;
                case "0":
                    return;
                default:
                    ShowError("Invalid selection. Please try again.");
                    break;
            }
        }
    }

    private async Task ImportRolesFromCsvAsync(string projectId)
    {
        Console.Clear();
        DisplayHeader("IMPORT ROLES FROM CSV");
        
        Console.WriteLine("📥 This will import roles from a CSV file.");
        Console.WriteLine();
        Console.WriteLine("CSV Format: RoleName, Description, FeaturePermissions, Department, MaxUsers, ExpirationDate");
        Console.WriteLine("Example: \"Project Admin\", \"Full access\", \"Publishing,UserManagement\", \"IT\", \"5\", \"\"");
        Console.WriteLine();

        var filePath = PromptForInput("CSV file path: ");
        
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            ShowError("File not found or invalid path.");
            PromptToContinue();
            return;
        }

        try
        {
            var csvContent = await File.ReadAllTextAsync(filePath);
            
            Console.WriteLine("🔄 Validating CSV content...");
            var (isValid, errors, warnings) = await _csvService.ValidateCsvContentAsync(
                csvContent, CsvTemplateType.Roles, projectId);

            if (!isValid)
            {
                ShowError("❌ CSV validation failed:");
                foreach (var error in errors)
                {
                    Console.WriteLine($"   • {error}");
                }
                PromptToContinue();
                return;
            }

            if (warnings.Any())
            {
                ShowWarning("⚠️ Validation warnings:");
                foreach (var warning in warnings)
                {
                    Console.WriteLine($"   • {warning}");
                }
                Console.WriteLine();
                
                if (!PromptForConfirmation("Continue with import? (y/n): "))
                {
                    return;
                }
            }

            Console.WriteLine("🔄 Importing roles...");
            var (success, createdRoleIds, importErrors) = await _csvService.ImportRolesFromCsvAsync(
                csvContent, projectId, _userContext.UserId);

            if (success)
            {
                ShowSuccess($"✅ Successfully imported {createdRoleIds.Count} roles!");
                Console.WriteLine();
                Console.WriteLine("Created roles:");
                foreach (var roleId in createdRoleIds)
                {
                    var role = await _permissionService.GetRoleAsync(roleId);
                    Console.WriteLine($"   • {role?.Name ?? roleId}");
                }
            }
            else
            {
                ShowError("❌ Import completed with errors:");
                foreach (var error in importErrors)
                {
                    Console.WriteLine($"   • {error}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing roles from CSV");
            ShowError($"Import failed: {ex.Message}");
        }

        PromptToContinue();
    }

    private async Task GenerateCsvTemplatesAsync(string projectId)
    {
        Console.Clear();
        DisplayHeader("GENERATE CSV TEMPLATES");
        
        Console.WriteLine("📋 Select template type to generate:");
        Console.WriteLine("1. Roles Template");
        Console.WriteLine("2. Permissions Template");
        Console.WriteLine("3. User Assignments Template");
        Console.WriteLine("4. Content Structure Template");
        Console.WriteLine("5. Complete Setup Template");
        Console.WriteLine();

        var choice = PromptForInput("Select template type (1-5): ");
        var outputDir = PromptForInput("Output directory (leave empty for current): ");
        
        if (string.IsNullOrWhiteSpace(outputDir))
        {
            outputDir = Directory.GetCurrentDirectory();
        }

        try
        {
            string templateContent;
            string fileName;

            switch (choice)
            {
                case "1":
                    templateContent = await _csvService.GenerateRoleTemplateAsync(projectId);
                    fileName = "roles_template.csv";
                    break;
                case "2":
                    templateContent = await _csvService.GeneratePermissionTemplateAsync(projectId);
                    fileName = "permissions_template.csv";
                    break;
                case "3":
                    templateContent = await _csvService.GenerateUserAssignmentTemplateAsync(projectId);
                    fileName = "user_assignments_template.csv";
                    break;
                case "4":
                    templateContent = await _csvService.GenerateContentStructureTemplateAsync(projectId);
                    fileName = "content_structure_template.csv";
                    break;
                case "5":
                    // Generate all templates
                    var roles = await _csvService.GenerateRoleTemplateAsync(projectId);
                    var permissions = await _csvService.GeneratePermissionTemplateAsync(projectId);
                    var users = await _csvService.GenerateUserAssignmentTemplateAsync(projectId);
                    var content = await _csvService.GenerateContentStructureTemplateAsync(projectId);

                    await File.WriteAllTextAsync(Path.Combine(outputDir, "roles_template.csv"), roles);
                    await File.WriteAllTextAsync(Path.Combine(outputDir, "permissions_template.csv"), permissions);
                    await File.WriteAllTextAsync(Path.Combine(outputDir, "user_assignments_template.csv"), users);
                    await File.WriteAllTextAsync(Path.Combine(outputDir, "content_structure_template.csv"), content);

                    ShowSuccess("✅ All templates generated successfully!");
                    Console.WriteLine($"Files saved to: {outputDir}");
                    PromptToContinue();
                    return;
                default:
                    ShowError("Invalid selection.");
                    PromptToContinue();
                    return;
            }

            var filePath = Path.Combine(outputDir, fileName);
            await File.WriteAllTextAsync(filePath, templateContent);

            ShowSuccess($"✅ Template generated successfully!");
            Console.WriteLine($"File saved to: {filePath}");
            Console.WriteLine();
            Console.WriteLine("Template includes:");
            Console.WriteLine("• Header with instructions");
            Console.WriteLine("• Example data rows");
            Console.WriteLine("• Validation rules in comments");
            Console.WriteLine();
            Console.WriteLine("Next steps:");
            Console.WriteLine("1. Open the CSV file in Excel or text editor");
            Console.WriteLine("2. Replace example data with your actual data");
            Console.WriteLine("3. Remove comment lines starting with #");
            Console.WriteLine("4. Import the completed CSV file");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating CSV template");
            ShowError($"Template generation failed: {ex.Message}");
        }

        PromptToContinue();
    }

    #endregion

    #region Helper Methods

    private void DisplayHeader(string title)
    {
        Console.WriteLine("═".PadRight(80, '═'));
        Console.WriteLine($" {title.ToUpperInvariant()} ");
        Console.WriteLine("═".PadRight(80, '═'));
        Console.WriteLine();
    }

    private string PromptForInput(string prompt)
    {
        Console.Write(prompt);
        return Console.ReadLine() ?? string.Empty;
    }

    private bool PromptForConfirmation(string prompt)
    {
        Console.Write(prompt);
        var response = Console.ReadLine()?.ToLowerInvariant();
        return response == "y" || response == "yes";
    }

    private void ShowSuccess(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    private void ShowError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    private void ShowWarning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    private void ShowInfo(string message)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    private void PromptToContinue()
    {
        Console.WriteLine();
        Console.WriteLine("Press any key to continue...");
        Console.ReadKey();
    }

    #endregion

    #region Placeholder Methods

    private async Task CustomDataroomSetupAsync(string projectId)
    {
        ShowInfo("Custom setup wizard coming soon...");
        PromptToContinue();
    }

    private async Task CsvDataroomSetupAsync(string projectId)
    {
        ShowInfo("CSV-based setup coming soon...");
        PromptToContinue();
    }

    private async Task GenerateSetupTemplatesAsync(string projectId)
    {
        ShowInfo("Template generation coming soon...");
        PromptToContinue();
    }

    private async Task CloneDataroomAsync(string projectId)
    {
        ShowInfo("Dataroom cloning coming soon...");
        PromptToContinue();
    }

    private async Task DataroomSetupWizardAsync(string projectId)
    {
        ShowInfo("Setup wizard coming soon...");
        PromptToContinue();
    }

    private async Task ShowContentManagementMenuAsync(string projectId)
    {
        ShowInfo("Content management menu coming soon...");
        PromptToContinue();
    }

    private async Task ShowUserManagementMenuAsync(string projectId)
    {
        ShowInfo("User management menu coming soon...");
        PromptToContinue();
    }

    private async Task ShowPermissionManagementMenuAsync(string projectId)
    {
        ShowInfo("Permission management menu coming soon...");
        PromptToContinue();
    }

    private async Task ShowAnalyticsMenuAsync(string projectId)
    {
        ShowInfo("Analytics menu coming soon...");
        PromptToContinue();
    }

    private async Task ShowTemplatesMenuAsync(string projectId)
    {
        ShowInfo("Templates menu coming soon...");
        PromptToContinue();
    }

    private async Task ShowAdministrationMenuAsync(string projectId)
    {
        ShowInfo("Administration menu coming soon...");
        PromptToContinue();
    }

    private void ShowHelpMenu()
    {
        Console.Clear();
        DisplayHeader("HELP & DOCUMENTATION");
        
        Console.WriteLine("📚 HELP TOPICS");
        Console.WriteLine();
        Console.WriteLine("🏗️  DATAROOM SETUP");
        Console.WriteLine("   • Quick Setup: Uses predefined templates for common use cases");
        Console.WriteLine("   • Custom Setup: Manual configuration of all aspects");
        Console.WriteLine("   • CSV Setup: Bulk import from CSV templates");
        Console.WriteLine();
        Console.WriteLine("🔐 PERMISSIONS");
        Console.WriteLine("   • Hidden: Content not visible to role");
        Console.WriteLine("   • View: Can view content on screen only");
        Console.WriteLine("   • Print: Can view and print content");
        Console.WriteLine("   • Download: Can view, print, and download content");
        Console.WriteLine("   • Manage: Full administrative access (create, upload, etc.)");
        Console.WriteLine();
        Console.WriteLine("👥 USER MANAGEMENT");
        Console.WriteLine("   • Roles define what users can do and see");
        Console.WriteLine("   • Users can be assigned multiple roles");
        Console.WriteLine("   • Permissions are inherited from parent folders");
        Console.WriteLine();
        Console.WriteLine("📊 CSV IMPORT/EXPORT");
        Console.WriteLine("   • Generate templates with examples and instructions");
        Console.WriteLine("   • Validate CSV files before importing");
        Console.WriteLine("   • Preview changes before applying");
        Console.WriteLine("   • Export current configuration for backup");
        
        PromptToContinue();
    }

    private async Task ImportPermissionsFromCsvAsync(string projectId)
    {
        ShowInfo("Permission import coming soon...");
        PromptToContinue();
    }

    private async Task ImportUserAssignmentsFromCsvAsync(string projectId)
    {
        ShowInfo("User assignment import coming soon...");
        PromptToContinue();
    }

    private async Task ImportCompleteSetupFromCsvAsync(string projectId)
    {
        ShowInfo("Complete setup import coming soon...");
        PromptToContinue();
    }

    private async Task ExportConfigurationToCsvAsync(string projectId)
    {
        ShowInfo("Configuration export coming soon...");
        PromptToContinue();
    }

    private async Task ValidateCsvFileAsync(string projectId)
    {
        ShowInfo("CSV validation coming soon...");
        PromptToContinue();
    }

    private async Task PreviewCsvImportAsync(string projectId)
    {
        ShowInfo("Import preview coming soon...");
        PromptToContinue();
    }

    private void ShowCsvDocumentation()
    {
        ShowInfo("CSV documentation coming soon...");
        PromptToContinue();
    }

    #endregion
}