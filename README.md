# ai-support-system

**Secure AI Assistant with RAG Pipeline** — A production-grade customer support chatbot powered by Google Gemini and Retrieval-Augmented Generation. Built with ASP.NET Core MVC, Qdrant vector search, and a modern Tailwind CSS interface with persistent dark/light mode.

---

## Tech Stack

| Layer | Technology |
|-------|------------|
| **Backend** | .NET 10 — ASP.NET Core MVC, ASP.NET Core Identity |
| **Database** | PostgreSQL (via Npgsql + Entity Framework Core) |
| **AI / LLM** | Google Gemini API (`gemini-2.5-flash`) via Microsoft.Extensions.AI |
| **Embeddings** | Gemini Embedding API (`gemini-embedding-001`, 3 072-dim vectors) |
| **Vector DB** | Qdrant (gRPC on port 6334) for RAG retrieval |
| **Frontend** | Tailwind CSS (Play CDN), Inter typeface, Vanilla JS — zero jQuery |
| **Markdown** | Markdig for server-side Markdown → HTML rendering |
| **Tokenizer** | Microsoft.ML.Tokenizers (`cl100k_base`) for chunk splitting |
| **Security** | JWT Bearer Authentication (HttpOnly cookie), Role-based access (`admin` / `user`), CSRF via `ValidateAntiForgeryToken`, Global Exception Handling |

---

## Project Structure

```
ai-support-system/
├── ai-support-system.slnx                    # Solution file
├── LICENSE.txt
├── README.md
│
└── ai-support-system/
    ├── ai-support-system.csproj              # .NET 10 project definition & NuGet packages
    ├── Program.cs                       # Application bootstrap, DI, middleware pipeline
    ├── appsettings.json                 # Configuration (Gemini, Qdrant, JWT, DB)
    │
    ├── Controllers/
    │   ├── AdminController.cs           # [Authorize(Roles="admin")] — User & document management
    │   ├── AuthController.cs            # Login, Register, Logout (JWT cookie flow)
    │   ├── ChatController.cs            # [Authorize] — Conversational RAG chat endpoint
    │   └── HomeController.cs            # Global error page handler
    │
    ├── Services/
    │   ├── RagPipeline.cs               # Orchestrates ingest (chunk → embed → upsert) and query (embed → search → context)
    │   ├── EmbeddingService.cs          # Gemini Embedding API client (query & batch document embedding)
    │   ├── QdrantService.cs             # Qdrant vector CRUD — collection, upsert, search, delete
    │   ├── JwtService.cs                # JWT token generation with role claims
    │   └── TextChunker.cs               # Recursive token-aware text splitter with overlap
    │
    ├── Models/
    │   ├── DocumentModel .cs            # Document entity (Id, Title, Category, Content)
    │   ├── DocumentChunkModel.cs        # Chunk entity with DocId, index, hash, content
    │   └── ErrorViewModel.cs            # Error page view model
    │
    ├── DTOs/
    │   ├── ChatTurnDto.cs               # Lightweight session-serializable chat turn
    │   ├── LoginDto.cs                  # Login form binding
    │   └── RegisterDto.cs               # Registration form binding
    │
    ├── Data/
    │   └── ApplicationDbContext.cs      # EF Core Identity DbContext (PostgreSQL)
    │
    ├── Enums/
    │   └── ErrorSeverity.cs             # Transient | Degraded | Fatal
    │
    ├── Exceptions/
    │   ├── DomainException.cs           # Abstract base (UserMessage + TechnicalDetail + Severity)
    │   ├── AIServiceException.cs        # Gemini / LLM errors
    │   ├── EmbeddingException.cs        # Embedding-specific errors
    │   └── VectorDatabaseException.cs   # Qdrant connectivity / operation errors
    │
    ├── Extensions/
    │   └── SessionExtensions.cs         # Generic JSON session get/set helpers
    │
    ├── Filters/
    │   └── DomainExceptionFilter.cs     # Global MVC filter — catches DomainException, renders user-friendly banners
    │
    ├── Migrations/                      # EF Core migration files (PostgreSQL)
    │
    ├── Views/
    │   ├── _ViewImports.cshtml
    │   ├── _ViewStart.cshtml
    │   ├── Shared/
    │   │   ├── _Layout.cshtml           # Master layout — navbar, dark/light toggle, Tailwind config
    │   │   ├── _Layout.cshtml.css       # Scoped layout styles
    │   │   ├── _ValidationScriptsPartial.cshtml
    │   │   └── Error.cshtml             # Global 500 error page
    │   ├── Chat/
    │   │   └── Index.cshtml             # Chat UI — message bubbles, input bar, "New Chat" action
    │   ├── Admin/
    │   │   ├── Index.cshtml             # User management — role switching
    │   │   └── Documents.cshtml         # Document CRUD — add, filter, edit (native <dialog>), delete
    │   ├── Auth/
    │   │   ├── Login.cshtml             # Login form
    │   │   └── Register.cshtml          # Registration form
    │   └── Home/
    │       └── Error.cshtml             # Fallback error view
    │
    ├── Properties/
    │   └── launchSettings.json
    │
    └── wwwroot/
        ├── css/
        │   └── site.css                 # Base overrides — animations, dialog backdrop, scrollbar
        ├── js/
        │   └── admin.js                 # Document edit modal logic (native <dialog> API)
        └── favicon.ico
```

---

## Key Features

### RAG Pipeline (Retrieval-Augmented Generation)

The application implements a full ingest-and-query RAG pipeline:

1. **Ingest** — Admin uploads a document → `TextChunker` splits it into ~500-token chunks with 60-token overlap → `EmbeddingService` generates 3 072-dim vectors via Gemini's `gemini-embedding-001` model → `QdrantService` upserts each chunk as a vector point with metadata payload.
2. **Query** — User sends a message → query is embedded with `RETRIEVAL_QUERY` task type → Qdrant returns the top 20 candidates above a 0.65 cosine similarity threshold → a basic **MMR (Maximal Marginal Relevance)** filter selects the top 5 chunks (max 2 per document for diversity) → the assembled context is injected into the Gemini system prompt.
3. **Deduplication** — Documents are SHA-256 hashed before ingestion; duplicates are silently skipped.
4. **Conversational Memory** — Chat history is maintained in-session with a sliding window of the last 10 messages.

### Modern Corporate UI

- **Tailwind CSS** via Play CDN with a custom `tailwind.config` extending the default theme.
- **Inter** web font for clean, professional typography.
- **Persistent Dark / Light Mode** toggle — theme preference is stored in `localStorage` and applied before first paint (FOUC prevention via inline script in `_Layout.cshtml`).
- **Slate-900 color palette** for the navbar and chat header; slate-50/slate-800 for content surfaces.
- Fully **responsive** layout with a collapsible mobile navigation menu.

### Resilient Architecture

- **Structured Exception Hierarchy** — `DomainException` → `AIServiceException`, `EmbeddingException`, `VectorDatabaseException`, each carrying a user-facing message, a technical detail string, and an `ErrorSeverity` enum (`Transient`, `Degraded`, `Fatal`).
- **Global `DomainExceptionFilter`** — catches domain exceptions in the MVC pipeline and renders non-disruptive in-page error banners with fade-in animation instead of redirecting to a generic 500 page.
- **Fallback Error Handler** — `UseExceptionHandler("/Home/Error")` catches any unhandled exception and renders a safe error page (environment-aware messaging).

### Admin Management

- **User Management** (`/Admin`) — View all registered users, change roles between `admin` and `user`.
- **Document CRUD** (`/Admin/Documents`) — Add, edit, and delete knowledge-base documents. Filtering by category and title. Edit uses a **native HTML `<dialog>`** modal powered by Vanilla JS (`admin.js`) — no Bootstrap or jQuery dependency.
- **Role-Gated Access** — Admin controllers are protected with `[Authorize(Roles = "admin")]`.

### Authentication & Security

- **ASP.NET Core Identity** with PostgreSQL for user persistence.
- **JWT Bearer** tokens generated via `JwtService` (HMAC-SHA256), stored in `HttpOnly`, `Secure`, `SameSite=Strict` cookies.
- JWT is configured as the default authentication scheme, overriding Identity's cookie defaults.
- All mutating endpoints are protected with `[ValidateAntiForgeryToken]`.
- Chat input is capped at 2 000 characters to prevent abuse.

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [PostgreSQL](https://www.postgresql.org/download/)
- [Qdrant](https://qdrant.tech/documentation/quick-start/) (running on `localhost:6334`)
- A [Google Gemini API Key](https://aistudio.google.com/apikey)

### Configuration

Update `ai-support-system/appsettings.json` (or use User Secrets / environment variables):

```jsonc
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=aisupportdb;Username=YOUR-USERNAME;Password=YOUR_PASSWORD"
  },
  "Qdrant": {
    "Host": "localhost",
    "Port": 6334
  },
  "Gemini": {
    "ApiKey": "YOUR_GEMINI_API_KEY",
    "Model": "gemini-2.5-flash",
    "EmbeddingModel": "gemini-embedding-001"
  },
  "Jwt": {
    "Secret": "YOUR_JWT_SECRET_KEY_MIN_32_CHARS",
    "Issuer": "ai-support-system",
    "Audience": "ai-support-system"
  }
}
```

### Build & Run

```bash
cd ai-support-system

# Restore dependencies
dotnet restore

# Apply EF Core migrations
dotnet ef database update

# Build the project
dotnet build

# Run the application
dotnet run
```

The application will be available at `https://localhost:5225` (or the port configured in `launchSettings.json`).

On first launch, the application automatically creates `admin` and `user` roles and initializes the Qdrant `support_chunks` collection.

---

### **First Admin Setup**

Initially, all users are registered with the `user` role. To promote yourself to **Admin**, follow these steps:

1. **Register** a new account (e.g., `test@test.com`) via the application UI.
2. **Connect** to your PostgreSQL database and execute the following SQL commands to link the user to the admin role:

```sql
-- 1. Find your User ID and the Admin Role ID
SELECT "Id", "Email" FROM "AspNetUsers" WHERE "Email" = 'test@test.com';
SELECT "Id" FROM "AspNetRoles" WHERE "Name" = 'admin';

-- 2. Link the user to the admin role 
-- Replace 'YOUR_USER_ID' and 'YOUR_ADMIN_ID' with the results from the queries above
INSERT INTO "AspNetUserRoles" ("UserId", "RoleId") 
VALUES ('YOUR_USER_ID', 'YOUR_ADMIN_ID');
```

## UI/UX Standards

| Aspect | Standard |
|--------|----------|
| **Design Language** | Modern Corporate — clean lines, generous whitespace, subtle shadows |
| **Typography** | Inter (400, 500, 600, 700) via Google Fonts |
| **Color Palette** | Slate-900 navbar/headers, Slate-50 light surfaces, Slate-800 dark surfaces |
| **Dark Mode** | Class-based (`dark:` variants), persisted in `localStorage`, FOUC-free |
| **Components** | Native HTML `<dialog>` modals, Tailwind utility classes, zero external UI libraries |
| **Animations** | CSS `fadeIn` keyframes for error banners, `transition-colors duration-300` for theme switching |
| **Scrollbar** | Custom WebKit scrollbar styling for chat area (6px, slate tones) |
| **Responsiveness** | Mobile-first with `lg:` breakpoint for desktop navigation |

---

## Roadmap  
  
- [x] RAG Pipeline integration with Gemini AI & Qdrant
- [x] Recursive Character Text Splitting with `cl100k_base`
- [ ] Interface-based abstractions for full testability  
- [ ] Server-side HTML sanitization for LLM output  
- [ ] Docker Compose setup for local environment

## License

This project is licensed under the MIT License.
