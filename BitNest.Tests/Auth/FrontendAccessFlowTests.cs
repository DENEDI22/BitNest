using System.Text.RegularExpressions;

namespace BitNest.Tests.Auth;

/// <summary>
/// Frontend behavior contract tests for admin routing and unified access control outcomes.
/// These tests assert that frontend source code contains required patterns for admin visibility,
/// access-denied handling, and unified file-404 flows based on Phase 7 authorization decisions.
/// </summary>
public class FrontendAccessFlowTests
{
    private static string ReadFrontendFile(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "FrontEnd", fileName);
        var fullPath = Path.GetFullPath(path);
        return File.ReadAllText(fullPath);
    }

    [Fact]
    public void Frontend_main_js_contains_admin_route_visibility_based_on_isAdmin_field()
    {
        var mainJs = ReadFrontendFile("main.js");

        // Assert that script checks for isAdmin from auth/me response
        Assert.Contains("isAdmin", mainJs, StringComparison.OrdinalIgnoreCase);

        // Assert that admin entry visibility is conditional on admin role
        Assert.Matches(
            new Regex(@"(admin|Admin)\s*.*\s*(visible|hidden|show|hide|display)", RegexOptions.IgnoreCase),
            mainJs
        );

        // Assert that route handling exists for /admin path
        Assert.Contains("/admin", mainJs);
    }

    [Fact]
    public void Frontend_main_js_contains_unified_file_404_flow()
    {
        var mainJs = ReadFrontendFile("main.js");

        // Assert that 404 handling or "not found" state exists
        Assert.Matches(
            new Regex(@"(404|not\s*found|notfound|file.*not.*found)", RegexOptions.IgnoreCase),
            mainJs
        );

        // Assert that there's error routing or view toggling logic
        Assert.Matches(
            new Regex(@"(error|404|access.*denied|unauthorized).*view|route.*to\s*(error|404)", RegexOptions.IgnoreCase),
            mainJs
        );
    }

    [Fact]
    public void Frontend_main_js_contains_access_denied_routing()
    {
        var mainJs = ReadFrontendFile("main.js");

        // Assert that access-denied or forbidden handling exists
        Assert.Matches(
            new Regex(@"(access.*denied|forbidden|not.*permitted|denied.*access)", RegexOptions.IgnoreCase),
            mainJs
        );

        // Assert that non-admin /admin path handling routes to access-denied view
        Assert.Matches(
            new Regex(@"(/admin|admin.*route).*(?:access.*denied|forbidden|denied)", RegexOptions.IgnoreCase),
            mainJs
        );
    }

    [Fact]
    public void Frontend_index_html_contains_admin_view_container()
    {
        var indexHtml = ReadFrontendFile("index.html");

        // Assert that there's a container/section for admin view
        Assert.Matches(
            new Regex(@"<(section|div).*(?:admin|id.*admin)", RegexOptions.IgnoreCase),
            indexHtml
        );
    }

    [Fact]
    public void Frontend_index_html_contains_access_denied_view_container()
    {
        var indexHtml = ReadFrontendFile("index.html");

        // Assert that there's a container for access-denied or error view
        Assert.Matches(
            new Regex(@"<(section|div).*(?:access.*denied|forbidden|error|denied)", RegexOptions.IgnoreCase),
            indexHtml
        );
    }

    [Fact]
    public void Frontend_index_html_contains_unified_file_404_container()
    {
        var indexHtml = ReadFrontendFile("index.html");

        // Assert that there's a container for file not found / 404 view
        Assert.Matches(
            new Regex(@"<(section|div).*(?:404|not\s*found|file.*not|error)", RegexOptions.IgnoreCase),
            indexHtml
        );
    }

    [Fact]
    public void Frontend_contains_back_to_files_action()
    {
        var mainJs = ReadFrontendFile("main.js");
        var indexHtml = ReadFrontendFile("index.html");

        var combined = mainJs + "\n" + indexHtml;

        // Assert that "Back to files" button or link text exists
        Assert.Matches(
            new Regex(@"back\s*to\s*files|return\s*to\s*files", RegexOptions.IgnoreCase),
            combined
        );
    }

    [Fact]
    public void Frontend_main_js_uses_admin_users_api()
    {
        var mainJs = ReadFrontendFile("main.js");

        // Assert that /admin/users endpoint is referenced
        Assert.Contains("/admin/users", mainJs);
    }
}
