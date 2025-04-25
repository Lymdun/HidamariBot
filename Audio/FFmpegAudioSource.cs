using System.Collections.ObjectModel;
using System.Diagnostics;
using Disqord.Extensions.Voice;

namespace HidamariBot.Audio;

// Represents an audio source implementation that uses FFmpeg to produce
// the Opus audio packets. The input stream can be any format
// that FFmpeg is able to convert (virtually anything).
public class FFmpegAudioSource : AudioSource {
    readonly string _url;

    // The path to the FFmpeg executable.
    // For this default value to work
    // put FFmpeg in the bot's working directory or add it to PATH.
    const string FFmpegPath = "ffmpeg";

    public FFmpegAudioSource(string url) {
        _url = url;
    }

    // https://ffmpeg.org/ffmpeg.html
    static void PopulateFFmpegArguments(Collection<string> arguments, string url) {
        arguments.Add("-loglevel");
        arguments.Add("error");

        arguments.Add("-re");

        arguments.Add("-thread_queue_size");
        arguments.Add("8192");

        arguments.Add("-i");
        arguments.Add(url);

        arguments.Add("-ar");
        arguments.Add("48000");

        arguments.Add("-ac");
        arguments.Add("2");

        arguments.Add("-c:a");
        arguments.Add("libopus");

        arguments.Add("-b:a");
        arguments.Add("128k");

        arguments.Add("-tune");
        arguments.Add("zerolatency");

        arguments.Add("-filter:a");
        arguments.Add("volume=0.5");

        arguments.Add("-application");
        arguments.Add("audio");

        arguments.Add("-frame_duration");
        arguments.Add("20");

        arguments.Add("-f");
        arguments.Add("oga");

        arguments.Add("pipe:1");
    }

    public override async IAsyncEnumerator<Memory<byte>> GetAsyncEnumerator(CancellationToken cancellationToken) {
        var startInfo = new ProcessStartInfo {
            FileName = FFmpegPath, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
        };

        PopulateFFmpegArguments(startInfo.ArgumentList, _url);

        using var ffmpeg = Process.Start(startInfo)!;
        try {
            var ogg = new OggStreamAudioSource(ffmpeg.StandardOutput.BaseStream);

            await foreach (Memory<byte> packet in ogg.WithCancellation(cancellationToken))
                yield return packet;

            if (ffmpeg.ExitCode != 0) {
                throw new Exception($"FFmpeg exited with code {ffmpeg.ExitCode}");
            }
        } finally {
            await CleanUpFFmpegAsync(ffmpeg);
        }
    }

    static async Task CleanUpFFmpegAsync(Process ffmpeg) {
        Task exitTask = ffmpeg.WaitForExitAsync();

        await ffmpeg.StandardOutput.BaseStream.DisposeAsync();
        await ffmpeg.StandardError.BaseStream.DisposeAsync();

        try {
            await exitTask.WaitAsync(TimeSpan.FromSeconds(1), CancellationToken.None);
        } catch (TimeoutException) {
            try { ffmpeg.Kill(); } catch { }
        }

        await exitTask;
    }
}
