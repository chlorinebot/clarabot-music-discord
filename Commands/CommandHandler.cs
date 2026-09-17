using Discord;

using Discord.Commands;

using Discord.WebSocket;

using Microsoft.Extensions.DependencyInjection;

using System;

using System.Reflection;

using System.Threading.Tasks;



namespace Clara_bot.Commands

{

    public class CommandHandler

    {

        private readonly DiscordSocketClient _client;

        private readonly CommandService _commands;

        private readonly IServiceProvider _services;

        private readonly StatusModule _statusModule;

        private readonly BotLoggingService _loggingService;



        public CommandHandler(DiscordSocketClient client, CommandService commands, IServiceProvider services, StatusModule statusModule, BotLoggingService loggingService)

        {

            _client = client;

            _commands = commands;

            _services = services;

            _statusModule = statusModule;

            _loggingService = loggingService;

        }



        public async Task InitializeAsync()

        {

            // Đăng ký tất cả Modules (MusicModule, GeneralModule, ...)

            try
            {
                await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
                _loggingService.Log("✅ Đã load tất cả modules thành công.");
            }
            catch (Exception ex)
            {
                _loggingService.Log($"❌ Lỗi khi load modules: {ex}");
            }



            // Đăng ký sự kiện nhận tin nhắn

            _client.MessageReceived += message =>

            {

                _ = Task.Run(async () =>

                {

                    try

                    {

                        await HandleCommandAsync(message);

                    }

                    catch (Exception ex)

                    {

                        _loggingService.Log($"Lỗi handler: {ex}");

                    }

                });



                return Task.CompletedTask;

            };



            _client.ButtonExecuted += component =>

            {

                _ = Task.Run(async () =>

                {

                    try

                    {

                        _statusModule.RecordInteraction();

                        // Thử xử lý help component trước

                        var handled = await GeneralModule.TryHandleHelpComponentAsync(component);

                        if (handled) return;



                        // Thử xử lý speed component

                        handled = await MusicModule.TryHandleSpeedComponentAsync(component, _services.GetRequiredService<Lavalink4NET.IAudioService>());

                        if (!handled)

                        {

                            // Nếu không phải speed, thử xử lý playlist

                            await MusicModule.TryHandleShowPlaylistComponentAsync(component);

                        }

                    }

                    catch (Exception ex)

                    {

                        _loggingService.Log($"Lỗi button handler: {ex}");

                    }

                });



                return Task.CompletedTask;

            };

        }



        private async Task HandleCommandAsync(SocketMessage messageParam)

        {

            if (messageParam is not SocketUserMessage message) return;

            if (message.Author.IsBot) return;



            int argPos = 0;



            if (message.HasCharPrefix('/', ref argPos))

            {

                _statusModule.RecordInteraction();

                var context = new SocketCommandContext(_client, message);



                var result = await _commands.ExecuteAsync(context, argPos, _services);



                if (!result.IsSuccess && result.Error != CommandError.UnknownCommand)

                {

                    _loggingService.Log($"Lỗi lệnh: {result.ErrorReason}");

                }



                // Xóa tin nhắn command sau khi xử lý xong (nuốt tin nhắn)

                /*

                if (result.IsSuccess || result.Error == CommandError.UnknownCommand)

                {

                    try

                    {

                        await Task.Delay(500); // Đợi 0.5s để người dùng thấy phản hồi

                        await message.DeleteAsync();

                    }

                    catch (Exception ex)

                    {

                        _loggingService.Log($"Không thể xóa tin nhắn: {ex.Message}");

                    }

                }

                */



                return;

            }



            await RoleplayModule.TryHandleRoleplayMessageAsync(message);

        }

    }

}

