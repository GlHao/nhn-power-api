using System;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using NHN.Power.API.Models;

namespace NHN.Power.API.Services
{
    public interface IStorageService
    {
        string GeneratePreSignedReadUrl(string fileKey, TimeSpan expiresIn);
        string GeneratePreSignedUploadUrl(string fileKey, TimeSpan expiresIn, string contentType);
    }

    public class StorageService : IStorageService
    {
        private readonly AppSettings _appSettings;
        private readonly IAmazonS3 _s3Client;

        public StorageService(IConfiguration configuration)
        {
            _appSettings = configuration.GetSection("AppSettings").Get<AppSettings>() ?? new AppSettings();

            var s3Config = new AmazonS3Config
            {
                ServiceURL = $"https://{_appSettings.R2AccountId}.r2.cloudflarestorage.com",
            };

            _s3Client = new AmazonS3Client(
                _appSettings.R2AccessKeyId,
                _appSettings.R2SecretAccessKey,
                s3Config);
        }

        public string GeneratePreSignedReadUrl(string fileKey, TimeSpan expiresIn)
        {
            if (string.IsNullOrEmpty(_appSettings.R2BucketName))
                return string.Empty;

            var request = new GetPreSignedUrlRequest
            {
                BucketName = _appSettings.R2BucketName,
                Key = fileKey,
                Expires = DateTime.UtcNow.Add(expiresIn),
                Verb = HttpVerb.GET
            };

            return _s3Client.GetPreSignedURL(request);
        }

        public string GeneratePreSignedUploadUrl(string fileKey, TimeSpan expiresIn, string contentType)
        {
            if (string.IsNullOrEmpty(_appSettings.R2BucketName))
                return string.Empty;

            var request = new GetPreSignedUrlRequest
            {
                BucketName = _appSettings.R2BucketName,
                Key = fileKey,
                Expires = DateTime.UtcNow.Add(expiresIn),
                Verb = HttpVerb.PUT,
                ContentType = contentType
            };

            return _s3Client.GetPreSignedURL(request);
        }
    }
}
