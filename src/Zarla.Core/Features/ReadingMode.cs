namespace Zarla.Core.Features;

/// <summary>
/// Provides reading mode functionality for distraction-free reading
/// </summary>
public static class ReadingMode
{
    /// <summary>
    /// Gets the JavaScript to inject for enabling reading mode
    /// </summary>
    public static string GetReadingModeScript(bool isDarkMode)
    {
        var bgColor = isDarkMode ? "#1a1a1a" : "#fafafa";
        var textColor = isDarkMode ? "#e0e0e0" : "#333333";
        var linkColor = isDarkMode ? "#6ea8fe" : "#0066cc";

        return $@"
(function() {{
    'use strict';

    // Check if already in reading mode
    if (window.__zarlaReadingMode) {{
        // Exit reading mode - reload the page
        location.reload();
        return;
    }}

    window.__zarlaReadingMode = true;

    // Find the main content
    function findMainContent() {{
        // Try common article selectors
        const selectors = [
            'article',
            '[role=""main""]',
            'main',
            '.post-content',
            '.article-content',
            '.entry-content',
            '.content',
            '#content',
            '.post',
            '.article'
        ];

        for (const selector of selectors) {{
            const el = document.querySelector(selector);
            if (el && el.textContent.length > 500) {{
                return el;
            }}
        }}

        // Fallback: find the element with most text
        let maxText = '';
        let maxElement = document.body;

        document.querySelectorAll('div, section, article').forEach(el => {{
            const text = el.textContent || '';
            if (text.length > maxText.length && text.length > 500) {{
                // Check if this element is visible
                const rect = el.getBoundingClientRect();
                if (rect.width > 200 && rect.height > 200) {{
                    maxText = text;
                    maxElement = el;
                }}
            }}
        }});

        return maxElement;
    }}

    // Extract title
    function getTitle() {{
        const h1 = document.querySelector('h1');
        if (h1) return h1.textContent;
        return document.title;
    }}

    // Get the main content
    const mainContent = findMainContent();
    const title = getTitle();

    // Clone the content
    const content = mainContent.cloneNode(true);

    // Remove unwanted elements from clone
    content.querySelectorAll('script, style, nav, header, footer, aside, .ad, .advertisement, .social-share, .comments, .sidebar, [role=""navigation""], [role=""complementary""]').forEach(el => el.remove());

    // Create reading mode overlay
    const overlay = document.createElement('div');
    overlay.id = 'zarla-reading-mode';
    overlay.innerHTML = `
        <style>
            #zarla-reading-mode {{
                position: fixed;
                top: 0;
                left: 0;
                width: 100%;
                height: 100%;
                background: {bgColor};
                z-index: 999999;
                overflow-y: auto;
                font-family: Georgia, 'Times New Roman', serif;
            }}
            #zarla-reading-mode .zarla-reader-container {{
                max-width: 700px;
                margin: 0 auto;
                padding: 40px 20px;
                color: {textColor};
                line-height: 1.8;
            }}
            #zarla-reading-mode .zarla-reader-header {{
                margin-bottom: 30px;
                padding-bottom: 20px;
                border-bottom: 1px solid {(isDarkMode ? "#333" : "#ddd")};
            }}
            #zarla-reading-mode .zarla-reader-title {{
                font-size: 32px;
                font-weight: bold;
                margin: 0 0 10px 0;
                line-height: 1.3;
            }}
            #zarla-reading-mode .zarla-reader-meta {{
                font-size: 14px;
                color: {(isDarkMode ? "#888" : "#666")};
            }}
            #zarla-reading-mode .zarla-reader-content {{
                font-size: 18px;
            }}
            #zarla-reading-mode .zarla-reader-content p {{
                margin: 0 0 1.5em 0;
            }}
            #zarla-reading-mode .zarla-reader-content h1,
            #zarla-reading-mode .zarla-reader-content h2,
            #zarla-reading-mode .zarla-reader-content h3 {{
                margin: 1.5em 0 0.5em 0;
                font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
            }}
            #zarla-reading-mode .zarla-reader-content img {{
                max-width: 100%;
                height: auto;
                margin: 1em 0;
                border-radius: 8px;
            }}
            #zarla-reading-mode .zarla-reader-content a {{
                color: {linkColor};
            }}
            #zarla-reading-mode .zarla-reader-content blockquote {{
                border-left: 4px solid {linkColor};
                margin: 1em 0;
                padding-left: 20px;
                font-style: italic;
            }}
            #zarla-reading-mode .zarla-reader-content code {{
                background: {(isDarkMode ? "#2d2d2d" : "#f0f0f0")};
                padding: 2px 6px;
                border-radius: 4px;
                font-family: 'Consolas', monospace;
            }}
            #zarla-reading-mode .zarla-reader-content pre {{
                background: {(isDarkMode ? "#2d2d2d" : "#f0f0f0")};
                padding: 15px;
                border-radius: 8px;
                overflow-x: auto;
            }}
            #zarla-reading-mode .zarla-close-btn {{
                position: fixed;
                top: 20px;
                right: 20px;
                background: {(isDarkMode ? "#333" : "#f0f0f0")};
                border: none;
                padding: 10px 20px;
                border-radius: 20px;
                cursor: pointer;
                font-size: 14px;
                color: {textColor};
                z-index: 1000000;
            }}
            #zarla-reading-mode .zarla-close-btn:hover {{
                background: {(isDarkMode ? "#444" : "#e0e0e0")};
            }}
        </style>
        <button class=""zarla-close-btn"" onclick=""location.reload()"">Exit Reading Mode</button>
        <div class=""zarla-reader-container"">
            <div class=""zarla-reader-header"">
                <h1 class=""zarla-reader-title"">${{title}}</h1>
                <div class=""zarla-reader-meta"">${{window.location.hostname}}</div>
            </div>
            <div class=""zarla-reader-content""></div>
        </div>
    `;

    document.body.appendChild(overlay);
    overlay.querySelector('.zarla-reader-content').appendChild(content);

    // Prevent background scrolling
    document.body.style.overflow = 'hidden';

    // ESC to exit
    document.addEventListener('keydown', function(e) {{
        if (e.key === 'Escape' && window.__zarlaReadingMode) {{
            location.reload();
        }}
    }});
}})();
";
    }
}
