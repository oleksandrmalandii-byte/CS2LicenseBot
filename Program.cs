using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;

namespace MyTelegramBot
{
    class Program
    {
        private static readonly string token = Environment.GetEnvironmentVariable("BOT_TOKEN") ?? "8751944130:AAFdPiS9gB2gDQ5cHP557G1lyGsBBu8YSjs";
        private static readonly long adminId = 8688724190;
        private static int lastId = 0;
        private static Dictionary<string, (string key, DateTime expires, string hwid)> licenses = new Dictionary<string, (string, DateTime, string)>();
        private static string licenseFile = "/tmp/licenses.txt";

        static void Main(string[] args)
        {
            MainAsync(args).GetAwaiter().GetResult();
        }

        static async Task MainAsync(string[] args)
        {
            Console.WriteLine("🤖 CS2 Helper Bot");
            Console.WriteLine($"Бот: @CS3helper_bot");
            Console.WriteLine($"Админ ID: {adminId}");
            Console.WriteLine($"Токен: {token.Substring(0, 10)}...");
            Console.WriteLine();

            LoadLicenses();

            while (true)
            {
                try
                {
                    using (var client = new HttpClient())
                    {
                        string url = $"https://api.telegram.org/bot{token}/getUpdates?offset={lastId + 1}";
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
            string reply = "";

            switch (text)
            {
                case "/start":
                    reply = "🤖 <b>CS2 Helper Bot</b>\n\n" +
                            "📌 <b>Команды:</b>\n" +
                            "/id - Узнать свой ID\n" +
                            "/buy - Купить лицензию\n" +
                            "/download - Скачать чит\n" +
                            "/check КЛЮЧ - Проверить ключ\n" +
                            "/help - Помощь";
                    break;

                case "/id":
                    reply = $"✅ <b>Ваш ID:</b> <code>{chatId}</code>";
                    break;

                case "/help":
                    reply = "📖 <b>Помощь:</b>\n\n" +
                            "/id - Ваш ID\n" +
                            "/buy - Тарифы\n" +
                            "/download - Скачать чит\n" +
                            "/check КЛЮЧ - Проверить ключ\n" +
                            "/help - Эта справка\n\n" +
                            "👤 <b>Поддержка:</b> @YourSupport";
                    break;

                case "/download":
                    reply = "📥 <b>Скачать CS2 Helper:</b>\n\n" +
                            "🔗 https://disk.yandex.ru/d/your_cheat_link\n\n" +
                            "🔐 <b>Пароль:</b> <code>cs2helper</code>\n\n" +
                            "⚠️ Отключите антивирус перед установкой!";
                    break;

                case "/buy":
                    reply = "🛒 <b>Тарифы:</b>\n\n" +
                            "📅 <b>7 дней</b> - 300₽\n" +
                            "📆 <b>14 дней</b> - 500₽\n" +
                            "📅 <b>1 месяц</b> - 800₽\n" +
                            "📆 <b>3 месяца</b> - 2000₽\n\n" +
                            "💰 <b>Реквизиты для оплаты:</b>\n" +
                            "💳 Qiwi: +7XXXXXXXXXX\n" +
                            "💳 ЮMoney: XXXXXXXXXX\n" +
                            "₿ BTC: bc1qxxxxxxxxxxxxxx\n\n" +
                            "📌 <b>После оплаты:</b>\n" +
                            "1️⃣ Пришлите скриншот @YourSupport\n" +
                            "2️⃣ Получите ключ!\n\n" +
                            "🔥 Ключ активируется только на одном ПК!";
                    break;

                default:
                    if (text.StartsWith("/check "))
                    {
                        string key = text.Replace("/check ", "").ToUpper().Trim();
                        reply = CheckLicense(key);
                    }
                    else if (chatId == adminId && text.StartsWith("/gen "))
                    {
                        string[] parts = text.Split(' ');
                        if (parts.Length > 1 && int.TryParse(parts[1], out int days))
                        {
                            string key = GenerateKey(days);
                            reply = $"✅ <b>Ключ создан!</b>\n\n" +
                                    $"🔑 <code>{key}</code>\n" +
                                    $"📅 Срок: {days} дней\n" +
                                    $"📅 До: {DateTime.Now.AddDays(days):dd.MM.yyyy}\n\n" +
                                    "📩 Отправьте ключ покупателю!";
                        }
                        else
                        {
                            reply = "❌ Использование: /gen [дни]\nПример: /gen 30";
                        }
                    }
                    else if (chatId == adminId && text == "/keys")
                    {
                        reply = "📋 <b>Список ключей:</b>\n\n";
                        if (licenses.Count == 0)
                            reply += "📭 Нет ключей";
                        else
                        {
                            int count = 0;
                            foreach (var kvp in licenses)
                            {
                                count++;
                                var (key, expires, hwid) = kvp.Value;
                                string status = string.IsNullOrEmpty(hwid) ? "⬜ Не активирован" : "✅ Активирован";
                                reply += $"{count}. <code>{kvp.Key}</code> - {status}\n";
                                if (count >= 20) { reply += "\n..."; break; }
                            }
                        }
                    }
                    else
                    {
                        reply = "❓ Неизвестная команда. Напишите /help";
                    }
                    break;
            }

            if (!string.IsNullOrEmpty(reply))
            {
                await SendMessage(chatId, reply);
            }
        }

        private static string CheckLicense(string key)
        {
            if (string.IsNullOrEmpty(key) || key.Length < 20)
                return "❌ Неверный формат ключа! (XXXXX-XXXXX-XXXXX-XXXXX)";

            if (licenses.ContainsKey(key))
            {
                var (savedKey, expires, hwid) = licenses[key];

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
            var parts = new List<string>();

            for (int i = 0; i < 4; i++)
            {
                char[] part = new char[5];
                for (int j = 0; j < 5; j++)
                    part[j] = chars[random.Next(chars.Length)];
                parts.Add(new string(part));
            }

            string key = string.Join("-", parts);
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
                    string url = $"https://api.telegram.org/bot{token}/sendMessage?chat_id={chatId}&text={Uri.EscapeDataString(text)}&parse_mode=HTML";
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
