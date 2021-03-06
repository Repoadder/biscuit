using Discord;
using Discord.Commands;
using NadekoBot.Classes;
using NadekoBot.Modules.Permissions.Classes;
using System;
using System.Collections.Concurrent;
using System.Timers;

namespace NadekoBot.Modules.Administration.Commands
{
    class MessageRepeater : DiscordCommand
    {
        private readonly ConcurrentDictionary<Server, Repeater> repeaters = new ConcurrentDictionary<Server, Repeater>();
        private class Repeater
        {
            [Newtonsoft.Json.JsonIgnore]
            public Timer MessageTimer { get; set; }
            [Newtonsoft.Json.JsonIgnore]
            public Channel RepeatingChannel { get; set; }

            public ulong RepeatingServerId { get; set; }
            public ulong RepeatingChannelId { get; set; }
            public Message lastMessage { get; set; } = null;
            public string RepeatingMessage { get; set; }
            public int Interval { get; set; }

            public Repeater Start()
            {
                MessageTimer = new Timer { Interval = Interval };
                MessageTimer.Elapsed += async (s, e) =>
                {
                    var ch = RepeatingChannel;
                    var msg = RepeatingMessage;
                    if (ch != null && !string.IsNullOrWhiteSpace(msg))
                    {
                        try
                        {
                            if (lastMessage != null)
                                await lastMessage.Delete().ConfigureAwait(false);
                        }
                        catch { }
                        try
                        {
                            lastMessage = await ch.SendMessage(msg).ConfigureAwait(false);
                        }
                        catch { }
                    }
                };
                return this;
            }
        }
        internal override void Init(CommandGroupBuilder cgb)
        {

            cgb.CreateCommand(Module.Prefix + "repeat")
                .Description("Repeat a message every X minutes. If no parameters are specified, " +
                             "repeat is disabled. Requires manage messages.\n**Usage**:`.repeat 5 Hello there`")
                .Parameter("minutes", ParameterType.Optional)
                .Parameter("msg", ParameterType.Unparsed)
                .AddCheck(SimpleCheckers.ManageMessages())
                .Do(async e =>
                {
                    var minutesStr = e.GetArg("minutes");
                    var msg = e.GetArg("msg");

                    // if both null, disable
                    if (string.IsNullOrWhiteSpace(msg) && string.IsNullOrWhiteSpace(minutesStr))
                    {
                        await e.Channel.SendMessage("Repeating disabled").ConfigureAwait(false);
                        Repeater rep;
                        if (repeaters.TryRemove(e.Server, out rep))
                            rep.MessageTimer.Stop();
                        return;
                    }
                    int minutes;
                    if (!int.TryParse(minutesStr, out minutes) || minutes < 1 || minutes > 1440)
                    {
                        await e.Channel.SendMessage("Invalid value").ConfigureAwait(false);
                        return;
                    }

                    var repeater = repeaters.GetOrAdd(
                        e.Server,
                        s => new Repeater
                        {
                            Interval = minutes * 60 * 1000,
                            RepeatingChannel = e.Channel,
                            RepeatingChannelId = e.Channel.Id,
                            RepeatingServerId = e.Server.Id,
                        }.Start()
                    );

                    if (!string.IsNullOrWhiteSpace(msg))
                        repeater.RepeatingMessage = msg;

                    repeater.MessageTimer.Stop();
                    repeater.MessageTimer.Start();

                    await e.Channel.SendMessage(String.Format("👌 Repeating `{0}` every " +
                                                              "**{1}** minutes on {2} channel.",
                                                              repeater.RepeatingMessage, minutes, repeater.RepeatingChannel))
                                                              .ConfigureAwait(false);
                });
        }

        public MessageRepeater(DiscordModule module) : base(module) { }
    }
}
