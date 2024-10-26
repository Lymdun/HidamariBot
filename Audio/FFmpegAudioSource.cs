using System.Buffers;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using Disqord.Extensions.Voice;

namespace HidamariBot.Audio;

// Represents an audio source implementation that uses FFmpeg to produce
// the Opus audio packets. The input stream can be any format
// that FFmpeg is able to convert (virtually anything).
public class FFmpegAudioSource : AudioSource {
    readonly Stream _stream;

    // The path to the FFmpeg executable.
    // For this default value to work
    // put FFmpeg in the bot's working directory or add it to PATH.
    const string FFmpegPath = "ffmpeg";

    public FFmpegAudioSource(Stream stream) {
        _stream = stream;
    }

    // https://ffmpeg.org/ffmpeg.html
    static void PopulateFFmpegArguments(Collection<string> arguments) {
        arguments.Add("-loglevel");
        arguments.Add("error");

        arguments.Add("-i");
        arguments.Add("pipe:0");

        arguments.Add("-ar");
        arguments.Add("48000");

        arguments.Add("-b:a");
        arguments.Add("128k");

        arguments.Add("-filter:a");
        arguments.Add("volume=0.5");

        arguments.Add("-c:a");
        arguments.Add("libopus");

        arguments.Add("-application");
        arguments.Add("audio");

        arguments.Add("-frame_duration");
        arguments.Add("20");

        arguments.Add("-f");
        arguments.Add("oga");

        // Must be the LAST argument
        arguments.Add("pipe:1");
    }

    public override async IAsyncEnumerator<Memory<byte>> GetAsyncEnumerator(CancellationToken cancellationToken) {
        // If the stream is seekable, rewind it to the beginning.
        if (_stream.CanSeek) {
            // This project doesn't reuse audio sources, but if it did,
            // this code would make this audio source reusable as long as
            // the stream passed in allows for rewinding.
            _stream.Seek(0, SeekOrigin.Begin);
        }

        var ffmpegStartInfo = new ProcessStartInfo {
            FileName = FFmpegPath,
            RedirectStandardOutput = true,
            RedirectStandardInput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        PopulateFFmpegArguments(ffmpegStartInfo.ArgumentList);

        using (var ffmpeg = Process.Start(ffmpegStartInfo)!) {
            try {
                // Start the error reading and stream copying tasks.
                Task<string?> readErrorTask = ReadFFmpegStderrAsync(ffmpeg.StandardError.BaseStream,
                    ffmpeg.StandardError.CurrentEncoding, cancellationToken);
                Task copyStreamTask =
                    CopyStreamToFFmpegStdinAsync(_stream, ffmpeg.StandardInput.BaseStream, cancellationToken);

                // Create an Ogg source for stdout.
                var ogg = new OggStreamAudioSource(ffmpeg.StandardOutput.BaseStream);

                // Enumerate the Ogg source to yield Ogg packets.
                await foreach (var packet in ogg.WithCancellation(cancellationToken)) {
                    yield return packet;
                }

                // Check if FFmpeg exited with an error.
                if (ffmpeg.ExitCode != 0) {
                    string? error = await readErrorTask;
                    error = !string.IsNullOrWhiteSpace(error)
                        ? error.ReplaceLineEndings(";")
                        : "unknown error";

                    throw new Exception($"FFmpeg exited with code {ffmpeg.ExitCode} ({error}).");
                }

                // Propagate the error from stream copying, if any.
                await copyStreamTask;
            } finally {
                await CleanUpFFmpegAsync(ffmpeg);
            }
        }
    }

    static async Task CleanUpFFmpegAsync(Process ffmpeg) {
        // Start the exit waiting task.
        Task exitTask = ffmpeg.WaitForExitAsync(default);

        // Close the data streams; order matters here.
        await ffmpeg.StandardOutput.BaseStream.DisposeAsync();
        await ffmpeg.StandardError.BaseStream.DisposeAsync();
        await ffmpeg.StandardInput.BaseStream.DisposeAsync();

        try {
            // Give FFmpeg some time to close gracefully.
            await exitTask.WaitAsync(TimeSpan.FromSeconds(1), CancellationToken.None);
        } catch (TimeoutException) {
            try {
                // Kill FFmpeg if it took too long to close.
                ffmpeg.Kill();
            } catch { }
        }

        await exitTask;
    }

    static async Task CopyStreamToFFmpegStdinAsync(Stream stream, Stream stdin,
        CancellationToken cancellationToken) {
        // Yield, so the code below runs in background.
        await Task.Yield();

        // Copy the stream to stdin.
        await stream.CopyToAsync(stdin, cancellationToken);

        // Close stdin after copying is complete.
        await stdin.DisposeAsync();
    }

    // This method may seem complex, but it's actually
    // just a bunch of boilerplate code that's necessary
    // to efficiently, correctly, and asynchronously
    // capture stderr of any size and convert it into a string.
    static async Task<string?> ReadFFmpegStderrAsync(Stream stderr, Encoding encoding,
        CancellationToken cancellationToken) {
        // Yield, so the code below runs in background.
        await Task.Yield();

        StringBuilder? sb = null;
        var buffer = new byte[256];
        try {
            int bytesRead;
            while ((bytesRead = await stderr.ReadAsync(buffer, cancellationToken)) != 0) {
                static void AppendChars(StringBuilder sb, Encoding errorEncoding, ReadOnlySpan<byte> bufferSpan) {
                    int charCount = errorEncoding.GetCharCount(bufferSpan);
                    char[] chars = ArrayPool<char>.Shared.Rent(charCount);
                    Span<char> charSpan = chars.AsSpan(0, charCount);
                    try {
                        errorEncoding.GetChars(bufferSpan, charSpan);
                        sb.Append(charSpan);
                    } finally {
                        ArrayPool<char>.Shared.Return(chars);
                    }
                }

                AppendChars(sb ??= new(), encoding, buffer.AsSpan(0, bytesRead));
            }
        } catch {
            // Ignored, so that we can return any errors read thus far.
        }

        return sb?.ToString();
    }
}
