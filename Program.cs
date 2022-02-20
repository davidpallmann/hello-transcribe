using Amazon;
using Amazon.TranscribeService;

namespace hello_transcribe
{
    public class Program
    {
        private static RegionEndpoint _region = RegionEndpoint.USWest2;
        private static string _bucketName = "hello-transcribe";

        public static async Task Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage:   dotnet run -- languageCode http-uri-s3-media-file");
                Console.WriteLine("Example: dotnet run -- en-US https://hello-transcribe1.s3.us-west-2.amazonaws.com/winstonchurchillarmyourselves.mp3");
            }

            var languageCode = args[0];
            var filePath = args[1];

            var transcribeHelper = new TranscribeHelper(_region, _bucketName);
            await transcribeHelper.TranscribeMediaFile(filePath, languageCode);
        }
    }
}
