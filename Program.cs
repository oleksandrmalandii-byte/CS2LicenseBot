using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MyTelegramBot
{
    class Program
    {
        private static readonly string botToken = Environment.GetEnvironmentVariable("BOT_TOKEN") ?? "8751944130:AAFdPiS9gB2gDQ5cHP557G1lyGsBBu8YSjs";
        private static readonly string cryptoToken = "606723:AAFHn5i2qIa6sMK0gHC2HV4g4BaG2Y0lihO";
        private static readonly long adminId = 8688724190;
        private static readonly string adminUsername = "@yooooyooyo";
        private static int lastId = 0;
        private static readonly Dictionary<string, (string key, DateTime expires, string hwid)> licenses = new Dictionary<string, (string, DateTime, string)>();
        private static readonly string licenseFile = "/tmp/licenses.txt";
        private static readonly Dictionary<long, int> pendingPurchases = new Dictionary<long, int>();

        static void Main()
        {
            MainAsync().GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            Console.WriteLine("🤖 CS2 Helper Bot");
            Console.WriteLine($"Бот: @CS3helper_bot");
            Console.WriteLine($"Админ: {adminUsername}");
            Console.WriteLine();

            LoadLicenses();

            while (true)
            {
                try
                {
                    using (var client = new HttpClient())
                    {
                        string url = $"https://api.telegram.org/bot{botToken}/getUpdates?offset={lastId + 1}";
                        string json = await client.GetStringAsync(url);

                        if (json.Contains("\"result\":[]"))
                        {
                            await Task.Delay(1000);
                            continue;
                        }

                        if (json.Contains("\"message\""))
                        {
                            int idStart = json.IndexOf("\"update_id\":") + 12;
                            int idEnd = json.IndexOf(",", idStart);
                            string idStr = json.Substring(idStart, idEnd - idStart);
                            int newId = int.Parse(idStr);

                            if (newId > lastId)
                            {
                                lastId = newId;

                                int chatStart = json.IndexOf("\"chat\":{\"id\":") + 13;
                                int chatEnd = json.IndexOf(",", chatStart);
                                string chatId = json.Substring(chatStart, chatEnd - chatStart);

                                int textStart = json.IndexOf("\"text\":\"") + 8;
                                int textEnd = json.IndexOf("\"", textStart);
                                string text = "";
                                if (textStart > 8 && textEnd > textStart)
                                {
                                    text = json.Substring(textStart, textEnd - textStart).ToLower();
                                }

                                if (!string.IsNullOrEmpty(text))
                                {
                                    Console.WriteLine($"📩 Сообщение: {text} от {chatId}");
                                    await HandleCommand(chatId, text);
                                }
                            }
                        }
                    }

                    await Task.Delay(500);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Ошибка: {ex.Message}");
                    await Task.Delay(5000);
                }
            }
        }

        private static async Task HandleCommand(string chatIdStr, string text)
        {
            long chatId = long.Parse(chatIdStr);

            // /start
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

            // /id
            if (text == "/id")
            {
                await SendMessage(chatId, $"✅ <b>Ваш ID:</b> <code>{chatId}</code>");
                return;
            }

            // /help
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

            // /download
            if (text == "/download")
            {
                await SendMessage(chatId, "📥 <b>Скачать CS2 Helper:</b>\n\n" +
                        "🔗 https://drive.google.com/ваша_ссылка\n\n" +
                        "🔐 <b>Пароль:</b> <code>cs2helper</code>");
                return;
            }

            // /buy - ПОКАЗЫВАЕТ СПОСОБЫ ОПЛАТЫ
            if (text == "/buy")
            {
                await SendMessage(chatId, "🛒 <b>Способы оплаты:</b>\n\n" +
                        "🌍 <b>Криптовалюта (USDT):</b>\n" +
                        "   /crypto 7  - 7 дней (5$)\n" +
                        "   /crypto 14 - 14 дней (8$)\n" +
                        "   /crypto 30 - 30 дней (12$)\n" +
                        "   /crypto 90 - 90 дней (30$)\n\n" +
                        "🇷🇺 <b>Для СНГ (Россия, Украина, Казахстан и др.):</b>\n" +
                        "   Напишите админу: " + adminUsername + "\n" +
                        "   Он пришлет реквизиты для оплаты\n" +
                        "   После оплаты выдаст ключ\n\n" +
                        "💡 <b>Как оплатить криптой:</b>\n" +
                        "   1. Напишите /crypto [дни]\n" +
                        "   2. Бот создаст счет в USDT\n" +
                        "   3. Оплатите и получите ключ автоматически!");
                return;
            }

            // ============ ОПЛАТА КРИПТОВАЛЮТОЙ ============
            if (text.StartsWith("/crypto "))
            {
                string[] cmdParts = text.Split(' ');
                if (cmdParts.Length > 1 && int.TryParse(cmdParts[1], out int daysCount))
                {
                    if (daysCount == 7 || daysCount == 14 || daysCount == 30 || daysCount == 90)
                    {
                        double amount = 0;
                        if (daysCount == 7) amount = 5;
                        else if (daysCount == 14) amount = 8;
                        else if (daysCount == 30) amount = 12;
                        else if (daysCount == 90) amount = 30;

                        string invoice = await CreateCryptoInvoice(chatId, amount, daysCount);

                        if (!string.IsNullOrEmpty(invoice))
                        {
                            await SendMessage(chatId, "💳 <b>Оплата криптовалютой</b>\n\n" +
                                    $"📅 Тариф: {daysCount} дней\n" +
                                    $"💰 Сумма: ${amount} USDT\n\n" +
                                    $"🔗 <a href='{invoice}'>Оплатить через @CryptoBot</a>\n\n" +
                                    "⏳ Счет действителен 30 минут\n" +
                                    "✅ После оплаты ключ придет автоматически!");

                            pendingPurchases[chatId] = daysCount;
                        }
                        else
                        {
                            await SendMessage(chatId, "❌ Ошибка создания счета. Попробуйте позже.");
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

            // ПРОВЕРКА КЛЮЧА
            if (text.StartsWith("/check "))
            {
                string key = text.Replace("/check ", "").ToUpper().Trim();
                await SendMessage(chatId, CheckLicense(key));
                return;
            }

            // ============ АДМИН-КОМАНДЫ ============
            if (chatId == adminId)
            {
                // ГЕНЕРАЦИЯ КЛЮЧА
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

                // СПИСОК КЛЮЧЕЙ
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

            // НЕИЗВЕСТНАЯ КОМАНДА
            await SendMessage(chatId, "❓ Неизвестная команда. Напишите /help");
        }

        // ============ CRYPTOBOT API ============
        private static async Task<string> CreateCryptoInvoice(long chatId, double amount, int days)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    var data = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("amount", amount.ToString()),
                        new KeyValuePair<string, string>("currency", "USDT"),
                        new KeyValuePair<string, string>("description", $"CS2 Helper - {days} days"),
                        new KeyValuePair<string, string>("hidden_message", $"Ваш ключ будет отправлен после оплаты"),
                        new KeyValuePair<string, string>("paid_btn_name", "open"),
                        new KeyValuePair<string, string>("paid_btn_url", "https://t.me/CS3helper_bot")
                    });

                    string url = $"https://api.crypt.bot/v1/createInvoice?token={cryptoToken}";
                    var response = await client.PostAsync(url, data);
                    string json = await response.Content.ReadAsStringAsync();

                    Console.WriteLine($"📨 CryptoBot ответ: {json}");

                    var doc = JsonDocument.Parse(json);
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ CryptoBot ошибка: {ex.Message}");
                return null;
            }
        }

        // ============ ОСТАЛЬНОЙ КОД ============
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
            var random = new Random();
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
                    Console.WriteLine($"📂 Загружено {licenses.Count} ключей");
                }
                else
                {
                    Console.WriteLine("📭 Файл licenses.txt не найден");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Ошибка загрузки ключей: {ex.Message}");
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
                    lines.Add($"{key}|{expires}|{hwid}");
                }
                File.WriteAllLines(licenseFile, lines);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Ошибка сохранения ключей: {ex.Message}");
            }
        }

        private static async Task SendMessage(long chatId, string text)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    string url = $"https://api.telegram.org/bot{botToken}/sendMessage?chat_id={chatId}&text={Uri.EscapeDataString(text)}&parse_mode=HTML";
                    await client.GetAsync(url);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка отправки сообщения: {ex.Message}");
            }
        }
    }
}
