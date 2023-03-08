using Autotest.Bot.Console.Models.Users;
using Autotest.Bot.Console.Services;
using AutoTestBot.Models.Users;
using JFA.Telegram.Console;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using User = Autotest.Bot.Console.Models.Users.User;

var botManager = new TelegramBotManager();
var bot = botManager.Create("5815471063:AAFmlX2m7BhUMa7vDnzaCRr0MYzm4c4F4bY");

var questionService = new QuestionService(bot);
var userService = new UserService(questionService);

botManager.Start(OnUpdate);

void OnUpdate(Update update)
{
    var (chatId, message, name, isSuccess) = GetMessage(update);



    if (!isSuccess)
        return;

    var user = userService.AddUser(chatId, name);

    if (update.Type == UpdateType.CallbackQuery)
    {
        if (message.StartsWith("page"))
        {
            bot.DeleteMessageAsync(user.ChatId, update.CallbackQuery.Message.MessageId);
            var page = Convert.ToInt32(message.Replace("page", ""));
            ShowTickets(user, page);
        }
    }

    switch (user.Step)
    {
        case EUserStep.Default: SendMenu(user); break;
        case EUserStep.InMenu: ChooseMenu(user, message); break;
        case EUserStep.InTest: CheckAnswer(user, message); break;
    }

}

Tuple<long, string, string, bool> GetMessage(Update update)
{
    if (update.Type == UpdateType.Message)
    {
        return new(update.Message!.From!.Id, update.Message!.Text!, update.Message!.From!.FirstName, true);
    }

    if (update.Type == UpdateType.CallbackQuery)
    {
        return new(update.CallbackQuery!.From.Id, update.CallbackQuery!.Data!, update.CallbackQuery!.From.FirstName, true);
    }

    return new(default, default, default, false);
}

void SendMenu(User user)
{
    var buttons = new List<List<KeyboardButton>>()
    {
        new List<KeyboardButton>()
        {
            new KeyboardButton("Start Test")
        },
        new List<KeyboardButton>()
        {
            new KeyboardButton("Tickets")
        },
        new List<KeyboardButton>()
        {
            new KeyboardButton("Show Result")
        }
    };

    bot.SendTextMessageAsync(user.ChatId, "Menu", replyMarkup: new ReplyKeyboardMarkup(buttons));

    userService.UpdateUserStep(user, EUserStep.InMenu);
}

void ChooseMenu(User user, string message)
{
    switch (message)
    {
        case "Start Test": StartTest(user); break;
        case "Tickets": ShowTickets(user); break;
        case "Show Result": ShowResult(user); break;
        case "Start":
            {
                userService.UpdateUserStep(user, EUserStep.InTest);
                SendTicketQuestion(user);
            }
            break;
    }

    if (message.StartsWith("start-ticket"))
    {
        var ticketIndex = Convert.ToInt32(message.Replace("start-ticket", ""));
        StartTicket(user, ticketIndex);
    }
}

void StartTest(User user)
{
    user.CurrentTicket = questionService.CreateTicket();

    bot.SendTextMessageAsync(
        user.ChatId,
        $"Ticket{user.CurrentTicket.Index}\n {user.CurrentTicket.QuestionsCount}",
        replyMarkup: new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("Start")));
}

void ShowResult(User user)
{
    var message = "Ticket results\n";

    message += $"Tickets: {user.Tickets.Count(t => t.IsCompleted)}\n";
    message += $"Questions: {user.Tickets.Sum(t => t.CorrectCount)}\n";

    bot.SendTextMessageAsync(user.ChatId, message);
}

void SendTicketQuestion(User user)
{
    questionService.SendQuestionByIndex(user.ChatId, user.CurrentTicket.CurrentQuestionIndex);
}

void CheckAnswer(User user, string message)
{
    try
    {
        int[] data = message.Split(',').Select(int.Parse).ToArray();

        var answer = questionService.QuestionAnswer(data[0], data[1]);

        if (answer)
            user.CurrentTicket!.CorrectCount++;

        user.CurrentTicket!.CurrentQuestionIndex++;

        if (user.CurrentTicket.IsCompleted)
        {
            bot.SendTextMessageAsync(user.ChatId,
                $"Result: {user.CurrentTicket.CorrectCount}/{user.CurrentTicket.QuestionsCount}");

            //user.Tickets.Add(user.CurrentTicket);

            userService.UpdateUserStep(user, EUserStep.InMenu);
        }
        else
        {
            SendTicketQuestion(user);
        }
    }
    catch (Exception e)
    {
        Console.WriteLine(e.Message);
    }
}

void ShowTickets(User user, int page = 1)
{
    var pagesCount = questionService.TicketsCount / 5;
    var message = $"Tickets\nPage: {page}/{pagesCount}";

    var buttons = new List<List<InlineKeyboardButton>>();

    for (int i = page * 5 - 5; i < page * 5; i++)
    {
        var ticket = user.Tickets[i];
        var ticketInfo = $"Ticket{ticket.Index + 1}";
        if (ticket.StartIndex != ticket.CurrentQuestionIndex)
        {
            if (ticket.CorrectCount == ticket.QuestionsCount)
            {
                ticketInfo += $" ✅";
            }
            else
            {
                ticketInfo += $" {ticket.CorrectCount}/{ticket.QuestionsCount}";
            }
        }

        buttons.Add(new List<InlineKeyboardButton>()
        {
            InlineKeyboardButton.WithCallbackData(ticketInfo, $"start-ticket{ticket.Index}")
        });
    }

    buttons.Add(CreatePaginationButtons(pagesCount, page));

    bot.SendTextMessageAsync(user.ChatId, message, replyMarkup: new InlineKeyboardMarkup(buttons));
}

List<InlineKeyboardButton> CreatePaginationButtons(int pagesCount, int page)
{
    var buttons = new List<InlineKeyboardButton>();

    if (page > 1)
        buttons.Add(InlineKeyboardButton.WithCallbackData($"<", $"page{page - 1}"));
    if (pagesCount - 1 > page)
    {
        buttons.Add(InlineKeyboardButton.WithCallbackData($"{page}", $"page{page}"));
    }
    if (pagesCount - 2 > page)
    {
        buttons.Add(InlineKeyboardButton.WithCallbackData($"{page + 1}", $"page{page + 1}"));
    }
    if (pagesCount - 3 > page)
    {
        buttons.Add(InlineKeyboardButton.WithCallbackData($"{page + 2}", $"page{page + 2}"));
    }
    if (pagesCount - 4 > page)
    {
        buttons.Add(InlineKeyboardButton.WithCallbackData($"{page + 3}", $"page{page + 3}"));
    }
    if (pagesCount - 5 > page)
    {
        buttons.Add(InlineKeyboardButton.WithCallbackData($"{page + 4}", $"page{page + 4}"));
    }
    if (pagesCount > page)
        buttons.Add(InlineKeyboardButton.WithCallbackData($">", $"page{page + 1}"));

    return buttons;
}

void StartTicket(User user, int ticketIndex)
{
    user.CurrentTicket = user.Tickets[ticketIndex];
    user.CurrentTicket.SetDefault();

    bot.SendTextMessageAsync(
        user.ChatId,
        $"Ticket{user.CurrentTicket.Index + 1}\n {user.CurrentTicket.QuestionsCount}",
        replyMarkup: new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("Start")));
}