namespace BitNest.Tests.Auth;

public class AuthFrontendFlowTests
{
    private static readonly string MainJsPath =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../FrontEnd/main.js"));

    [Fact]
    public void Startup_requires_auth_resolution_before_file_ui()
    {
        var script = ReadMainScript();

        Assert.Matches("fetch\\(`\\$\\{API_URL\\}/auth/me", script);
        Assert.Contains("async function bootstrapAuthGate", script, StringComparison.Ordinal);

        var bootstrapCall = script.IndexOf("await bootstrapAuthGate()", StringComparison.Ordinal);
        var loadFilesCall = script.IndexOf("loadFiles(currentPage)", StringComparison.Ordinal);

        Assert.True(bootstrapCall >= 0, "Expected startup to await bootstrapAuthGate().");
        Assert.True(loadFilesCall >= 0, "Expected startup to call loadFiles(currentPage).");
        Assert.True(
            bootstrapCall < loadFilesCall,
            "Expected auth bootstrap to run before loading the files UI.");
    }

    [Fact]
    public void Login_submits_remember_me_and_enters_app_state()
    {
        var script = ReadMainScript();

        Assert.Matches("fetch\\(`\\$\\{API_URL\\}/auth/login", script);
        Assert.Contains("rememberMe", script, StringComparison.Ordinal);
        Assert.Contains("showAppView();", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Logout_resets_auth_state_and_shows_confirmation()
    {
        var script = ReadMainScript();

        Assert.Matches("fetch\\(`\\$\\{API_URL\\}/auth/logout", script);
        Assert.Contains("resetAuthState", script, StringComparison.Ordinal);
        Assert.Matches("showAuthView\\(\\\"[^\\\"]+\\\"", script);
    }

    private static string ReadMainScript()
    {
        if (!File.Exists(MainJsPath))
        {
            throw new FileNotFoundException("Cannot locate FrontEnd/main.js for auth-flow assertions.", MainJsPath);
        }

        return File.ReadAllText(MainJsPath);
    }
}
