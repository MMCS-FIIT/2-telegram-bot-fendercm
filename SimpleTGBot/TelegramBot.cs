using System.Reflection.Metadata.Ecma335;

namespace SimpleTGBot;
using System.IO;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using static System.Formats.Asn1.AsnWriter;

public class TelegramBot
{
    // Токен TG-бота. Можно получить у @BotFather
    private const string BotToken =
    private static readonly Dictionary<long, int> UserTasks = new();
    private static readonly Dictionary<long, int> UserScores = new(); // <--- добавлено это поле
    private const string LogFilePath = "bot_logs.txt";
    /// <summary>
    /// Инициализирует и обеспечивает работу бота до нажатия клавиши Esc
    /// </summary>
    public async Task Run()
    {
        // Если вам нужно хранить какие-то данные во время работы бота (массив информации, логи бота,
        // историю сообщений для каждого пользователя), то это всё надо инициализировать в этом методе.
        // TODO: Инициализация необходимых полей
        
        // Инициализируем наш клиент, передавая ему токен.
        var botClient = new TelegramBotClient(BotToken);
        
        // Служебные вещи для организации правильной работы с потоками
        using CancellationTokenSource cts = new CancellationTokenSource();
        
        // Разрешённые события, которые будет получать и обрабатывать наш бот.
        // Будем получать только сообщения. При желании можно поработать с другими событиями.
        ReceiverOptions receiverOptions = new ReceiverOptions()
        {
            AllowedUpdates = new [] { UpdateType.Message }
        };

        // Привязываем все обработчики и начинаем принимать сообщения для бота
        botClient.StartReceiving(
            updateHandler: OnMessageReceived,
            pollingErrorHandler: OnErrorOccured,
            receiverOptions: receiverOptions,
            cancellationToken: cts.Token
        );

        // Проверяем что токен верный и получаем информацию о боте
        var me = await botClient.GetMeAsync(cancellationToken: cts.Token);
        Console.WriteLine($"Бот @{me.Username} запущен.\nДля остановки нажмите клавишу Esc...");
        
        // Ждём, пока будет нажата клавиша Esc, тогда завершаем работу бота
        while (Console.ReadKey().Key != ConsoleKey.Escape){}

        // Отправляем запрос для остановки работы клиента.
        cts.Cancel();
    }
    
    /// <summary>
    /// Обработчик события получения сообщения.
    /// </summary>
    /// <param name="botClient">Клиент, который получил сообщение</param>
    /// <param name="update">Событие, произошедшее в чате. Новое сообщение, голос в опросе, исключение из чата и т. д.</param>
    /// <param name="cancellationToken">Служебный токен для работы с многопоточностью</param>
    async Task OnMessageReceived(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        // Работаем только с сообщениями. Остальные события игнорируем
        var message = update.Message;
        if (message.Text is not { } messageText)
        {
            return;
        }

        // Получаем ID чата, в которое пришло сообщение. Полезно, чтобы отличать пользователей друг от друга.
        var chatId = message.Chat.Id;
        var userName = message.From?.FirstName ?? "User";
        await System.IO.File.AppendAllTextAsync(LogFilePath, $"{DateTime.Now}: {chatId} ({userName}) -> {messageText}\n", cancellationToken);
        // Печатаем на консоль факт получения сообщенияS
        Console.WriteLine($"Получено сообщение в чате {chatId}: '{messageText}'");
        var mainMenu = new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[] { " Новая задача", "Мой счёт" },
            new KeyboardButton[] { "Помощь" }
        })
        { ResizeKeyboard = true };
        // TODO: Обработка пришедших сообщений
        string input = messageText.ToLower().Trim();
        string[] startSynonyms = { "да", "ок", "давай", "го", "старт", "/start", "новая задача" };
        if (startSynonyms.Contains(input))
        {
            var rand = new Random();
            int a = rand.Next(2, 12);
            int b = rand.Next(2, 12);
            UserTasks[chatId] = a * b;

            await botClient.SendTextMessageAsync(chatId, $"Реши пример: {a} × {b} = ?", replyMarkup: mainMenu, cancellationToken: cancellationToken);
        }
        else if (input == "мой счёт")
        {
            int score = UserScores.GetValueOrDefault(chatId, 0);
            await botClient.SendTextMessageAsync(chatId, $"Твой результат: {score} правильных ответов! 🔥", cancellationToken: cancellationToken);
        }
        else if (input == "помощь")
        {
            await botClient.SendTextMessageAsync(chatId, $"Для получения задачи - нажми {"Новая задача"}, для того чтобы узнать свой счет - нажми {"Мой счет"}. Для ответа бот принимает только числа ", cancellationToken: cancellationToken);
        }
        else if (int.TryParse(messageText, out int userGuess)) // Обработка неадекватного ввода 
        {
            if (UserTasks.TryGetValue(chatId, out int correctAnswer))
            {
                if (userGuess == correctAnswer)
                {

                    UserScores[chatId] = UserScores.GetValueOrDefault(chatId, 0) + 1;
                    UserTasks.Remove(chatId);

                    await botClient.SendTextMessageAsync(chatId, "Бинго! Красавчик. Ещё один пример?", cancellationToken: cancellationToken);
                }
                else
                {
                    await botClient.SendTextMessageAsync(chatId, "Мимо! Попробуй посчитать ещё раз или нажми 'Новая задача'.", cancellationToken: cancellationToken);
                }
            }
            else
            {
                await botClient.SendTextMessageAsync(chatId, "Чтобы начать, нажми на кнопку 'Новая задача'.", cancellationToken: cancellationToken);
            }
        }
        else
        {
            await botClient.SendTextMessageAsync(chatId, "Я понимаю только числа или кнопки внизу! 😊", replyMarkup: mainMenu, cancellationToken: cancellationToken);
        }
    }
    

    /// <summary>
    /// Обработчик исключений, возникших при работе бота
    /// </summary>
    /// <param name="botClient">Клиент, для которого возникло исключение</param>
    /// <param name="exception">Возникшее исключение</param>
    /// <param name="cancellationToken">Служебный токен для работы с многопоточностью</param>
    /// <returns></returns>
    Task OnErrorOccured(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        // В зависимости от типа исключения печатаем различные сообщения об ошибке
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException
                => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            
            _ => exception.ToString()
        };

        Console.WriteLine(errorMessage);
        
        // Завершаем работу
        return Task.CompletedTask;
    }
}