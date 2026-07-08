using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text;

namespace CS2LicenseBot;

class Program
{
    private static readonly string botToken = "8751944130:AAFdPiS9gB2gDQ5cHP557G1lyGsBBu8YSjs";
    private static readonly string cryptoToken = "606723:AAFHn5i2qIa6sMK0gHC2HV4g4BaG2Y0lihO";
    private static readonly long adminId = 8688724190;
    private static readonly string adminUsername = "@yooooyooyo";
    private static int lastId = 0;
    private static readonly Dictionary<string, (string key, DateTime expires, string hwid)> licenses = new();
    private static readonly string licenseFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "licenses.txt");
    private static readonly Dictionary<long, int> pendingPurchases = new();
    private static readonly HttpClient httpClient = new();
    private static readonly Random random = new();

    static void Main()
    {
        Console.WriteLine("🤖 CS2 Helper Bot with CryptoBot");
        Console.WriteLine($"Bot: @CS3helper_bot");
        Console.WriteLine($"Admin: {adminUsername}");
        Console.WriteLine();

        LoadLicenses();
        Console.WriteLine($"📂 Loaded {licenses.Count} licenses");

        TelegramBotLoop().GetAwaiter().GetResult();
    }

    static async Task TelegramBotLoop()
    {
        while (true)
        {
            try
            {
                string url = $"https://api.telegram.org/bot{botToken}/getUpdates?offset={lastId + 1}&timeout=30";
                string json = await httpClient.GetStringAsync(url);

                if (!json.Contains("\"result\":[]"))
                {
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("result", out var result))
                    {
                        foreach (var update in result.EnumerateArray())
                        {
                            if (update.TryGetProperty("update_id", out var idProp))
                            {
                                int newId = idProp.GetInt32();
                                if (newId > lastId)
                                {
                                    lastId = newId;

                                    if (update.TryGetProperty("message", out var message))
                                    {
                                        if (message.TryGetProperty("chat", out var chat) &&
                                            chat.TryGetProperty("id", out var chatIdProp))
                                        {
                                            long chatId = chatIdProp.GetInt64();

                                            if (message.TryGetProperty("text", out var textProp))
                                            {
                                                string text = textProp.GetString()?.ToLower() ?? "";
                                                Console.WriteLine($"📩 Message: {text} from {chatId}");
                                                await HandleCommand(chatId.ToString(), text);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Bot error: {ex.Message}");
                await Task.Delay(5000);
            }
        }
    }

    private static async Task HandleCommand(string chatIdStr, string text)
    {
        long chatId = long.Parse(chatIdStr);

        if (text == "/start")
        {
            await SendMessage(chatId, "🤖 <b>CS2 Helper Bot</b>\n\n" +
                    "📌 <b>Команды:</b>\n" +
                    "/id - Узнать свой ID\n" +
                    "/buy - Купить лицензию\n" +
                    "/download - Скачать чит\n" +
                    "/check КЛЮЧ - Проверить ключ\n" +
                    "/help - Помощь");
            return;
        }

        if (text == "/id")
        {
            await SendMessage(chatId, $"✅ <b>Ваш ID:</b> <code>{chatId}</code>");
            return;
        }

        if (text == "/help")
        {
            await SendMessage(chatId, "📖 <b>Помощь:</b>\n\n" +
                    "/id - Ваш ID\n" +
                    "/buy - Тарифы и оплата\n" +
                    "/download - Скачать чит\n" +
                    "/check КЛЮЧ - Проверить ключ\n" +
                    "/help - Эта справка\n\n" +
                    "👤 <b>Поддержка:</b> " + adminUsername);
            return;
        }

        if (text == "/download")
        {
            await SendMessage(chatId, "📥 <b>Скачать CS2 Helper:</b>\n\n" +
                    "🔗 https://drive.google.com/ваша_ссылка\n\n" +
                    "🔐 <b>Пароль:</b> <code>cs2helper</code>");
            return;
        }

        if (text == "/buy")
        {
            await SendMessage(chatId, "🛒 <b>Способы оплаты:</b>\n\n" +
                    "🌍 <b>Криптовалюта (USDT):</b>\n" +
                    "   /crypto 7  - 7 дней (5$)\n" +
                    "   /crypto 14 - 14 дней (8$)\n" +
                    "   /crypto 30 - 30 дней (12$)\n" +
                    "   /crypto 90 - 90 дней (30$)\n\n" +
                    "🇷🇺 <b>Для СНГ:</b>\n" +
                    "   Напишите админу: " + adminUsername + "\n\n" +
                    "💡 <b>Как оплатить криптой:</b>\n" +
                    "   1. Напишите /crypto [дни]\n" +
                    "   2. Бот создаст счет в USDT\n" +
                    "   3. Оплатите и получите ключ автоматически!");
            return;
        }

        if (text.StartsWith("/crypto "))
        {
            string[] cmdParts = text.Split(' ');
            if (cmdParts.Length > 1 && int.TryParse(cmdParts[1], out int daysCount))
            {
                if (daysCount == 7 || daysCount == 14 || daysCount == 30 || daysCount == 90)
                {
                    double amount = daysCount switch
                    {
                        7 => 5,
                        14 => 8,
                        30 => 12,
                        90 => 30,
                        _ => 0
                    };

                    Console.WriteLine($"📝 Creating invoice for {chatId}, {daysCount} days, ${amount}");

                    pendingPurchases[chatId] = daysCount;

                    string invoice = await CreateCryptoInvoice(chatId, amount, daysCount);

                    if (!string.IsNullOrEmpty(invoice))
                    {
                        string message = "💳 <b>Оплата криптовалютой</b>\n\n" +
                                $"📅 Тариф: {daysCount} дней\n" +
                                $"💰 Сумма: ${amount} USDT\n\n" +
                                $"🔗 <a href='{invoice}'>Оплатить через @CryptoBot</a>\n\n" +
                                "⏳ Счет действителен 30 минут\n" +
                                "✅ После оплаты ключ придет автоматически!";

                        Console.WriteLine($"✅ Invoice created: {invoice}");
                        await SendMessage(chatId, message);
                    }
                    else
                    {
                        Console.WriteLine($"❌ Failed to create invoice for {chatId}");
                        await SendMessage(chatId, "❌ Ошибка создания счета. Попробуйте позже.");
                        pendingPurchases.Remove(chatId);
                    }
                }
                else
                {
                    await SendMessage(chatId, "❌ Доступные тарифы: 7, 14, 30, 90");
                }
            }
            else
            {
                await SendMessage(chatId, "❌ Использование: /crypto [дни]\nПример: /crypto 30");
            }
            return;
        }

        if (text.StartsWith("/check "))
        {
            string key = text.Replace("/check ", "").ToUpper().Trim();
            await SendMessage(chatId, CheckLicense(key));
            return;
        }

        if (chatId == adminId)
        {
            if (text.StartsWith("/gen "))
            {
                string[] cmdParts = text.Split(' ');
                if (cmdParts.Length > 1 && int.TryParse(cmdParts[1], out int daysCount))
                {
                    string key = GenerateKey(daysCount);
                    await SendMessage(chatId, $"✅ <b>Ключ создан!</b>\n\n" +
                            $"🔑 <code>{key}</code>\n" +
                            $"📅 Срок: {daysCount} дней\n" +
                            $"📅 До: {DateTime.Now.AddDays(daysCount):dd.MM.yyyy}\n\n" +
                            "📩 Отправьте ключ покупателю!");
                }
                else
                {
                    await SendMessage(chatId, "❌ Использование: /gen [дни]\nПример: /gen 30");
                }
                return;
            }

            if (text == "/keys")
            {
                string reply = "📋 <b>Список ключей:</b>\n\n";
                if (licenses.Count == 0)
                {
                    reply += "📭 Нет ключей";
                }
                else
                {
                    int count = 0;
                    foreach (var kvp in licenses)
                    {
                        count++;
                        var (_, expires, hwid) = kvp.Value;
                        string status = string.IsNullOrEmpty(hwid) ? "⬜ Не активирован" : "✅ Активирован";
                        reply += $"{count}. <code>{kvp.Key}</code> - {status}\n";
                        if (count >= 20) { reply += "\n..."; break; }
                    }
                }
                await SendMessage(chatId, reply);
                return;
            }
        }

        await SendMessage(chatId, "❓ Неизвестная команда. Напишите /help");
    }

    private static async Task<string> CreateCryptoInvoice(long chatId, double amount, int days)
    {
        try
        {
            var data = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("amount", amount.ToString("0.00")),
                new KeyValuePair<string, string>("currency", "USDT"),
                new KeyValuePair<string, string>("description", $"CS2 Helper - {days} days"),
                new KeyValuePair<string, string>("hidden_message", $"Ваш ключ будет отправлен после оплаты"),
                new KeyValuePair<string, string>("paid_btn_name", "open"),
                new KeyValuePair<string, string>("paid_btn_url", "https://t.me/CS3helper_bot")
            });

            string url = $"https://api.crypt.bot/v1/createInvoice?token={cryptoToken}";
            Console.WriteLine($"📤 Sending request to CryptoBot");

            var response = await httpClient.PostAsync(url, data);
            string json = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"📨 CryptoBot response: {json}");

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("ok", out var ok) && ok.GetBoolean())
            {
                var result = doc.RootElement.GetProperty("result");
                if (result.TryGetProperty("bot_invoice_url", out var urlProp))
                {
                    return urlProp.GetString();
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ CryptoBot error: {ex.Message}");
            return null;
        }
    }

    private static string CheckLicense(string key)
    {
        if (string.IsNullOrEmpty(key) || key.Length < 20)
            return "❌ Неверный формат ключа! (XXXXX-XXXXX-XXXXX-XXXXX)";

        if (licenses.ContainsKey(key))
        {
            var (_, expires, hwid) = licenses[key];

            if (DateTime.Now > expires)
                return "❌ Срок действия ключа истек!";

            if (!string.IsNullOrEmpty(hwid))
                return $"✅ Ключ активирован и привязан к устройству.\n📅 Действует до: {expires:dd.MM.yyyy}";

            return $"✅ Ключ действителен!\n📅 Действует до: {expires:dd.MM.yyyy}\n⚠️ Ключ не активирован";
        }

        return "❌ Ключ не найден!";
    }

    private static string GenerateKey(int days)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var keyParts = new List<string>();

        for (int i = 0; i < 4; i++)
        {
            char[] part = new char[5];
            for (int j = 0; j < 5; j++)
                part[j] = chars[random.Next(chars.Length)];
            keyParts.Add(new string(part));
        }

        string key = string.Join("-", keyParts);
        DateTime expires = DateTime.Now.AddDays(days);

        licenses[key] = (key, expires, "");
        SaveLicenses();

        return key;
    }

    private static void LoadLicenses()
    {
        try
        {
            if (File.Exists(licenseFile))
            {
                string[] lines = File.ReadAllLines(licenseFile);
                foreach (string line in lines)
                {
                    string[] parts = line.Split('|');
                    if (parts.Length >= 3)
                    {
                        string key = parts[0];
                        DateTime expires = DateTime.Parse(parts[1]);
                        string hwid = parts[2];
                        licenses[key] = (key, expires, hwid);
                    }
                }
                Console.WriteLine($"📂 Loaded {licenses.Count} licenses from file");
            }
            else
            {
                Console.WriteLine("📭 License file not found, starting fresh");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Load error: {ex.Message}");
        }
    }

    private static void SaveLicenses()
    {
        try
        {
            var lines = new List<string>();
            foreach (var kvp in licenses)
            {
                var (key, expires, hwid) = kvp.Value;
                lines.Add($"{key}|{expires:O}|{hwid}");
            }
            File.WriteAllLines(licenseFile, lines);
            Console.WriteLine($"💾 Saved {licenses.Count} licenses to file");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Save error: {ex.Message}");
        }
    }

    private static async Task SendMessage(long chatId, string text)
    {
        try
        {
            string url = $"https://api.telegram.org/bot{botToken}/sendMessage?chat_id={chatId}&text={Uri.EscapeDataString(text)}&parse_mode=HTML";
            await httpClient.GetAsync(url);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Send error: {ex.Message}");
        }
    }
}
