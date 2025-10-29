using Azure;
using Azure.AI.OpenAI;
using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Plugins.Web;
using Microsoft.SemanticKernel.Plugins.Core;
using Microsoft.SemanticKernel.Plugins.Web.Bing;
using Microsoft.SemanticKernel.TemplateEngine;
using Newtonsoft.Json;
using Kernel = Microsoft.SemanticKernel.Kernel;
using Microsoft.SemanticKernel.Planning.Handlebars;
using backend.api.Dtos;
using Microsoft.Azure.Cosmos;
using backend.api.NativePlugins;
using backend.api.Credentials;

namespace backend.api.pluginFunctions
{
    public class Plugin
    {
        public async Task<ResponseArray> BasicChat(string user_query, bool WebSearcherPluginChoice, bool AISearchPluginChoice, bool GraphPluginChoice)
        {
            IConfiguration config = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();

            // ✅ Local Mode Check
            string? localMode = config["LocalMode"];
            if (localMode != null && localMode.ToLower() == "true")
            {
                string mockResponse = $"🤖 Local AI (Mock Mode): You asked — '{user_query}'. This is a simulated offline response.";
                string mockPlan = "No Azure/OpenAI/Cosmos APIs used — running in Local Mode.";
                return new ResponseArray(mockResponse, mockPlan);
            }

            // 🔑 Load Azure/OpenAI credentials
            string? key = config["OPENAI_KEY"];
            string? endpoint = config["OPENAI_ENDPOINT"];
            string? model = config["OPENAI_CHAT_MODEL"];

            var builder = Kernel.CreateBuilder();

            // ✅ Safely add OpenAI model only if details exist
            if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(endpoint) && !string.IsNullOrEmpty(model))
            {
                builder.AddAzureOpenAIChatCompletion(model, endpoint, key);
            }
            else
            {
                Console.WriteLine("⚠️ Azure OpenAI credentials missing — proceeding without remote model (fallback mode).");
            }

            var kernel = builder.Build();
            string parentDirectory = Directory.GetCurrentDirectory();

            // 🧩 Load Plugins
            string writerPluginPath = Path.Combine(parentDirectory, "plugins", "promptTemplatePlugins", "WriterPlugin");
            if (Directory.Exists(writerPluginPath))
                kernel.ImportPluginFromPromptDirectory(writerPluginPath);

            kernel.ImportPluginFromType<BasicChatPlugin>();

            if (WebSearcherPluginChoice)
                kernel.ImportPluginFromType<WebSearcherPlugin>();
            if (AISearchPluginChoice)
                kernel.ImportPluginFromType<AISearchPlugin>();
            if (GraphPluginChoice)
                kernel.ImportPluginFromType<GraphPlugin>();

            var planner = new HandlebarsPlanner(new HandlebarsPlannerOptions() { AllowLoops = true });

            try
            {
                // 🧠 Try using Semantic Kernel planner
                var plan = await planner.CreatePlanAsync(kernel, user_query);
                var serializedPlan = plan.ToString();

                var result = await plan.InvokeAsync(kernel);
                var chatResponse = result.ToString();

                var responseArray = new ResponseArray(chatResponse, serializedPlan);
                return responseArray;
            }
            catch (Exception e)
            {
                Console.WriteLine("⚠️ Error in planner, switching to fallback mode...");
                Console.WriteLine(e.Message);

                try
                {
                    // 💬 Try using Azure OpenAI fallback
                    if (string.IsNullOrEmpty(Secrets.openaiEndpoint) || string.IsNullOrEmpty(Secrets.openaiKey))
                    {
                        throw new Exception("Missing OpenAI credentials in Secrets.");
                    }

                    OpenAIClient client = new OpenAIClient(new Uri(Secrets.openaiEndpoint!), new AzureKeyCredential(Secrets.openaiKey!));

                    string systemMessage = "You are a helpful AI assistant meant to assist the user by answering their queries.";
                    string userMessage = user_query;

                    ChatCompletionsOptions chatCompletionsOptions = new ChatCompletionsOptions()
                    {
                        Messages =
                        {
                            new ChatRequestSystemMessage(systemMessage),
                            new ChatRequestUserMessage(userMessage),
                        },
                        MaxTokens = 400,
                        Temperature = 0.7f,
                        DeploymentName = Secrets.openaiChatModel
                    };

                    ChatCompletions response = client.GetChatCompletions(chatCompletionsOptions);

                    string chatResponse = response.Choices[0].Message.Content;
                    string planUsed = "Used Azure OpenAI fallback chat completions.";

                    return new ResponseArray(chatResponse, planUsed);
                }
                catch (Exception fallbackError)
                {
                    // 🪶 Final Local fallback if both Azure & Cosmos fail
                    Console.WriteLine("⚠️ Azure & Cosmos fallback failed — using local offline response.");
                    Console.WriteLine(fallbackError.Message);

                    string chatResponse = $"🤖 Offline Local Mode: I cannot access external services. You asked: '{user_query}'.";
                    string planUsed = "No Azure/OpenAI used — full local fallback.";

                    return new ResponseArray(chatResponse, planUsed);
                }
            }
        }
    }
}
