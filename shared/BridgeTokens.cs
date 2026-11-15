using System;
using System.IO;
using System.Text.RegularExpressions;

// Per-install secrets for the loopback WebSocket bridges, written by install.ps1 as
// bridge-tokens.json next to the helper executables:
//   { "center": "<64 hex>", "spotlight": "<64 hex>" }
// A missing or malformed token is null, and the bridge then refuses every connection.
static class BridgeTokens {
    public static readonly string Center = Read("center");
    public static readonly string Spotlight = Read("spotlight");

    static string Read(string key) {
        try {
            var text = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bridge-tokens.json"));
            var match = Regex.Match(text, "\"" + key + "\"\\s*:\\s*\"([0-9a-f]{64})\"");
            return match.Success ? match.Groups[1].Value : null;
        } catch { return null; }
    }

    // True when the WebSocket request line carries this token.
    public static bool Matches(string requestLine, string token) {
        return token != null && requestLine == "GET /" + token + " HTTP/1.1";
    }
}
