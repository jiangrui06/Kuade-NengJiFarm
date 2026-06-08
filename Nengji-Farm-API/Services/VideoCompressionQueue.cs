using System.Threading.Channels;

namespace WebAPI.Services;

public class VideoCompressionQueue
{
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });

    public ValueTask EnqueueAsync(string filePath) =>
        _channel.Writer.WriteAsync(filePath);

    public ValueTask<string> DequeueAsync(CancellationToken cancellationToken = default) =>
        _channel.Reader.ReadAsync(cancellationToken);
}
