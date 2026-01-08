namespace Zarla.Core.Security;

/// <summary>
/// Provides password autofill functionality for web pages
/// </summary>
public class PasswordAutofill
{
    private readonly PasswordManager _passwordManager;

    public PasswordAutofill(PasswordManager passwordManager)
    {
        _passwordManager = passwordManager;
    }

    /// <summary>
    /// Gets JavaScript to detect and autofill login forms
    /// </summary>
    public string GetAutofillScript(string? username = null, string? password = null)
    {
        var escapedUsername = EscapeJsString(username ?? "");
        var escapedPassword = EscapeJsString(password ?? "");

        return $@"
(function() {{
    const username = '{escapedUsername}';
    const password = '{escapedPassword}';

    // Find username/email fields
    const usernameSelectors = [
        'input[type=""email""]',
        'input[type=""text""][name*=""user""]',
        'input[type=""text""][name*=""email""]',
        'input[type=""text""][name*=""login""]',
        'input[type=""text""][id*=""user""]',
        'input[type=""text""][id*=""email""]',
        'input[type=""text""][id*=""login""]',
        'input[type=""text""][autocomplete=""username""]',
        'input[type=""text""][autocomplete=""email""]',
        'input[name=""username""]',
        'input[name=""email""]',
        'input[name=""login""]',
        'input[name=""identifier""]'
    ];

    // Find password fields
    const passwordSelectors = [
        'input[type=""password""]',
        'input[autocomplete=""current-password""]',
        'input[autocomplete=""password""]'
    ];

    let usernameField = null;
    let passwordField = null;

    // Find username field
    for (const selector of usernameSelectors) {{
        const field = document.querySelector(selector);
        if (field && field.offsetParent !== null) {{
            usernameField = field;
            break;
        }}
    }}

    // Find password field
    for (const selector of passwordSelectors) {{
        const field = document.querySelector(selector);
        if (field && field.offsetParent !== null) {{
            passwordField = field;
            break;
        }}
    }}

    // Fill fields if found
    if (usernameField && username) {{
        usernameField.value = username;
        usernameField.dispatchEvent(new Event('input', {{ bubbles: true }}));
        usernameField.dispatchEvent(new Event('change', {{ bubbles: true }}));
    }}

    if (passwordField && password) {{
        passwordField.value = password;
        passwordField.dispatchEvent(new Event('input', {{ bubbles: true }}));
        passwordField.dispatchEvent(new Event('change', {{ bubbles: true }}));
    }}

    return {{
        usernameFound: !!usernameField,
        passwordFound: !!passwordField,
        filled: (!!usernameField && username) || (!!passwordField && password)
    }};
}})();
";
    }

    /// <summary>
    /// Gets JavaScript to detect login forms on the page
    /// </summary>
    public static string GetDetectLoginFormScript()
    {
        return @"
(function() {
    const passwordFields = document.querySelectorAll('input[type=""password""]');
    if (passwordFields.length === 0) return null;

    // Find the form containing the password field
    const passwordField = passwordFields[0];
    const form = passwordField.closest('form');

    // Find username field near the password field
    let usernameField = null;
    const usernameSelectors = [
        'input[type=""email""]',
        'input[type=""text""][name*=""user""]',
        'input[type=""text""][name*=""email""]',
        'input[type=""text""][name*=""login""]',
        'input[name=""username""]',
        'input[name=""email""]',
        'input[name=""identifier""]'
    ];

    const searchContainer = form || document;
    for (const selector of usernameSelectors) {
        const field = searchContainer.querySelector(selector);
        if (field && field.offsetParent !== null) {
            usernameField = field;
            break;
        }
    }

    return {
        hasLoginForm: true,
        hasUsernameField: !!usernameField,
        hasPasswordField: true,
        usernameValue: usernameField ? usernameField.value : '',
        isSignUp: document.body.innerText.toLowerCase().includes('sign up') ||
                  document.body.innerText.toLowerCase().includes('create account') ||
                  document.body.innerText.toLowerCase().includes('register')
    };
})();
";
    }

    /// <summary>
    /// Gets JavaScript to capture submitted credentials - smart detection like Chrome
    /// </summary>
    public static string GetCaptureCredentialsScript()
    {
        return @"
(function() {
    if (window.__zarlaCaptureInitialized) return true;
    window.__zarlaCaptureInitialized = true;

    // Track password fields and their associated username fields
    let trackedForms = new Map();

    // Find username field near a password field
    function findUsernameField(passwordField) {
        const form = passwordField.closest('form');
        const container = form || document.body;

        const selectors = [
            'input[type=""email""]',
            'input[type=""text""][name*=""user""]',
            'input[type=""text""][name*=""email""]',
            'input[type=""text""][name*=""login""]',
            'input[type=""text""][name*=""account""]',
            'input[type=""text""][id*=""user""]',
            'input[type=""text""][id*=""email""]',
            'input[type=""text""][id*=""login""]',
            'input[type=""tel""]', // Sometimes phone is used as username
            'input[autocomplete=""username""]',
            'input[autocomplete=""email""]',
            'input[name=""username""]',
            'input[name=""email""]',
            'input[name=""login""]',
            'input[name=""identifier""]',
            'input[name=""userId""]',
            'input[name=""loginId""]'
        ];

        for (const selector of selectors) {
            const fields = container.querySelectorAll(selector);
            for (const field of fields) {
                if (field.value && field.offsetParent !== null) {
                    return field;
                }
            }
        }

        // Fallback: find any visible text input before the password field
        const allInputs = Array.from(container.querySelectorAll('input[type=""text""], input[type=""email""]'));
        for (const input of allInputs) {
            if (input.value && input.offsetParent !== null) {
                // Check if it appears before the password field in DOM order
                const inputRect = input.getBoundingClientRect();
                const passRect = passwordField.getBoundingClientRect();
                if (inputRect.top <= passRect.top) {
                    return input;
                }
            }
        }

        return null;
    }

    // Capture and send credentials
    function captureCredentials(passwordField, trigger) {
        if (!passwordField.value || passwordField.value.length < 3) return;

        const usernameField = findUsernameField(passwordField);
        if (!usernameField || !usernameField.value) return;

        // Don't capture if it looks like registration (password confirmation nearby)
        const form = passwordField.closest('form');
        if (form) {
            const passwordFields = form.querySelectorAll('input[type=""password""]');
            if (passwordFields.length > 1) {
                // Multiple password fields = likely registration, skip
                return;
            }
        }

        // Extract clean domain
        let site = window.location.hostname;
        if (site.startsWith('www.')) site = site.substring(4);

        const key = site + '|' + usernameField.value;
        if (trackedForms.has(key)) return; // Already captured
        trackedForms.set(key, true);

        window.chrome.webview.postMessage({
            type: 'credentials_captured',
            username: usernameField.value,
            password: passwordField.value,
            url: window.location.href,
            site: site,
            trigger: trigger
        });
    }

    // Intercept form submissions
    document.addEventListener('submit', function(e) {
        const form = e.target;
        const passwordField = form.querySelector('input[type=""password""]');
        if (passwordField) {
            captureCredentials(passwordField, 'form_submit');
        }
    }, true);

    // Intercept button clicks that might submit login
    document.addEventListener('click', function(e) {
        const button = e.target.closest('button, input[type=""submit""], [role=""button""]');
        if (!button) return;

        const text = (button.textContent || button.value || '').toLowerCase();
        const isLoginButton = text.includes('sign in') || text.includes('log in') ||
                             text.includes('login') || text.includes('submit') ||
                             text.includes('continue') || text.includes('next');

        if (isLoginButton) {
            // Find password field in the same form or nearby
            const form = button.closest('form');
            const container = form || document.body;
            const passwordField = container.querySelector('input[type=""password""]');

            if (passwordField && passwordField.value) {
                setTimeout(() => captureCredentials(passwordField, 'button_click'), 100);
            }
        }
    }, true);

    // Intercept Enter key in password fields
    document.addEventListener('keydown', function(e) {
        if (e.key !== 'Enter') return;

        const target = e.target;
        if (target.type === 'password' && target.value) {
            setTimeout(() => captureCredentials(target, 'enter_key'), 100);
        }
    }, true);

    // Watch for AJAX form submissions (password field losing focus with value)
    document.addEventListener('blur', function(e) {
        if (e.target.type === 'password' && e.target.value && e.target.value.length >= 6) {
            // Delay to see if form is submitted via AJAX
            setTimeout(() => {
                // Only capture if we're still on the same page (not redirected)
                if (document.contains(e.target)) {
                    // Don't capture on blur, wait for explicit action
                }
            }, 500);
        }
    }, true);

    return true;
})();
";
    }

    /// <summary>
    /// Gets JavaScript to show autofill popup near login fields
    /// </summary>
    public string GetShowAutofillPopupScript(List<PasswordEntry> entries)
    {
        if (entries.Count == 0) return "";

        var entriesJson = string.Join(",", entries.Select(e =>
            $"{{username: '{EscapeJsString(e.Username)}', website: '{EscapeJsString(e.Website)}'}}"
        ));

        return $@"
(function() {{
    // Remove existing popup
    const existing = document.getElementById('zarla-autofill-popup');
    if (existing) existing.remove();

    const entries = [{entriesJson}];

    // Find password field
    const passwordField = document.querySelector('input[type=""password""]');
    if (!passwordField) return;

    // Create popup
    const popup = document.createElement('div');
    popup.id = 'zarla-autofill-popup';
    popup.style.cssText = `
        position: absolute;
        background: #1a1a1a;
        border: 1px solid #333;
        border-radius: 8px;
        box-shadow: 0 4px 12px rgba(0,0,0,0.3);
        z-index: 999999;
        min-width: 250px;
        max-width: 350px;
        font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
    `;

    // Header
    const header = document.createElement('div');
    header.style.cssText = 'padding: 12px 16px; border-bottom: 1px solid #333; display: flex; align-items: center;';
    header.innerHTML = '<span style=""color: #8ab4f8; margin-right: 8px;"">🔐</span><span style=""color: white; font-size: 14px; font-weight: 500;"">Zarla Passwords</span>';
    popup.appendChild(header);

    // Entries
    entries.forEach((entry, index) => {{
        const item = document.createElement('div');
        item.style.cssText = 'padding: 12px 16px; cursor: pointer; transition: background 0.2s;';
        item.innerHTML = `
            <div style=""color: white; font-size: 13px; font-weight: 500;"">${{entry.username}}</div>
            <div style=""color: #888; font-size: 11px; margin-top: 2px;"">${{entry.website}}</div>
        `;
        item.onmouseover = () => item.style.background = '#252525';
        item.onmouseout = () => item.style.background = 'transparent';
        item.onclick = () => {{
            window.chrome.webview.postMessage({{
                type: 'autofill_selected',
                index: index
            }});
            popup.remove();
        }};
        popup.appendChild(item);
    }});

    // Position popup below password field
    const rect = passwordField.getBoundingClientRect();
    popup.style.left = rect.left + window.scrollX + 'px';
    popup.style.top = rect.bottom + window.scrollY + 4 + 'px';

    document.body.appendChild(popup);

    // Close on click outside
    setTimeout(() => {{
        document.addEventListener('click', function closePopup(e) {{
            if (!popup.contains(e.target) && e.target !== passwordField) {{
                popup.remove();
                document.removeEventListener('click', closePopup);
            }}
        }});
    }}, 100);
}})();
";
    }

    /// <summary>
    /// Escapes a string for use in JavaScript
    /// </summary>
    private static string EscapeJsString(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        return input
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    /// <summary>
    /// Gets matching password entries for a URL
    /// </summary>
    public PasswordEntry? GetMatchingEntry(string url)
    {
        if (!_passwordManager.IsUnlocked) return null;
        return _passwordManager.GetPasswordForSite(url);
    }

    /// <summary>
    /// Gets all matching entries for a URL
    /// </summary>
    public List<PasswordEntry> GetMatchingEntries(string url)
    {
        if (!_passwordManager.IsUnlocked) return new List<PasswordEntry>();

        try
        {
            var uri = new Uri(url);
            var host = uri.Host.ToLower();
            if (host.StartsWith("www."))
                host = host.Substring(4);

            return _passwordManager.Entries
                .Where(e =>
                {
                    var entryHost = e.Website.ToLower();
                    if (entryHost.StartsWith("http"))
                    {
                        try
                        {
                            var entryUri = new Uri(entryHost);
                            entryHost = entryUri.Host;
                        }
                        catch { }
                    }
                    if (entryHost.StartsWith("www."))
                        entryHost = entryHost.Substring(4);

                    return entryHost == host || host.EndsWith("." + entryHost) || entryHost.Contains(host);
                })
                .ToList();
        }
        catch
        {
            return new List<PasswordEntry>();
        }
    }
}
