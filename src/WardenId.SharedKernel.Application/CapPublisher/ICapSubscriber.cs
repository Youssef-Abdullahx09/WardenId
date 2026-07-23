using DotNetCore.CAP;

namespace WardenId.SharedKernel.Application.CapPublisher;

public interface ICapSubscriber<in TMessage> : ICapSubscribe where TMessage : class
{
    public Task OnReceive(TMessage message);
}