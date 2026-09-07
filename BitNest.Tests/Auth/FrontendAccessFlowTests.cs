namespace BitNest.Tests.Auth;

public class FrontendAccessFlowTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task Main_page_admin_link_visibility_uses_current_user_role(bool isAdmin) =>
        FrontendScript.Run("main", $$"""
            assert.equal(element('adminLink').href, 'admin.html');
            assert.equal(element('adminLink').style.display, 'none');
            await respond(200, tokens);
            assert.equal(pending[0].path, '/auth/me');
            await respond(200, { id: 7, isAdmin: {{(isAdmin ? "true" : "false")}} });
            assert.equal(hidden('headerNav'), false);
            assert.equal(element('adminLink').style.display, '{{(isAdmin ? "" : "none")}}');
            await respond(200, []);
            """, persistedSession: true);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task Admin_page_gates_panel_and_users_api_on_current_user_role(bool isAdmin) =>
        FrontendScript.Run("admin", $$"""
            const panel = document.querySelector('.admin-card');
            assert.ok(panel, 'Admin page must contain the admin panel');
            assert.equal(hidden('appContainer'), true);
            assert.equal(hidden('accessDeniedView'), true);
            assert.equal(pending[0].path, '/auth/refresh');
            await respond(200, tokens);
            assert.equal(pending[0].path, '/auth/me');
            assert.equal(hidden('appContainer'), true);
            assert.equal(requests.some(r => r.path.startsWith('/admin')), false);
            await respond(200, { id: 7, isAdmin: {{(isAdmin ? "true" : "false")}} });
            assert.equal(hidden('authLoadingGate'), true);
            assert.equal(hidden('appContainer'), false);
            assert.equal(panel.classList.contains('view-hidden'), {{(isAdmin ? "false" : "true")}});
            assert.equal(hidden('accessDeniedView'), {{(isAdmin ? "true" : "false")}});
            if ({{(isAdmin ? "true" : "false")}}) {
                assert.equal(pending[0].path, '/admin/users');
                assert.equal(pending[0].options.method || 'GET', 'GET');
                assert.equal(pending[0].options.headers.get('Authorization'), 'Bearer ' + tokens.accessToken);
                await respond(200, []);
                assert.match(element('adminUserList').innerHTML, /No users found/);
            } else {
                assert.equal(requests.some(r => r.path.startsWith('/admin')), false);
            }
            """, persistedSession: true);

    [Fact]
    public Task Admin_page_without_session_redirects_to_login_without_users_request() =>
        FrontendScript.Run("admin", """
            assert.equal(window.location.href, 'index.html');
            assert.equal(hidden('appContainer'), true);
            assert.equal(requests.length, 0);
            """);

    [Fact]
    public Task File_404_shows_unified_view_without_ending_session() =>
        FrontendScript.Run("main", """
            await respond(200, tokens);
            await respond(200, { id: 7, isAdmin: false });
            await respond(200, []);
            const deletion = deleteFile('unavailable-file');
            await tick();
            assert.equal(pending[0].path, '/Storage/unavailable-file');
            assert.equal(pending[0].options.method, 'DELETE');
            await respond(404, {});
            await deletion;
            assert.equal(hidden('file404View'), false);
            assert.equal(hidden('filesView'), true);
            assert.equal(hidden('accessDeniedView'), true);
            assert.equal(hidden('appContainer'), false);
            assert.equal(hidden('authContainer'), true);
            assert.equal(authState.accessToken, tokens.accessToken);
            assert.equal(window.localStorage.getItem('bitnest.refresh.local'), tokens.refreshToken);
            window.showFilesView();
            assert.equal(hidden('filesView'), false);
            assert.equal(hidden('file404View'), true);
            """, persistedSession: true);

    [Fact]
    public void Error_views_offer_back_to_files_actions()
    {
        var index = FrontendScript.Read("index.html");
        var admin = FrontendScript.Read("admin.html");
        Assert.Matches("(?s)id=\"file404View\".*?<button[^>]*onclick=\"showFilesView\\(\\)\"[^>]*>Back to files</button>", index);
        Assert.Matches("(?s)id=\"accessDeniedView\".*?<a[^>]*href=\"index.html\"[^>]*>Back to files</a>", admin);
    }
}
