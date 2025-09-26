using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

internal partial class Program
{
    static async Task<int> Main(string[] args)
    {
        var cfg = SeedConfig.FromEnv();
        Console.WriteLine($"[seeder] API = {cfg.ApiBaseUrl}");
        Console.WriteLine($"[seeder] Users={cfg.UsersCount}, Events={cfg.EventsCount}, Reviews/User={cfg.ReviewsPerUser}, EventReviews/Event={cfg.EventReviewsPerEvent}");
        Console.WriteLine($"[seeder] Media users dir: {cfg.UsersMediaDir}");
        Console.WriteLine($"[seeder] Media events dir: {cfg.EventsMediaDir}");
        Console.WriteLine($"[seeder] LLM: {cfg.LlmBaseUrl} model={cfg.LlmModel}, timeout={cfg.LlmTimeoutSec}s, batches users={cfg.UsersBatch}, events={cfg.EventsBatch}");

        var api = new ApiClient(cfg.ApiBaseUrl);
        try
        {
            // Логинимся админом
            Console.WriteLine("[seeder] Admin login...");
            var admin = await api.LoginAsync(cfg.AdminEmail, cfg.AdminPassword);

            // Инициализируем LLM‑клиент (увеличенный timeout)
            var llm = new LlmClient(cfg.LlmBaseUrl, cfg.LlmModel, cfg.LlmTimeoutSec);

            // ---------- Пользователи (батчами) ----------
            var usersPlan = new List<LlmUserPlan>();
            if (cfg.UsersCount > 0)
            {
                while (usersPlan.Count < cfg.UsersCount)
                {
                    var need = Math.Min(cfg.UsersBatch, cfg.UsersCount - usersPlan.Count);
                    var chunk = await llm.GenerateUsersAsync(need);
                    if (chunk.Count == 0)
                    {
                        Console.WriteLine("[seeder] LLM returned 0 users in batch, using fallback for this batch");
                        chunk = LlmClient.FallbackUsers(need);
                    }
                    usersPlan.AddRange(chunk);
                }
            }

            // ---------- Регистрация пользователей ----------
            var userAccounts = new List<UserAccount>();
            var userImages = SafeEnumerateImages(cfg.UsersMediaDir).ToArray();
            var imageIdx = 0;

            foreach (var u in usersPlan)
            {
                var password = "Passw0rd!"; // одинаковый для демо
                                            // Санитизация полей (согласована с валидацией бэка)
                var fName = SeedSanitizer.SanitizeName(u.FirstName, maxLen: 50, fallback: "User");
                var lName = SeedSanitizer.SanitizeName(u.LastName, maxLen: 50, fallback: "Seed");
                var nick = SeedSanitizer.SanitizeNickname(u.Nickname, maxLen: 30);
                var email = SeedSanitizer.MakeSafeEmail(fName, lName, u.Email);

                // До 5 попыток регистрации на случай коллизии email/валидации; при неудаче — генерим новый email и пробуем снова
                RegisterResult reg;
                int attempts = 0;
                while (true)
                {
                    attempts++;
                    try
                    {
                        reg = await api.RegisterAsync(fName, lName, email, password, nick);
                        break;
                    }
                    catch (HttpRequestException hex)
                    {
                        var msg = hex.Message ?? "";
                        Console.WriteLine($"[seeder][warn] register failed for {email}: {msg}");
                        if (attempts >= 5)
                            throw; // пусть упадет на 5-й, чтобы не зациклиться

                        // если причина похожа на конфликт или валидацию — сгенерим новый email и попробуем снова
                        email = SeedSanitizer.MakeSafeEmail(fName, lName, null);
                    }
                }

                var userToken = reg.Token;
                var userId = reg.User.Id;
                userAccounts.Add(new UserAccount { Id = userId, Email = u.Email, Password = password, Token = userToken, FirstName = u.FirstName, LastName = u.LastName });

                var bio = (u.Bio ?? "").Trim();
                if (bio.Length > 480) bio = bio.Substring(0, 480).Trim();
                // Профиль (не валимся на единичных 400)
                try
                {
                    await api.UpdateUserAsync(userToken, userId, new
                    {
                        Bio = bio,
                        DancerRole = NormalizeRole(u.DancerRole),     // "Lead"/"Follow"/"Both"
                        SelfAssessedLevel = NormalizeLevel(u.Level),  // допускаем длинные строки
                        StartDancingDate = DateTime.UtcNow.AddYears(-new Random().Next(1, 8))
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[seeder][warn] update profile failed for {u.Email}: {ex.Message}");
                }

                // Фото (если есть медиа)
                if (userImages.Length > 0)
                {
                    var path = userImages[imageIdx % userImages.Length];
                    imageIdx++;
                    try
                    {
                        var photoResp = await api.UploadUserPhotoAsync(userToken, path);
                        await api.SetMainPhotoAsync(userToken, photoResp.PhotoId);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[warn] user photo upload failed for {u.Email}: {ex.Message}");
                    }
                }
            }

            Console.WriteLine($"[seeder] Users created: {userAccounts.Count}");

            // ---------- События (батчами) ----------
            var eventsPlan = new List<LlmEventPlan>();
            if (cfg.EventsCount > 0)
            {
                while (eventsPlan.Count < cfg.EventsCount)
                {
                    var need = Math.Min(cfg.EventsBatch, cfg.EventsCount - eventsPlan.Count);
                    var chunk = await llm.GenerateEventsAsync(need);
                    if (chunk.Count == 0)
                    {
                        Console.WriteLine("[seeder] LLM returned 0 events in batch, using fallback for this batch");
                        chunk = Enumerable.Range(1, need).Select(i => new LlmEventPlan
                        {
                            Name = $"Bachata Social #{eventsPlan.Count + i}",
                            Description = "Вечеринка бачаты с дружелюбной атмосферой.",
                            Location = "Plyas Dance, Minsk",
                            DateUtc = DateTime.UtcNow.AddDays(new Random().Next(1, 30))
                        }).ToList();
                    }
                    eventsPlan.AddRange(chunk);
                }
            }

            // ---------- Создание событий ----------
            var eventImages = SafeEnumerateImages(cfg.EventsMediaDir).ToArray();
            var eimgIdx = 0;

            var evList = new List<(int Id, string Name)>();
            foreach (var e in eventsPlan)
            {
                var ev = await api.CreateEventAsync(admin.Token, new
                {
                    name = e.Name,
                    description = e.Description,
                    date = e.DateUtc,
                    location = e.Location
                });
                evList.Add((ev.Id, ev.Name));

                // Обложка
                if (eventImages.Length > 0)
                {
                    var p = eventImages[eimgIdx % eventImages.Length];
                    eimgIdx++;
                    try
                    {
                        await api.UploadEventCoverAsync(admin.Token, ev.Id, p);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[warn] event cover upload failed: {ex.Message}");
                    }
                }

                // Альбом: по 2-3 фото
                if (eventImages.Length > 0)
                {
                    var take = Math.Min(3, eventImages.Length);
                    var batch = new List<string>();
                    for (int i = 0; i < take; i++)
                    {
                        batch.Add(eventImages[(eimgIdx + i) % eventImages.Length]);
                    }
                    eimgIdx += take;
                    try
                    {
                        await api.UploadEventPhotosAsync(admin.Token, ev.Id, batch);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[warn] event photos upload failed: {ex.Message}");
                    }
                }
            }
            Console.WriteLine($"[seeder] Events created: {evList.Count}");

            // ---------- Участники событий ----------
            var rndJoin = new Random();
            foreach (var ev in evList)
            {
                foreach (var acc in userAccounts.Where(_ => rndJoin.NextDouble() < 0.2))
                {
                    try { await api.JoinEventAsync(acc.Token, ev.Id); } catch { /* ignore */ }
                }
            }

            // ---------- Отзывы пользователей ----------
            var rnd = new Random();
            foreach (var acc in userAccounts)
            {
                var peers = userAccounts
                    .Where(u => u.Id != acc.Id)
                    .OrderBy(_ => rnd.Next())
                    .Take(cfg.ReviewsPerUser)
                    .ToList();

                foreach (var peer in peers)
                {
                    var ratings = RandomRatings(rnd);
                    string text;
                    try
                    {
                        text = await llm.GenerateUserReviewTextAsync(acc.FirstName, peer.FirstName);
                    }
                    catch
                    {
                        text = "Отличное чувство ритма и приятный контакт. Можно добавить больше внимания к партнёру.";
                    }

                    try
                    {
                        await api.CreateUserReviewAsync(acc.Token, new
                        {
                            revieweeId = peer.Id,
                            eventId = (int?)null,
                            leadRatings = ratings.lead,
                            followRatings = ratings.follow,
                            textReview = text,
                            tags = (string[]?)null,
                            isAnonymous = rnd.NextDouble() < 0.4
                        });
                    }
                    catch { /* ignore single failures */ }
                }
            }

            // ---------- Отзывы о событиях ----------
            foreach (var ev in evList)
            {
                var participants = userAccounts
                    .Where(_ => rnd.NextDouble() < 0.2)
                    .Take(cfg.EventReviewsPerEvent)
                    .ToList();

                foreach (var p in participants)
                {
                    var ratings = new Dictionary<string, int>
                    {
                        ["location"] = rnd.Next(3, 6),
                        ["music"] = rnd.Next(3, 6),
                        ["crowd"] = rnd.Next(3, 6),
                        ["organization"] = rnd.Next(3, 6),
                    };

                    string text;
                    try
                    {
                        text = await llm.GenerateEventReviewTextAsync(p.FirstName, ev.Name);
                    }
                    catch
                    {
                        text = "Отличная музыка и тёплая атмосфера. Можно улучшить организацию входа.";
                    }

                    try
                    {
                        // гарантируем участие
                        try { await api.JoinEventAsync(p.Token, ev.Id); } catch { }
                        await api.CreateEventReviewAsync(p.Token, new
                        {
                            eventId = ev.Id,
                            ratings,
                            textReview = text,
                            tags = (string[]?)null,
                            isAnonymous = rnd.NextDouble() < 0.4
                        });
                    }
                    catch { /* ignore */ }
                }
            }

            Console.WriteLine("[seeder] Done.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("[seeder] ERROR: " + ex);
            return 1;
        }
    }

    static (Dictionary<string, int>? lead, Dictionary<string, int>? follow) RandomRatings(Random rnd)
    {
        Dictionary<string, int>? starsOrNull(Dictionary<string, int> s)
            => s.Values.All(v => v == 0) ? null : s;

        // иногда только lead, иногда только follow, иногда оба, иногда только текст
        var mode = rnd.Next(0, 4);
        var lead = new Dictionary<string, int>
        {
            ["technique"] = mode == 2 ? 0 : rnd.Next(3, 6),
            ["musicality"] = mode == 2 ? 0 : rnd.Next(3, 6),
            ["leading"] = mode == 2 ? 0 : rnd.Next(3, 6),
            ["comfort"] = mode == 2 ? 0 : rnd.Next(3, 6),
        };
        var follow = new Dictionary<string, int>
        {
            ["technique"] = mode == 1 ? 0 : rnd.Next(3, 6),
            ["musicality"] = mode == 1 ? 0 : rnd.Next(3, 6),
            ["following"] = mode == 1 ? 0 : rnd.Next(3, 6),
            ["connection"] = mode == 1 ? 0 : rnd.Next(3, 6),
        };
        return (starsOrNull(lead), starsOrNull(follow));
    }

    static IEnumerable<string> SafeEnumerateImages(string dir)
    {
        if (!Directory.Exists(dir)) yield break;
        var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };
        foreach (var f in Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories))
            if (exts.Contains(Path.GetExtension(f))) yield return f;
    }

    // нормализация значений, если LLM вернул что-то странное
    static string NormalizeRole(string? r)
        => r?.StartsWith("L", StringComparison.OrdinalIgnoreCase) == true ? "Lead"
         : r?.StartsWith("F", StringComparison.OrdinalIgnoreCase) == true ? "Follow"
         : "Both";

    static string NormalizeLevel(string? l)
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "Beginner","Beginner-Intermediate","Intermediate","Intermediate-Advanced","Advanced","Professional"
        };
        if (l != null && allowed.Contains(l)) return l;
        return "Intermediate";
    }
}

public class SeedConfig
{
    public string ApiBaseUrl { get; set; } = "http://localhost:5000/api";
    public string AdminEmail { get; set; } = "admin@example.com";
    public string AdminPassword { get; set; } = "admin123";
    public string UsersMediaDir { get; set; } = "/seed-media/users";
    public string EventsMediaDir { get; set; } = "/seed-media/events";
    public string LlmBaseUrl { get; set; } = "http://host.docker.internal:1234";
    public string LlmModel { get; set; } = "qwen/qwen3-8b";
    public int LlmTimeoutSec { get; set; } = 120;
    public int UsersCount { get; set; } = 30;
    public int EventsCount { get; set; } = 5;
    public int ReviewsPerUser { get; set; } = 3;
    public int EventReviewsPerEvent { get; set; } = 5;
    public int UsersBatch { get; set; } = 10;
    public int EventsBatch { get; set; } = 3;

    public static SeedConfig FromEnv()
    {
        int Int(string key, int def) => int.TryParse(Environment.GetEnvironmentVariable(key), out var v) ? v : def;
        string S(string key, string def) => Environment.GetEnvironmentVariable(key) ?? def;
        return new SeedConfig
        {
            ApiBaseUrl = S("SEEDER__API_BASE_URL", S("API_BASE_URL", "http://localhost:5000/api")),
            AdminEmail = S("SEEDER__ADMIN_EMAIL", "admin@example.com"),
            AdminPassword = S("SEEDER__ADMIN_PASSWORD", "admin123"),
            UsersMediaDir = S("SEEDER__USERS_MEDIA_DIR", "/seed-media/users"),
            EventsMediaDir = S("SEEDER__EVENTS_MEDIA_DIR", "/seed-media/events"),
            LlmBaseUrl = S("SEEDER__LLM_BASE_URL", S("LLM__BaseUrl", "http://host.docker.internal:1234")),
            LlmModel = S("SEEDER__LLM_MODEL", S("LLM__Model", "qwen/qwen3-8b")),
            LlmTimeoutSec = Int("SEEDER__LLM_TIMEOUT_SEC", 120),
            UsersCount = Int("SEEDER__USERS", 30),
            EventsCount = Int("SEEDER__EVENTS", 5),
            ReviewsPerUser = Int("SEEDER__REVIEWS_PER_USER", 3),
            EventReviewsPerEvent = Int("SEEDER__EVENT_REVIEWS_PER_EVENT", 5),
            UsersBatch = Int("SEEDER__USERS_BATCH", 10),
            EventsBatch = Int("SEEDER__EVENTS_BATCH", 3)
        };
    }
}

public record LoginResult(string Token, ApiUser User);
public record RegisterResult(string Token, ApiUser User);
public record ApiUser(string Id, string Email, string FirstName, string LastName);
public record UserAccount
{
    public string Id { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string Password { get; set; } = default!;
    public string Token { get; set; } = default!;
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
}

// ----------------- API CLIENT -----------------
public class ApiClient
{
    private readonly HttpClient _http;

    public ApiClient(string baseUrl)
    {
        _http = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<LoginResult> LoginAsync(string email, string password)
    {
        var resp = await _http.PostAsJsonAsync("auth/login", new { email, password });
        resp.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        var token = doc.RootElement.GetProperty("token").GetString()!;
        var u = doc.RootElement.GetProperty("user");
        var user = new ApiUser(
            u.GetProperty("id").GetString()!,
            u.GetProperty("email").GetString()!,
            u.GetProperty("firstName").GetString()!,
            u.GetProperty("lastName").GetString()!
        );
        return new LoginResult(token, user);
    }

    public async Task<RegisterResult> RegisterAsync(string firstName, string lastName, string email, string password, string? nickname)
    {
        var resp = await _http.PostAsJsonAsync("auth/register", new { firstName, lastName, email, password, nickname });
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Register failed: {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {body}");
        }
        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        var token = doc.RootElement.GetProperty("token").GetString()!;
        var u = doc.RootElement.GetProperty("user");
        var user = new ApiUser(
            u.GetProperty("id").GetString()!,
            u.GetProperty("email").GetString()!,
            u.GetProperty("firstName").GetString()!,
            u.GetProperty("lastName").GetString()!
        );
        return new RegisterResult(token, user);
    }

    public async Task UpdateUserAsync(string token, string userId, object payload)
    {
        using var req = new HttpRequestMessage(HttpMethod.Put, $"users/{userId}")
        {
            Content = JsonContent.Create(payload)
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            throw new HttpRequestException($"UpdateUser failed: {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {body}");
        }
    }

    public async Task<(int PhotoId, string SmallUrl)> UploadUserPhotoAsync(string token, string filePath)
    {
        using var content = new MultipartFormDataContent();
        var stream = File.OpenRead(filePath);
        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(DetectContentType(filePath));
        content.Add(fileContent, "file", Path.GetFileName(filePath));

        using var req = new HttpRequestMessage(HttpMethod.Post, "userphotos/me/upload") { Content = content };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await _http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        var photoId = doc.RootElement.GetProperty("photoId").GetInt32();
        var small = doc.RootElement.GetProperty("urls").GetProperty("small").GetString()!;
        return (photoId, small);
    }

    public async Task SetMainPhotoAsync(string token, int photoId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "userphotos/me/set-main")
        {
            Content = JsonContent.Create(new { photoId })
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await _http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<(int Id, string Name)> CreateEventAsync(string token, object payload)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "events")
        {
            Content = JsonContent.Create(payload)
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await _http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        var id = doc.RootElement.GetProperty("id").GetInt32();
        var name = doc.RootElement.GetProperty("name").GetString()!;
        return (id, name);
    }

    public async Task UploadEventCoverAsync(string token, int eventId, string filePath)
    {
        using var content = new MultipartFormDataContent();
        using var stream = File.OpenRead(filePath);
        var fileContent = new StreamContent(stream);
        var fileContentType = new MediaTypeHeaderValue(DetectContentType(filePath));
        fileContent.Headers.ContentType = fileContentType;
        content.Add(fileContent, "file", Path.GetFileName(filePath));

        using var req = new HttpRequestMessage(HttpMethod.Post, $"events/{eventId}/cover") { Content = content };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await _http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
    }

    public async Task UploadEventPhotosAsync(string token, int eventId, IEnumerable<string> files)
    {
        using var content = new MultipartFormDataContent();
        foreach (var f in files)
        {
            var stream = File.OpenRead(f);
            var fileContent = new StreamContent(stream);
            var fileContentType = new MediaTypeHeaderValue(DetectContentType(f));
            fileContent.Headers.ContentType = fileContentType;
            content.Add(fileContent, "files", Path.GetFileName(f));
        }

        using var req = new HttpRequestMessage(HttpMethod.Post, $"events/{eventId}/photos") { Content = content };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await _http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
    }

    public async Task JoinEventAsync(string token, int eventId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"events/{eventId}/join");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await _http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
    }

    public async Task CreateUserReviewAsync(string token, object payload)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "reviews")
        {
            Content = JsonContent.Create(payload)
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await _http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
    }

    public async Task CreateEventReviewAsync(string token, object payload)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "eventreviews")
        {
            Content = JsonContent.Create(payload)
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await _http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
    }

    static string DetectContentType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }
}

// ----------------- LLM CLIENT -----------------
public class LlmClient
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _model;

    public LlmClient(string baseUrl, string model, int timeoutSec = 120)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _model = model;
        _http = new HttpClient { BaseAddress = new Uri(_baseUrl + "/"), Timeout = TimeSpan.FromSeconds(timeoutSec) };
    }

    public async Task<List<LlmUserPlan>> GenerateUsersAsync(int count)
    {
        var prompt = $@"
Generate {count} diverse bachata dancers as JSON array of objects with:
- firstName (short), lastName (short), email (unique, latin letters), nickname (optional),
- bio (2 short sentences in RU), dancerRole = ""Lead""|""Follow""|""Both"", level (one of: Beginner, Beginner-Intermediate, Intermediate, Intermediate-Advanced, Advanced, Professional).
Return ONLY JSON.
".Trim();

        var json = await ChatJsonAsync(prompt);
        try
        {
            var arr = JsonSerializer.Deserialize<List<LlmUserPlan>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            if (arr.Count == 0)
            {
                Console.WriteLine("[seeder][LLM][json] empty array, using fallback users");
                return FallbackUsers(count);
            }

            var rnd = new Random();
            foreach (var u in arr)
            {
                u.Email ??= $"{u.FirstName}.{u.LastName}.{rnd.Next(1000, 9999)}@seed.local".ToLowerInvariant();
                u.Nickname ??= null;
                u.DancerRole = NormalizeRole(u.DancerRole);
                u.Level = NormalizeLevel(u.Level);
            }
            return arr;
        }
        catch
        {
            Console.WriteLine("[seeder][LLM][json] deserialize failed, using fallback users");
            return FallbackUsers(count);
        }
    }

    public async Task<List<LlmEventPlan>> GenerateEventsAsync(int count)
    {
        var prompt = $@"
Generate {count} bachata events in Minsk as JSON array with:
- name (short), description (1-2 sentences in RU), location (short),
- dateUtc (ISO8601 UTC within next 30 days).
Return ONLY JSON.".Trim();

        var json = await ChatJsonAsync(prompt);
        try
        {
            var arr = JsonSerializer.Deserialize<List<LlmEventPlan>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            foreach (var e in arr)
            {
                if (e.DateUtc == default) e.DateUtc = DateTime.UtcNow.AddDays(new Random().Next(1, 30));
            }
            return arr;
        }
        catch
        {
            var rnd = new Random();
            return Enumerable.Range(1, count).Select(i => new LlmEventPlan
            {
                Name = $"Bachata Social #{i}",
                Description = "Вечеринка бачаты с дружелюбной атмосферой.",
                Location = "Plyas Dance, Minsk",
                DateUtc = DateTime.UtcNow.AddDays(rnd.Next(1, 30))
            }).ToList();
        }
    }

    public async Task<string> GenerateUserReviewTextAsync(string reviewerName, string revieweeName)
    {
        var prompt = $@"
Сгенерируй отзыв в формате JSON.
Требования:
- Верни ТОЛЬКО JSON-объект без пояснений вне JSON.
- Структура строго: {{ ""text_ru"": ""...""
}}
- Поле text_ru: русский язык, 1–3 предложения, доброжелательно и конструктивно, либо снебольшой вероятностью наоборот токсично и грубо, без HTML/Markdown, без <think>, длина ≤ 250 символов.
- Тема: отзыв по бачате от ""{reviewerName}"" для ""{revieweeName}"".
Пример (только формат, текст другой, побольше фантазии):
{{ ""text_ru"": ""Отличный ритм и мягкий контакт. Можно чуть четче завершать движения для большей выразительности."" }}
".Trim();

        var fallback = "Хороший ритм и контакт. Можно добавить чуть больше внимания к ведению/следованию.";
        return await GenerateReviewTextRuJsonAsync(prompt, fallback);
    }

    public async Task<string> GenerateEventReviewTextAsync(string reviewerName, string eventName)
    {
        var prompt = $@"
Сгенерируй отзыв о событии в формате JSON.
Требования:
- Верни ТОЛЬКО JSON-объект без пояснений вне JSON.
- Структура строго: {{ ""text_ru"": ""...""
}}
- Поле text_ru: русский язык, 1–3 предложения, что понравилось + одно улучшение, без HTML/Markdown, без <think>, длина ≤ 220 символов.
- Тема: отзыв об событии ""{eventName}"" от ""{reviewerName}"".
Пример (только формат, текст другой):
{{ ""text_ru"": ""Отличная музыка и тёплая атмосфера. Для удобства гостей можно улучшить навигацию у входа."" }}
".Trim();

        var fallback = "Отличная музыка и тёплая атмосфера. Можно улучшить организацию входа.";
        return await GenerateReviewTextRuJsonAsync(prompt, fallback);
    }

    private async Task<string> ChatJsonAsync(string prompt)
    {
        var req = new
        {
            model = _model,
            messages = new[] { new { role = "user", content = prompt } },
            temperature = 0.7
        };

        using var resp = await _http.PostAsJsonAsync("v1/chat/completions", req);
        var raw = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode || string.IsNullOrWhiteSpace(raw))
        {
            Console.WriteLine($"[seeder][LLM][json] status {(int)resp.StatusCode}, body: {Trunc(raw)}");
            return "[]";
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            if (root.TryGetProperty("choices", out var choices) &&
                choices.ValueKind == JsonValueKind.Array &&
                choices.GetArrayLength() > 0)
            {
                var msg = choices[0].GetProperty("message");
                var content = msg.GetProperty("content").GetString() ?? "[]";

                // Санитизация: вырезаем <think>…</think> и лишнее до JSON
                var cleaned = ExtractJson(content);
                return string.IsNullOrWhiteSpace(cleaned) ? "[]" : cleaned;
            }

            if (root.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
            {
                return raw.Trim();
            }

            Console.WriteLine($"[seeder][LLM][json] unexpected shape, body: {Trunc(raw)}");
            return "[]";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[seeder][LLM][json] parse error: {ex.Message}, body: {Trunc(raw)}");
            return "[]";
        }

        static string Trunc(string s) => string.IsNullOrEmpty(s) ? "" : (s.Length > 500 ? s[..500] + "..." : s);
    }

    private async Task<string> ChatTextAsync(string prompt)
    {
        var system = "Отвечай только коротким текстом на русском языке, без разметки, без рассуждений, без <think>, без JSON, без кавычек.";
        var req = new
        {
            model = _model,
            messages = new[]
            {
            new { role = "system", content = system },
            new { role = "user", content = prompt }
        },
            temperature = 0.8
        };

        using var resp = await _http.PostAsJsonAsync("v1/chat/completions", req);
        var raw = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode || string.IsNullOrWhiteSpace(raw))
        {
            Console.WriteLine($"[seeder][LLM][text] status {(int)resp.StatusCode}, body: {Trunc(raw)}");
            return string.Empty;
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            if (root.TryGetProperty("choices", out var choices) &&
                choices.ValueKind == JsonValueKind.Array &&
                choices.GetArrayLength() > 0)
            {
                var msg = choices[0].GetProperty("message");
                var content = msg.GetProperty("content").GetString() ?? "";
                return content.Trim();
            }

            if (root.ValueKind == JsonValueKind.String)
            {
                return root.GetString()!.Trim();
            }

            return raw.Trim();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[seeder][LLM][text] parse error: {ex.Message}, body: {Trunc(raw)}");
            return raw.Trim();
        }

        static string Trunc(string s) => string.IsNullOrEmpty(s) ? "" : (s.Length > 500 ? s[..500] + "..." : s);
    }

    private static string NormalizeRole(string? r)
        => r?.StartsWith("L", StringComparison.OrdinalIgnoreCase) == true ? "Lead"
         : r?.StartsWith("F", StringComparison.OrdinalIgnoreCase) == true ? "Follow"
         : "Both";

    private static string NormalizeLevel(string? l)
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "Beginner","Beginner-Intermediate","Intermediate","Intermediate-Advanced","Advanced","Professional"
        };
        if (l != null && allowed.Contains(l)) return l;
        return "Intermediate";
    }

    private static string TrimToMax(string s, int max) => (s ?? "").Length <= max ? (s ?? "") : s.Substring(0, max);

    // Удаляем <think>…</think>, берём JSON от первого '[' или '{' до последней закрывающей скобки
    private static string ExtractJson(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return "[]";
        var s = content;

        // вырежем <think>…</think>
        int t1 = s.IndexOf("<think>", StringComparison.OrdinalIgnoreCase);
        if (t1 >= 0)
        {
            int t2 = s.IndexOf("</think>", t1, StringComparison.OrdinalIgnoreCase);
            if (t2 > t1) s = s.Remove(t1, (t2 + "</think>".Length) - t1);
        }

        // найдём начало JSON
        int startArr = s.IndexOf('[');
        int startObj = s.IndexOf('{');
        int start = (startArr >= 0 && (startObj < 0 || startArr < startObj)) ? startArr : startObj;
        if (start < 0) return "[]";

        // подрежем хвост по последнему ']' или '}'
        int end = -1;
        if (s[start] == '[')
        {
            end = s.LastIndexOf(']');
        }
        else
        {
            end = s.LastIndexOf('}');
        }
        if (end >= start) s = s.Substring(start, end - start + 1);
        else s = s.Substring(start);

        return s.Trim();
    }

    // fallback users (оставляем публичным, чтобы Program мог вызвать при пустом батче)
    public static List<LlmUserPlan> FallbackUsers(int n)
    {
        var first = new[] { "Alex", "Ivan", "Dmitry", "Nikita", "Sergey", "Anna", "Maria", "Julia", "Olga", "Kate" };
        var last = new[] { "Petrov", "Ivanov", "Sidorov", "Smirnov", "Kuznetsov", "Orlova", "Scherbak", "Novikova", "Zaytseva", "Belova" };
        var roles = new[] { "Lead", "Follow", "Both" };
        var levels = new[] { "Beginner", "Beginner-Intermediate", "Intermediate", "Intermediate-Advanced", "Advanced", "Professional" };
        var rnd = new Random();
        return Enumerable.Range(0, n).Select(i =>
        {
            var f = first[rnd.Next(first.Length)];
            var l = last[rnd.Next(last.Length)];
            return new LlmUserPlan
            {
                FirstName = f,
                LastName = l,
                Email = $"{f}.{l}.{rnd.Next(1000, 9999)}@seed.local".ToLowerInvariant(),
                Nickname = null,
                Bio = "Люблю бачату, открыт(а) к обратной связи.",
                DancerRole = roles[rnd.Next(roles.Length)],
                Level = levels[rnd.Next(levels.Length)]
            };
        }).ToList();
    }

    // Чистим ответ до “короткого русского текста”:
    // - вырезаем <think>...</think>
    // - убираем кавычки/бэктики/переводы строк
    // - если нет кириллицы — берём fallback
    // - ограничиваем длину
    private static string CleanRuText(string raw, string fallback, int max)
    {
        if (string.IsNullOrWhiteSpace(raw)) return fallback;
        var s = raw;

        // вырезать <think>...</think>
        s = StripThink(s);

        // если попал JSON/разметка — оставим только текст до первой фигурной/квадратной скобки
        // (сидер просит plain text; если модель вернула JSON — считаем это ошибкой контента)
        int jsonIdx = s.IndexOf('{');
        if (jsonIdx < 0) jsonIdx = s.IndexOf('[');
        if (jsonIdx >= 0) s = s.Substring(0, jsonIdx);

        // нормализуем пробелы/кавычки
        s = s.Replace("\r", " ").Replace("\n", " ").Trim();
        s = s.Trim('«', '»', '“', '”', '"', '\'', '`').Trim();
        while (s.Contains("  ")) s = s.Replace("  ", " ");

        // Если кириллицы почти нет — fallback
        if (CountCyrillic(s) < 5) return fallback;

        // обрежем до max
        return s.Length <= max ? s : s.Substring(0, max).Trim();
    }

    private static string StripThink(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return s;
        int t1 = s.IndexOf("<think>", StringComparison.OrdinalIgnoreCase);
        while (t1 >= 0)
        {
            int t2 = s.IndexOf("</think>", t1, StringComparison.OrdinalIgnoreCase);
            if (t2 > t1) s = s.Remove(t1, (t2 + "</think>".Length) - t1);
            else break;
            t1 = s.IndexOf("<think>", StringComparison.OrdinalIgnoreCase);
        }
        return s;
    }

    private static int CountCyrillic(string s)
    {
        int c = 0;
        foreach (var ch in s)
        {
            if ((ch >= 0x0400 && ch <= 0x04FF) || // кириллица
                (ch >= 0x0500 && ch <= 0x052F))   // дополнительные кириллические символы
                c++;
        }
        return c;
    }

    private async Task<string> GenerateReviewTextRuJsonAsync(string prompt, string fallback)
    {
        // Просим LLM вернуть JSON-объект (см. prompt). ChatJsonAsync вернёт только JSON-кусок (ExtractJson).
        var json = await ChatJsonAsync(prompt);
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
        {
            return fallback;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string? textRu = null;
            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("text_ru", out var tr))
                    textRu = tr.GetString();
            }
            else if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0 && root[0].ValueKind == JsonValueKind.Object)
            {
                var obj = root[0];
                if (obj.TryGetProperty("text_ru", out var tr))
                    textRu = tr.GetString();
            }

            // Финальная очистка и проверка на кириллицу
            return CleanRuText(textRu ?? string.Empty, fallback, 300);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[seeder][LLM][json] parse review error: {ex.Message}, json: {Trunc(json)}");
            return fallback;
        }

        static string Trunc(string s) => string.IsNullOrEmpty(s) ? "" : (s.Length > 500 ? s[..500] + "..." : s);
    }
}

public class LlmUserPlan
{
    public string FirstName { get; set; } = "Alex";
    public string LastName { get; set; } = "Ivanov";
    public string? Email { get; set; }
    public string? Nickname { get; set; }
    public string Bio { get; set; } = "Люблю бачату и атмосферу социальных танцев.";
    public string DancerRole { get; set; } = "Both";
    public string Level { get; set; } = "Intermediate";
}

public class LlmEventPlan
{
    public string Name { get; set; } = "Bachata Social";
    public string Description { get; set; } = "Вечеринка бачаты";
    public string Location { get; set; } = "Plyas Dance, Minsk";
    public DateTime DateUtc { get; set; } = DateTime.UtcNow.AddDays(7);
}

static class LlmFallback
{
}

static class LlmUtils
{
}

static class FallbackGen
{
}

static class Helpers
{
}

static class Fallbacks
{
}

static class Util
{
}

static class SeedSanitizer
{
    private static readonly Random Rnd = new Random();

    // Удаляет диакритику/не‑латиницу, разрешает [a-z], [0-9], точку, дефис
    public static string ToAsciiSlug(string s, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        // нормализуем
        var norm = s.Trim();
        // простой транслит: уберём всё не ASCII и заменим пробелы на '-'
        var sb = new StringBuilder(norm.Length);
        foreach (var ch in norm)
        {
            char c = ch;
            if (char.IsLetter(c) || char.IsDigit(c))
            {
                // латиница или цифра
                if (c <= 127) sb.Append(char.ToLowerInvariant(c));
                else
                {
                    // грубый транслит для самых частых: ñáéíóúüç etc. -> удалим/заменим
                    // чтобы не перебарщивать — удаляем диакритику и пропускаем
                    var lower = char.ToLowerInvariant(c);
                    if ("àáâäåãāăą".Contains(lower)) sb.Append('a');
                    else if ("çćč".Contains(lower)) sb.Append('c');
                    else if ("èéêëēĕėęě".Contains(lower)) sb.Append('e');
                    else if ("ìíîïīĭįı".Contains(lower)) sb.Append('i');
                    else if ("ñń".Contains(lower)) sb.Append('n');
                    else if ("òóôöõōŏő".Contains(lower)) sb.Append('o');
                    else if ("ùúûüūŭůűų".Contains(lower)) sb.Append('u');
                    else if ("ýÿŷ".Contains(lower)) sb.Append('y');
                    else if ("šśş".Contains(lower)) sb.Append('s');
                    else if ("žźż".Contains(lower)) sb.Append('z');
                    else if ("ł".Contains(lower)) sb.Append('l');
                    else if ("ß".Contains(lower)) sb.Append("ss");
                    else if ("đ".Contains(lower)) sb.Append('d');
                    // иначе пропустим
                }
            }
            else if (char.IsWhiteSpace(c))
            {
                sb.Append('-');
            }
            else if (c == '.' || c == '-' || c == '_')
            {
                sb.Append(c);
            }
        }
        var res = sb.ToString().Trim('-', '.');
        if (res.Length > maxLen) res = res.Substring(0, maxLen);
        if (string.IsNullOrEmpty(res)) res = "user";
        return res;
    }

    public static string SanitizeName(string? s, int maxLen, string fallback)
    {
        if (string.IsNullOrWhiteSpace(s)) return fallback;
        var t = s.Trim();
        if (t.Length > maxLen) t = t.Substring(0, maxLen).Trim();
        // уберём кавычки и экзотику
        t = t.Replace("\"", "").Replace("'", "").Replace("`", "");
        if (string.IsNullOrWhiteSpace(t)) t = fallback;
        return t;
    }

    public static string SanitizeNickname(string? s, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var t = s.Trim();
        if (t.Length > maxLen) t = t.Substring(0, maxLen).Trim();
        return t;
    }

    public static string MakeSafeEmail(string firstName, string lastName, string? emailCandidate)
    {
        // если кандидат валиден — используем (простая проверка)
        if (IsSimpleEmail(emailCandidate)) return emailCandidate!;

        // иначе создаём формулу: {slug}.{slug}.{rand}@seed.local
        var f = ToAsciiSlug(firstName, 20);
        var l = ToAsciiSlug(lastName, 20);
        var rnd = Rnd.Next(1000, 999999); // больше разрядов -> меньше коллизий
        return $"{f}.{l}.{rnd}@seed.local";
    }

    private static bool IsSimpleEmail(string? e)
    {
        if (string.IsNullOrWhiteSpace(e)) return false;
        e = e.Trim();
        if (e.Length < 6 || e.Length > 100) return false;
        int at = e.IndexOf('@');
        if (at <= 0 || at != e.LastIndexOf('@')) return false;
        var local = e.Substring(0, at);
        var domain = e.Substring(at + 1);
        if (local.Length == 0 || domain.Length < 3) return false;
        if (!domain.Contains('.')) return false;
        // очень грубая фильтрация символов
        string allowed = "abcdefghijklmnopqrstuvwxyz0123456789.-_+";
        foreach (var ch in e.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch)) continue;
            if (!allowed.Contains(ch) && ch != '@') return false;
        }
        return true;
    }
}