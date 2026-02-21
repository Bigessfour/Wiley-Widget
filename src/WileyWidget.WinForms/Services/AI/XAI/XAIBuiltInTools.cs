#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel;

namespace WileyWidget.WinForms.Services.AI.XAI
{
    /// <summary>
    /// Provides xAI built-in tool definitions and helper methods for Semantic Kernel integration.
    /// These tools execute on xAI's servers when invoked by Grok.
    /// 
    /// xAI Built-in Tools (Server-side):
    /// - web_search: Real-time web search and page browsing
    /// - x_search: Search X (Twitter) posts, users, threads
    /// - code_execution (aka code_interpreter): Python sandbox with pandas, numpy, matplotlib, scipy
    /// - collections_search: Query uploaded knowledge base documents
    /// 
    /// Reference: https://docs.x.ai/developers/tools/overview
    /// </summary>
    public static class XAIBuiltInTools
    {
        /// <summary>
        /// Tool types supported by xAI API
        /// </summary>
        public enum ToolType
        {
            WebSearch,
            XSearch,
            CodeExecution,
            CollectionsSearch
        }

        /// <summary>
        /// Configuration for xAI built-in tools
        /// </summary>
        public sealed class XAIToolConfiguration
        {
            [JsonPropertyName("enabled")]
            public bool Enabled { get; set; } = false;

            [JsonPropertyName("web_search")]
            public WebSearchConfig? WebSearch { get; set; }

            [JsonPropertyName("x_search")]
            public XSearchConfig? XSearch { get; set; }

            [JsonPropertyName("code_execution")]
            public CodeExecutionConfig? CodeExecution { get; set; }

            [JsonPropertyName("collections_search")]
            public CollectionsSearchConfig? CollectionsSearch { get; set; }
        }

        /// <summary>
        /// Web Search tool configuration
        /// </summary>
        public sealed class WebSearchConfig
        {
            [JsonPropertyName("enabled")]
            public bool Enabled { get; set; } = false;

            [JsonPropertyName("allowed_domains")]
            public List<string>? AllowedDomains { get; set; }

            [JsonPropertyName("excluded_domains")]
            public List<string>? ExcludedDomains { get; set; }

            [JsonPropertyName("enable_image_understanding")]
            public bool EnableImageUnderstanding { get; set; } = false;

            [JsonIgnore]
            public int MaxDomains => 5; // xAI limit
        }

        /// <summary>
        /// X Search tool configuration
        /// </summary>
        public sealed class XSearchConfig
        {
            [JsonPropertyName("enabled")]
            public bool Enabled { get; set; } = false;

            [JsonPropertyName("enable_image_understanding")]
            public bool EnableImageUnderstanding { get; set; } = false;
        }

        /// <summary>
        /// Code Execution (Python sandbox) tool configuration
        /// </summary>
        public sealed class CodeExecutionConfig
        {
            [JsonPropertyName("enabled")]
            public bool Enabled { get; set; } = false;

            [JsonPropertyName("timeout_seconds")]
            public int TimeoutSeconds { get; set; } = 30;

            [JsonIgnore]
            public string Description => "Python 3.x sandbox with pandas, numpy, matplotlib, scipy, sympy pre-installed";
        }

        /// <summary>
        /// Collections Search tool configuration
        /// </summary>
        public sealed class CollectionsSearchConfig
        {
            [JsonPropertyName("enabled")]
            public bool Enabled { get; set; } = false;

            [JsonPropertyName("collection_ids")]
            public List<string>? CollectionIds { get; set; }
        }

        /// <summary>
        /// Creates tool definitions for xAI API (OpenAI-compatible format)
        /// These are passed to the API and executed server-side by xAI
        /// </summary>
        public static List<object> CreateToolDefinitions(XAIToolConfiguration config)
        {
            var tools = new List<object>();

            if (!config.Enabled) return tools;

            // Web Search Tool
            if (config.WebSearch?.Enabled == true)
            {
                var webSearchTool = new Dictionary<string, object>
                {
                    ["type"] = "function",
                    ["function"] = new Dictionary<string, object>
                    {
                        ["name"] = "web_search",
                        ["description"] = "Search the web in real-time and browse web pages to find up-to-date information. Use for current events, regulations, market data, or any information not in your training data.",
                        ["parameters"] = new Dictionary<string, object>
                        {
                            ["type"] = "object",
                            ["properties"] = new Dictionary<string, object>
                            {
                                ["query"] = new Dictionary<string, object>
                                {
                                    ["type"] = "string",
                                    ["description"] = "The search query to execute"
                                }
                            },
                            ["required"] = new[] { "query" }
                        }
                    }
                };

                // Add optional parameters if configured
                var functionDict = (Dictionary<string, object>)webSearchTool["function"];
                var parametersDict = (Dictionary<string, object>)functionDict["parameters"];
                var propertiesDict = (Dictionary<string, object>)parametersDict["properties"];

                if (config.WebSearch.AllowedDomains?.Count > 0)
                {
                    propertiesDict["allowed_domains"] = new Dictionary<string, object>
                    {
                        ["type"] = "array",
                        ["items"] = new Dictionary<string, object> { ["type"] = "string" },
                        ["description"] = "List of domains to restrict search to (max 5)",
                        ["maxItems"] = config.WebSearch.MaxDomains
                    };
                }

                if (config.WebSearch.ExcludedDomains?.Count > 0)
                {
                    propertiesDict["excluded_domains"] = new Dictionary<string, object>
                    {
                        ["type"] = "array",
                        ["items"] = new Dictionary<string, object> { ["type"] = "string" },
                        ["description"] = "List of domains to exclude from search (max 5)",
                        ["maxItems"] = config.WebSearch.MaxDomains
                    };
                }

                if (config.WebSearch.EnableImageUnderstanding)
                {
                    propertiesDict["enable_image_understanding"] = new Dictionary<string, object>
                    {
                        ["type"] = "boolean",
                        ["description"] = "Enable analysis of images found during browsing",
                        ["default"] = true
                    };
                }

                tools.Add(webSearchTool);
            }

            // X Search Tool
            if (config.XSearch?.Enabled == true)
            {
                var xSearchTool = new Dictionary<string, object>
                {
                    ["type"] = "function",
                    ["function"] = new Dictionary<string, object>
                    {
                        ["name"] = "x_search",
                        ["description"] = "Search X (Twitter) for posts, users, threads, and extract relevant social sentiment. Use for public discourse, trending topics, or community sentiment analysis.",
                        ["parameters"] = new Dictionary<string, object>
                        {
                            ["type"] = "object",
                            ["properties"] = new Dictionary<string, object>
                            {
                                ["query"] = new Dictionary<string, object>
                                {
                                    ["type"] = "string",
                                    ["description"] = "The X search query"
                                }
                            },
                            ["required"] = new[] { "query" }
                        }
                    }
                };

                if (config.XSearch.EnableImageUnderstanding)
                {
                    var functionDict = (Dictionary<string, object>)xSearchTool["function"];
                    var parametersDict = (Dictionary<string, object>)functionDict["parameters"];
                    var propertiesDict = (Dictionary<string, object>)parametersDict["properties"];

                    propertiesDict["enable_image_understanding"] = new Dictionary<string, object>
                    {
                        ["type"] = "boolean",
                        ["description"] = "Enable analysis of images in posts",
                        ["default"] = true
                    };
                }

                tools.Add(xSearchTool);
            }

            // Code Execution Tool (Python sandbox)
            if (config.CodeExecution?.Enabled == true)
            {
                tools.Add(new Dictionary<string, object>
                {
                    ["type"] = "function",
                    ["function"] = new Dictionary<string, object>
                    {
                        ["name"] = "code_interpreter", // xAI OpenAI-compatible name
                        ["description"] = "Execute Python code in a secure sandbox with pandas, numpy, matplotlib, scipy, and sympy. Use for complex calculations, statistical analysis, financial modeling, or data visualization. " + config.CodeExecution.Description,
                        ["parameters"] = new Dictionary<string, object>
                        {
                            ["type"] = "object",
                            ["properties"] = new Dictionary<string, object>
                            {
                                ["code"] = new Dictionary<string, object>
                                {
                                    ["type"] = "string",
                                    ["description"] = "Python code to execute"
                                }
                            },
                            ["required"] = new[] { "code" }
                        }
                    }
                });
            }

            // Collections Search Tool
            if (config.CollectionsSearch?.Enabled == true)
            {
                var collectionsTool = new Dictionary<string, object>
                {
                    ["type"] = "function",
                    ["function"] = new Dictionary<string, object>
                    {
                        ["name"] = "collections_search",
                        ["description"] = "Query uploaded knowledge base documents (PDFs, reports, compliance docs). Use for organization-specific information.",
                        ["parameters"] = new Dictionary<string, object>
                        {
                            ["type"] = "object",
                            ["properties"] = new Dictionary<string, object>
                            {
                                ["query"] = new Dictionary<string, object>
                                {
                                    ["type"] = "string",
                                    ["description"] = "Search query for document collection"
                                }
                            },
                            ["required"] = new[] { "query" }
                        }
                    }
                };

                if (config.CollectionsSearch.CollectionIds?.Count > 0)
                {
                    var functionDict = (Dictionary<string, object>)collectionsTool["function"];
                    var parametersDict = (Dictionary<string, object>)functionDict["parameters"];
                    var propertiesDict = (Dictionary<string, object>)parametersDict["properties"];

                    propertiesDict["collection_ids"] = new Dictionary<string, object>
                    {
                        ["type"] = "array",
                        ["items"] = new Dictionary<string, object> { ["type"] = "string" },
                        ["description"] = "Specific collection IDs to search"
                    };
                }

                tools.Add(collectionsTool);
            }

            return tools;
        }

        /// <summary>
        /// Creates a default configuration with reasonable defaults for municipal finance use cases
        /// </summary>
        public static XAIToolConfiguration CreateDefaultMunicipalFinanceConfig()
        {
            return new XAIToolConfiguration
            {
                Enabled = true,
                WebSearch = new WebSearchConfig
                {
                    Enabled = true,
                    EnableImageUnderstanding = false,
                    ExcludedDomains = new List<string>(), // No restrictions by default
                    AllowedDomains = null // Search all domains
                },
                CodeExecution = new CodeExecutionConfig
                {
                    Enabled = true,
                    TimeoutSeconds = 60 // Municipal finance calculations may be complex
                },
                XSearch = new XSearchConfig
                {
                    Enabled = false, // Off by default - enable for sentiment analysis
                    EnableImageUnderstanding = false
                },
                CollectionsSearch = new CollectionsSearchConfig
                {
                    Enabled = false, // Requires documents to be uploaded first
                    CollectionIds = new List<string>()
                }
            };
        }

        /// <summary>
        /// Validates tool configuration against xAI API limits
        /// </summary>
        public static (bool IsValid, List<string> Errors) ValidateConfiguration(XAIToolConfiguration config)
        {
            var errors = new List<string>();

            if (!config.Enabled)
            {
                return (true, errors); // Valid to be disabled
            }

            // Validate web search
            if (config.WebSearch?.Enabled == true)
            {
                if (config.WebSearch.AllowedDomains != null && config.WebSearch.ExcludedDomains != null)
                {
                    if (config.WebSearch.AllowedDomains.Count > 0 && config.WebSearch.ExcludedDomains.Count > 0)
                    {
                        errors.Add("WebSearch: Cannot specify both allowed_domains and excluded_domains");
                    }
                }

                if (config.WebSearch.AllowedDomains?.Count > config.WebSearch.MaxDomains)
                {
                    errors.Add($"WebSearch: allowed_domains exceeds limit of {config.WebSearch.MaxDomains}");
                }

                if (config.WebSearch.ExcludedDomains?.Count > config.WebSearch.MaxDomains)
                {
                    errors.Add($"WebSearch: excluded_domains exceeds limit of {config.WebSearch.MaxDomains}");
                }
            }

            // Validate code execution
            if (config.CodeExecution?.Enabled == true)
            {
                if (config.CodeExecution.TimeoutSeconds < 1 || config.CodeExecution.TimeoutSeconds > 300)
                {
                    errors.Add("CodeExecution: timeout_seconds must be between 1 and 300");
                }
            }

            return (errors.Count == 0, errors);
        }
    }
}
