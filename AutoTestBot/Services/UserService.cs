using Autotest.Bot.Console.Models.Questions;
using Autotest.Bot.Console.Models.Users;
using AutoTestBot.Models.Users;
using Newtonsoft.Json;

namespace Autotest.Bot.Console.Services
{
    class UserService
    {
        private List<User> _users;
        private const string UserJsonFilePath = "users.json";
        private readonly QuestionService _questionService;

        public UserService(QuestionService questionService)
        {
            _questionService = questionService;
            ReadUsersJson();
        }

        public User AddUser(long chatId, string name)
        {
            if (_users.Any(u => u.ChatId == chatId))
            {
                return _users.First(u => u.ChatId == chatId);
            }
            else
            {
                var user = new User
                {
                    ChatId = chatId,
                    Name = name,
                    Tickets = new List<Ticket>()
                };

                // create tickets 
                for (int i = 0; i < _questionService.TicketsCount; i++)
                {
                    user.Tickets.Add(new Ticket(i, QuestionService.TicketQuestionsCount));
                }

                _users.Add(user);

                SaveUsersJson();
                return user;
            }
        }

        public void UpdateUserStep(User user, EUserStep step)
        {
            user.Step = step;
            SaveUsersJson();
        }

        private void ReadUsersJson()
        {
            if (File.Exists(UserJsonFilePath))
            {
                var json = File.ReadAllText(UserJsonFilePath);
                _users = JsonConvert.DeserializeObject<List<User>>(json) ?? new List<User>();
            }
            else
            {
                _users = new List<User>();
            }
        }

        public void SaveUsersJson()
        {
            var json = JsonConvert.SerializeObject(_users);
            File.WriteAllText(UserJsonFilePath, json);
        }
    }
}