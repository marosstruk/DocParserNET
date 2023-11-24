using OpenAI;
using OpenAI.Interfaces;
using OpenAI.Managers;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing.Text;

namespace DocParser
{
    public class GptTextCorrector : ITextCorrector
    {
        private const string API_KEY = "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";
        private Model gptModel;
        private OpenAIService gpt;

        public enum Model
        {
            DaVinci,
            GPT3_5Turbo,
            GPT4
        }

        public GptTextCorrector(Model gptModel = Model.GPT3_5Turbo)
        {
            this.gptModel = gptModel;
            this.gpt = new OpenAIService(new OpenAiOptions()
            {
                ApiKey = API_KEY
            });
        }

        public string Correct(string text)
        {
            return this.CorrectAsync(text).GetAwaiter().GetResult();
        }

        public async Task<string> CorrectAsync(string text)
        {
            if (this.gptModel == Model.DaVinci)
            {
                var completionResult = await this.gpt.Completions.CreateCompletion(new CompletionCreateRequest()
                {
                    Prompt = $"Please correct spelling in the following text inside the quotes:\n\"{text}\"",
                    Model = Models.TextDavinciV3,
                    Temperature = 0.5F,
                    MaxTokens = 1000
                });

                if (completionResult.Successful)
                {
                    return completionResult.Choices.First().Text;
                }
                else
                {
                    if (completionResult.Error == null)
                    {
                        throw new Exception("Unknown Error");
                    }
                    Console.WriteLine($"{completionResult.Error.Code}: {completionResult.Error.Message}");
                    return "";
                }
            }
            else
            {
                var completionResult = await this.gpt.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
                {
                    Messages = new List<ChatMessage>
                    {
                        ChatMessage.FromSystem("You are a helpful assistant."),
                        ChatMessage.FromUser($"Please correct spelling in the following text inside the quotes:\n\"{text}\"")
                    },
                    Model = gptModel == Model.GPT4 ? Models.Gpt_4 : Models.ChatGpt3_5Turbo
                });

                if (completionResult.Successful)
                {
                    return completionResult.Choices.First().Message.Content;
                }
                else
                {
                    if (completionResult.Error == null)
                    {
                        throw new Exception("Unknown Error");
                    }
                    Console.WriteLine($"{completionResult.Error.Code}: {completionResult.Error.Message}");
                    return "";
                }
            }
        }
    }
}
