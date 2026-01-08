namespace Zarla.Core.Privacy;

public class FingerprintProtection
{
    private bool _isEnabled = true;

    public bool IsEnabled
    {
        get => _isEnabled;
        set => _isEnabled = value;
    }

    /// <summary>
    /// JavaScript to inject for fingerprint protection
    /// </summary>
    public string GetProtectionScript()
    {
        if (!_isEnabled) return string.Empty;

        return """
            (function() {
                'use strict';

                // Randomize canvas fingerprint
                const originalToDataURL = HTMLCanvasElement.prototype.toDataURL;
                HTMLCanvasElement.prototype.toDataURL = function(type) {
                    const context = this.getContext('2d');
                    if (context) {
                        const imageData = context.getImageData(0, 0, this.width, this.height);
                        for (let i = 0; i < imageData.data.length; i += 4) {
                            imageData.data[i] ^= (Math.random() * 2) | 0;
                        }
                        context.putImageData(imageData, 0, 0);
                    }
                    return originalToDataURL.apply(this, arguments);
                };

                // Randomize WebGL fingerprint
                const getParameterProxyHandler = {
                    apply: function(target, thisArg, args) {
                        const param = args[0];
                        const result = Reflect.apply(target, thisArg, args);

                        // Randomize renderer and vendor strings
                        if (param === 37445) { // UNMASKED_VENDOR_WEBGL
                            return 'Generic Vendor';
                        }
                        if (param === 37446) { // UNMASKED_RENDERER_WEBGL
                            return 'Generic Renderer';
                        }
                        return result;
                    }
                };

                try {
                    const webgl = document.createElement('canvas').getContext('webgl');
                    if (webgl) {
                        webgl.getParameter = new Proxy(webgl.getParameter, getParameterProxyHandler);
                    }
                } catch(e) {}

                // Limit navigator properties exposure
                const navigatorProps = {
                    hardwareConcurrency: 4,
                    deviceMemory: 8,
                    platform: 'Win32'
                };

                for (const [prop, value] of Object.entries(navigatorProps)) {
                    try {
                        Object.defineProperty(Navigator.prototype, prop, {
                            get: () => value,
                            configurable: true
                        });
                    } catch(e) {}
                }

                // Randomize audio fingerprint
                const originalGetChannelData = AudioBuffer.prototype.getChannelData;
                AudioBuffer.prototype.getChannelData = function(channel) {
                    const data = originalGetChannelData.call(this, channel);
                    for (let i = 0; i < data.length; i += 100) {
                        data[i] += Math.random() * 0.0001;
                    }
                    return data;
                };

                // Disable battery API
                if (navigator.getBattery) {
                    navigator.getBattery = undefined;
                }

                // Limit screen info
                try {
                    Object.defineProperty(screen, 'availWidth', { get: () => screen.width });
                    Object.defineProperty(screen, 'availHeight', { get: () => screen.height });
                    Object.defineProperty(screen, 'availLeft', { get: () => 0 });
                    Object.defineProperty(screen, 'availTop', { get: () => 0 });
                } catch(e) {}

                // Block client rects fingerprinting noise (create new DOMRectList with noise)
                const originalGetClientRects = Element.prototype.getClientRects;
                Element.prototype.getClientRects = function() {
                    const rects = originalGetClientRects.call(this);
                    // DOMRect objects are read-only, so just return original rects
                    // Adding noise here would break layouts
                    return rects;
                };

                console.log('[Zarla] Fingerprint protection active');
            })();
            """;
    }

    /// <summary>
    /// Additional headers to send for privacy
    /// </summary>
    public Dictionary<string, string> GetPrivacyHeaders()
    {
        return new Dictionary<string, string>
        {
            { "DNT", "1" },
            { "Sec-GPC", "1" },  // Global Privacy Control
            { "Permissions-Policy", "interest-cohort=()" }  // Disable FLoC
        };
    }
}
