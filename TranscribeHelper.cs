using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.TranscribeService;
using Amazon.TranscribeService.Model;

namespace hello_transcribe
{ 
    public class TranscribeHelper
    {
        private RegionEndpoint _region = RegionEndpoint.USWest2;
        private AmazonTranscribeServiceClient _transcribeClient { get; set; } = null!;
        private AmazonS3Client _s3Client { get; set; } = null!;

        private string _bucketName;

        /// <summary>
        /// Constructor. Instantiates Amazon S3 client and Amazon Transcribe client.
        /// </summary>
        /// <param name="region">AWS region</param>
        /// <param name="bucketName">S3 bucket name for input media files and output transcripts</param>

        public TranscribeHelper(RegionEndpoint region, string bucketName)
        {
            _region = region;
            _bucketName = bucketName;
            _s3Client = new AmazonS3Client(_region);
            _transcribeClient = new AmazonTranscribeServiceClient(_region);
        }

        /// <summary>
        /// Transcribe a media file. Uploads local file to S3, transcribes with Amazon S3, and retrieves results to a local transcript file.
        /// </summary>
        /// <param name="filePath">Local file path</param>
        /// <param name="languageCode">Language code, such as en-US or en-GB</param>
        /// <returns></returns>

        public async Task TranscribeMediaFile(string filePath, string languageCode)
        {
            var mediaFileName = Path.GetFileName(filePath);

            // upload local file to S3 and get HTTP Uri
            var s3HttpUri = await UploadFileToBucket(filePath, _bucketName);
            
            // set output transcript file to same name as media file, but with an extension of .json
            var pos = mediaFileName.LastIndexOf(".");
            var transcriptFileName = (pos != -1) ? mediaFileName.Substring(0, pos) + ".json" : mediaFileName + ".json";

            // Start job

            var startJobRequest = new StartTranscriptionJobRequest()
            {
                Media = new Media()
                {
                    MediaFileUri = s3HttpUri
                },
                OutputBucketName = _bucketName,
                OutputKey = transcriptFileName,
                TranscriptionJobName = $"{DateTime.Now.Ticks}-{mediaFileName}",
                LanguageCode = new LanguageCode(languageCode),
            };

            Console.WriteLine($"Creating transcription job\n    S3 bucket: {_bucketName}\n    input media file: {mediaFileName}\n    language code: {languageCode}\n    output transcript: {transcriptFileName}");

            var startJobResponse = await _transcribeClient.StartTranscriptionJobAsync(startJobRequest);
            Console.WriteLine($"Job {startJobResponse.TranscriptionJob.TranscriptionJobName} created, status {startJobResponse.TranscriptionJob.TranscriptionJobStatus.Value}");

            var getJobRequest = new GetTranscriptionJobRequest() { TranscriptionJobName = startJobRequest.TranscriptionJobName };

            // Wait for job completion

            Console.WriteLine("Awaiting job completion");
            GetTranscriptionJobResponse getJobResponse;
            do
            {
                Thread.Sleep(15 * 1000);
                Console.Write(".");
                getJobResponse = await _transcribeClient.GetTranscriptionJobAsync(getJobRequest);
            } while (getJobResponse.TranscriptionJob.TranscriptionJobStatus == "IN_PROGRESS");
            Console.WriteLine($"Job complete, status: {getJobResponse.TranscriptionJob.TranscriptionJobStatus}");

            // Save transcription file locally
            await SaveS3ObjectAsFile(_bucketName, transcriptFileName, transcriptFileName);

            // Delete media file and output file from S3 to avoid accruing charges.
            await DeleteObjectFromBucket(mediaFileName, _bucketName);
            await DeleteObjectFromBucket(transcriptFileName, _bucketName);

            Console.WriteLine($"Results saved to file {transcriptFileName}");

        }

        /// <summary>
        /// Upload local file to S3 bucket.
        /// </summary>
        /// <param name="filePath">local file path</param>
        /// <param name="bucketName">bucket name</param>
        /// <returns>S3 object Http Uri.</returns>
        private async Task<string> UploadFileToBucket(string filePath, string bucketName)
        {
            Console.WriteLine($"Uploading {filePath} to bucket {bucketName}");
            var putRequest = new PutObjectRequest
            {
                BucketName = bucketName,
                FilePath = filePath,
                Key = Path.GetFileName(filePath)
            };

            var putResponse = await _s3Client.PutObjectAsync(putRequest);

            if (putResponse.HttpStatusCode != System.Net.HttpStatusCode.OK)
            {
                throw new ApplicationException("Media file upload to S3 failed");
            }

            var httpUri = $"https://{bucketName}.s3.amazonaws.com/{putRequest.Key}";
            Console.WriteLine($"    S3 object HttpUri: {httpUri}");
            return httpUri;
        }

        private async Task SaveS3ObjectAsFile(string bucketName, string key, string filePath)
        {
            using (var obj = await _s3Client.GetObjectAsync(bucketName, key))
            {
                await obj.WriteResponseStreamToFileAsync(filePath, false, new CancellationToken());
            }
        }

        /// <summary>
        /// Delete file from S3 bucket.
        /// </summary>
        /// <param name="filename"></param>
        private async Task DeleteObjectFromBucket(string filename, string bucketName)
        {
            Console.WriteLine($"Delete {filename} from bucket {bucketName}");
            var deleteRequest = new DeleteObjectRequest
            {
                BucketName = bucketName,
                Key = Path.GetFileName(filename)
            };
            await _s3Client.DeleteObjectAsync(deleteRequest);
        }
    }
}
