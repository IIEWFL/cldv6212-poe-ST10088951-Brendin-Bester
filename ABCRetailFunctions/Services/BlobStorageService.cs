using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ABCRetailFunctions.Services
{
    public class BlobStorageService
    {
        private readonly BlobContainerClient _blobContainerClient;

        public BlobStorageService(string storageConnectionString, string containerName)
        {
            var serviceClient = new BlobServiceClient(storageConnectionString);
            _blobContainerClient = serviceClient.GetBlobContainerClient(containerName);
            _blobContainerClient.CreateIfNotExists();


        }

        //upload Photo Create.cshtml

        public async Task<string> UploadPhotoAsync(string blobName, Stream stream)
        {
            var blobClient = _blobContainerClient.GetBlobClient(blobName);
            await blobClient.UploadAsync(stream, true);
            return GetBlobUriWithSas(blobClient);

        }

        //get blob uri with sas token
        private string GetBlobUriWithSas(BlobClient blobClient)
        {
            if (blobClient.CanGenerateSasUri)
            {
                var sasBuilder = new BlobSasBuilder
                {
                    BlobContainerName = blobClient.BlobContainerName,
                    BlobName = blobClient.Name,
                    Resource = "b",
                    ExpiresOn = DateTimeOffset.UtcNow.AddDays(240),
                };

                sasBuilder.SetPermissions(BlobContainerSasPermissions.Read);
                var sasUri = blobClient.GenerateSasUri(sasBuilder);
                return sasUri.ToString();


            }
            else
            {
                throw new InvalidOperationException("Blob Client does not support generating SAS URIs");
            }

        }

        //delete Photo Delete.cshtml
        public async Task DeletePhotoAsync(string blobName)
        {
            var blobClient = _blobContainerClient.GetBlobClient(blobName);
            await blobClient.DeleteIfExistsAsync();

        }
    }
}
