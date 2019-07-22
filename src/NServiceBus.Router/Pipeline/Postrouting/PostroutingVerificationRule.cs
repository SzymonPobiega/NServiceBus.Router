using System;
using System.Threading.Tasks;
using NServiceBus.Router;

class PostroutingVerificationRule : IRule<PostroutingContext, PostroutingContext>
{
    public async Task Invoke(PostroutingContext context, Func<PostroutingContext, Task> next)
    {
        await next(context).ConfigureAwait(false);

        if (context.Extensions.TryGet<State>(out var state))
        {
            foreach (var message in context.Messages)
            {
                state.MessageSent(message.Message.MessageId);
            }
        }
    }

    public class State
    {
        string incomingMessageId;

        public State(string incomingMessageId)
        {
            this.incomingMessageId = incomingMessageId;
        }

        public bool HasBeenForwarded { get; private set; }

        public void MessageSent(string messageId)
        {
            if (messageId == incomingMessageId)
            {
                HasBeenForwarded = true;
            }
        }
    }
}