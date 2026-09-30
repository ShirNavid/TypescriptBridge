using System.Text.Json.Serialization;

namespace TypescriptBridge.Vsix.Debug
{
    // POCO mirroring the debug-manifest.json produced by
    // TypescriptBridge.Tool in --mode debug. The property names must
    // match the JsonPropertyName values on the Tool side exactly.
    //
    // This class is the contract between the Tool and the VSIX.
    internal sealed class DebugManifest
    {
        [JsonPropertyName("sessionId")]
        public string SessionId { get; set; } = "";

        [JsonPropertyName("projectRoot")]
        public string ProjectRoot { get; set; } = "";

        [JsonPropertyName("sessionRoot")]
        public string SessionRoot { get; set; } = "";

        [JsonPropertyName("javascript")]
        public string JavaScript { get; set; } = "";

        [JsonPropertyName("sourceMap")]
        public string SourceMap { get; set; } = "";

        [JsonPropertyName("html")]
        public string Html { get; set; } = "";

        [JsonPropertyName("serverScript")]
        public string ServerScript { get; set; } = "";

        [JsonPropertyName("esproj")]
        public string Esproj { get; set; } = "";

        [JsonPropertyName("launchJson")]
        public string LaunchJson { get; set; } = "";

        [JsonPropertyName("port")]
        public int Port { get; set; }

        [JsonPropertyName("format")]
        public string Format { get; set; } = "";

        [JsonPropertyName("configuration")]
        public string Configuration { get; set; } = "";
    }
}
