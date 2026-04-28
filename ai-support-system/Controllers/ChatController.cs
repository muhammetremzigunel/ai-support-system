using Markdig;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using AiSupportApp.DTOs;
using AiSupportApp.Extensions;
using AiSupportApp.Services;

namespace AiSupportApp.Controllers
{
    [Authorize]
    public class ChatController : Controller
    {
        private readonly IChatClient _chatClient;
        private readonly RagPipeline _ragPipeline;

        private const string ChatHistoryKey = "ChatHistory";
        private const int MaxHistoryMessages = 10;

        public ChatController(IChatClient chatClient, RagPipeline ragPipeline)
        {
            _chatClient = chatClient;
            _ragPipeline = ragPipeline;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage(string message, CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrEmpty(message) && message.Length > 2000)
            {
                message = message.Substring(0, 2000);
            }

            // 1. Retrieve RAG context for the current query
            var context = await _ragPipeline.AskAsync(message, cancellationToken);

            string systemPrompt = string.IsNullOrEmpty(context)
                ? "İlgili içerik bulunamadı."
                : $"Sen bir müşteri destek asistanısın. Sadece aşağıdaki bilgilere dayanarak cevap ver ve [doc:ID#CHUNK] şeklinde alıntı yap, eğer yeterli bilgi yoksa 'bilmiyorum' de:\n\n{context}";

            // 2. Retrieve conversation history from session
            var history = HttpContext.Session.GetObject<List<ChatTurnDto>>(ChatHistoryKey) ?? new List<ChatTurnDto>();

            // 3. Build the full message list: System + History + Current User Message
            var chatMessages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, systemPrompt)
            };

            foreach (var turn in history)
            {
                var role = turn.Role == "assistant" ? ChatRole.Assistant : ChatRole.User;
                chatMessages.Add(new ChatMessage(role, turn.Text));
            }

            chatMessages.Add(new ChatMessage(ChatRole.User, message));

            // 4. Get the LLM response with full conversational context
            var response = await _chatClient.GetResponseAsync(chatMessages, cancellationToken: cancellationToken);

            // 5. Append current turn to history and enforce sliding window
            history.Add(new ChatTurnDto { Role = "user", Text = message });
            history.Add(new ChatTurnDto { Role = "assistant", Text = response.Text });

            // Keep only the last N messages to stay within context/session limits
            if (history.Count > MaxHistoryMessages)
            {
                history = history.GetRange(history.Count - MaxHistoryMessages, MaxHistoryMessages);
            }

            // 6. Persist back to session
            HttpContext.Session.SetObject(ChatHistoryKey, history);

            ViewBag.Message = message;
            ViewBag.Response = Markdown.ToHtml(response.Text);
            return View("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ClearHistory()
        {
            HttpContext.Session.Remove(ChatHistoryKey);
            return RedirectToAction("Index");
        }
    }
}