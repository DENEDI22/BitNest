using System.Diagnostics;
using System.Text.Json;

namespace BitNest.Tests.Auth;

public class AuthFrontendFlowTests
{
    [Theory]
    [InlineData(200)]
    [InlineData(401)]
    public Task Startup_requires_auth_resolution_before_file_ui(int profileStatus) =>
        FrontendScript.Run("main", $$"""
            assert.equal(hidden('appContainer'), true);
            assert.equal(hidden('authLoadingGate'), false);
            assert.deepEqual(requests.map(r => r.path), ['/auth/refresh']);
            await respond(200, tokens);
            assert.equal(pending[0].path, '/auth/me');
            assert.equal(pending[0].options.headers.get('Authorization'), 'Bearer ' + tokens.accessToken);
            assert.equal(hidden('appContainer'), true);
            assert.equal(requests.some(r => r.path.startsWith('/Storage')), false);
            await respond({{profileStatus}}, { id: 7, isAdmin: false });
            if ({{profileStatus}} === 200) {
                assert.equal(hidden('appContainer'), false);
                assert.equal(hidden('filesView'), false);
                assert.equal(pending[0].path, '/Storage/1');
                await respond(200, []);
            } else {
                assert.equal(hidden('appContainer'), true);
                assert.equal(hidden('authContainer'), false);
                assert.equal(requests.some(r => r.path.startsWith('/Storage')), false);
            }
            """, persistedSession: true);

    [Fact]
    public Task Startup_without_session_shows_login_without_loading_files() =>
        FrontendScript.Run("main", """
            assert.equal(hidden('authContainer'), false);
            assert.equal(hidden('appContainer'), true);
            assert.equal(hidden('authLoadingGate'), true);
            assert.equal(requests.length, 0);
            """);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task Login_submits_remember_me_and_enters_app_state(bool rememberMe) =>
        FrontendScript.Run("main", $$"""
            element('authUsername').value = ' alice ';
            element('authPassword').value = 'password123';
            element('rememberMe').checked = {{JsonSerializer.Serialize(rememberMe)}};
            element('loginButton').listeners.click();
            assert.equal(pending[0].path, '/auth/login');
            assert.equal(pending[0].options.method, 'POST');
            assert.deepEqual(JSON.parse(pending[0].options.body), {
                username: 'alice', password: 'password123', rememberMe: {{JsonSerializer.Serialize(rememberMe)}}
            });
            assert.equal(hidden('appContainer'), true);
            await respond(200, tokens);
            assert.equal(pending[0].path, '/auth/me');
            await respond(200, { id: 7, isAdmin: false });
            assert.equal(hidden('authContainer'), true);
            assert.equal(hidden('appContainer'), false);
            assert.equal(hidden('filesView'), false);
            assert.equal(element('loginButton').disabled, false);
            assert.equal(window.localStorage.getItem('bitnest.refresh.local'), {{(rememberMe ? "tokens.refreshToken" : "null")}});
            assert.equal(window.sessionStorage.getItem('bitnest.refresh.session'), {{(rememberMe ? "null" : "tokens.refreshToken")}});
            assert.equal(pending[0].path, '/Storage/1');
            assert.equal(pending[0].options.headers.get('Authorization'), 'Bearer ' + tokens.accessToken);
            await respond(200, []);
            """);

    [Fact]
    public Task Logout_resets_auth_state_and_shows_confirmation() =>
        FrontendScript.Run("main", """
            await respond(200, tokens);
            await respond(200, { id: 7, isAdmin: true });
            await respond(200, []);
            element('logoutButton').listeners.click();
            assert.equal(pending[0].path, '/auth/logout');
            assert.equal(pending[0].options.method, 'POST');
            assert.deepEqual(JSON.parse(pending[0].options.body), { refreshToken: tokens.refreshToken });
            await respond(200, {});
            assert.equal(authState.accessToken, '');
            assert.equal(authState.refreshToken, '');
            assert.equal(window.localStorage.getItem('bitnest.refresh.local'), null);
            assert.equal(window.sessionStorage.getItem('bitnest.refresh.session'), null);
            assert.equal(hidden('appContainer'), true);
            assert.equal(hidden('headerNav'), true);
            assert.equal(hidden('authContainer'), false);
            assert.equal(element('authInlineMessage').textContent, 'Signed out.');
            """, persistedSession: true);
}

// Node's built-in runtime only: execute complete production scripts with controlled HTTP
// responses and the elements/classes from their real HTML. This is not a browser/layout test.
internal static class FrontendScript
{
    internal static string Read(string fileName) => File.ReadAllText(Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../FrontEnd", fileName)));

    internal static async Task Run(string page, string assertions, bool persistedSession = false)
    {
        var html = Read(page == "main" ? "index.html" : "admin.html");
        var script = Read(page + ".js");
        Assert.Matches($"<script\\b[^>]*src=\"{page}\\.js(?:\\?[^\"]*)?\"[^>]*defer", html);
        var harness = """
            const assert = require('node:assert/strict');
            const elements = [];
            const storage = () => {
                const values = new Map();
                return { getItem: k => values.get(k) ?? null,
                    setItem: (k, v) => values.set(k, String(v)), removeItem: k => values.delete(k) };
            };
            for (const tag of html.matchAll(/<[a-z][^>]*>/gi)) {
                const attr = name => tag[0].match(new RegExp('\\b' + name + '="([^"]*)"'))?.[1] || '';
                const classes = new Set(attr('class').split(/\s+/));
                elements.push({ id: attr('id'), href: attr('href'), value: '', checked: false,
                    style: { display: /display:\s*none/.test(attr('style')) ? 'none' : '' },
                    dataset: {}, listeners: {}, textContent: '', innerHTML: '',
                    classList: { contains: c => classes.has(c), add: c => classes.add(c),
                        remove: c => classes.delete(c), toggle: (c, on) => on ? classes.add(c) : classes.delete(c) },
                    addEventListener(event, fn) { this.listeners[event] = fn; }, focus() {} });
            }
            const element = id => {
                const found = elements.find(e => e.id === id);
                assert.ok(found, 'Missing HTML element: ' + id);
                return found;
            };
            const hidden = id => element(id).classList.contains('view-hidden');
            const document = { getElementById: id => elements.find(e => e.id === id) || null,
                querySelector: selector => elements.find(e => e.classList.contains(selector.slice(1))) || null,
                querySelectorAll: selector => elements.filter(e => e.classList.contains(selector.slice(1))) };
            const window = { location: { origin: 'http://localhost:3000', href: '' },
                localStorage: storage(), sessionStorage: storage() };
            if (persistedSession) window.localStorage.setItem('bitnest.refresh.local', 'saved-refresh');
            const tokens = { accessToken: 'header.' + Buffer.from(JSON.stringify({ exp: 4102444800 })).toString('base64url') + '.signature',
                refreshToken: 'rotated-refresh' };
            const pending = [], requests = [];
            const fetch = (url, options = {}) => new Promise(resolve => {
                const request = { path: new URL(url).pathname, options, resolve };
                pending.push(request); requests.push(request);
            });
            const tick = () => new Promise(resolve => setImmediate(resolve));
            const respond = async (status, body) => {
                assert.ok(pending.length, 'Expected an HTTP request');
                pending.shift().resolve({ status, ok: status >= 200 && status < 300, json: async () => body });
                await tick();
            };
            const setTimeout = () => {}; // Keep transient confirmation messages visible for assertions.
            """;

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("node", "--input-type=commonjs")
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };
        Assert.True(process.Start(), "Frontend behavior tests require Node.js 18+ on PATH.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.StandardInput.WriteAsync(
            $"const html = {JsonSerializer.Serialize(html)};\n" +
            $"const persistedSession = {JsonSerializer.Serialize(persistedSession)};\n" + harness + "\n" +
            $"eval({JsonSerializer.Serialize(script + "\n(async () => { await tick();\n" + assertions + "\nassert.equal(pending.length, 0, 'Unanswered HTTP requests');\nconsole.log('Frontend assertions passed');\n})().catch(error => { console.error(error); process.exitCode = 1; });")});");
        process.StandardInput.Close();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Frontend JavaScript assertions did not finish within 15 seconds.");
        }
        var stdout = await output;
        var stderr = await error;
        Assert.True(process.ExitCode == 0, $"Frontend JavaScript failed:\n{stderr}\n{stdout}");
        Assert.Contains("Frontend assertions passed", stdout);
    }
}
