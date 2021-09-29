using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace MwiInterview.AchToggle.App
{
    /// <summary>
    /// Simple ACH record split toggler.  The first console argument is treated as the filepath
    /// </summary>
    /// <remarks>
    /// Destructively writes the toggled file on top of the file's current location.  Does not correct jagged files, although that could be a trivial addition.
    /// </remarks>
    class Program
    {
        /// <summary>
        /// The newline character.
        /// </summary>
        const char NEWLINE = '\n';

        /// <summary>
        /// The carriage return character
        /// </summary>
        const char CARRIAGE_RETURN = '\r';

        /// <summary>
        /// ACH file record length (94).
        /// </summary>
        const int ACH_RECORD_LENGTH = 94;

        /// <summary>
        /// The program entry point and main logic.
        /// </summary>
        /// <param name="args">The console arguments.</param>
        /// <returns>A <see cref="Task"/> to await the work.</returns>
        static async Task Main(string[] args)
        {
            if (args.Length > 0)
            {
                bool? isMultilineInput = null;
                var lines = new List<string>();
                Stream stream = null;
                try
                {
                    stream = new FileStream(args[0], FileMode.Open);
                    if (stream.Length > ACH_RECORD_LENGTH)
                    {
                        using (var reader = new StreamReader(stream))
                        {
                            var buffer = new char[ACH_RECORD_LENGTH];
                            int charReadCount = await reader.ReadBlockAsync(buffer, 0, buffer.Length);
                            var nextCharInt = reader.Peek();
                            if (nextCharInt > -1)
                            {
                                var nextChar = (char)nextCharInt;
                                if (nextChar == NEWLINE || nextChar == CARRIAGE_RETURN)
                                {
                                    // already split (technically this could also catch 1-record files with a trailing newline)
                                    isMultilineInput = true;
                                    reader.BaseStream.Seek(0, SeekOrigin.Begin);
                                    reader.DiscardBufferedData();
                                    while (!reader.EndOfStream)
                                    {
                                        lines.Add(await reader.ReadLineAsync());
                                    }
                                }
                                else
                                {
                                    isMultilineInput = false;
                                    lines.Add(new string(buffer, 0, charReadCount));
                                    while (!reader.EndOfStream)
                                    {
                                        charReadCount = await reader.ReadBlockAsync(buffer, 0, buffer.Length);
                                        lines.Add(new string(buffer, 0, charReadCount));
                                    }
                                }
                            }
                            else
                            {
                                Console.Out.WriteLine($"failed to read character {ACH_RECORD_LENGTH + 1} to determine whether file is original or split");
                            }
                        }
                        stream.Dispose();
                        if (isMultilineInput.HasValue)
                        {
                            stream = new FileStream(args[0], FileMode.Create);
                            stream.SetLength(0);
                            if (isMultilineInput.Value)
                            {
                                await WriteUnsplitToStreamAsync(stream, lines);
                            }
                            else
                            {
                                await WriteSplitToStreamAsync(stream, lines);
                            }
                        }
                    }
                    else
                    {
                        Console.Out.WriteLine($"file is {stream.Length} characters (less than or equal to {ACH_RECORD_LENGTH}) so no operations were performed");
                    }
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e);
                    Environment.Exit(e.HResult);
                }
                finally
                {
                    stream?.Dispose();
                }
            }
        }

        /// <summary>
        /// Write the specified <paramref name="records"/> to the <paramref name="stream"/>, where each <see cref="string"/> is written on its own line.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> to which to write.</param>
        /// <param name="records">The ACH records to write.</param>
        /// <returns>A <see cref="Task"/> to await the work.</returns>
        static async Task WriteSplitToStreamAsync(Stream stream, IEnumerable<string> records)
        {
            using (var writer = new StreamWriter(stream))
            {
                foreach (var record in records)
                {
                    await writer.WriteLineAsync(record);
                }
                await writer.FlushAsync();
            }
        }


        /// <summary>
        /// Write the specified <paramref name="records"/> to the <paramref name="stream"/>, where each <see cref="string"/> is appended to the first line.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> to which to write.</param>
        /// <param name="records">The ACH records to write.</param>
        /// <returns>A <see cref="Task"/> to await the work.</returns>
        static async Task WriteUnsplitToStreamAsync(Stream stream, IEnumerable<string> records)
        {
            using (var writer = new StreamWriter(stream))
            {
                foreach (var record in records)
                {
                    await writer.WriteAsync(record);
                }
                await writer.FlushAsync();
            }
        }
    }
}
